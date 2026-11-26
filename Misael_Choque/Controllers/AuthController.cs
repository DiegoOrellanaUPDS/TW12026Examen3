using Examen_3.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using System.Security.Cryptography;

namespace Examen_3.Controllers;

[ApiController]
public class AuthController : Controller
{
    private readonly KickOAuthService _kick;

    public AuthController(KickOAuthService kick)
    {
        _kick = kick;
    }

    [HttpGet("/auth/kick/login")]
    public IActionResult Login()
    {
        string state = WebEncoders.Base64UrlEncode(
            RandomNumberGenerator.GetBytes(32));

        string verifier = _kick.CreateCodeVerifier();
        string challenge = _kick.CreateCodeChallenge(verifier);

        HttpContext.Session.SetString("KickOAuthState", state);
        HttpContext.Session.SetString("KickCodeVerifier", verifier);

        string authorizationUrl =
            _kick.CreateAuthorizationUrl(state, challenge);

        return Redirect(authorizationUrl);
    }

    [HttpGet("/auth/kick/callback")]
    public async Task<IActionResult> Callback(
        [FromQuery] string? code,
        [FromQuery] string? state,
        CancellationToken cancellationToken)
    {
        string? savedState = HttpContext.Session.GetString("KickOAuthState");
        string? verifier = HttpContext.Session.GetString("KickCodeVerifier");

        if (string.IsNullOrWhiteSpace(code) ||
            string.IsNullOrWhiteSpace(state) ||
            string.IsNullOrWhiteSpace(savedState) ||
            string.IsNullOrWhiteSpace(verifier) ||
            state != savedState)
        {
            return BadRequest("La respuesta de autenticación de Kick no es válida.");
        }

        try
        {
            string accessToken = await _kick.ExchangeCodeAsync(
                code, verifier, cancellationToken);

            KickUser user = await _kick.GetUserAsync(
                accessToken, cancellationToken);

            HttpContext.Session.Remove("KickOAuthState");
            HttpContext.Session.Remove("KickCodeVerifier");

            HttpContext.Session.SetString("AuthenticatedUserId", user.UserId);
            HttpContext.Session.SetString("AuthenticatedUsername", user.Username);
            HttpContext.Session.SetString("AuthenticationProvider", "Kick");

            return Redirect("/");
        }
        catch (Exception)
        {
            HttpContext.Session.Clear();
            return StatusCode(502,
                "No se pudo completar la autenticación con Kick. Revisa la configuración y vuelve a intentarlo.");
        }
    }

    [HttpGet("/auth/me")]
    public IActionResult Me()
    {
        string? username =
            HttpContext.Session.GetString("AuthenticatedUsername");

        if (string.IsNullOrWhiteSpace(username))
        {
            return Unauthorized(new
            {
                authenticated = false,
                message = "No hay una sesión iniciada."
            });
        }

        return Ok(new
        {
            authenticated = true,
            username,
            provider = HttpContext.Session.GetString("AuthenticationProvider"),
            userId = HttpContext.Session.GetString("AuthenticatedUserId")
        });
    }

    [HttpPost("/auth/logout")]
    public IActionResult Logout()
    {
        HttpContext.Session.Clear();

        return Ok(new
        {
            message = "La sesión se cerró correctamente."
        });
    }
}
