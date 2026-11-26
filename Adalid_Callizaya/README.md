# Examen 3 - Tecnología Web I

- Estudiante: Adalid Aebi Callizaya Huarahuara
- Opción asignada: C (Reddit o Steam)
- Proveedor elegido: Steam
- Rama: `feature/adalid-callizaya-examen3`

## Preparación y ejecución

Requiere SDK .NET 10, conexión a Internet y una cuenta de Steam. Desde esta carpeta:

```bash
dotnet restore
dotnet build
dotnet run --no-launch-profile
```

Abrir `http://localhost:5080` (usar el mismo host durante todo el login). El callback es `http://localhost:5080/signin-steam`. Steam utiliza OpenID 2.0, no OAuth 2.0 ni OpenID Connect. No requiere registrar un cliente ni obtener Client ID o Client Secret. El SteamID64 basta para identificar al usuario. `appsettings.json` contiene la configuración de ejemplo. La clave opcional para obtener el nombre del perfil puede configurarse, si se dispone de ella, con `dotnet user-secrets set Steam:ApplicationKey 'TU_CLAVE'` o la variable `Steam__ApplicationKey`, nunca en Git. No hace falta para la prueba.

## Funcionamiento

El middleware inicia el desafío OpenID, valida la correlación y la respuesta con Steam. Solo después del éxito se crea una identidad local con SteamID64, proveedor Steam y un identificador de sesión generado por el servidor. La cookie protegida permite identificar la sesión en peticiones posteriores. La política SoloSteam exige autenticación y el claim de proveedor emitido por el backend. El cierre de sesión revoca el identificador en el servidor y elimina la cookie. No cierra la cuenta de Steam en otros sitios.

| Método | Ruta | Resultado esperado |
| --- | --- | --- |
| GET | `/auth/login` | Redirección a Steam |
| GET | `/api/sesion` | Identidad o 401 |
| POST | `/api/registros` | 201 con sesión; 401 sin sesión |
| GET | `/api/registros` | 200 con sesión; 401 sin sesión |
| POST | `/auth/logout` | 200 con sesión; 401 sin sesión |

Los POST requieren la cabecera `X-Examen: 1` como protección adicional frente a peticiones de otros orígenes. El frontend la agrega automáticamente. Los registros se filtran por SteamID; se conservan durante la ejecución del proceso. Reiniciar la aplicación elimina registros y sesiones. Es una demostración local; en producción se debe usar HTTPS y almacenamiento persistente/compartido.

## Pruebas

Sin sesión, desde otra terminal:

```bash
curl -i http://localhost:5080/api/registros
curl -i -X POST http://localhost:5080/api/registros -H 'Content-Type: application/json' -H 'X-Examen: 1' -d '{"texto":"Prueba sin sesion"}'
```

Ambas solicitudes deben responder 401. En la página iniciar sesión con Steam, verificar SteamID64 y proveedor, guardar un dato (201) y consultar (200). Cerrar sesión (200) y repetir GET y POST: deben responder 401. Las respuestas se muestran en pantalla. Estos son resultados esperados; las pruebas deben ejecutarse antes de publicar.

## GitFlow

El trabajo está separado en esta carpeta; no modifica el proyecto base. La rama feature nace de develop y se entrega mediante una sola subida final y una única Pull Request hacia `DiegoOrellanaUPDS/TW12026Examen3:develop`. La integración corresponde al docente.

Referencias: https://partner.steamgames.com/doc/features/auth y https://www.nuget.org/packages/AspNet.Security.OpenId.Steam/10.0.0
