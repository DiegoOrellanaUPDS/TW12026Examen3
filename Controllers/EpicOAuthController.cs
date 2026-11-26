using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using Examen_3.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;

namespace Examen_3.Controllers;

[ApiController]
[Route("api/oauth/epic")]
public sealed class EpicOAuthController(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration) : ControllerBase
{
    private const string EpicAuthorizeUrl = "https://www.epicgames.com/id/authorize";
    private const string EpicTokenUrl =
        "https://api.epicgames.dev/epic/oauth/v2/token";

    private static readonly string[] AllowedReturnOrigins =
    [
        "http://127.0.0.1:5500",
        "http://localhost:5500",
        "http://localhost:5170",
        "https://localhost:7010"
    ];

    [HttpGet("login")]
    [ProducesResponseType(StatusCodes.Status302Found)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public IActionResult Login([FromQuery] string? returnUrl = null)
    {
        var clientId = configuration["Epic:ClientId"];
        var redirectUri = configuration["Epic:RedirectUri"];
        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(redirectUri))
        {
            return Problem(
                title: "Configuración OAuth incompleta",
                detail: "Configura Epic:ClientId y Epic:RedirectUri en el archivo .env.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        var state = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        HttpContext.Session.SetString(EpicSessionHelper.StateSessionKey, state);

        var safeReturnUrl = IsAllowedReturnUrl(returnUrl) ? returnUrl! : "/";
        HttpContext.Session.SetString(EpicSessionHelper.ReturnUrlSessionKey, safeReturnUrl);

        var authorizationUrl = QueryHelpers.AddQueryString(EpicAuthorizeUrl, new Dictionary<string, string?>
        {
            ["client_id"] = clientId,
            ["response_type"] = "code",
            ["scope"] = "basic_profile",
            ["redirect_uri"] = redirectUri,
            ["state"] = state
        });

        return Redirect(authorizationUrl);
    }

    [HttpGet("callback")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Callback(
        [FromQuery] string? code,
        [FromQuery] string? state,
        CancellationToken cancellationToken)
    {
        var expectedState = HttpContext.Session.GetString(EpicSessionHelper.StateSessionKey);
        HttpContext.Session.Remove(EpicSessionHelper.StateSessionKey);

        if (string.IsNullOrWhiteSpace(code) ||
            string.IsNullOrWhiteSpace(state) ||
            string.IsNullOrWhiteSpace(expectedState) ||
            !CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(state),
                Encoding.UTF8.GetBytes(expectedState)))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Respuesta OAuth inválida",
                Detail = "El código de autorización no es válido o la sesión expiró.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        var clientId = configuration["Epic:ClientId"];
        var clientSecret = configuration["Epic:ClientSecret"];
        var redirectUri = configuration["Epic:RedirectUri"];
        if (string.IsNullOrWhiteSpace(clientId) ||
            string.IsNullOrWhiteSpace(clientSecret) ||
            string.IsNullOrWhiteSpace(redirectUri))
        {
            return Problem(
                title: "Configuración OAuth incompleta",
                detail: "Configura Epic:ClientId, Epic:ClientSecret y Epic:RedirectUri en el archivo .env.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, EpicTokenUrl);
        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = redirectUri
        });

        using var response = await httpClientFactory.CreateClient()
            .SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            return Problem(
                title: "No se pudo completar la autorización de Epic Games",
                detail: $"Epic HTTP {(int)response.StatusCode}: {errorBody}",
                statusCode: StatusCodes.Status502BadGateway);
        }

        var token = await response.Content.ReadFromJsonAsync<EpicTokenResponse>(
            cancellationToken: cancellationToken);
        if (token is null || string.IsNullOrWhiteSpace(token.AccessToken))
        {
            return Problem(
                title: "Respuesta inválida de Epic Games",
                statusCode: StatusCodes.Status502BadGateway);
        }

        var expiresAt = DateTimeOffset.UtcNow.AddSeconds(Math.Max(0, token.ExpiresIn));
        EpicSessionHelper.SetToken(HttpContext.Session, token.AccessToken, expiresAt);

        var returnUrl = HttpContext.Session.GetString(EpicSessionHelper.ReturnUrlSessionKey);
        HttpContext.Session.Remove(EpicSessionHelper.ReturnUrlSessionKey);

        return IsAllowedReturnUrl(returnUrl)
            ? Redirect(returnUrl!)
            : LocalRedirect("/");
    }

    private bool IsAllowedReturnUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return false;
        }

        if (Url.IsLocalUrl(url))
        {
            return true;
        }

        return Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
            AllowedReturnOrigins.Contains(uri.GetLeftPart(UriPartial.Authority), StringComparer.OrdinalIgnoreCase);
    }

    [HttpGet("authorization")]
    [ProducesResponseType(typeof(EpicAuthorizationStatus), StatusCodes.Status200OK)]
    public IActionResult GetAuthorization()
    {
        var (isAuthorized, expiresAt) = EpicSessionHelper.GetStatus(HttpContext.Session);
        return Ok(new EpicAuthorizationStatus(isAuthorized, expiresAt));
    }

    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public IActionResult Logout()
    {
        EpicSessionHelper.Clear(HttpContext.Session);
        return NoContent();
    }

    private sealed record EpicTokenResponse(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn);

    /// <summary>Estado de autorización de la sesión actual con Epic Games.</summary>
    public sealed record EpicAuthorizationStatus(bool IsAuthorized, DateTimeOffset? ExpiresAt);
}
