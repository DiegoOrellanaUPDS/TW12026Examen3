# Examen 3 - Tecnología Web 1

## Información del estudiante

- **Estudiante:** Carlos Miranda
- **Opción seleccionada:** Opción A
- **Proveedor OAuth:** Vimeo
- **Tecnología backend:** ASP.NET Core Web API (.NET 10)
- **Tecnología frontend:** React + Vite
- **Autenticación:** OAuth 2.0 con Vimeo
- **Gestión de sesión:** Cookies de autenticación

## Descripción del proyecto

Aplicación web que implementa autenticación mediante OAuth 2.0 utilizando Vimeo como proveedor de identidad.

El backend valida la identidad del usuario mediante Vimeo, crea una sesión autenticada y protege los endpoints de consulta y creación de mensajes.

El frontend permite iniciar sesión, consultar los datos del usuario autenticado, probar los endpoints protegidos y cerrar sesión.

## Estructura del proyecto

```text
TW12026Examen3/
├── Carlos_Miranda/
│   ├── Controllers/
│   │   ├── VimeoAuthController.cs
│   │   └── ProtegidoController.cs
│   ├── Properties/
│   ├── frontend/
│   │   └── src/
│   ├── Program.cs
│   ├── appsettings.json
│   └── Examen 3.csproj
├── .gitignore
└── README.md
```

## Requisitos previos

- .NET SDK 10.
- Node.js y npm.
- Cuenta de Vimeo.
- Aplicación registrada en el portal de desarrolladores de Vimeo.

## Configuración de Vimeo

1. Registrar una aplicación en el portal de desarrolladores de Vimeo.
2. Obtener el Client ID y el Client Secret.
3. Configurar la URL de redirección:

```text
https://localhost:7010/auth/vimeo/callback
```

4. Configurar los siguientes valores mediante User Secrets:

Desde la carpeta `Carlos_Miranda`:

```bash
dotnet user-secrets init

dotnet user-secrets set "Vimeo:ClientId" "TU_CLIENT_ID"

dotnet user-secrets set "Vimeo:ClientSecret" "TU_CLIENT_SECRET"

dotnet user-secrets set "Vimeo:RedirectUri" "https://localhost:7010/auth/vimeo/callback"
```

Los valores reales deben ser extraídos desde la página de Vimio, al crear una aplicación, se proporcionan las claves.

## Ejecución del backend

Desde la carpeta `Carlos_Miranda`:

```bash
dotnet restore
dotnet run
```

El backend estará disponible en:

- HTTPS: `https://localhost:7010`
- HTTP: `http://localhost:5170`

Documentación OpenAPI disponible durante el desarrollo:

```text
https://localhost:7010/openapi/v1.json
```

## Ejecución del frontend

Desde otra terminal:

```bash
cd Carlos_Miranda/frontend
npm install
npm run dev
```

El frontend estará disponible en:

```text
http://localhost:5173
```

## Endpoints de autenticación

| Método | Endpoint | Descripción |
|---|---|---|
| GET | `/auth/vimeo/login` | Inicia el proceso de autenticación con Vimeo. |
| GET | `/auth/vimeo/callback` | Recibe y valida la respuesta OAuth de Vimeo. |
| GET | `/auth/vimeo/me` | Consulta la información del usuario autenticado. |
| POST | `/auth/vimeo/logout` | Cierra la sesión del usuario. |

## Endpoints protegidos

Los siguientes endpoints requieren una sesión autenticada mediante Vimeo.

| Método | Endpoint | Descripción |
|---|---|---|
| GET | `/api/protegido` | Consulta los mensajes registrados. |
| POST | `/api/protegido` | Crea un nuevo mensaje. |

### Ejemplo de petición POST

```json
{
  "contenido": "Mi primer mensaje desde la aplicación"
}
```

Los endpoints protegidos verifican que el usuario esté autenticado y que el proveedor de identidad sea Vimeo.

Las peticiones sin autenticación reciben una respuesta HTTP `401 Unauthorized`.

## Seguridad

- Autenticación mediante OAuth 2.0.
- Validación del parámetro `state` durante el flujo OAuth.
- Verificación de identidad consultando la API de Vimeo.
- Cookies de sesión `HttpOnly` y `Secure`.
- Protección de endpoints mediante políticas de autorización.
- Credenciales almacenadas mediante User Secrets.
- Configuración CORS para permitir la comunicación con el frontend.

## Pruebas realizadas

Se verificó el funcionamiento de los siguientes procesos:

- Inicio de sesión mediante Vimeo.
- Autorización de la aplicación desde Vimeo.
- Recuperación de los datos del usuario autenticado.
- Acceso autorizado al endpoint GET.
- Creación de mensajes mediante POST.
- Rechazo de peticiones no autenticadas.
- Cierre de sesión.
- Verificación de la pérdida de acceso a los endpoints protegidos después del logout.

## Almacenamiento de mensajes

Los mensajes se almacenan temporalmente en memoria mediante `ConcurrentBag`.

Por este motivo, los mensajes se conservan mientras el backend permanece en ejecución y se pierden al reiniciar la aplicación.

## Autor

**Carlos Miranda**  
Tecnología Web 1 - Examen 3