using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Examen_3.Controllers;

[ApiController]
[Route("auth/vimeo")]
public class VimeoAuthController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;

    public VimeoAuthController(
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory)
    {
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
    }

    [HttpGet("login")]
    public IActionResult Login()
    {
        var clientId = _configuration["Vimeo:ClientId"];
        var redirectUri = _configuration["Vimeo:RedirectUri"];

        if (string.IsNullOrWhiteSpace(clientId) ||
            string.IsNullOrWhiteSpace(redirectUri))
        {
            return StatusCode(500, "Configuración de Vimeo incompleta.");
        }

        var state = Convert.ToBase64String(
            RandomNumberGenerator.GetBytes(32));

        Response.Cookies.Append("Vimeo.State", state, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            Expires = DateTimeOffset.UtcNow.AddMinutes(5),
            IsEssential = true
        });

        var authorizationUrl =
            "https://api.vimeo.com/oauth/authorize" +
            $"?response_type=code" +
            $"&client_id={Uri.EscapeDataString(clientId)}" +
            $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
            $"&scope=public" +
            $"&state={Uri.EscapeDataString(state)}";

        return Redirect(authorizationUrl);
    }

    [HttpGet("callback")]
    public async Task<IActionResult> Callback(
        [FromQuery] string? code,
        [FromQuery] string? state,
        [FromQuery] string? error)
    {
        if (!string.IsNullOrEmpty(error))
            return BadRequest("Vimeo rechazó la autenticación.");

        var savedState = Request.Cookies["Vimeo.State"];

        Response.Cookies.Delete("Vimeo.State");

        if (string.IsNullOrEmpty(code) ||
            string.IsNullOrEmpty(state) ||
            string.IsNullOrEmpty(savedState))
        {
            return BadRequest("Respuesta OAuth incompleta.");
        }

        var receivedBytes = Encoding.UTF8.GetBytes(state);
        var savedBytes = Encoding.UTF8.GetBytes(savedState);

        if (receivedBytes.Length != savedBytes.Length ||
            !CryptographicOperations.FixedTimeEquals(
                receivedBytes, savedBytes))
        {
            return BadRequest("Estado OAuth inválido.");
        }

        var clientId = _configuration["Vimeo:ClientId"];
        var clientSecret = _configuration["Vimeo:ClientSecret"];
        var redirectUri = _configuration["Vimeo:RedirectUri"];

        if (string.IsNullOrWhiteSpace(clientId) ||
            string.IsNullOrWhiteSpace(clientSecret) ||
            string.IsNullOrWhiteSpace(redirectUri))
        {
            return StatusCode(500, "Configuración OAuth incompleta.");
        }

        var http = _httpClientFactory.CreateClient();

        var credentials = Convert.ToBase64String(
            Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}"));

        using var tokenRequest = new HttpRequestMessage(
            HttpMethod.Post,
            "https://api.vimeo.com/oauth/access_token");

        tokenRequest.Headers.Authorization =
            new AuthenticationHeaderValue("Basic", credentials);

        tokenRequest.Headers.TryAddWithoutValidation(
            "Accept",
            "application/vnd.vimeo.*+json;version=3.4");

        tokenRequest.Content = new StringContent(
            JsonSerializer.Serialize(new
            {
                grant_type = "authorization_code",
                code,
                redirect_uri = redirectUri
            }),
            Encoding.UTF8,
            "application/json");

        using var tokenResponse = await http.SendAsync(tokenRequest);

        if (!tokenResponse.IsSuccessStatusCode)
        {
            return StatusCode(
                502,
                "Vimeo no permitió intercambiar el código OAuth.");
        }

        using var tokenJson = JsonDocument.Parse(
            await tokenResponse.Content.ReadAsStringAsync());

        var accessToken = tokenJson.RootElement
            .GetProperty("access_token")
            .GetString();

        if (string.IsNullOrWhiteSpace(accessToken))
            return StatusCode(502, "Vimeo no devolvió un token válido.");

        using var userRequest = new HttpRequestMessage(
            HttpMethod.Get,
            "https://api.vimeo.com/me");

        userRequest.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);

        userRequest.Headers.TryAddWithoutValidation(
            "Accept",
            "application/vnd.vimeo.*+json;version=3.4");

        using var userResponse = await http.SendAsync(userRequest);

        if (!userResponse.IsSuccessStatusCode)
        {
            return StatusCode(
                502,
                "No se pudo verificar la identidad en Vimeo.");
        }

        using var userJson = JsonDocument.Parse(
            await userResponse.Content.ReadAsStringAsync());

        var user = userJson.RootElement;

        var userUri = user.GetProperty("uri").GetString();
        var userName = user.GetProperty("name").GetString();

        if (string.IsNullOrWhiteSpace(userUri))
            return StatusCode(502, "Vimeo devolvió una identidad inválida.");

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userUri),
            new(ClaimTypes.Name, userName ?? "Usuario Vimeo"),
            new("identity_provider", "Vimeo")
        };

        if (user.TryGetProperty("link", out var link))
        {
            claims.Add(new Claim("vimeo_link", link.GetString() ?? ""));
        }

        var identity = new ClaimsIdentity(
            claims,
            CookieAuthenticationDefaults.AuthenticationScheme);

        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal);

        var frontendUrl = _configuration["Frontend:BaseUrl"]
            ?? "http://localhost:5173";

        return Redirect(frontendUrl);
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(
            CookieAuthenticationDefaults.AuthenticationScheme);

        return NoContent();
    }


    [HttpGet("me")]
    public IActionResult Me()
    {
        if (User.Identity?.IsAuthenticated != true)
            return Unauthorized();

        return Ok(new
        {
            Usuario = User.Identity.Name,
            Identificador = User.FindFirst(
                ClaimTypes.NameIdentifier)?.Value,
            Proveedor = User.FindFirst(
                "identity_provider")?.Value
        });
    }

}
