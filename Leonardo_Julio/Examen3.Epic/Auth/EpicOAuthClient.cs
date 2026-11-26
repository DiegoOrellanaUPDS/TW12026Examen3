using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Examen3.Epic.Options;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace Examen3.Epic.Auth;

/// <summary>
/// Implementa el flujo OAuth 2.0 de Epic Games (Epic Account Services).
/// El client secret y el access token nunca salen del servidor.
/// </summary>
public sealed class EpicOAuthClient : IEpicOAuthClient
{
    private const string JsonMediaType = "application/json";
    private const string UserInfoErrorCode = "userinfo_failed";

    private readonly HttpClient _httpClient;
    private readonly EpicOptions _options;
    private readonly ILogger<EpicOAuthClient> _logger;

    public EpicOAuthClient(
        HttpClient httpClient,
        IOptions<EpicOptions> options,
        ILogger<EpicOAuthClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public string BuildAuthorizeUrl(string state)
    {
        EnsureConfigured();

        var query = new Dictionary<string, string?>
        {
            ["client_id"] = _options.ClientId,
            ["response_type"] = "code",
            ["scope"] = _options.EffectiveScope,
            ["redirect_uri"] = _options.RedirectUri,
            ["state"] = state
        };

        return QueryHelpers.AddQueryString(_options.AuthorizationEndpoint, query);
    }

    public async Task<EpicUser> ExchangeCodeAsync(
        string code,
        CancellationToken cancellationToken)
    {
        EnsureConfigured();

        var token = await RequestTokenAsync(code, cancellationToken);

        try
        {
            var user = await RequestUserInfoAsync(
                token.AccessToken,
                token.AccountId,
                cancellationToken);

            // El token solo se usa para leer la identidad. Se revoca de inmediato
            // para no conservar acceso al perfil en el servidor.
            await RevokeTokenAsync(token.AccessToken, CancellationToken.None);

            return user;
        }
        catch (EpicAuthException)
        {
            await RevokeTokenAsync(token.AccessToken, CancellationToken.None);
            throw;
        }
        catch (Exception exception) when (
            exception is HttpRequestException or TaskCanceledException or JsonException)
        {
            await RevokeTokenAsync(token.AccessToken, CancellationToken.None);
            throw new EpicAuthException(
                UserInfoErrorCode,
                "No se pudo leer el perfil de Epic Games.",
                exception);
        }
    }

    private async Task<TokenResponse> RequestTokenAsync(
        string code,
        CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, _options.TokenEndpoint);
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", BasicCredentials());
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(JsonMediaType));

            var form = new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["code"] = code,
                ["redirect_uri"] = _options.RedirectUri,
                ["scope"] = _options.EffectiveScope
            };

            if (_options.HasDeploymentId)
            {
                form["deployment_id"] = _options.DeploymentId;
            }

            request.Content = new FormUrlEncodedContent(form);

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Epic rechazó el canje del código con HTTP {StatusCode}.",
                    (int)response.StatusCode);

                throw new EpicAuthException(
                    "token_exchange_failed",
                    "Epic Games rechazó el código de autorización.");
            }

            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;

            if (!root.TryGetProperty("access_token", out var accessTokenElement) ||
                accessTokenElement.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(accessTokenElement.GetString()))
            {
                throw new EpicAuthException(
                    "token_exchange_failed",
                    "Epic Games no devolvió un access token válido.");
            }

            var accountId = GetString(root, "account_id");

            return new TokenResponse(accessTokenElement.GetString()!, accountId);
        }
        catch (EpicAuthException)
        {
            throw;
        }
        catch (JsonException exception)
        {
            throw new EpicAuthException(
                "token_exchange_failed",
                "Epic Games devolvió una respuesta de token inválida.",
                exception);
        }
        catch (Exception exception) when (
            exception is HttpRequestException or TaskCanceledException)
        {
            throw new EpicAuthException(
                "token_exchange_unavailable",
                "No se pudo contactar a Epic Games para canjear el código.",
                exception);
        }
    }

    private async Task<EpicUser> RequestUserInfoAsync(
        string accessToken,
        string? tokenAccountId,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, _options.UserInfoEndpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(JsonMediaType));

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "Epic rechazó la lectura del perfil con HTTP {StatusCode}.",
                (int)response.StatusCode);

            throw new EpicAuthException(
                UserInfoErrorCode,
                "Epic Games no permitió leer el perfil del usuario.");
        }

        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;

        var subject = GetString(root, "sub") ?? GetString(root, "account_id");

        // El token incluye el account_id de Epic. Si el cliente usa subjects
        // pairwise, el "sub" de userInfo puede ser distinto; por eso se prefiere
        // el account_id del token y solo se usa "sub" como respaldo.
        var accountId = !string.IsNullOrWhiteSpace(tokenAccountId)
            ? tokenAccountId
            : subject;

        if (string.IsNullOrWhiteSpace(accountId))
        {
            throw new EpicAuthException(
                UserInfoErrorCode,
                "Epic Games no devolvió un identificador de cuenta.");
        }

        var preferredUsername = GetString(root, "preferred_username");
        var displayName =
            GetString(root, "display_name") ??
            preferredUsername ??
            GetString(root, "nickname") ??
            GetString(root, "given_name") ??
            "Usuario de Epic Games";

        return new EpicUser(
            accountId,
            displayName,
            preferredUsername,
            GetString(root, "picture"));
    }

    private async Task RevokeTokenAsync(string accessToken, CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, _options.RevokeEndpoint);
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", BasicCredentials());
            request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["token"] = accessToken,
                ["token_type_hint"] = "access_token"
            });

            using var response = await _httpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Epic no confirmó la revocación del token: HTTP {StatusCode}.",
                    (int)response.StatusCode);
            }
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "No se pudo revocar el token de Epic Games.");
        }
    }

    private void EnsureConfigured()
    {
        if (!_options.IsConfigured)
        {
            throw new EpicAuthException(
                "not_configured",
                "Epic Games no está configurado. Define Epic:ClientId y Epic:ClientSecret.");
        }
    }

    private string BasicCredentials() =>
        Convert.ToBase64String(
            Encoding.UTF8.GetBytes($"{_options.ClientId}:{_options.ClientSecret}"));

    private static string? GetString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var property) &&
        property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;

    private sealed record TokenResponse(string AccessToken, string? AccountId);
}
