using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Examen3.Epic.Auth;
using Examen3.Epic.Options;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Examen3.Epic.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly IEpicOAuthClient _epic;
    private readonly IAppSessionStore _sessions;
    private readonly EpicOptions _options;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        IEpicOAuthClient epic,
        IAppSessionStore sessions,
        IOptions<EpicOptions> options,
        ILogger<AuthController> logger)
    {
        _epic = epic;
        _sessions = sessions;
        _options = options.Value;
        _logger = logger;
    }

    [HttpGet("status")]
    [AllowAnonymous]
    public IActionResult Status() => Ok(new
    {
        provider = EpicConstants.ProviderDisplayName,
        providerId = EpicConstants.Provider,
        configured = _options.IsConfigured,
        loginUrl = "/api/auth/epic/login"
    });

    [HttpGet("epic/login")]
    [AllowAnonymous]
    public IActionResult Login()
    {
        if (!_options.IsConfigured)
        {
            return Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Epic Games no configurado",
                detail: "Define Epic:ClientId y Epic:ClientSecret con dotnet user-secrets o variables de entorno.");
        }

        var state = Base64UrlEncode(RandomNumberGenerator.GetBytes(32));

        Response.Cookies.Append(
            EpicConstants.StateCookieName,
            state,
            new CookieOptions
            {
                HttpOnly = true,
                Secure = Request.IsHttps,
                SameSite = SameSiteMode.Lax,
                Path = EpicConstants.StateCookiePath,
                MaxAge = EpicConstants.StateLifetime,
                IsEssential = true
            });

        try
        {
            return Redirect(_epic.BuildAuthorizeUrl(state));
        }
        catch (EpicAuthException exception)
        {
            _logger.LogError(exception, "No se pudo construir la URL de autorización de Epic.");
            return Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Epic Games no disponible",
                detail: "Revisa la configuración del proveedor.");
        }
    }

    [HttpGet("epic/callback")]
    [AllowAnonymous]
    public async Task<IActionResult> Callback(
        [FromQuery] string? code,
        [FromQuery] string? state,
        [FromQuery] string? error,
        CancellationToken cancellationToken)
    {
        var expectedState = Request.Cookies[EpicConstants.StateCookieName];
        DeleteStateCookie();

        if (!string.IsNullOrWhiteSpace(error))
        {
            return RedirectToFrontend("authError", NormalizeProviderError(error));
        }

        if (string.IsNullOrWhiteSpace(expectedState) ||
            string.IsNullOrWhiteSpace(state) ||
            !CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(expectedState),
                Encoding.UTF8.GetBytes(state)))
        {
            return RedirectToFrontend("authError", "invalid_state");
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            return RedirectToFrontend("authError", "invalid_callback");
        }

        EpicUser user;

        try
        {
            user = await _epic.ExchangeCodeAsync(code, cancellationToken);
        }
        catch (EpicAuthException exception)
        {
            _logger.LogWarning(exception, "Falló el login con Epic: {Code}", exception.Code);
            return RedirectToFrontend("authError", NormalizeAuthError(exception.Code));
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Error inesperado durante el callback de Epic.");
            return RedirectToFrontend("authError", "login_failed");
        }

        var sessionId = _sessions.Create(
            user.AccountId,
            EpicConstants.Provider,
            user.DisplayName,
            EpicConstants.SessionLifetime);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.AccountId),
            new(ClaimTypes.Name, user.DisplayName),
            new(EpicConstants.ProviderClaim, EpicConstants.Provider),
            new(EpicConstants.SessionIdClaim, sessionId)
        };

        if (!string.IsNullOrWhiteSpace(user.AvatarUrl))
        {
            claims.Add(new Claim(EpicConstants.AvatarClaim, user.AvatarUrl));
        }

        if (!string.IsNullOrWhiteSpace(user.PreferredUsername))
        {
            claims.Add(new Claim(EpicConstants.PreferredUsernameClaim, user.PreferredUsername));
        }

        var identity = new ClaimsIdentity(claims, authenticationType: EpicConstants.Provider);

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            new AuthenticationProperties
            {
                ExpiresUtc = DateTimeOffset.UtcNow.Add(EpicConstants.SessionLifetime),
                IsPersistent = false
            });

        return RedirectToFrontend("login", "ok");
    }

    [HttpGet("me")]
    [AllowAnonymous]
    public IActionResult Me()
    {
        if (User.Identity?.IsAuthenticated != true ||
            !string.Equals(
                User.FindFirstValue(EpicConstants.ProviderClaim),
                EpicConstants.Provider,
                StringComparison.Ordinal))
        {
            return Ok(new
            {
                authenticated = false,
                provider = EpicConstants.ProviderDisplayName,
                providerId = EpicConstants.Provider,
                configured = _options.IsConfigured,
                user = (object?)null
            });
        }

        var accountId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var displayName = User.FindFirstValue(ClaimTypes.Name);

        if (string.IsNullOrWhiteSpace(accountId) || string.IsNullOrWhiteSpace(displayName))
        {
            return Ok(new
            {
                authenticated = false,
                provider = EpicConstants.ProviderDisplayName,
                providerId = EpicConstants.Provider,
                configured = _options.IsConfigured,
                user = (object?)null
            });
        }

        return Ok(new
        {
            authenticated = true,
            provider = EpicConstants.ProviderDisplayName,
            providerId = EpicConstants.Provider,
            configured = _options.IsConfigured,
            user = new
            {
                id = accountId,
                username = displayName,
                displayName,
                preferredUsername = User.FindFirstValue(EpicConstants.PreferredUsernameClaim),
                avatarUrl = User.FindFirstValue(EpicConstants.AvatarClaim),
                provider = EpicConstants.ProviderDisplayName
            }
        });
    }

    [HttpPost("logout")]
    [Authorize(Policy = EpicConstants.EpicPolicy)]
    public async Task<IActionResult> Logout()
    {
        var sessionId = User.FindFirstValue(EpicConstants.SessionIdClaim);

        if (!string.IsNullOrWhiteSpace(sessionId))
        {
            _sessions.Revoke(sessionId);
        }

        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

        return NoContent();
    }

    private IActionResult RedirectToFrontend(string key, string value) =>
        Redirect($"/?{key}={Uri.EscapeDataString(value)}");

    private void DeleteStateCookie() =>
        Response.Cookies.Delete(
            EpicConstants.StateCookieName,
            new CookieOptions
            {
                Path = EpicConstants.StateCookiePath,
                Secure = Request.IsHttps,
                SameSite = SameSiteMode.Lax
            });

    private static string NormalizeProviderError(string error) => error switch
    {
        "access_denied" => "access_denied",
        "invalid_request" => "invalid_request",
        "unauthorized_client" => "unauthorized_client",
        "unsupported_response_type" => "unsupported_response_type",
        "invalid_scope" => "invalid_scope",
        "server_error" => "server_error",
        "temporarily_unavailable" => "temporarily_unavailable",
        _ => "provider_error"
    };

    private static string NormalizeAuthError(string code) => code switch
    {
        "not_configured" => "not_configured",
        "token_exchange_failed" => "token_exchange_failed",
        "token_exchange_unavailable" => "provider_unavailable",
        "userinfo_failed" => "userinfo_failed",
        _ => "login_failed"
    };

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
}
