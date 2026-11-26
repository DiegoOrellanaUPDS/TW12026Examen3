using Microsoft.AspNetCore.Mvc;

namespace Examen_3.Controllers;

[ApiController]
[Route("api/protegido")]
public class ProtectedController : ControllerBase
{
    private bool IsAuthenticated()
    {
        return !string.IsNullOrWhiteSpace(
            HttpContext.Session.GetString("AuthenticatedUserId"));
    }

    [HttpGet]
    public IActionResult Get()
    {
        if (!IsAuthenticated())
        {
            return Unauthorized(new
            {
                message = "Debes iniciar sesión con Kick para acceder a este GET."
            });
        }

        return Ok(new
        {
            message = "GET autorizado correctamente.",
            user = HttpContext.Session.GetString("AuthenticatedUsername"),
            provider = HttpContext.Session.GetString("AuthenticationProvider")
        });
    }

    [HttpPost]
    public IActionResult Post([FromBody] ProtectedRequest request)
    {
        if (!IsAuthenticated())
        {
            return Unauthorized(new
            {
                message = "Debes iniciar sesión con Kick para acceder a este POST."
            });
        }

        if (string.IsNullOrWhiteSpace(request.Message))
        {
            return BadRequest(new { message = "El mensaje es obligatorio." });
        }

        return Ok(new
        {
            message = "POST autorizado correctamente.",
            receivedMessage = request.Message,
            user = HttpContext.Session.GetString("AuthenticatedUsername"),
            provider = HttpContext.Session.GetString("AuthenticationProvider")
        });
    }
}

public class ProtectedRequest
{
    public string Message { get; set; } = "";
}
