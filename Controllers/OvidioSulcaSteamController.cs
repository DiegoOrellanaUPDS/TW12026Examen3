using System.Security.Claims;
using AspNet.Security.OpenId.Steam;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Examen_3.Controllers;

[ApiController]
[Route("auth/steam")]
public class OvidioSulcaSteamController(IAntiforgery antiforgery) : ControllerBase
{
    [HttpGet("login")]
    public IActionResult Login() => Challenge(
        new AuthenticationProperties { RedirectUri = "/sulcaindex.html" },
        SteamAuthenticationDefaults.AuthenticationScheme);

    [HttpGet("me")]
    [Authorize(Policy = "SoloSteam")]
    public IActionResult Me()
    {
        Response.Headers.CacheControl = "no-store";
        return Ok(new
        {
            steamId64 = User.FindFirstValue(ClaimTypes.NameIdentifier),
            identity = User.Identity!.Name ?? User.FindFirstValue(ClaimTypes.NameIdentifier),
            provider = User.FindFirstValue("provider"),
            csrfToken = antiforgery.GetAndStoreTokens(HttpContext).RequestToken
        });
    }

    [HttpPost("logout")]
    [Authorize(Policy = "SoloSteam")]
    public async Task<IActionResult> Logout()
    {
        try
        {
            await antiforgery.ValidateRequestAsync(HttpContext);
        }
        catch (AntiforgeryValidationException)
        {
            return BadRequest("Token CSRF no válido.");
        }

        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return NoContent();
    }
}
