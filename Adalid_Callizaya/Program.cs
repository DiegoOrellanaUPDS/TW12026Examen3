using System.Collections.Concurrent;
using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

var builder = WebApplication.CreateBuilder(args);
const string cookie = "ExamenCookie";
const string provider = "Steam";
const string policy = "SoloSteam";
var sessions = new ConcurrentDictionary<string, DateTimeOffset>();
var registros = new ConcurrentQueue<Registro>();

builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = cookie;
    options.DefaultChallengeScheme = cookie;
})
.AddCookie(cookie, options =>
{
    options.Cookie.Name = "Examen3.Adalid.Session";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.ExpireTimeSpan = TimeSpan.FromHours(2);
    options.SlidingExpiration = false;
    options.Events.OnRedirectToLogin = ctx =>
    {
        ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return ctx.Response.WriteAsJsonAsync(new { mensaje = "Debes iniciar sesion con Steam." });
    };
    options.Events.OnRedirectToAccessDenied = ctx =>
    {
        ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
        return ctx.Response.WriteAsJsonAsync(new { mensaje = "Acceso denegado." });
    };
    options.Events.OnValidatePrincipal = ctx =>
    {
        var sid = ctx.Principal?.FindFirstValue("sid");
        if (sid is null || !sessions.TryGetValue(sid, out var expiry) || expiry <= DateTimeOffset.UtcNow)
        {
            if (sid is not null) sessions.TryRemove(sid, out _);
            ctx.RejectPrincipal();
        }
        return Task.CompletedTask;
    };
})
.AddSteam(options =>
{
    options.SignInScheme = cookie;
    options.CallbackPath = builder.Configuration["Steam:CallbackPath"] ?? "/signin-steam";
    options.ApplicationKey = builder.Configuration["Steam:ApplicationKey"];
    // El callback de Steam vuelve por una navegacion GET; Lax permite usar localhost.
    options.CorrelationCookie.SameSite = SameSiteMode.Lax;
    options.CorrelationCookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.Events.OnTicketReceived = ctx =>
    {
        // Este evento ocurre despues de que el middleware verifica la respuesta OpenID.
        var identifier = ctx.Principal?.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
        var match = Regex.Match(identifier, @"^https?://steamcommunity\.com/openid/id/([0-9]{17})$");
        if (!match.Success)
        {
            ctx.Fail("SteamID64 invalido.");
            return Task.CompletedTask;
        }
        var steamId = match.Groups[1].Value;
        var name = ctx.Principal?.Identity?.Name ?? "Steam " + steamId;
        var sid = Guid.NewGuid().ToString("N");
        var now = DateTimeOffset.UtcNow;
        foreach (var entry in sessions)
            if (entry.Value <= now) sessions.TryRemove(entry.Key, out _);
        sessions[sid] = now.AddHours(2);
        ctx.Properties!.IssuedUtc = now;
        ctx.Properties.ExpiresUtc = now.AddHours(2);
        ctx.Principal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, steamId),
            new Claim(ClaimTypes.Name, name),
            new Claim("provider", provider),
            new Claim("sid", sid)
        }, cookie));
        return Task.CompletedTask;
    };
    options.Events.OnRemoteFailure = ctx =>
    {
        ctx.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>()
            .CreateLogger("SteamLogin").LogWarning(ctx.Failure, "Fallo de autenticacion Steam");
        ctx.HandleResponse();
        ctx.Response.Redirect("/?error=steam");
        return Task.CompletedTask;
    };
});

builder.Services.AddAuthorization(options => options.AddPolicy(policy, p =>
{
    p.AddAuthenticationSchemes(cookie);
    p.RequireAuthenticatedUser();
    p.RequireClaim("provider", provider);
    p.RequireClaim(ClaimTypes.NameIdentifier);
}));

var app = builder.Build();
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/auth/login", (HttpContext ctx) =>
    ctx.User.Identity?.IsAuthenticated == true
        ? Results.Redirect("/")
        : Results.Challenge(new AuthenticationProperties { RedirectUri = "/" }, new[] { provider }));

app.MapGet("/api/sesion", (HttpContext ctx) => Results.Ok(new
{
    autenticado = true,
    proveedor = ctx.User.FindFirstValue("provider"),
    steamId = ctx.User.FindFirstValue(ClaimTypes.NameIdentifier),
    nombre = ctx.User.Identity?.Name
})).RequireAuthorization(policy);

app.MapPost("/auth/logout", async (HttpContext ctx) =>
{
    if (!EsPeticionLocal(ctx)) return Results.BadRequest(new { mensaje = "Falta cabecera X-Examen." });
    var sid = ctx.User.FindFirstValue("sid");
    if (sid is not null) sessions.TryRemove(sid, out _);
    await ctx.SignOutAsync(cookie);
    return Results.Ok(new { mensaje = "Sesion cerrada e invalidada en el servidor." });
}).RequireAuthorization(policy);

app.MapPost("/api/registros", (NuevoRegistro entrada, HttpContext ctx) =>
{
    if (!EsPeticionLocal(ctx)) return Results.BadRequest(new { mensaje = "Falta cabecera X-Examen." });
    var texto = entrada.Texto?.Trim();
    if (string.IsNullOrWhiteSpace(texto) || texto.Length > 200)
        return Results.BadRequest(new { mensaje = "Escribe entre 1 y 200 caracteres." });
    var registro = new Registro(Guid.NewGuid(), texto, ctx.User.FindFirstValue(ClaimTypes.NameIdentifier)!, DateTimeOffset.UtcNow);
    registros.Enqueue(registro);
    return Results.Json(registro, statusCode: StatusCodes.Status201Created);
}).RequireAuthorization(policy);

app.MapGet("/api/registros", (HttpContext ctx) =>
{
    var steamId = ctx.User.FindFirstValue(ClaimTypes.NameIdentifier);
    return Results.Ok(registros.Where(r => r.SteamId == steamId).ToArray());
}).RequireAuthorization(policy);

app.Run();

// Cabecera no simple: un sitio de otro origen no puede enviarla sin preflight autorizado.
static bool EsPeticionLocal(HttpContext ctx) => ctx.Request.Headers["X-Examen"] == "1";
record NuevoRegistro(string? Texto);
record Registro(Guid Id, string Texto, string SteamId, DateTimeOffset Fecha);
