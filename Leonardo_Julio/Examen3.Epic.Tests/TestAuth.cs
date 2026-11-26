using System.Security.Claims;
using Examen3.Epic.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Examen3.Epic.Tests;

internal static class TestAuth
{
    public static async Task<string> CreateCookieAsync(
        TestWebApplicationFactory factory,
        string userId,
        string displayName,
        string provider = EpicConstants.Provider)
    {
        using var scope = factory.Services.CreateScope();
        var sessions = scope.ServiceProvider.GetRequiredService<IAppSessionStore>();
        var sessionId = sessions.Create(
            userId,
            provider,
            displayName,
            EpicConstants.SessionLifetime);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId),
            new(ClaimTypes.Name, displayName),
            new(EpicConstants.ProviderClaim, provider),
            new(EpicConstants.SessionIdClaim, sessionId)
        };

        var principal = new ClaimsPrincipal(
            new ClaimsIdentity(claims, authenticationType: EpicConstants.Provider));

        var context = new DefaultHttpContext
        {
            RequestServices = scope.ServiceProvider
        };
        context.Request.Scheme = "http";
        context.Request.Host = new HostString("localhost");

        await context.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties
            {
                ExpiresUtc = DateTimeOffset.UtcNow.Add(EpicConstants.SessionLifetime)
            });

        var setCookie = context.Response.Headers["Set-Cookie"].ToString();
        return setCookie.Split(';', 2)[0];
    }
}
