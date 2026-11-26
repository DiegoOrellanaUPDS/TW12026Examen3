# Examen 3 — Login con Epic Games (Leonardo Julio)

## Opción elegida

**Opción con proveedor externo: Epic Games (OAuth 2.0 / Epic Account Services).**

Inicialmente se intentó Reddit y luego Steam, pero ambos quedaron bloqueados por la red del
entorno. Se cambió a Epic Games porque `api.epicgames.dev` es accesible y permite un flujo
OAuth 2.0 real de authorization code.

## Qué incluye

- Login real con Epic Games desde el frontend.
- Intercambio del código OAuth en el servidor con `client_id` + `client_secret`.
- Validación de `state` con cookie `HttpOnly` y comparación de tiempo constante.
- Lectura de identidad con `GET /epic/oauth/v2/userInfo` y revocación inmediata del token.
- Cookie de sesión `HttpOnly` respaldada por una sesión en memoria del servidor.
- Logout que revoca la sesión; una cookie copiada antes del logout recibe `401`.
- Endpoints protegidos:
  - `GET /api/notes`
  - `POST /api/notes`
- Política de autorización `EpicUser` que exige `provider=Epic`.
- Frontend con:
  - botón de login con Epic Games;
  - nombre, usuario e ID del usuario autenticado;
  - badge del proveedor;
  - botón de logout;
  - botones para probar POST, GET, `/api/auth/me` y GET sin cookie;
  - visualización del código HTTP y del JSON de respuesta;
  - lista de notas guardadas.
- Persistencia de notas por usuario en `App_Data/notes.json` (ignorado por Git).
- 23 pruebas automatizadas con xUnit + `WebApplicationFactory` y un cliente OAuth falso.
- Secretos fuera del repositorio: user-secrets o variables de entorno.
- `appsettings.Example.json` con marcadores de posición.
- README con pasos de configuración de Epic, ejecución y verificación.

## Cómo ejecutar

```bash
dotnet run --project Leonardo_Julio/Examen3.Epic
```

Abre <http://localhost:5170>.

Para el login real, Epic no acepta `http://localhost` como redirect. Hay que usar un túnel
HTTPS (ngrok o cloudflared), registrar en Epic:

```text
https://TU_URL_PUBLICA/api/auth/epic/callback
```

y configurar las credenciales:

```bash
cd Leonardo_Julio/Examen3.Epic
dotnet user-secrets set "Epic:ClientId" "TU_CLIENT_ID"
dotnet user-secrets set "Epic:ClientSecret" "TU_CLIENT_SECRET"
dotnet user-secrets set "Epic:RedirectUri" "https://TU_URL_PUBLICA/api/auth/epic/callback"
```

El detalle completo está en `Leonardo_Julio/README.md`.

## Cómo verificar

### Tests

```bash
dotnet build Leonardo_Julio/Leonardo_Julio.slnx
dotnet test  Leonardo_Julio/Leonardo_Julio.slnx
```

Resultado esperado: **23/23 tests aprobados**, sin necesidad de credenciales reales.

### Evidencia de la ejecución local

- `dotnet build Leonardo_Julio/Leonardo_Julio.slnx` → 0 errores, 0 warnings.
- `dotnet test Leonardo_Julio/Leonardo_Julio.slnx` → **23/23 aprobados**.
- `GET /api/notes` y `POST /api/notes` sin sesión → `401` JSON.
- `GET /api/auth/epic/login` → `302` a Epic con `state` y cookie `HttpOnly`.
- `state` inválido y `access_denied` → redirección controlada al frontend.
- POST con `Origin` de otro dominio → `403`.
- `/health`, `/`, `/styles.css` y `/app.js` → `200`.
- Login real con Epic Games completado: token `200`, `/userInfo` `200` y revocación `204`.
- Con la sesión real: `POST /api/notes` → `201`, `GET /api/notes` → `200`,
  `GET sin cookie` → `401`, logout → `204` y GET posterior → `401`.
- Frontend probado en navegador: estado anónimo, 401 visible, usuario/proveedor
  autenticado y notas renderizadas.

### Sin sesión

```bash
curl -i http://localhost:5170/api/notes
curl -i -X POST http://localhost:5170/api/notes \
  -H 'Content-Type: application/json' \
  -d '{"text":"sin sesion"}'
```

Ambos deben devolver `401` en JSON.

### Con sesión

1. Abrir la URL del túnel.
2. Login con Epic Games.
3. Verificar que el frontend muestra el usuario y el proveedor.
4. `POST /api/notes` → `201`.
5. `GET /api/notes` → `200` con la nota.
6. `GET sin cookie` → `401`.
7. Logout → `204`.
8. Repetir GET/POST con la cookie vieja → `401`.

## Seguridad

- El `client_secret` y el `access_token` de Epic nunca llegan al navegador.
- El token de Epic se revoca después de leer `/userInfo`.
- La sesión se valida contra el servidor en cada request.
- El logout elimina la sesión del servidor, no solo oculta la UI.
- La política exige `provider=Epic`; sesiones de otros proveedores reciben `403`.
- Los POST de `/api` validan `Origin` y la cookie usa `SameSite=Lax`.
- Secretos fuera del repositorio; `App_Data/` ignorado.

## Notas

- Las sesiones en memoria se pierden al reiniciar; las notas persisten.
- Epic puede requerir brand review para cuentas que no pertenecen a la organización.
- No se modificó ningún proyecto de otro alumno.
