# Loopback test fixture only: deterministic CAPTCHA and HTTP cookie for testing
# the native protocol client without installing a test TLS root certificate.
import asyncio, sys
from pathlib import Path
sys.path.insert(0, str(Path(sys.argv[1]).resolve()))
from aiohttp import web
from server import create_app, CHAT_KEY
async def main():
    async def verify(token, ip): return token == 'fixture-token'
    app = create_app(':memory:', 'https://fixture.invalid', 'fixture-key', 'fixture-secret', verify)
    @web.middleware
    async def test_cookie(request, handler):
        response = await handler(request)
        if 'chat_ticket' in response.cookies:
            response.cookies['chat_ticket']['secure'] = False
        return response
    app.middlewares.insert(0, test_cookie)
    runner = web.AppRunner(app, access_log=None)
    await runner.setup()
    site = web.TCPSite(runner, '127.0.0.1', 0)
    await site.start()
    port = site._server.sockets[0].getsockname()[1]
    app[CHAT_KEY].origin = 'http://127.0.0.1:' + str(port)
    print(port, flush=True)
    await asyncio.Event().wait()
asyncio.run(main())
