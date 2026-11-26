using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Controllers;

[ApiController]
[Route("api/melissa")]
public class MelissaController : ControllerBase
{
    // Lista en memoria hasta que se agregue una base de datos.
    private static readonly List<Registro> registros = new();

    [HttpPost]
    [Authorize(Policy = "SoloVimeo")]
    public IActionResult Crear([FromBody] Registro registro)
    {
        registros.Add(registro);
        return Ok(registro);
    }

    [HttpGet]
    [Authorize(Policy = "SoloVimeo")]
    public IActionResult ObtenerTodos()
    {
        return Ok(registros);
    }
}

public class Registro
{
    public string Titulo { get; set; } = "";
    public string Descripcion { get; set; } = "";
}
