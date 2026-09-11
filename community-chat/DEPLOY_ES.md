# Desplegar KiwiDX Community Chat en DigitalOcean

El servidor es un chat estilo IRC sobre HTTPS/WebSocket, no un servidor compatible con clientes IRC tradicionales. Un proceso Python atiende simultáneamente #hamradio y #shortwave. SQLite conserva los nicks registrados y los últimos 500 mensajes por canal; cada conexión recibe los últimos 100. Las sesiones de login terminan al desconectar. systemd mantiene el servicio activo y lo inicia después de reiniciar el droplet.

Necesitas un droplet Ubuntu 24.04 LTS, acceso SSH con sudo y un dominio/subdominio. Sustituye `kiwidx.noakmilo.com`, `IP_DEL_DROPLET` y `TU_USUARIO` en los ejemplos. El despliegue no se realiza desde KiwiDX: estos archivos se entregan para que los instales en tu servidor.

## 1. DNS y firewall

Crea un registro A para `kiwidx.noakmilo.com` apuntando a la IPv4 del droplet. Si usas Cloudflare DNS, déjalo en **DNS only** (nube gris) con esta configuración: así Nginx recibe la IP real y los límites no agrupan a todos los usuarios detrás de una IP de Cloudflare. Turnstile funciona sin activar el proxy de Cloudflare. No crees un AAAA si no tienes IPv6 correctamente configurado.

Permite TCP 22 desde tu IP de administración, y TCP 80/443 desde Internet en el firewall de DigitalOcean. No abras 8080. En Ubuntu, si usas UFW:

```bash
sudo ufw allow OpenSSH
sudo ufw allow 80/tcp
sudo ufw allow 443/tcp
sudo ufw enable
```

Mantén tu sesión SSH abierta al comprobar el firewall.

## 2. Crear el CAPTCHA invisible

En el panel de Cloudflare, abre **Turnstile > Add widget**. Dale un nombre como `KiwiDX Community Chat`, agrega **solamente** el hostname `kiwidx.noakmilo.com` y elige **Invisible** como modo. Guarda y copia:

- **Site key:** pública; la página de chat la obtiene de `/config`.
- **Secret key:** privada; se guarda únicamente en el archivo de entorno del droplet.

No uses claves de prueba en producción. La página solicita el CAPTCHA con la acción `chat`. Python valida el token en Siteverify y exige éxito, el hostname configurado y esa misma acción. Solo entonces emite una cookie de acceso HTTPS/HttpOnly de un solo uso y 60 segundos para abrir el WebSocket. Un error del CAPTCHA bloquea el acceso; el usuario puede pulsar Connect otra vez. No se necesita una cuenta de Cloudflare para cada oyente.

La página incluye enlaces de privacidad y términos de Cloudflare. Añade además tu propia política de privacidad/contacto antes de abrir el servicio al público. No incluyas claves privadas en KiwiDX, Git ni los ZIP de distribución.

## 3. Subir los archivos

Desde PowerShell en la carpeta del proyecto:

```powershell
scp -r .\community-chat TU_USUARIO@IP_DEL_DROPLET:~/
```

Entra por SSH y prepara los paquetes y el usuario del servicio:

```bash
sudo apt update
sudo apt install -y python3-venv nginx certbot python3-certbot-nginx sqlite3
sudo adduser --system --group --no-create-home kiwidx-chat
sudo install -d -o root -g root /opt/kiwidx-chat
sudo cp -a ~/community-chat/. /opt/kiwidx-chat/
sudo chown -R root:root /opt/kiwidx-chat
sudo python3 -m venv /opt/kiwidx-chat/.venv
sudo /opt/kiwidx-chat/.venv/bin/python -m pip install -r /opt/kiwidx-chat/requirements.txt
```

## 4. Configurar el entorno

```bash
sudo install -m 600 /opt/kiwidx-chat/deploy/chat.env.example /etc/kiwidx-chat.env
sudo nano /etc/kiwidx-chat.env
```

Contenido, usando tus claves reales (sin espacios alrededor del signo igual):

```dotenv
CHAT_ORIGIN=https://kiwidx.noakmilo.com
TURNSTILE_SITEKEY=TU_SITE_KEY
TURNSTILE_SECRET=TU_SECRET_KEY
CHAT_DB=/var/lib/kiwidx-chat/chat.sqlite3
PORT=8080
```

`CHAT_ORIGIN` debe coincidir exactamente con el origen HTTPS usado por los clientes, sin ruta. El servicio solo escucha en 127.0.0.1. La base de datos se guarda fuera del código y de la carpeta de instalación de KiwiDX.

## 5. Habilitar el servicio persistente

```bash
sudo install -m 644 /opt/kiwidx-chat/deploy/kiwidx-chat.service /etc/systemd/system/kiwidx-chat.service
sudo systemctl daemon-reload
sudo systemctl enable --now kiwidx-chat
sudo systemctl status kiwidx-chat --no-pager
curl http://127.0.0.1:8080/config
```

La respuesta de `/config` debe contener la site key, nunca la secret key. El comando del servicio ejecuta **un solo proceso**: no agregues múltiples workers, porque los clientes activos y las cookies de admisión residen en memoria.

## 6. Nginx y certificado HTTPS

```bash
sudo install -m 644 /opt/kiwidx-chat/deploy/nginx.conf /etc/nginx/sites-available/kiwidx-chat
sudo nano /etc/nginx/sites-available/kiwidx-chat
```

Cambia `server_name kiwidx.noakmilo.com;` por tu hostname. Después:

```bash
sudo ln -s /etc/nginx/sites-available/kiwidx-chat /etc/nginx/sites-enabled/kiwidx-chat
sudo nginx -t
sudo systemctl reload nginx
sudo certbot --nginx -d kiwidx.noakmilo.com --redirect
sudo certbot renew --dry-run
```

Certbot agregará TLS y redirección de HTTP a HTTPS. Aunque la página pueda cargar por HTTP antes de este paso, el chat requiere HTTPS y no debe probarse como servicio público hasta terminar el certificado. Si configuras otro proxy delante de Nginx, revisa primero la restauración segura de IP real; no confíes en cabeceras X-Real-IP suministradas directamente por Internet.

## 7. Usarlo en KiwiDX (cliente nativo)

1. Instala el KiwiDX actualizado y abre **Chat**. Conecta automaticamente al servicio configurado, sin mostrar su URL.
2. Turnstile se ejecuta en un componente temporal de verificacion; al terminar, este se destruye. El chat, sus mensajes y las credenciales no pasan por una pagina web: .NET abre `/session` por HTTPS y `/ws` por WebSocket seguro directamente.
3. El nick invitado aparece como ANON aleatorio. Usa **Set nick** para cambiarlo o `/register MiNick UnaClaveLargaDe12OMasCaracteres` y despues `/login MiNick UnaClaveLargaDe12OMasCaracteres`.
4. Las pestanas nativas **#hamradio** y **#shortwave** reciben mensajes simultaneamente. Un asterisco indica actividad en el canal no seleccionado.
5. `/help`, las respuestas de autenticacion y los errores son privados. No se hace eco local de los comandos y las contrasenas se ocultan en el cuadro de entrada. Las sesiones terminan al desconectar.
6. **Paste RX-Freq** prepara el servidor, frecuencia y modo actuales. **Send** publica el mensaje. El enlace subrayado permite sintonizar desde KiwiDX.
7. El boton **Chat** oculta el panel y mantiene la sesion. **View > Detach chat** lo abre en una ventana independiente. **Disconnect** cierra la conexion; **Reconnect** realiza una nueva verificacion. Ante errores de CAPTCHA, revisa las claves y vuelve a intentar.

## 8. Verificación, actualización y respaldo

Prueba con dos ventanas: crea dos invitados, comprueba ambos canales, registra un nick, intenta usarlo como invitado (debe rechazarse), inicia sesión, prueba Mute y un enlace RX. Reinicia el servicio y comprueba que el registro y el historial persisten. Una conexión directa a `/ws` sin la cookie emitida después del CAPTCHA debe responder 403.

```bash
cd /opt/kiwidx-chat
sudo .venv/bin/python -m unittest discover -p test_server.py -v
sudo journalctl -u kiwidx-chat -n 100 --no-pager
sudo systemctl restart kiwidx-chat
```

Los tests simulan la respuesta de Siteverify; no sustituyen una prueba con tus claves y dominio reales. Si Turnstile no carga en WebView2, prueba primero el mismo dominio en un navegador actualizado, comprueba las claves/hostname, el certificado y el acceso a `challenges.cloudflare.com`. No desactives la validación para resolverlo.

Respaldo consistente de SQLite (cuentas y mensajes):

```bash
sudo install -d -m 700 /var/backups/kiwidx-chat
sudo sqlite3 /var/lib/kiwidx-chat/chat.sqlite3 ".backup '/var/backups/kiwidx-chat/chat.sqlite3'"
sudo chmod 600 /var/backups/kiwidx-chat/chat.sqlite3
```

Para actualizar: sube el nuevo código, detén el servicio, copia el código a `/opt/kiwidx-chat`, actualiza dependencias en `.venv` y vuelve a iniciarlo. Conserva `/etc/kiwidx-chat.env` y `/var/lib/kiwidx-chat`. El servicio tiene límites de conexiones, mensajes e intentos de login; un despliegue público todavía necesita supervisión y una política de moderación. No se incluye federación IRC ni administración gráfica de usuarios.

Referencias oficiales: [Turnstile invisible](https://developers.cloudflare.com/turnstile/concepts/widget/), [validación del servidor](https://developers.cloudflare.com/turnstile/get-started/server-side-validation/), [claves de prueba](https://developers.cloudflare.com/turnstile/troubleshooting/testing/), [preparar un droplet](https://docs.digitalocean.com/products/droplets/getting-started/recommended-droplet-setup/).

## Ayuda y privacidad de comandos

Escribe `/help` para ver los comandos disponibles. La ayuda, las respuestas de registro/login y los errores se envian exclusivamente a tu conexion: no se publican en ninguno de los canales ni se guardan en su historial. Todo mensaje que comienza con `/` se procesa como comando privado, incluso si es desconocido o tiene argumentos incorrectos. `/register`, `/login` y `/nick register` ocultan el texto mientras se escribe; los nombres de comandos admiten mayusculas y minusculas.

Para aplicar esta actualizacion a un droplet existente, copia `server.py` y la carpeta `static` a `/opt/kiwidx-chat/`, conservando el entorno y la base de datos, y ejecuta `sudo systemctl restart kiwidx-chat`. Vuelve a abrir la pagina del chat en KiwiDX. No hace falta reinstalar KiwiDX.

El cliente nativo requiere instalar el nuevo KiwiDX y subir `static/verify.html` y `static/verify.js` al servidor. El servidor sigue validando CAPTCHA antes de admitir cada conexion.

## Actualizar el servidor para el cliente nativo

El servicio Python conserva los mismos endpoints y base de datos. Agrega los dos archivos de verificacion antes de instalar el nuevo cliente. Desde PowerShell en el proyecto:

```powershell
scp .\community-chat\static\verify.html .\community-chat\static\verify.js TU_USUARIO@IP_DEL_DROPLET:~/
```

En el droplet:

```bash
sudo install -m 644 ~/verify.html /opt/kiwidx-chat/static/verify.html
sudo install -m 644 ~/verify.js /opt/kiwidx-chat/static/verify.js
```

El servidor publica esos archivos automaticamente mediante `/static/`; no hace falta reiniciar ni modificar las cuentas o las claves de Turnstile. La pagina principal de chat web puede seguir disponible para navegadores, pero KiwiDX ya no la carga. La validacion real con Turnstile debe probarse desde KiwiDX despues de subir estos archivos; las pruebas automatizadas usan una respuesta de CAPTCHA simulada y una conexion local HTTP, sin modificar la verificacion HTTPS de produccion.

En v0.2.0 el chat ocupa el lateral derecho y conserva el mismo servicio y los mismos archivos de CAPTCHA. Si verify.html y verify.js ya estan desplegados, este redise?o no requiere actualizar el droplet.
