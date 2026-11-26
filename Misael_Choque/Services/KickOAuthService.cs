using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace Examen_3.Services;

public class KickOAuthService
{
    private readonly HttpClient _http;
    private readonly KickOptions _options;

    public KickOAuthService(HttpClient http, IOptions<KickOptions> options)
    {
        _http = http;
        _options = options.Value;
    }

    public string CreateCodeVerifier()
    {
        return WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(64));
    }

    public string CreateCodeChallenge(string verifier)
    {
        byte[] hash = SHA256.HashData(Encoding.ASCII.GetBytes(verifier));
        return WebEncoders.Base64UrlEncode(hash);
    }

    public string CreateAuthorizationUrl(string state, string challenge)
    {
        var parameters = new Dictionary<string, string?>
        {
            ["client_id"] = _options.ClientId,
            ["response_type"] = "code",
            ["redirect_uri"] = _options.RedirectUri,
            ["scope"] = _options.Scope,
            ["state"] = state,
            ["code_challenge"] = challenge,
            ["code_challenge_method"] = "S256"
        };

        return QueryHelpers.AddQueryString(
            "https://id.kick.com/oauth/authorize",
            parameters);
    }

    public async Task<string> ExchangeCodeAsync(
        string code,
        string verifier,
        CancellationToken cancellationToken = default)
    {
        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["client_id"] = _options.ClientId,
            ["client_secret"] = _options.ClientSecret,
            ["redirect_uri"] = _options.RedirectUri,
            ["code_verifier"] = verifier,
            ["code"] = code
        };

        using var response = await _http.PostAsync(
            "https://id.kick.com/oauth/token",
            new FormUrlEncodedContent(form),
            cancellationToken);

        string content = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Kick rechazó el intercambio del código: {(int)response.StatusCode} {content}");
        }

        using var json = JsonDocument.Parse(content);

        return json.RootElement.GetProperty("access_token").GetString()
            ?? throw new InvalidOperationException("Kick no devolvió un access token.");
    }

    public async Task<KickUser> GetUserAsync(
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "https://api.kick.com/public/v1/users");

        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await _http.SendAsync(request, cancellationToken);
        string content = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"No se pudo consultar el usuario de Kick: {(int)response.StatusCode} {content}");
        }

        using var json = JsonDocument.Parse(content);
        var users = json.RootElement.GetProperty("data");

        if (users.GetArrayLength() == 0)
        {
            throw new InvalidOperationException("Kick no devolvió información del usuario.");
        }

        var user = users[0];

        string username =
            user.TryGetProperty("name", out var name) ? name.GetString() ?? "Usuario Kick" :
            user.TryGetProperty("username", out var userName) ? userName.GetString() ?? "Usuario Kick" :
            "Usuario Kick";

        string userId =
            user.TryGetProperty("user_id", out var id) ? id.ToString() : "";

        return new KickUser(userId, username);
    }
}

public record KickUser(string UserId, string Username);
