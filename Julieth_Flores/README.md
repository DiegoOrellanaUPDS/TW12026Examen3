# Autenticación con Vimeo

- **Estudiante:** Julieth Flores
- **Materia:** Tecnología Web 1
- **Opción asignada:** A
- **Proveedor elegido:** Vimeo
- **Requisitos:** .NET 10

## Credenciales

Las claves se guardan con User Secrets, nunca en el código. Desde `Backend`:

```bash
dotnet user-secrets set "Vimeo:ClientId" "TU_CLIENT_ID"
dotnet user-secrets set "Vimeo:ClientSecret" "TU_CLIENT_SECRET"
dotnet user-secrets list
```

Ejemplo de configuración sin credenciales reales:

```json
{
  "Vimeo:ClientId": "REEMPLAZAR_CON_CLIENT_ID",
  "Vimeo:ClientSecret": "REEMPLAZAR_CON_CLIENT_SECRET"
}
```

Los valores reales quedan en `~/.microsoft/usersecrets/<UserSecretsId>/secrets.json`, fuera del proyecto.

URL de retorno registrada en Vimeo: `https://localhost:7001/signin-vimeo`.

## Ejecutar

```bash
cd Backend
dotnet restore
dotnet build
dotnet run --launch-profile https
```

Abrir `https://localhost:7001/`.

## Cómo funciona

OAuth 2.0: Vimeo autoriza al usuario y devuelve un código; el backend lo cambia por un token y consulta `/me` para obtener el nombre y el id. Con esos datos se crea una cookie de sesión. La política `SoloVimeo` valida en el backend la claim `proveedor = Vimeo`. Una petición rechazada devuelve **401** (sin sesión) o **403** (sin permiso).

## MelissaController

- `POST /api/melissa`: recibe `{ "titulo": "...", "descripcion": "..." }`, lo guarda en una lista en memoria y responde con el registro.
- `GET /api/melissa`: devuelve todos los registros guardados.

Ambos requieren la política `SoloVimeo`.

## Comprobar los endpoints

1. **Con sesión:** pulsa *Iniciar sesión con Vimeo*, autoriza y vuelve al frontend. Escribe título y descripción, pulsa *Registrar* y luego *Ver registros*. Respuesta: 200 con los datos.
2. **Sin sesión:** abre `https://localhost:7001/api/melissa` en otro navegador o sin cookies. Respuesta: 401 y mensaje «Debes iniciar sesión con Vimeo.».
3. **Después del logout:** pulsa *Cerrar sesión* (la cookie se elimina) e intenta *Ver registros* otra vez. Respuesta: 401 y el usuario vuelve a mostrarse vacío.

Con `curl` también se puede ver el 401:

```bash
curl -i https://localhost:7001/api/melissa
```

## Archivos principales

- `Backend/Program.cs`: OAuth, cookies, política, archivos estáticos y respuestas 401/403.
- `Backend/Controllers/MelissaController.cs`: POST y GET.
- `Backend/wwwroot/`: `index.html`, `styles.css`, `app.js`.

## Git

El `.gitignore` de la raíz excluye `bin/` y `obj/`. Esta carpeta tiene un `.gitignore` local que además excluye `secrets.json`, `appsettings.Development.json` y archivos de usuario. No se guardan credenciales reales.
