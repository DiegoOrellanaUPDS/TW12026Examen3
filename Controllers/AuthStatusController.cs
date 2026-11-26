using Examen_3.Services;
using Microsoft.AspNetCore.Mvc;

namespace Examen_3.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthStatusController : ControllerBase
{
    [HttpGet("status")]
    [ProducesResponseType(typeof(AuthStatusResponse), StatusCodes.Status200OK)]
    public IActionResult GetStatus()
    {
        var (isAuthenticated, expiresAt) = EpicSessionHelper.GetStatus(HttpContext.Session);
        var message = isAuthenticated
            ? "Estás autenticado con Epic Games."
            : "No estás autenticado.";

        return Ok(new AuthStatusResponse(isAuthenticated, message, expiresAt));
    }

    /// <summary>Estado actual de autenticación del usuario.</summary>
    public sealed record AuthStatusResponse(
        bool IsAuthenticated,
        string Message,
        DateTimeOffset? ExpiresAt);
}
