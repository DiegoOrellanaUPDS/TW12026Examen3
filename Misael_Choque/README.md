# Examen 3 - Tecnología Web I

- Estudiante: Misael Choque
- Opción asignada: B
- Proveedor implementado: Kick

## Requisitos
.NET 10 SDK y una aplicación registrada en Kick Developer.

## Configuración
Desde esta carpeta, configura las credenciales con User Secrets:

dotnet user-secrets set "Kick:ClientId" "TU_CLIENT_ID"
dotnet user-secrets set "Kick:ClientSecret" "TU_CLIENT_SECRET"

URL de retorno:
https://localhost:7010/auth/kick/callback

Copia appsettings.example.json como appsettings.json antes de ejecutar.

## Ejecución
dotnet restore
dotnet run --launch-profile https

Abre https://localhost:7010/

## Pruebas
Inicia sesión con Kick, prueba GET y POST, cierra sesión y verifica que ambos endpoints respondan HTTP 401 sin autenticación.

Las credenciales reales no se incluyen en el repositorio.
