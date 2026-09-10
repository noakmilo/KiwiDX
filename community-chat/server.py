"""KiwiDX Community Chat: one aiohttp worker, SQLite identities/history, Turnstile admission."""
import asyncio
import hashlib
import hmac
import json
import os
import re
import secrets
import sqlite3
import time
from collections import defaultdict, deque
from pathlib import Path
from urllib.parse import urlparse

from aiohttp import ClientError, ClientSession, ClientTimeout, WSMsgType, web

CHANNELS = ("#hamradio", "#shortwave")
NICK = re.compile(r"[A-Za-z][A-Za-z0-9_\-]{2,23}\Z")
ROOT = Path(__file__).parent


def password_hash(password, salt):
    return hashlib.scrypt(password.encode(), salt=bytes.fromhex(salt), n=16384, r=8, p=1, dklen=32).hex()


class Chat:
    def __init__(self, db_path, origin, sitekey, secret, verify=None):
        self.origin = origin.rstrip("/")
        self.hostname = urlparse(origin).hostname
        if urlparse(origin).scheme != "https" or not self.hostname or not sitekey or not secret:
            raise ValueError("Set HTTPS CHAT_ORIGIN, TURNSTILE_SITEKEY and TURNSTILE_SECRET.")
        self.sitekey, self.secret = sitekey, secret
        self.verify_override = verify  # only injected by unit tests; no deployment bypass
        self.db = sqlite3.connect(db_path)
        self.db.execute("PRAGMA journal_mode=WAL")
        self.db.execute("CREATE TABLE IF NOT EXISTS users(nick TEXT PRIMARY KEY COLLATE NOCASE, salt TEXT NOT NULL, hash TEXT NOT NULL)")
        self.db.execute("CREATE TABLE IF NOT EXISTS messages(id INTEGER PRIMARY KEY, channel TEXT, nick TEXT, text TEXT, time INTEGER)")
        self.db.commit()
        self.clients = {}
        self.tickets = {}
        self.limits = {}
        self.hash_slots = asyncio.Semaphore(2)
        self.session_slots = asyncio.Semaphore(8)

    def limit(self, key, count, period):
        now = time.monotonic()
        if len(self.limits) > 10000:
            self.limits = {k: v for k, v in self.limits.items() if v and now - v[-1] < 3600}
            if len(self.limits) > 10000:
                raise web.HTTPTooManyRequests(text="Server busy. Try later.")
        queue = self.limits.setdefault(key, deque())
        while queue and now - queue[0] >= period:
            queue.popleft()
        if len(queue) >= count:
            raise web.HTTPTooManyRequests(text="Too many requests. Try later.")
        queue.append(now)

    def ip(self, request):
        # Only the loopback Nginx proxy may supply this header.
        return request.headers.get("X-Real-IP", request.remote) if request.remote in ("127.0.0.1", "::1") else request.remote

    def check_origin(self, request):
        if request.headers.get("Origin") != self.origin:
            raise web.HTTPForbidden(text="Invalid origin")

    async def verify(self, token, ip):
        if self.verify_override:
            return await self.verify_override(token, ip)
        try:
            async with ClientSession(timeout=ClientTimeout(total=10)) as client:
                async with client.post("https://challenges.cloudflare.com/turnstile/v0/siteverify",
                    data={"secret": self.secret, "response": token, "remoteip": ip}) as response:
                    data = await response.json()
                    return data.get("success") is True and data.get("hostname") == self.hostname and data.get("action") == "chat"
        except (ClientError, TimeoutError, OSError, ValueError):
            return False

    async def index(self, request):
        return web.Response(text=(ROOT / "static/index.html").read_text(encoding="utf-8"), content_type="text/html")

    async def config(self, request):
        return web.json_response({"sitekey": self.sitekey})

    async def session(self, request):
        self.check_origin(request)
        ip = self.ip(request)
        self.limit((ip, "captcha"), 8, 60)
        data = await request.json()
        token = data.get("token", "") if isinstance(data, dict) else ""
        if not isinstance(token, str) or not 1 <= len(token) <= 2048:
            raise web.HTTPBadRequest(text="Invalid CAPTCHA token")
        async with self.session_slots:
            if not await self.verify(token, ip):
                raise web.HTTPForbidden(text="Verification failed. Reconnect to try again.")
        now = time.monotonic()
        self.tickets = {key: value for key, value in self.tickets.items() if value[1] > now}
        if len(self.tickets) >= 2000:
            raise web.HTTPServiceUnavailable(text="Server busy")
        ticket = secrets.token_urlsafe(32)
        self.tickets[ticket] = (ip, now + 60)
        response = web.json_response({"ok": True})
        response.set_cookie("chat_ticket", ticket, secure=True, httponly=True, samesite="Strict", max_age=60, path="/ws")
        return response

    def available(self, nick, own=None):
        return all(ws is own or item["nick"].casefold() != nick.casefold() for ws, item in self.clients.items())

    def guest(self):
        while True:
            nick = "ANON" + str(secrets.randbelow(900000) + 1000)
            if self.available(nick) and not self.db.execute("SELECT 1 FROM users WHERE nick=?", (nick,)).fetchone():
                return nick

    async def notice(self, ws, text):
        await ws.send_json({"type": "notice", "text": text})

    async def command(self, ws, text):
        item = self.clients[ws]
        parts = text.split()
        register = len(parts) >= 2 and parts[:2] == ["/nick", "register"]
        login = parts and parts[0] == "/login"
        if register or login:
            self.limit((item["ip"], "auth"), 5, 60)
            args = parts[2:] if register else parts[1:]
            if len(args) != 2 or not NICK.fullmatch(args[0]) or not 12 <= len(args[1]) <= 128:
                return await self.notice(ws, "Use /nick register nick password or /login nick password. Password: 12-128 characters without spaces; nick: 3-24 letters/digits/_/-.")
            nick, password = args
            row = self.db.execute("SELECT nick,salt,hash FROM users WHERE nick=?", (nick,)).fetchone()
            if register and row:
                return await self.notice(ws, "This nick is registered. Use /login.")
            salt = secrets.token_hex(16) if register or not row else row[1]
            async with self.hash_slots:
                digest = await asyncio.to_thread(password_hash, password, salt)
            if not register and (not row or not hmac.compare_digest(row[2], digest)):
                return await self.notice(ws, "Invalid nick or password.")
            if not self.available(nick, ws):
                return await self.notice(ws, "This nick is currently in use.")
            if register:
                try:
                    self.db.execute("INSERT INTO users VALUES(?,?,?)", (nick, salt, digest))
                    self.db.commit()
                except sqlite3.IntegrityError:
                    return await self.notice(ws, "This nick is registered. Use /login.")
                return await self.notice(ws, "Nick registered. Use /login nick password to sign in.")
            item["nick"] = row[0]
            item["authenticated"] = True
        elif len(parts) == 2 and parts[0] == "/nick" and NICK.fullmatch(parts[1]):
            nick = parts[1]
            if self.db.execute("SELECT 1 FROM users WHERE nick=?", (nick,)).fetchone() or not self.available(nick, ws):
                return await self.notice(ws, "Nick reserved or in use. Choose another or use /login.")
            item["nick"], item["authenticated"] = nick, False
        else:
            return await self.notice(ws, "Commands: /nick nickname, /nick register nick password, /login nick password. Both channels are joined automatically.")
        await ws.send_json({"type": "identity", "nick": item["nick"], "authenticated": item["authenticated"]})

    async def socket(self, request):
        self.check_origin(request)
        ip = self.ip(request)
        ticket = self.tickets.pop(request.cookies.get("chat_ticket", ""), None)
        if not ticket or ticket[0] != ip or ticket[1] <= time.monotonic():
            raise web.HTTPForbidden(text="CAPTCHA verification required")
        if len(self.clients) >= 200 or sum(c["ip"] == ip for c in self.clients.values()) >= 5:
            raise web.HTTPTooManyRequests(text="Connection limit")
        ws = web.WebSocketResponse(heartbeat=25, max_msg_size=8192)
        await ws.prepare(request)
        self.clients[ws] = {"nick": self.guest(), "ip": ip, "authenticated": False}
        try:
            await ws.send_json({"type": "identity", "nick": self.clients[ws]["nick"], "authenticated": False})
            for channel in CHANNELS:
                rows = self.db.execute("SELECT nick,text,time FROM messages WHERE channel=? ORDER BY id DESC LIMIT 100", (channel,)).fetchall()
                await ws.send_json({"type": "history", "channel": channel, "messages": [{"nick": n, "text": t, "time": ts} for n,t,ts in reversed(rows)]})
            async for message in ws:
                if message.type != WSMsgType.TEXT:
                    continue
                try:
                    self.limit((ip, "message"), 20, 10)
                    data = json.loads(message.data)
                    if not isinstance(data, dict):
                        raise ValueError()
                    text, channel = data.get("text"), data.get("channel")
                    if not isinstance(text, str) or not 1 <= len(text.strip()) <= 1500 or channel not in CHANNELS:
                        raise ValueError()
                    text = text.strip()
                    if text.startswith("/"):
                        await self.command(ws, text)  # Commands/passwords are never persisted or broadcast.
                        continue
                    if any(ord(c) < 32 for c in text):
                        raise ValueError()
                    event = {"type": "message", "channel": channel, "nick": self.clients[ws]["nick"], "text": text, "time": int(time.time())}
                    self.db.execute("INSERT INTO messages(channel,nick,text,time) VALUES(?,?,?,?)", (channel, event["nick"], text, event["time"]))
                    self.db.execute("DELETE FROM messages WHERE channel=? AND id NOT IN (SELECT id FROM messages WHERE channel=? ORDER BY id DESC LIMIT 500)", (channel, channel))
                    self.db.commit()
                    await asyncio.gather(*(self.deliver(peer, event) for peer in tuple(self.clients)))
                except web.HTTPTooManyRequests:
                    await self.notice(ws, "Rate limit reached. Wait before trying again.")
                except (ValueError, TypeError):
                    await self.notice(ws, "Invalid message. Maximum 1500 characters.")
        finally:
            self.clients.pop(ws, None)
        return ws

    async def deliver(self, ws, event):
        try:
            await asyncio.wait_for(ws.send_json(event), timeout=3)
        except (ConnectionError, TimeoutError, RuntimeError):
            try:
                await asyncio.wait_for(ws.close(), timeout=2)
            except TimeoutError:
                pass

    async def shutdown(self, app):
        await asyncio.gather(*(ws.close(code=1001, message=b"Service restarting") for ws in tuple(self.clients)))

    async def cleanup(self, app):
        self.db.close()


@web.middleware
async def headers(request, handler):
    try:
        response = await handler(request)
    except web.HTTPException as exc:
        response = web.Response(status=exc.status, text=exc.text, headers={"Content-Type": "text/plain; charset=utf-8"})
    except (json.JSONDecodeError, UnicodeDecodeError):
        response = web.HTTPBadRequest(text="Invalid JSON")
    if not response.prepared:
        response.headers.update({"Cache-Control": "no-store", "X-Content-Type-Options": "nosniff", "Referrer-Policy": "no-referrer",
            "Content-Security-Policy": "default-src 'self'; script-src 'self' https://challenges.cloudflare.com; frame-src https://challenges.cloudflare.com; connect-src 'self'; style-src 'self'; object-src 'none'; base-uri 'none'; frame-ancestors 'none'"})
    return response


CHAT_KEY = web.AppKey("chat", Chat)


def create_app(db_path=None, origin=None, sitekey=None, secret=None, verify=None):
    chat = Chat(db_path or os.environ.get("CHAT_DB", "chat.sqlite3"), origin or os.environ.get("CHAT_ORIGIN", ""),
                sitekey or os.environ.get("TURNSTILE_SITEKEY", ""), secret or os.environ.get("TURNSTILE_SECRET", ""), verify)
    app = web.Application(client_max_size=8192, middlewares=[headers])
    app[CHAT_KEY] = chat
    app.add_routes([web.get("/", chat.index), web.get("/config", chat.config), web.post("/session", chat.session), web.get("/ws", chat.socket),
                    web.static("/static/", ROOT / "static", show_index=False)])
    app.on_shutdown.append(chat.shutdown)
    app.on_cleanup.append(chat.cleanup)
    return app


if __name__ == "__main__":
    web.run_app(create_app(), host="127.0.0.1", port=int(os.environ.get("PORT", "8080")), access_log=None)
