'use strict';
let socket=null,channel='#hamradio',widget=null,working=false,firstIdentity=false,desiredNick='';
const byId=id=>document.getElementById(id);
byId('nick').value='ANON'+(1000+crypto.getRandomValues(new Uint32Array(1))[0]%9000);
function status(text){byId('state').textContent=text;}
function connected(){return socket&&socket.readyState===WebSocket.OPEN;}
function notice(text){const el=document.createElement('div');el.className='line notice';el.textContent=text;append(channel,el);}
function append(ch,el){const box=byId(ch.slice(1));const bottom=box.scrollHeight-box.scrollTop-box.clientHeight<40;box.append(el);while(box.childElementCount>500)box.firstElementChild.remove();if(bottom)box.scrollTop=box.scrollHeight;}
function parseRx(text){try{const uri=new URL(text);if(uri.protocol!=='kiwidx:'||uri.hostname!=='tune')return null;const rx=new URL(uri.searchParams.get('rx'));const hz=Number(uri.searchParams.get('hz'));const mode=uri.searchParams.get('mode');if(!['http:','https:'].includes(rx.protocol)||rx.username||rx.password||!Number.isFinite(hz)||hz<=0||hz>1e12||!mode||!/^[A-Za-z0-9+_-]{1,20}$/.test(mode))return null;return {link:text,rx:rx.href,hz,mode};}catch{return null;}}
function message(event){const line=document.createElement('div');line.className='line';const prefix=document.createElement('span');prefix.className='time';prefix.textContent=new Date(event.time*1000).toLocaleTimeString()+' ';line.append(prefix);const nick=document.createElement('span');nick.className='nick';nick.textContent='<'+event.nick+'> ';line.append(nick);
 for(const part of event.text.split(/(kiwidx:\/\/tune\?[^\s]+)/g)){const rx=parseRx(part);if(!rx){line.append(document.createTextNode(part));continue;}const a=document.createElement('a');a.href=rx.link;a.textContent=rx.rx+' · '+(rx.hz/1000).toFixed(3)+' kHz · '+rx.mode;a.addEventListener('click',e=>{e.preventDefault();if(e.isTrusted&&window.chrome?.webview)window.chrome.webview.postMessage({type:'tune',link:rx.link});else notice('Open this chat in KiwiDX to tune the receiver.');});line.append(a);}append(event.channel,line);
 const tab=document.querySelector('[data-channel="'+event.channel+'"]');if(event.channel!==channel)tab.textContent=event.channel+' •';}
function send(text){if(!connected()){notice('Connect to chat first.');return false;}socket.send(JSON.stringify({channel,text}));return true;}
async function verified(token){try{const response=await fetch('/session',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({token})});if(!response.ok)throw new Error(await response.text());socket=new WebSocket(location.origin.replace(/^http/,'ws')+'/ws');firstIdentity=true;
 socket.onopen=()=>{working=false;byId('connect').disabled=false;byId('connect').textContent='Disconnect';status('Connected');};
 socket.onmessage=e=>{try{const v=JSON.parse(e.data);if(v.type==='identity'){byId('nick').value=v.nick;status(v.authenticated?'Logged in as '+v.nick:'Guest: '+v.nick);if(firstIdentity){firstIdentity=false;if(desiredNick&&desiredNick!==v.nick)send('/nick '+desiredNick);}}else if(v.type==='history'){byId(v.channel.slice(1)).replaceChildren();for(const item of v.messages)message({...item,channel:v.channel});}else if(v.type==='message')message(v);else if(v.type==='notice')notice(v.text);}catch{notice('Invalid server response.');}};
 socket.onclose=()=>{socket=null;working=false;byId('connect').disabled=false;byId('connect').textContent='Reconnect';status('Disconnected');};socket.onerror=()=>status('Connection failed');
 }catch(e){fail(e.message);}}
function fail(text){working=false;byId('connect').disabled=false;status(text||'Verification failed. Reconnect to retry.');}
async function connect(){if(connected()){socket.close();return;}if(working)return;working=true;byId('connect').disabled=true;desiredNick=byId('nick').value.trim();status('Verifying…');try{if(!window.turnstile)throw new Error('Verification unavailable. Check your connection and retry.');const config=await(await fetch('/config')).json();if(widget!==null)turnstile.remove(widget);widget=turnstile.render('#captcha',{sitekey:config.sitekey,action:'chat',callback:verified,'error-callback':()=>{fail();return true;},'expired-callback':()=>fail('Verification expired. Reconnect.')});}catch(e){fail(e.message);}}
byId('connect').onclick=connect;
byId('changeNick').onclick=()=>send('/nick '+byId('nick').value.trim());
byId('sendForm').onsubmit=e=>{e.preventDefault();const input=byId('message');if(input.value.trim()&&send(input.value.trim())){input.value='';input.type='text';}};
byId('message').oninput=e=>{e.target.type=/^\s*\/(login|register|nick\s+register)\b/i.test(e.target.value)?'password':'text';};
for(const button of document.querySelectorAll('[data-channel]'))button.onclick=()=>{channel=button.dataset.channel;for(const b of document.querySelectorAll('[data-channel]'))b.classList.toggle('active',b===button);byId('hamradio').hidden=channel!=='#hamradio';byId('shortwave').hidden=channel!=='#shortwave';button.textContent=channel;};
byId('pasteRx').onclick=()=>{if(window.chrome?.webview)window.chrome.webview.postMessage({type:'pasteRx'});else notice('Paste RX-Freq is available inside KiwiDX.');};
window.setRxDraft=text=>{byId('message').type='text';byId('message').value=text;byId('message').focus();};
window.addEventListener('pagehide',()=>socket?.close());
