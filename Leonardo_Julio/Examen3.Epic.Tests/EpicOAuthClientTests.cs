using System.Net;
using System.Text;
using Examen3.Epic.Auth;
using Examen3.Epic.Options;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Examen3.Epic.Tests;

public sealed class EpicOAuthClientTests
{
    [Fact]
    public void BuildAuthorizeUrl_includes_expected_parameters()
    {
        var options = CreateOptions();
        using var httpClient = new HttpClient(new RecordingHandler(_ =>
            throw new InvalidOperationException("No se esperaba una petición HTTP.")));
        var client = CreateClient(httpClient, options);

        var url = client.BuildAuthorizeUrl("state-123");

        Assert.StartsWith("https://epic.test/authorize?", url);
        var query = QueryHelpers.ParseQuery(new Uri(url).Query);
        Assert.Equal(options.ClientId, query["client_id"].ToString());
        Assert.Equal("code", query["response_type"].ToString());
        Assert.Equal("basic_profile", query["scope"].ToString());
        Assert.Equal(options.RedirectUri, query["redirect_uri"].ToString());
        Assert.Equal("state-123", query["state"].ToString());
    }

    [Fact]
    public async Task ExchangeCodeAsync_exchanges_code_reads_identity_and_revokes_token()
    {
        var handler = new RecordingHandler(request =>
        {
            return request.Uri!.AbsoluteUri switch
            {
                "https://epic.test/token" => JsonResponse(
                    """
                    {
                      "access_token": "access-token-123",
                      "token_type": "bearer",
                      "account_id": "account-123",
                      "expires_in": 7200,
                      "scope": "basic_profile"
                    }
                    """),
                "https://epic.test/userinfo" => JsonResponse(
                    """
                    {
                      "sub": "account-123",
                      "display_name": "Leonardo Julio",
                      "preferred_username": "leonardo_julio",
                      "picture": "https://cdn.example/avatar.png"
                    }
                    """),
                "https://epic.test/revoke" => new HttpResponseMessage(HttpStatusCode.NoContent),
                _ => new HttpResponseMessage(HttpStatusCode.NotFound)
            };
        });
        using var httpClient = new HttpClient(handler);
        var options = CreateOptions();
        var client = CreateClient(httpClient, options);

        var user = await client.ExchangeCodeAsync("code-123", CancellationToken.None);

        Assert.Equal("account-123", user.AccountId);
        Assert.Equal("Leonardo Julio", user.DisplayName);
        Assert.Equal("leonardo_julio", user.PreferredUsername);
        Assert.Equal("https://cdn.example/avatar.png", user.AvatarUrl);
        Assert.Equal(3, handler.Requests.Count);

        var tokenRequest = handler.Requests[0];
        Assert.Equal(HttpMethod.Post, tokenRequest.Method);
        Assert.Equal("Basic", tokenRequest.AuthScheme);
        var expectedBasic = Convert.ToBase64String(
            Encoding.UTF8.GetBytes($"{options.ClientId}:{options.ClientSecret}"));
        Assert.Equal(expectedBasic, tokenRequest.AuthParameter);
        Assert.Contains("grant_type=authorization_code", tokenRequest.Body ?? string.Empty);
        Assert.Contains("code=code-123", tokenRequest.Body ?? string.Empty);
        Assert.Contains(
            "redirect_uri=http%3A%2F%2Flocalhost%2Fcallback",
            tokenRequest.Body ?? string.Empty);
        Assert.Contains("scope=basic_profile", tokenRequest.Body ?? string.Empty);
        Assert.Contains("deployment_id=deployment-123", tokenRequest.Body ?? string.Empty);

        var userInfoRequest = handler.Requests[1];
        Assert.Equal(HttpMethod.Get, userInfoRequest.Method);
        Assert.Equal("Bearer", userInfoRequest.AuthScheme);
        Assert.Equal("access-token-123", userInfoRequest.AuthParameter);

        var revokeRequest = handler.Requests[2];
        Assert.Equal(HttpMethod.Post, revokeRequest.Method);
        Assert.Equal("Basic", revokeRequest.AuthScheme);
        Assert.Contains("token=access-token-123", revokeRequest.Body ?? string.Empty);
        Assert.Contains("token_type_hint=access_token", revokeRequest.Body ?? string.Empty);
    }

    [Fact]
    public async Task ExchangeCodeAsync_throws_when_epic_returns_token_error()
    {
        var handler = new RecordingHandler(_ => JsonResponse(
            """{"error":"invalid_grant","error_description":"bad code"}""",
            HttpStatusCode.BadRequest));
        using var httpClient = new HttpClient(handler);
        var client = CreateClient(httpClient, CreateOptions());

        var exception = await Assert.ThrowsAsync<EpicAuthException>(
            () => client.ExchangeCodeAsync("expired", CancellationToken.None));

        Assert.Equal("token_exchange_failed", exception.Code);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task ExchangeCodeAsync_revokes_token_when_userinfo_fails()
    {
        var handler = new RecordingHandler(request =>
        {
            return request.Uri!.AbsoluteUri switch
            {
                "https://epic.test/token" => JsonResponse(
                    """{"access_token":"access-token-123","account_id":"account-123"}"""),
                "https://epic.test/userinfo" => JsonResponse(
                    """{"error":"server_error"}""",
                    HttpStatusCode.InternalServerError),
                "https://epic.test/revoke" => new HttpResponseMessage(HttpStatusCode.NoContent),
                _ => new HttpResponseMessage(HttpStatusCode.NotFound)
            };
        });
        using var httpClient = new HttpClient(handler);
        var client = CreateClient(httpClient, CreateOptions());

        var exception = await Assert.ThrowsAsync<EpicAuthException>(
            () => client.ExchangeCodeAsync("code-123", CancellationToken.None));

        Assert.Equal("userinfo_failed", exception.Code);
        Assert.Equal(3, handler.Requests.Count);
        Assert.Equal("https://epic.test/revoke", handler.Requests[2].Uri!.AbsoluteUri);
    }

    [Fact]
    public async Task ExchangeCodeAsync_prefers_token_account_id_when_userinfo_sub_is_pairwise()
    {
        var handler = new RecordingHandler(request =>
        {
            return request.Uri!.AbsoluteUri switch
            {
                "https://epic.test/token" => JsonResponse(
                    """{"access_token":"access-token-123","account_id":"account-123"}"""),
                "https://epic.test/userinfo" => JsonResponse(
                    """{"sub":"pairwise-subject","display_name":"Leonardo Julio"}"""),
                "https://epic.test/revoke" => new HttpResponseMessage(HttpStatusCode.NoContent),
                _ => new HttpResponseMessage(HttpStatusCode.NotFound)
            };
        });
        using var httpClient = new HttpClient(handler);
        var client = CreateClient(httpClient, CreateOptions());

        var user = await client.ExchangeCodeAsync("code-123", CancellationToken.None);

        Assert.Equal("account-123", user.AccountId);
        Assert.Equal(3, handler.Requests.Count);
    }

    [Fact]
    public async Task ExchangeCodeAsync_uses_userinfo_sub_when_token_has_no_account_id()
    {
        var handler = new RecordingHandler(request =>
        {
            return request.Uri!.AbsoluteUri switch
            {
                "https://epic.test/token" => JsonResponse(
                    """{"access_token":"access-token-123"}"""),
                "https://epic.test/userinfo" => JsonResponse(
                    """{"sub":"account-456","display_name":"Leonardo Julio"}"""),
                "https://epic.test/revoke" => new HttpResponseMessage(HttpStatusCode.NoContent),
                _ => new HttpResponseMessage(HttpStatusCode.NotFound)
            };
        });
        using var httpClient = new HttpClient(handler);
        var client = CreateClient(httpClient, CreateOptions());

        var user = await client.ExchangeCodeAsync("code-123", CancellationToken.None);

        Assert.Equal("account-456", user.AccountId);
        Assert.Equal(3, handler.Requests.Count);
    }

    [Fact]
    public void BuildAuthorizeUrl_throws_when_not_configured()
    {
        using var httpClient = new HttpClient(new RecordingHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)));
        var client = CreateClient(httpClient, new EpicOptions());

        var exception = Assert.Throws<EpicAuthException>(() => client.BuildAuthorizeUrl("state"));

        Assert.Equal("not_configured", exception.Code);
    }

    private static EpicOAuthClient CreateClient(HttpClient httpClient, EpicOptions options) =>
        new(
            httpClient,
            Microsoft.Extensions.Options.Options.Create(options),
            NullLogger<EpicOAuthClient>.Instance);

    private static EpicOptions CreateOptions() => new()
    {
        ClientId = "test-client-id",
        ClientSecret = "test-client-secret",
        DeploymentId = "deployment-123",
        RedirectUri = "http://localhost/callback",
        Scope = "basic_profile",
        AuthorizationEndpoint = "https://epic.test/authorize",
        TokenEndpoint = "https://epic.test/token",
        UserInfoEndpoint = "https://epic.test/userinfo",
        RevokeEndpoint = "https://epic.test/revoke"
    };

    private static HttpResponseMessage JsonResponse(
        string json,
        HttpStatusCode statusCode = HttpStatusCode.OK) =>
        new(statusCode)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly Func<CapturedRequest, HttpResponseMessage> _responder;

        public RecordingHandler(Func<CapturedRequest, HttpResponseMessage> responder)
        {
            _responder = responder;
        }

        public List<CapturedRequest> Requests { get; } = new();

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var body = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);

            var captured = new CapturedRequest(
                request.Method,
                request.RequestUri,
                request.Headers.Authorization?.Scheme,
                request.Headers.Authorization?.Parameter,
                body);

            Requests.Add(captured);
            return _responder(captured);
        }
    }

    private sealed record CapturedRequest(
        HttpMethod Method,
        Uri? Uri,
        string? AuthScheme,
        string? AuthParameter,
        string? Body);
}
