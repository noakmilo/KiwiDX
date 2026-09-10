import asyncio
import json
import tempfile
import unittest
from pathlib import Path
from aiohttp import WSServerHandshakeError
from aiohttp.test_utils import TestClient, TestServer
from server import CHAT_KEY, Chat, create_app, password_hash

ORIGIN = 'https://chat.example.com'

class ChatTests(unittest.IsolatedAsyncioTestCase):
    async def asyncSetUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.used = set()
        async def verify(token, ip):
            if not token.startswith('valid-') or token in self.used:
                return False
            self.used.add(token)
            return True
        self.app = create_app(str(Path(self.tmp.name)/'chat.db'), ORIGIN, 'public-test-key', 'private-test-secret', verify)
        self.client = TestClient(TestServer(self.app))
        await self.client.start_server()
        self.sockets = []
        self.counter = 0
    async def asyncTearDown(self):
        for ws in self.sockets:
            await ws.close()
        await self.client.close()
        self.tmp.cleanup()
    async def ticket(self, token=None):
        self.counter += 1
        response = await self.client.post('/session', json={'token': token or 'valid-'+str(self.counter)}, headers={'Origin': ORIGIN})
        self.assertEqual(response.status, 200, await response.text())
        cookie = response.cookies['chat_ticket']
        self.assertTrue(cookie['secure'])
        self.assertTrue(cookie['httponly'])
        return cookie.value
    async def join(self):
        ticket = await self.ticket()
        ws = await self.client.ws_connect('/ws', headers={'Origin': ORIGIN, 'Cookie': 'chat_ticket='+ticket})
        self.sockets.append(ws)
        identity = await ws.receive_json(timeout=3)
        histories = [await ws.receive_json(timeout=3), await ws.receive_json(timeout=3)]
        self.assertEqual({h['channel'] for h in histories}, {'#hamradio','#shortwave'})
        return ws, identity, ticket
    async def send(self, ws, text, channel='#hamradio'):
        await ws.send_json({'channel': channel, 'text': text})
        return await ws.receive_json(timeout=3)
    async def test_captcha_required_and_origin(self):
        response = await self.client.post('/session', json={'token':'invalid'}, headers={'Origin':ORIGIN})
        self.assertEqual(response.status,403)
        response = await self.client.post('/session', json={'token':'valid-x'}, headers={'Origin':'https://evil.example'})
        self.assertEqual(response.status,403)
        with self.assertRaises(WSServerHandshakeError):
            await self.client.ws_connect('/ws', headers={'Origin':ORIGIN})
        await self.ticket('valid-once')
        response = await self.client.post('/session', json={'token':'valid-once'}, headers={'Origin':ORIGIN})
        self.assertEqual(response.status,403)
    async def test_ticket_single_use_and_both_channels(self):
        ws, identity, ticket = await self.join()
        self.assertTrue(identity['nick'].startswith('ANON'))
        with self.assertRaises(WSServerHandshakeError):
            await self.client.ws_connect('/ws', headers={'Origin':ORIGIN,'Cookie':'chat_ticket='+ticket})
        response = await self.send(ws,'hello','#shortwave')
        self.assertEqual(response['type'],'message')
        self.assertEqual(response['channel'],'#shortwave')
    async def test_register_login_and_secret_not_in_history(self):
        ws,_,_ = await self.join()
        password = 'test-Password-12345'
        response = await self.send(ws,'/nick register RadioFan '+password)
        self.assertIn('registered',response['text'])
        response = await self.send(ws,'/login RadioFan wrong-password-123')
        self.assertIn('Invalid',response['text'])
        response = await self.send(ws,'/login radiofan '+password)
        self.assertTrue(response['authenticated'])
        self.assertEqual(response['nick'],'RadioFan')
        row = self.app[CHAT_KEY].db.execute('SELECT salt,hash FROM users').fetchone()
        self.assertNotIn(password,row)
        self.assertEqual(password_hash(password,row[0]),row[1])
        self.assertEqual(self.app[CHAT_KEY].db.execute('SELECT COUNT(*) FROM messages').fetchone()[0],0)
    async def test_reserved_and_live_nicks(self):
        first,_,_=await self.join()
        await self.send(first,'/nick register Reserved test-password-12345')
        second,_,_=await self.join()
        response=await self.send(second,'/nick Reserved')
        self.assertIn('reserved',response['text'])
        await self.send(first,'/nick UniqueName')
        response=await self.send(second,'/nick uniquename')
        self.assertIn('in use',response['text'])
    async def test_broadcast_history_link_and_invalid_channel(self):
        first,_,_=await self.join()
        second,_,_=await self.join()
        link='kiwidx://tune?rx=http%3A%2F%2Fexample.org&hz=7100000&mode=USB&protocol=KiwiSDR'
        sent=await self.send(first,link)
        received=await second.receive_json(timeout=3)
        self.assertEqual(sent['text'],received['text'])
        self.assertEqual(self.app[CHAT_KEY].db.execute('SELECT text FROM messages').fetchone()[0],link)
        response=await self.send(first,'bad','#other')
        self.assertEqual(response['type'],'notice')
    async def test_identity_and_history_survive_restart(self):
        path = str(Path(self.tmp.name) / 'restart.db')
        first = Chat(path, ORIGIN, 'key', 'secret')
        salt = '11' * 16
        first.db.execute('INSERT INTO users VALUES(?,?,?)', ('Persisted', salt, password_hash('long-test-password', salt)))
        first.db.execute('INSERT INTO messages(channel,nick,text,time) VALUES(?,?,?,?)', ('#hamradio', 'Persisted', 'hello', 1))
        first.db.commit()
        first.db.close()
        second = Chat(path, ORIGIN, 'key', 'secret')
        try:
            self.assertEqual(second.db.execute('SELECT nick FROM users').fetchone()[0], 'Persisted')
            self.assertEqual(second.db.execute('SELECT text FROM messages').fetchone()[0], 'hello')
        finally:
            second.db.close()

    async def test_rate_limit_and_public_config(self):
        self.app[CHAT_KEY].limit(('unit','test'),1,60)
        from aiohttp import web
        with self.assertRaises(web.HTTPTooManyRequests):
            self.app[CHAT_KEY].limit(('unit','test'),1,60)
        response=await self.client.get('/config')
        self.assertNotIn('private-test-secret',await response.text())
        response=await self.client.get('/')
        self.assertIn('Content-Security-Policy',response.headers)

if __name__=='__main__': unittest.main()
