using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Examen3.Epic.Auth;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;

namespace Examen3.Epic.Tests;

public sealed class AuthAndNotesTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public AuthAndNotesTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Status_reports_epic_is_configured()
    {
        using var client = NewClient();

        var response = await client.GetAsync("/api/auth/status");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Epic Games", body.GetProperty("provider").GetString());
        Assert.Equal("Epic", body.GetProperty("providerId").GetString());
        Assert.True(body.GetProperty("configured").GetBoolean());
        Assert.Equal("/api/auth/epic/login", body.GetProperty("loginUrl").GetString());
    }

    [Fact]
    public async Task Protected_endpoints_reject_anonymous_requests()
    {
        using var client = NewClient();

        var get = await client.GetAsync("/api/notes");
        Assert.Equal(HttpStatusCode.Unauthorized, get.StatusCode);
        Assert.Equal("application/json", get.Content.Headers.ContentType?.MediaType);
        var getBody = await get.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(401, getBody.GetProperty("status").GetInt32());
        Assert.Equal("/api/auth/epic/login", getBody.GetProperty("loginUrl").GetString());

        var post = await client.PostAsJsonAsync("/api/notes", new { text = "sin sesión" });
        Assert.Equal(HttpStatusCode.Unauthorized, post.StatusCode);
        Assert.Equal("application/json", post.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Login_redirects_to_epic_with_state_cookie()
    {
        using var client = NewClient(handleCookies: true);

        var response = await client.GetAsync("/api/auth/epic/login");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location;
        Assert.NotNull(location);
        Assert.StartsWith("https://www.epicgames.com/id/authorize", location!.ToString());

        var query = QueryHelpers.ParseQuery(location.Query);
        Assert.Equal("test-client-id", query["client_id"].ToString());
        Assert.Equal("code", query["response_type"].ToString());
        Assert.Equal("basic_profile", query["scope"].ToString());
        Assert.Equal("http://localhost/api/auth/epic/callback", query["redirect_uri"].ToString());
        Assert.False(string.IsNullOrWhiteSpace(query["state"].ToString()));

        Assert.True(response.Headers.TryGetValues("Set-Cookie", out var cookies));
        var stateCookie = cookies!.Single(
            cookie => cookie.StartsWith("examen3.epic.oauth_state=", StringComparison.Ordinal));
        Assert.Contains("httponly", stateCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", stateCookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Callback_with_invalid_state_redirects_with_error()
    {
        using var client = NewClient(handleCookies: true);

        var login = await client.GetAsync("/api/auth/epic/login");
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);

        var callback = await client.GetAsync("/api/auth/epic/callback?code=abc&state=estado-falso");

        Assert.Equal(HttpStatusCode.Redirect, callback.StatusCode);
        Assert.NotNull(callback.Headers.Location);
        Assert.Equal("/?authError=invalid_state", callback.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task Callback_with_access_denied_redirects_with_error()
    {
        using var client = NewClient();

        var callback = await client.GetAsync("/api/auth/epic/callback?error=access_denied");

        Assert.Equal(HttpStatusCode.Redirect, callback.StatusCode);
        Assert.NotNull(callback.Headers.Location);
        Assert.Equal("/?authError=access_denied", callback.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task Full_oauth_callback_creates_authenticated_session()
    {
        using var client = NewClient(handleCookies: true);

        var login = await client.GetAsync("/api/auth/epic/login");
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        var state = QueryHelpers.ParseQuery(login.Headers.Location!.Query)["state"].ToString();

        var callback = await client.GetAsync(
            $"/api/auth/epic/callback?code=test-code&state={Uri.EscapeDataString(state)}");

        Assert.Equal(HttpStatusCode.Redirect, callback.StatusCode);
        Assert.NotNull(callback.Headers.Location);
        Assert.Equal("/?login=ok", callback.Headers.Location!.OriginalString);
        Assert.Equal("test-code", _factory.EpicClient.LastCode);

        var me = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        var meBody = await me.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(meBody.GetProperty("authenticated").GetBoolean());
        Assert.Equal("Epic Games", meBody.GetProperty("provider").GetString());
        Assert.Equal("Epic", meBody.GetProperty("providerId").GetString());
        var user = meBody.GetProperty("user");
        Assert.Equal("epic-account-123", user.GetProperty("id").GetString());
        Assert.Equal("Leonardo Julio", user.GetProperty("displayName").GetString());
        Assert.Equal("leonardo_julio", user.GetProperty("preferredUsername").GetString());
        Assert.Equal("https://cdn.example/avatar.png", user.GetProperty("avatarUrl").GetString());

        var notes = await client.GetAsync("/api/notes");
        Assert.Equal(HttpStatusCode.OK, notes.StatusCode);
    }

    [Fact]
    public async Task Post_and_get_notes_work_with_authenticated_session()
    {
        using var client = NewClient();
        var userId = $"epic-{Guid.NewGuid():N}";
        var cookie = await TestAuth.CreateCookieAsync(_factory, userId, "alice");

        var post = await client.SendAsync(
            WithCookie(HttpMethod.Post, "/api/notes", cookie, new { text = "  Hola desde Epic  " }));

        Assert.Equal(HttpStatusCode.Created, post.StatusCode);
        var postBody = await post.Content.ReadFromJsonAsync<JsonElement>();
        var note = postBody.GetProperty("note");
        Assert.Equal("Hola desde Epic", note.GetProperty("text").GetString());
        Assert.Equal("Epic Games", note.GetProperty("provider").GetString());
        Assert.Equal("alice", note.GetProperty("authorName").GetString());
        var noteId = note.GetProperty("id").GetGuid();

        var get = await client.SendAsync(WithCookie(HttpMethod.Get, "/api/notes", cookie));

        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        var getBody = await get.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, getBody.GetProperty("count").GetInt32());
        Assert.Equal("alice", getBody.GetProperty("user").GetProperty("username").GetString());
        Assert.Equal("Epic Games", getBody.GetProperty("user").GetProperty("provider").GetString());
        var savedNote = getBody.GetProperty("notes").EnumerateArray().Single();
        Assert.Equal(noteId, savedNote.GetProperty("id").GetGuid());
        Assert.Equal("Hola desde Epic", savedNote.GetProperty("text").GetString());
    }

    [Fact]
    public async Task Notes_are_isolated_per_epic_user()
    {
        using var client = NewClient();
        var cookieA = await TestAuth.CreateCookieAsync(
            _factory,
            $"epic-{Guid.NewGuid():N}",
            "alice");
        var cookieB = await TestAuth.CreateCookieAsync(
            _factory,
            $"epic-{Guid.NewGuid():N}",
            "bob");

        var post = await client.SendAsync(
            WithCookie(HttpMethod.Post, "/api/notes", cookieA, new { text = "nota de alice" }));
        Assert.Equal(HttpStatusCode.Created, post.StatusCode);

        var getB = await client.SendAsync(WithCookie(HttpMethod.Get, "/api/notes", cookieB));

        Assert.Equal(HttpStatusCode.OK, getB.StatusCode);
        var body = await getB.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, body.GetProperty("count").GetInt32());
        Assert.Empty(body.GetProperty("notes").EnumerateArray());
    }

    [Fact]
    public async Task Invalid_note_is_rejected()
    {
        using var client = NewClient();
        var cookie = await TestAuth.CreateCookieAsync(
            _factory,
            $"epic-{Guid.NewGuid():N}",
            "alice");

        var empty = await client.SendAsync(
            WithCookie(HttpMethod.Post, "/api/notes", cookie, new { text = "   " }));
        Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);

        var tooLong = await client.SendAsync(
            WithCookie(HttpMethod.Post, "/api/notes", cookie, new { text = new string('x', 501) }));
        Assert.Equal(HttpStatusCode.BadRequest, tooLong.StatusCode);
    }

    [Fact]
    public async Task Logout_revokes_server_side_session()
    {
        using var client = NewClient();
        var cookie = await TestAuth.CreateCookieAsync(
            _factory,
            $"epic-{Guid.NewGuid():N}",
            "alice");

        var before = await client.SendAsync(
            WithCookie(HttpMethod.Post, "/api/notes", cookie, new { text = "antes del logout" }));
        Assert.Equal(HttpStatusCode.Created, before.StatusCode);

        var logout = await client.SendAsync(WithCookie(HttpMethod.Post, "/api/auth/logout", cookie));
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);

        var getAfter = await client.SendAsync(WithCookie(HttpMethod.Get, "/api/notes", cookie));
        Assert.Equal(HttpStatusCode.Unauthorized, getAfter.StatusCode);

        var postAfter = await client.SendAsync(
            WithCookie(HttpMethod.Post, "/api/notes", cookie, new { text = "después del logout" }));
        Assert.Equal(HttpStatusCode.Unauthorized, postAfter.StatusCode);

        var meAfter = await client.SendAsync(WithCookie(HttpMethod.Get, "/api/auth/me", cookie));
        Assert.Equal(HttpStatusCode.OK, meAfter.StatusCode);
        var meBody = await meAfter.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(meBody.GetProperty("authenticated").GetBoolean());
    }

    [Fact]
    public async Task Session_from_other_provider_is_forbidden()
    {
        using var client = NewClient();
        var cookie = await TestAuth.CreateCookieAsync(
            _factory,
            $"epic-{Guid.NewGuid():N}",
            "alice",
            provider: "Google");

        var get = await client.SendAsync(WithCookie(HttpMethod.Get, "/api/notes", cookie));

        Assert.Equal(HttpStatusCode.Forbidden, get.StatusCode);
        var body = await get.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(403, body.GetProperty("status").GetInt32());
    }

    [Fact]
    public async Task Me_reports_anonymous_without_session()
    {
        using var client = NewClient();

        var me = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        var body = await me.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(body.GetProperty("authenticated").GetBoolean());
        Assert.Equal("Epic Games", body.GetProperty("provider").GetString());
        Assert.Equal("Epic", body.GetProperty("providerId").GetString());
    }

    [Fact]
    public async Task Logout_requires_authentication()
    {
        using var client = NewClient();

        var logout = await client.PostAsync("/api/auth/logout", content: null);

        Assert.Equal(HttpStatusCode.Unauthorized, logout.StatusCode);
    }

    [Fact]
    public async Task Cross_origin_post_is_rejected()
    {
        using var client = NewClient();
        var cookie = await TestAuth.CreateCookieAsync(
            _factory,
            $"epic-{Guid.NewGuid():N}",
            "alice");

        var request = WithCookie(
            HttpMethod.Post,
            "/api/notes",
            cookie,
            new { text = "desde otro origen" });
        request.Headers.Add("Origin", "https://evil.example");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(403, body.GetProperty("status").GetInt32());
    }

    [Fact]
    public async Task Health_reports_epic()
    {
        using var client = NewClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("ok", body.GetProperty("status").GetString());
        Assert.Equal("Epic Games", body.GetProperty("provider").GetString());
    }

    [Fact]
    public async Task Login_returns_503_when_not_configured()
    {
        using var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Epic:ClientId"] = "",
                    ["Epic:ClientSecret"] = ""
                });
            });
        });

        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var status = await client.GetAsync("/api/auth/status");
        Assert.Equal(HttpStatusCode.OK, status.StatusCode);
        var statusBody = await status.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(statusBody.GetProperty("configured").GetBoolean());

        var login = await client.GetAsync("/api/auth/epic/login");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, login.StatusCode);
    }

    private HttpClient NewClient(bool handleCookies = false) =>
        _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = handleCookies
        });

    private static HttpRequestMessage WithCookie(
        HttpMethod method,
        string path,
        string cookie)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Add("Cookie", cookie);
        return request;
    }

    private static HttpRequestMessage WithCookie<T>(
        HttpMethod method,
        string path,
        string cookie,
        T body)
    {
        var request = WithCookie(method, path, cookie);
        request.Content = new StringContent(
            JsonSerializer.Serialize(body),
            Encoding.UTF8,
            "application/json");
        return request;
    }
}
