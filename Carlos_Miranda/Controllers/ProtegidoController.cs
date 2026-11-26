using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Concurrent;
using System.Security.Claims;

namespace Examen_3.Controllers;

[ApiController]
[Route("api/protegido")]
[Authorize(Policy = "VimeoOnly")]
public class ProtegidoController : ControllerBase
{
    private static readonly ConcurrentBag<Mensaje> Mensajes = new();

    [HttpPost]
    public IActionResult Crear([FromBody] CrearMensajeRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Contenido))
            return BadRequest("El contenido es obligatorio.");

        var mensaje = new Mensaje(
            Guid.NewGuid(),
            request.Contenido,
            User.FindFirstValue(ClaimTypes.NameIdentifier)!,
            DateTime.UtcNow
        );

        Mensajes.Add(mensaje);

        return Ok(mensaje);
    }

    [HttpGet]
    public IActionResult Obtener()
    {
        return Ok(new
        {
            Usuario = User.Identity?.Name,
            Proveedor = User.FindFirst("identity_provider")?.Value,
            Mensajes = Mensajes.ToArray()
        });
    }
}

public record CrearMensajeRequest(string Contenido);

public record Mensaje(
    Guid Id,
    string Contenido,
    string Usuario,
    DateTime Fecha
);
