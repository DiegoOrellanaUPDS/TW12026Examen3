# Examen 3 — API + Autenticación con Epic Games

Proyecto en **.NET 10** con **Entity Framework Core + PostgreSQL**, documentación interactiva con **Scalar**, autenticación OAuth 2.0 con **Epic Games** y un frontend básico en `FE/index.html`.

---

## 1. Requisitos previos

- [.NET 10 SDK](https://dotnet.microsoft.com/)
- [PostgreSQL](https://www.postgresql.org/) corriendo en local (por defecto en `localhost:5432`)
- Herramienta CLI de EF Core (si necesitas aplicar migraciones):
  ```bash
  dotnet tool install --global dotnet-ef
  ```
- Una aplicación configurada en el [Epic Developer Portal](https://dev.epicgames.com/) con **Epic Account Services**

---

## 2. Configurar PostgreSQL

La cadena de conexión por defecto en `appsettings.json` es:

```json
"ConnectionStrings": {
  "DefaultConnection": "Host=localhost;Port=5432;Database=Examen3Db;Username=postgres;Password=as"
}
```

Ajústala en `appsettings.json` (o mediante variables de entorno) si tu usuario, contraseña o puerto de PostgreSQL son distintos.

---

## 3. Configurar variables de entorno (`.env`)

Crea un archivo `.env` en la raíz del proyecto con las credenciales de tu aplicación en Epic Games:

```env
Epic__ClientId=TU_CLIENT_ID
Epic__ClientSecret=TU_CLIENT_SECRET
Epic__RedirectUri=http://localhost:5170/api/oauth/epic/callback
```

### Configuración en el Epic Developer Portal
1. En **Epic Account Services → Permissions**, asegúrate de tener habilitado únicamente **Basic Profile** (el backend solicita `scope=basic_profile`).
2. En **Redirect URIs**, registra exactamente:
   ```text
   http://localhost:5170/api/oauth/epic/callback
   ```

---

## 4. Restaurar dependencias, aplicar migraciones y ejecutar

```bash
# 1. Restaurar paquetes
dotnet restore

# 2. Crear/actualizar la base de datos en PostgreSQL
dotnet ef database update

# 3. Iniciar el backend (por defecto en http://localhost:5170)
dotnet run
```

---

## 5. Usar el Frontend y probar los endpoints

- **Frontend servido por el propio backend (recomendado):**
  Abre `http://localhost:5170/` en tu navegador.
- **Frontend con Live Server:**
  Si usas Live Server en el puerto `5500`, ábrelo como `http://localhost:5500/FE/index.html` (usando `localhost`, no `127.0.0.1`, para que el navegador comparta la cookie de sesión `SameSite=Lax` con `http://localhost:5170`).
- **Documentación Scalar (en modo Development):**
  `http://localhost:5170/scalar/v1`

### Endpoints principales

| Método | Ruta | Descripción | Requiere sesión Epic |
| :--- | :--- | :--- | :--- |
| `GET` | `/api/oauth/epic/login` | Inicia el flujo OAuth con Epic Games | No |
| `GET` | `/api/oauth/epic/callback` | Recibe el código de Epic, guarda la sesión y redirige al frontend | No |
| `POST` | `/api/oauth/epic/logout` | Cierra la sesión actual | No |
| `GET` | `/api/auth/status` | Devuelve si el usuario está autenticado o no | No |
| `GET` | `/api/usuarios` | Lista todos los usuarios | Sí (`401` si no hay sesión) |
| `GET` | `/api/usuarios/{id}` | Obtiene un usuario por ID | Sí (`401` si no hay sesión) |
| `POST` | `/api/usuarios` | Crea un nuevo usuario | No |
