using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace MyApp.Namespace
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthNotionController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly IHttpClientFactory _httpClientFactory;

        public AuthNotionController(IConfiguration configuration, IHttpClientFactory httpClientFactory)
        {
            _configuration = configuration;
            _httpClientFactory = httpClientFactory;
        }

        [HttpGet("login")]
        public IActionResult Login()
        {
            var clientId = _configuration["Notion:ClientId"]!;
            var redirectUri = _configuration["Notion:RedirectUri"]!;
            var state = Guid.NewGuid().ToString("N");

            HttpContext.Session.SetString("NotionAuthState", state);

            var authorizeUrl =
                "https://api.notion.com/v1/oauth/authorize" +
                $"?client_id={Uri.EscapeDataString(clientId)}" +
                "&response_type=code" +
                "&owner=user" +
                $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
                $"&state={Uri.EscapeDataString(state)}";

            return Redirect(authorizeUrl);
        }

        [HttpGet("callback")]
        public async Task<IActionResult> Callback(
            [FromQuery] string? code,
            [FromQuery] string? state,
            [FromQuery] string? error)
        {
            if (!string.IsNullOrEmpty(error))
                return BadRequest($"Error devuelto por Notion: {error}");

            var savedState = HttpContext.Session.GetString("NotionAuthState");
            HttpContext.Session.Remove("NotionAuthState");

            if (string.IsNullOrEmpty(savedState) || !string.Equals(savedState, state, StringComparison.Ordinal))
                return Unauthorized("Estado OAuth inválido o expirado.");

            if (string.IsNullOrEmpty(code))
                return BadRequest("Falta el parámetro code.");

            var clientId = _configuration["Notion:ClientId"]!;
            var clientSecret = _configuration["Notion:ClientSecret"]!;
            var redirectUri = _configuration["Notion:RedirectUri"]!;

            var basicRaw = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}"));
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", basicRaw);
            client.DefaultRequestHeaders.Add("Notion-Version", "2022-06-28");

            var formData = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["code"] = code,
                ["redirect_uri"] = redirectUri
            });

            var tokenResponse = await client.PostAsync("https://api.notion.com/v1/oauth/token", formData);
            var tokenJson = await tokenResponse.Content.ReadAsStringAsync();

            if (!tokenResponse.IsSuccessStatusCode)
                return BadRequest($"Error al intercambiar token en Notion: {tokenJson}");

            using var doc = JsonDocument.Parse(tokenJson);
            var root = doc.RootElement;

            var accessToken = root.GetProperty("access_token").GetString()!;
            var workspaceName = root.TryGetProperty("workspace_name", out var ws) ? ws.GetString() : "Workspace Notion";

            string userName = workspaceName ?? "Usuario Notion";
            string? userEmail = null;

            if (root.TryGetProperty("owner", out var owner) &&
                owner.TryGetProperty("user", out var userObj) &&
                userObj.ValueKind == JsonValueKind.Object)
            {
                if (userObj.TryGetProperty("name", out var nameProp) && nameProp.ValueKind == JsonValueKind.String)
                    userName = nameProp.GetString() ?? userName;

                if (userObj.TryGetProperty("person", out var personProp) &&
                    personProp.TryGetProperty("email", out var emailProp) &&
                    emailProp.ValueKind == JsonValueKind.String)
                    userEmail = emailProp.GetString();
            }

            HttpContext.Session.SetString("AuthProvider", "Notion");
            HttpContext.Session.SetString("NotionAccessToken", accessToken);
            HttpContext.Session.SetString("NotionUserName", userName);
            HttpContext.Session.SetString("NotionWorkspace", workspaceName ?? "");
            if (!string.IsNullOrEmpty(userEmail))
                HttpContext.Session.SetString("NotionUserEmail", userEmail);


            var frontendUrl = (_configuration["Notion:FrontendUrl"] ?? "http://localhost:5500/FrontEnd/index.html")
            .Replace("http://127.0.0.1:", "http://localhost:");
            var separador = frontendUrl.Contains('?') ? "&" : "?";
            return Redirect($"{frontendUrl}{separador}auth=ok");
        }

        [HttpGet("me")]
        public IActionResult Me()
        {
            var provider = HttpContext.Session.GetString("AuthProvider");
            var token = HttpContext.Session.GetString("NotionAccessToken");

            if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(provider))
                return Unauthorized(new { authenticated = false });

            return Ok(new
            {
                authenticated = true,
                provider,
                userName = HttpContext.Session.GetString("NotionUserName"),
                email = HttpContext.Session.GetString("NotionUserEmail"),
                workspace = HttpContext.Session.GetString("NotionWorkspace")
            });
        }

        [HttpPost("logout")]
        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return Ok(new { message = "Sesión cerrada correctamente." });
        }
    }
}
