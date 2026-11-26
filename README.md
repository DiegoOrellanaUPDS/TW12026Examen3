# Examen 3 — Steam

- Estudiante: Ovidio Alvaro Sulca Vaca.
- Framework: .NET 10. Rama: `feature/ovidio-sulca-examen3`.
- Autenticación real OpenID con Steam y sesión mediante cookies; sin base de datos.

## Configuración y ejecución

Instalar el SDK .NET 10. La API Key es opcional: sin ella se muestra el SteamID64
validado por el middleware. Con una clave válida, Steam también proporciona el nombre
del perfil. La configuración se lee desde `Authentication:Steam:ApiKey` mediante
`IConfiguration`; `appsettings.json` contiene el ejemplo vacío.

Si tienes una clave, guardarla únicamente en User Secrets (no en el repositorio):

```bash
dotnet user-secrets set "Authentication:Steam:ApiKey" "TU_CLAVE_REAL"
```

```bash
dotnet restore
dotnet build
dotnet dev-certs https --trust
dotnet run --launch-profile https
```

Si Linux no permite confiar automáticamente en el certificado, confiar en el certificado
de desarrollo desde el navegador antes de iniciar sesión.

Abrir `https://localhost:7010/sulcaindex.html`. El frontend está en
`wwwroot/sulcaindex.html` (carpeta estándar de archivos estáticos).
Scalar permanece disponible en desarrollo en `https://localhost:7010/scalar`.

## Comprobación

1. Sin sesión, `GET /auth/steam/me` responde **401**, sin redirigir a Steam.
2. Pulsar **Ingresar con Steam**, autenticar una cuenta real en Steam y regresar
   automáticamente a la página. El callback `/signin-steam` lo procesa el middleware.
3. Verificar identidad, SteamID64 y proveedor `Steam`. `/me` exige la política
   `SoloSteam`: autenticación y claim `provider=Steam`; una sesión sin ese claim recibe **403**.
4. `/me` entrega también el token CSRF de la sesión. Un `POST /auth/steam/logout`
   autenticado sin el encabezado `X-CSRF-TOKEN` válido responde **400**.
5. Pulsar **Cerrar sesión**: envía el token CSRF, elimina la cookie mediante
   `SignOutAsync` y `/me` vuelve a responder **401**.

### POST y GET protegidos

`POST /api/mensajes` registra un campo de formulario `texto` (1 a 200 caracteres).
`GET /api/mensajes` devuelve los mensajes de la cuenta Steam autenticada. Ambos
requieren la política `SoloSteam` en el backend; el POST también exige el encabezado
`X-CSRF-TOKEN` entregado por `/auth/steam/me`. Los mensajes se guardan únicamente en
memoria y se pierden al reiniciar la aplicación.

1. Sin sesión, usar los botones POST y GET: ambos deben mostrar **401**.
2. Iniciar sesión con Steam. Escribir un mensaje y pulsar **Registrar mensaje**;
   debe responder **200**. Pulsar **Consultar mensajes** y verificar el texto.
3. Cerrar sesión y volver a probar ambos botones: deben responder **401**.
   También se puede comprobar directamente desde otra terminal:

   ```bash
   curl -k -i https://localhost:7010/api/mensajes
   curl -k -i -X POST -F 'texto=Prueba' https://localhost:7010/api/mensajes
   ```

   `-k` es solo para pruebas locales con el certificado de desarrollo.
   Con sesión válida, un POST sin token CSRF debe responder **400**.

No se requieren claves para probar el login básico.
