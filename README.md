# Examen 3 — Integración OAuth Manual con Notion (.NET + Frontend)

## Proveedor elegido
- **Notion** (flujo OAuth 2.0 manual con `HttpClient`, sesión en backend y filtro `RequiereSession` basado en proveedor/sesión real del servidor).

## Requisitos previos
- .NET SDK instalado
- Servidor estático para el frontend (por ejemplo Live Server en puerto `5500`)
- Una conexión OAuth creada en Notion Developers con:
  - URI de redireccionamiento: `http://localhost:5170/api/AuthNotion/callback`
  - Permisos de lectura y escritura de contenido
  - Una página de Notion compartida con la conexión

## Configuración de secretos y variables
Los secretos no se guardan en el código versionado. Usa `appsettings.Example.json` como referencia y configura tus valores con **User Secrets** desde la carpeta del proyecto .NET:

```bash
dotnet user-secrets init
dotnet user-secrets set "Notion:ClientId" "TU_CLIENT_ID"
dotnet user-secrets set "Notion:ClientSecret" "TU_CLIENT_SECRET"
dotnet user-secrets set "Notion:RedirectUri" "http://localhost:5170/api/AuthNotion/callback"
dotnet user-secrets set "Notion:ParentPageId" "ID_DE_TU_PAGINA"
dotnet user-secrets set "Notion:FrontendUrl" "http://localhost:5500/FrontEnd/index.html"