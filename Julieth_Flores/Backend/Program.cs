using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

var clienteId = builder.Configuration["Vimeo:ClientId"]
    ?? throw new InvalidOperationException("Falta Vimeo:ClientId en User Secrets.");
var clienteSecret = builder.Configuration["Vimeo:ClientSecret"]
    ?? throw new InvalidOperationException("Falta Vimeo:ClientSecret en User Secrets.");

builder.Services
    .AddAuthentication(opciones =>
    {
        opciones.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        opciones.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    })
    .AddCookie(opciones =>
    {
        opciones.LoginPath = "/login";
        opciones.LogoutPath = "/logout";

        // Respuestas 401/403 para las rutas protegidas rechazadas.
        opciones.Events.OnRedirectToLogin = async contexto =>
        {
            contexto.HttpContext.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await contexto.HttpContext.Response.WriteAsJsonAsync(new { mensaje = "Debes iniciar sesión con Vimeo." });
        };
        opciones.Events.OnRedirectToAccessDenied = async contexto =>
        {
            contexto.HttpContext.Response.StatusCode = StatusCodes.Status403Forbidden;
            await contexto.HttpContext.Response.WriteAsJsonAsync(new { mensaje = "Acceso denegado: se requiere Vimeo." });
        };
    })
    .AddOAuth("Vimeo", opciones =>
    {
        opciones.ClientId = clienteId;
        opciones.ClientSecret = clienteSecret;
        opciones.CallbackPath = "/signin-vimeo";
        opciones.AuthorizationEndpoint = "https://api.vimeo.com/oauth/authorize";
        opciones.TokenEndpoint = "https://api.vimeo.com/oauth/access_token";
        opciones.UserInformationEndpoint = "https://api.vimeo.com/me";
        opciones.Scope.Add("public");
        opciones.SaveTokens = true;

        // Pedimos el perfil a Vimeo con el token del usuario.
        opciones.Events.OnCreatingTicket = async contexto =>
        {
            using var peticion = new HttpRequestMessage(HttpMethod.Get, "https://api.vimeo.com/me");
            peticion.Headers.Authorization = new AuthenticationHeaderValue("Bearer", contexto.AccessToken);

            using var respuesta = await contexto.Backchannel.SendAsync(peticion, contexto.HttpContext.RequestAborted);
            respuesta.EnsureSuccessStatusCode();

            using var datos = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync(contexto.HttpContext.RequestAborted));
            var perfil = datos.RootElement;

            var nombre = perfil.TryGetProperty("name", out var datoNombre)
                ? datoNombre.GetString() : null;
            var uri = perfil.TryGetProperty("uri", out var datoUri)
                ? datoUri.GetString() : null;
            var id = uri?.TrimEnd('/').Split('/').LastOrDefault();

            if (contexto.Identity is null ||
                string.IsNullOrWhiteSpace(nombre) ||
                string.IsNullOrWhiteSpace(id))
                throw new InvalidOperationException("No se pudo obtener la identidad de Vimeo.");

            contexto.Identity.AddClaim(new Claim(ClaimTypes.Name, nombre));
            contexto.Identity.AddClaim(new Claim("vimeo_id", id));
            contexto.Identity.AddClaim(new Claim("proveedor", "Vimeo"));
        };
    });

builder.Services.AddControllers();

builder.Services.AddAuthorization(opciones =>
{
    opciones.AddPolicy("SoloVimeo", politica =>
        politica.RequireAuthenticatedUser()
                .RequireClaim("proveedor", "Vimeo"));
});

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapGet("/login", () => Results.Challenge(
    new AuthenticationProperties { RedirectUri = "/" },
    new[] { "Vimeo" }));

app.MapGet("/logout", async (HttpContext contexto) =>
{
    await contexto.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Redirect("/");
});

app.MapGet("/usuario", (ClaimsPrincipal usuario) => Results.Ok(new
{
    nombre = usuario.Identity?.Name,
    id = usuario.FindFirst("vimeo_id")?.Value,
    proveedor = usuario.FindFirst("proveedor")?.Value
})).RequireAuthorization("SoloVimeo");

app.Run();
