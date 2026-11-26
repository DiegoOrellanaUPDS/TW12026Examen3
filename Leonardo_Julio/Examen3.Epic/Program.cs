using System.Security.Claims;
using Examen3.Epic.Auth;
using Examen3.Epic.Options;
using Examen3.Epic.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.HttpOverrides;

var builder = WebApplication.CreateBuilder(args);

// Epic no acepta http://localhost como redirect URI. Para probar el login real
// se usa un túnel HTTPS (ngrok/cloudflared) que llega al app por loopback.
// El middleware confía en los proxies locales y respeta X-Forwarded-Proto/Host.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders =
        ForwardedHeaders.XForwardedFor |
        ForwardedHeaders.XForwardedProto |
        ForwardedHeaders.XForwardedHost;
});

builder.Services.Configure<EpicOptions>(builder.Configuration.GetSection(EpicOptions.SectionName));
builder.Services.Configure<StorageOptions>(builder.Configuration.GetSection(StorageOptions.SectionName));

builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);
builder.Services.AddSingleton<IAppSessionStore, InMemoryAppSessionStore>();
builder.Services.AddSingleton<INoteStore, JsonNoteStore>();
builder.Services.AddHttpClient<IEpicOAuthClient, EpicOAuthClient>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(20);
});

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = EpicConstants.SessionCookieName;
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.Cookie.IsEssential = true;
        options.ExpireTimeSpan = EpicConstants.SessionLifetime;
        options.SlidingExpiration = false;
        options.LoginPath = "/api/auth/epic/login";
        options.AccessDeniedPath = "/";

        options.Events.OnValidatePrincipal = async context =>
        {
            var sessionId = context.Principal?.FindFirstValue(EpicConstants.SessionIdClaim);
            var sessions = context.HttpContext.RequestServices.GetRequiredService<IAppSessionStore>();

            if (string.IsNullOrWhiteSpace(sessionId) || !sessions.IsActive(sessionId))
            {
                context.RejectPrincipal();
                await context.HttpContext.SignOutAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme);
            }
        };

        options.Events.OnRedirectToLogin = context =>
            WriteAuthProblem(
                context,
                StatusCodes.Status401Unauthorized,
                "No autenticado",
                "Inicia sesión con Epic Games para acceder a este recurso.");

        options.Events.OnRedirectToAccessDenied = context =>
            WriteAuthProblem(
                context,
                StatusCodes.Status403Forbidden,
                "Acceso denegado",
                "La sesión actual no fue creada con el proveedor Epic Games.");
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(EpicConstants.EpicPolicy, policy =>
        policy.RequireAuthenticatedUser()
            .RequireClaim(EpicConstants.ProviderClaim, EpicConstants.Provider));
});

builder.Services.AddControllers();
builder.Services.AddProblemDetails();

var app = builder.Build();

app.UseForwardedHeaders();

app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    context.Response.Headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
    context.Response.Headers["Content-Security-Policy"] =
        "default-src 'self'; img-src 'self' https: data:; style-src 'self'; script-src 'self'; " +
        "connect-src 'self'; base-uri 'none'; form-action 'self'; frame-ancestors 'none'";
    await next();
});

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler(errorApp =>
    {
        errorApp.Run(async context =>
        {
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/problem+json";
            await context.Response.WriteAsJsonAsync(new
            {
                status = StatusCodes.Status500InternalServerError,
                title = "Error interno",
                detail = "No se pudo procesar la solicitud."
            });
        });
    });
    app.UseHttpsRedirection();
}

// Defensa adicional contra CSRF: los POST de la API deben venir del mismo origen.
app.Use(async (context, next) =>
{
    if (HttpMethods.IsPost(context.Request.Method) &&
        context.Request.Path.StartsWithSegments("/api") &&
        context.Request.Headers.TryGetValue("Origin", out var origin) &&
        !string.IsNullOrWhiteSpace(origin))
    {
        var expected = $"{context.Request.Scheme}://{context.Request.Host}";

        if (!string.Equals(
                origin.ToString().TrimEnd('/'),
                expected.TrimEnd('/'),
                StringComparison.OrdinalIgnoreCase))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new
            {
                status = StatusCodes.Status403Forbidden,
                title = "Origen no permitido",
                detail = "La petición no proviene del frontend de la aplicación."
            });
            return;
        }
    }

    await next();
});

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapGet("/health", () => Results.Ok(new
{
    status = "ok",
    provider = EpicConstants.ProviderDisplayName,
    utc = DateTimeOffset.UtcNow
}));

app.Run();

static Task WriteAuthProblem(
    RedirectContext<CookieAuthenticationOptions> context,
    int statusCode,
    string title,
    string detail)
{
    if (!context.Request.Path.StartsWithSegments("/api"))
    {
        context.Response.Redirect(context.RedirectUri);
        return Task.CompletedTask;
    }

    context.Response.StatusCode = statusCode;
    return context.Response.WriteAsJsonAsync(new
    {
        status = statusCode,
        title,
        detail,
        loginUrl = "/api/auth/epic/login"
    });
}

public partial class Program { }
