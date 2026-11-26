# Examen 3 — Login con Epic Games

**Alumno:** Leonardo Julio  
**Proveedor elegido:** Epic Games (OAuth 2.0 / Epic Account Services)  
**Rama GitFlow:** `feature/leonardo-julio-examen3`  
**Proyecto:** `Leonardo_Julio/Examen3.Epic`

Aplicación web en ASP.NET Core (.NET 10) con:

- Login real mediante OAuth 2.0 con Epic Games.
- Intercambio del código de autorización en el servidor.
- Sesión de la aplicación validada en el servidor, no solo en el navegador.
- Endpoints protegidos `GET /api/notes` y `POST /api/notes`.
- Frontend con login, logout, usuario/proveedor autenticado y botones para probar POST/GET.
- Logout que invalida la sesión del servidor: una cookie vieja deja de funcionar.
- Pruebas de integración y unitarias que no necesitan credenciales reales.

> **Cambio de proveedor:** el enunciado original se trabajó inicialmente con Reddit y luego
> Steam, pero ambos quedaron bloqueados por la red del entorno. Se cambió a **Epic Games**,
> que sí es accesible desde `api.epicgames.dev`. La implementación es un OAuth 2.0 real, no
> un mock: los tests usan un cliente falso únicamente para poder ejecutarse sin credenciales.

---

## 1. Cómo funciona

```text
Navegador
   │  1. GET /api/auth/epic/login
   ▼
ASP.NET Core ──► 2. redirige a https://www.epicgames.com/id/authorize
   ▲                 (client_id, redirect_uri, scope=basic_profile, state)
   │
   │  3. Epic autentica al usuario y vuelve a
   │     /api/auth/epic/callback?code=...&state=...
   ▼
ASP.NET Core
   ├─ 4. valida el state contra la cookie HttpOnly
   ├─ 5. POST a api.epicgames.dev/epic/oauth/v2/token
   │     (Basic client_id:client_secret, grant_type=authorization_code)
   ├─ 6. GET api.epicgames.dev/epic/oauth/v2/userInfo con Bearer token
   ├─ 7. revoca el access token en /epic/oauth/v2/revoke
   ├─ 8. crea una sesión propia en memoria y firma una cookie HttpOnly
   └─ 9. redirige a /?login=ok
```

El navegador **nunca** recibe el `client_secret` ni el `access_token` de Epic. El token de
Epic solo se usa en el servidor para leer la identidad y se revoca inmediatamente.

---

## 2. Estructura

```text
Leonardo_Julio/
├── Examen3.Epic/
│   ├── Auth/                 # OAuth de Epic, constantes, sesión en memoria
│   ├── Controllers/          # AuthController y NotesController
│   ├── Models/               # Nota y request de creación
│   ├── Options/              # Configuración de Epic y almacenamiento
│   ├── Services/             # Persistencia JSON de notas por usuario
│   ├── wwwroot/              # Frontend HTML/CSS/JS sin Node
│   ├── Program.cs            # DI, cookie auth, políticas, seguridad
│   ├── appsettings.json
│   └── appsettings.Example.json
├── Examen3.Epic.Tests/       # xUnit + WebApplicationFactory
├── Leonardo_Julio.slnx
├── README.md
└── PULL_REQUEST.md
```

---

## 3. Endpoints

| Método | Ruta | Protegido | Descripción |
|---|---|---:|---|
| `GET` | `/health` | No | Estado básico del servicio. |
| `GET` | `/api/auth/status` | No | Proveedor, si está configurado y URL de login. |
| `GET` | `/api/auth/epic/login` | No | Genera `state`, guarda cookie y redirige a Epic. |
| `GET` | `/api/auth/epic/callback` | No | Valida `state`, canjea el código y crea la sesión. |
| `GET` | `/api/auth/me` | No | Devuelve si hay sesión y los datos públicos del usuario. |
| `POST` | `/api/auth/logout` | **Sí** | Revoca la sesión del servidor y borra la cookie. |
| `GET` | `/api/notes` | **Sí** | Lista las notas del usuario autenticado. |
| `POST` | `/api/notes` | **Sí** | Crea una nota (texto de 1 a 500 caracteres). |

Respuestas sin sesión:

- `GET /api/notes` → `401 application/json`
- `POST /api/notes` → `401 application/json`
- Sesión autenticada con otro proveedor → `403 application/json`

---

## 4. Configuración manual de Epic Games

Esta parte requiere una cuenta de Epic Games y acceso al Developer Portal. **No se puede
automatizar desde el repositorio** porque las credenciales son tuyas.

### 4.1. Crear el cliente OAuth

1. Entra a <https://dev.epicgames.com/portal> con tu cuenta de Epic Games.
2. Crea una **organización** y un **producto** si todavía no existen.
3. En el producto, abre **Product Settings → Clients** y crea un **Client**.
4. Copia el **Client ID** y el **Client Secret**.
5. El **Client Policy** debe permitir autenticación de usuario (no debe ser un cliente
   *userless*, porque necesitamos leer el perfil).
6. En **Epic Online Services → Accounts & Social → Epic Account Services**, crea la
   aplicación, habilita el permiso **Basic Profile** y vincula el cliente que creaste.
7. En la configuración del cliente agrega la **Redirect URL** exacta que usarás.

### 4.2. El problema de `localhost`

Epic no acepta `http://localhost` como Redirect URL. Para probar el login real hay dos
opciones:

- **Recomendada para la demo:** usar un túnel HTTPS temporal:
  ```bash
  # Opción A: ngrok
  ngrok http 5170
  # Opción B: cloudflared
  cloudflared tunnel --url http://localhost:5170
  ```
  Obtendrás una URL como `https://algo.ngrok-free.app` o
  `https://algo.trycloudflare.com`.

- Usar un dominio público propio con HTTPS.

La Redirect URL que debes registrar en Epic es:

```text
https://TU_URL_PUBLICA/api/auth/epic/callback
```

Y en la configuración de la app debes usar **exactamente la misma URL** en
`Epic:RedirectUri`.

> Abre el frontend usando la URL del túnel (no `http://localhost:5170`) cuando pruebes el
> login real. La cookie de sesión se crea para el host del callback; si inicias el login
> desde `localhost`, el navegador terminará en el host del túnel y la sesión no quedará
> asociada a `localhost`.

### 4.3. Guardar las credenciales fuera del repositorio

Desde `Leonardo_Julio/Examen3.Epic`:

```bash
cd Leonardo_Julio/Examen3.Epic

dotnet user-secrets set "Epic:ClientId" "TU_CLIENT_ID"
dotnet user-secrets set "Epic:ClientSecret" "TU_CLIENT_SECRET"
dotnet user-secrets set "Epic:RedirectUri" "https://TU_URL_PUBLICA/api/auth/epic/callback"

# Opcional: algunos clientes de Epic lo exigen al canjear el código.
dotnet user-secrets set "Epic:DeploymentId" "TU_DEPLOYMENT_ID"
```

También puedes usar variables de entorno:

```bash
export Epic__ClientId="TU_CLIENT_ID"
export Epic__ClientSecret="TU_CLIENT_SECRET"
export Epic__RedirectUri="https://TU_URL_PUBLICA/api/auth/epic/callback"
```

`appsettings.json` **no** contiene secretos reales. `appsettings.Example.json` solo tiene
marcadores de posición. `App_Data/` y los archivos locales están en `.gitignore`.

### 4.4. Brand review

Epic exige **brand review** para que cuentas externas (que no pertenecen a tu organización)
puedan iniciar sesión. Mientras la aplicación no esté aprobada, usa la cuenta dueña del
proyecto o una cuenta miembro de la organización para la demostración. Esto es una
restricción de Epic, no del código.

---

## 5. Ejecutar

Desde la raíz del repositorio:

```bash
dotnet run --project Leonardo_Julio/Examen3.Epic
```

La app escucha en <http://localhost:5170>. Si estás probando el login real, abre la URL del
túnel HTTPS.

Para compilar y ejecutar las pruebas:

```bash
dotnet build Leonardo_Julio/Leonardo_Julio.slnx
dotnet test  Leonardo_Julio/Leonardo_Julio.slnx
```

Resultado esperado: **23 tests aprobados** (no requieren credenciales de Epic ni red).

---

## 6. Verificación manual

### 6.1. Sin sesión

```bash
curl -i http://localhost:5170/api/notes
curl -i -X POST http://localhost:5170/api/notes \
  -H 'Content-Type: application/json' \
  -d '{"text":"sin sesion"}'
```

Ambos deben responder `401` con JSON que incluye `loginUrl`.

### 6.2. Login real

1. Abre la URL del túnel en el navegador.
2. Pulsa **Iniciar sesión con Epic Games**.
3. Inicia sesión en Epic y acepta los permisos.
4. Debes volver a `/?login=ok` y ver tu nombre, `@usuario` e ID de Epic.
5. Pulsa **POST /api/notes** con un texto: debe responder `201` y aparecer la nota.
6. Pulsa **GET /api/notes**: debe responder `200` con la lista.
7. Pulsa **GET sin cookie**: debe responder `401` aunque estés logueado (se envía sin la
   cookie de sesión).
8. Pulsa **Cerrar sesión**: debe responder `204`.
9. Vuelve a probar **GET /api/notes** o **POST**: deben responder `401`.
10. Si guardas la cookie vieja desde DevTools y la reenvías después del logout, también
    debe responder `401`: la sesión se revocó en el servidor.

> Este flujo se verificó con una cuenta real de Epic Games: login, POST `201`, GET `200`,
> GET sin cookie `401`, logout `204` y GET posterior al logout `401`.

### 6.3. Pruebas automatizadas

Los tests cubren:

- `401` sin sesión en GET y POST.
- Redirección a Epic con `state` y cookie `HttpOnly`/`SameSite=Lax`.
- `state` inválido y `access_denied`.
- Callback completo con cliente falso.
- Creación/lectura de notas y aislamiento por usuario.
- Validación de texto vacío o mayor a 500 caracteres.
- Logout con revocación de sesión y replay de cookie vieja.
- Sesión de otro proveedor → `403`.
- Origen cruzado en POST → `403`.
- Cliente OAuth: URL de autorización, Basic auth, canje, `/userInfo`, revocación y errores.

---

## 7. Decisiones de seguridad

- **Código OAuth canjeado solo en el servidor.** El `client_secret` nunca llega al navegador.
- **`state` aleatorio de 32 bytes** en cookie `HttpOnly`, `SameSite=Lax`, 10 minutos, con
  comparación de tiempo constante.
- **Cookie de sesión `HttpOnly`** (`examen3.auth`) con un ID de sesión; no contiene tokens
  de Epic.
- **Sesión en el servidor**: al hacer logout se elimina del `IAppSessionStore`. Una cookie
  copiada antes del logout ya no autentica.
- **Política `EpicUser`**: exige el claim `provider=Epic`. Una sesión de otro proveedor
  recibe `403`.
- **Token de Epic de corta vida**: se usa para `/userInfo` y se revoca de inmediato con
  `POST /epic/oauth/v2/revoke`; no se guarda.
- **CSRF**: cookie `SameSite=Lax` + validación de `Origin` en POST de `/api`.
- **Cabeceras**: CSP, `X-Content-Type-Options`, `X-Frame-Options`, `Referrer-Policy` y
  `Permissions-Policy`.
- **Secretos**: user-secrets o variables de entorno; `appsettings.Example.json` solo con
  marcadores; `App_Data/` ignorado por Git.
- **Notas por usuario**: el almacén usa el `NameIdentifier` de la sesión, no un valor que
  envíe el frontend.

---

## 8. Limitaciones conocidas

- Las sesiones están en memoria: se pierden al reiniciar la app. Las notas sí persisten en
  `App_Data/notes.json`.
- Es una demo de un solo proceso; para producción se usaría un almacén distribuido y una
  base de datos.
- Epic puede exigir brand review para cuentas externas.
- Epic no acepta `localhost` como redirect; usa túnel HTTPS para la prueba real.
- `ForwardedHeaders` confía en proxies de loopback (ngrok/cloudflared local). En producción
  hay que restringir `KnownProxies`/`KnownNetworks`.
- El token de Epic se revoca después de leer el perfil; no se implementa refresh token
  porque la app no necesita llamar a Epic después del login.

---

## 9. GitFlow y entrega

- Rama base de integración: `develop`.
- Rama de trabajo: `feature/leonardo-julio-examen3`.
- Un solo commit final y un solo Pull Request contra `develop`.
- El PR está descrito en `PULL_REQUEST.md`.

Si no hay permiso de escritura en el repositorio del docente, hay que hacer **fork** y abrir
el PR desde el fork. No se modifican proyectos de otros alumnos.
