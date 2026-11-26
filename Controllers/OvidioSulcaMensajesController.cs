using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Examen_3.Controllers;

[ApiController]
[Route("api/mensajes")]
[Authorize(Policy = "SoloSteam")]
public class OvidioSulcaMensajesController(IAntiforgery antiforgery) : ControllerBase
{
    // Los mensajes se guardan solo mientras la aplicación está ejecutándose.
    private static readonly Dictionary<string, List<string>> Mensajes = new();

    [HttpPost]
    public async Task<IActionResult> Crear([FromForm] string texto)
    {
        try
        {
            await antiforgery.ValidateRequestAsync(HttpContext);
        }
        catch (AntiforgeryValidationException)
        {
            return BadRequest("Token CSRF no válido.");
        }

        texto = texto.Trim();
        if (texto.Length == 0 || texto.Length > 200)
            return BadRequest("El mensaje debe tener entre 1 y 200 caracteres.");

        var steamId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        lock (Mensajes)
        {
            if (!Mensajes.ContainsKey(steamId)) Mensajes[steamId] = new List<string>();
            Mensajes[steamId].Add(texto);
        }
        return Ok(new { mensaje = "Mensaje registrado.", texto });
    }

    [HttpGet]
    public IActionResult Consultar()
    {
        var steamId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        Response.Headers.CacheControl = "no-store";
        lock (Mensajes)
        {
            return Ok(Mensajes.TryGetValue(steamId, out var mensajes) ? mensajes.ToArray() : Array.Empty<string>());
        }
    }
}
