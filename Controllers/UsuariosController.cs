using Examen_3.Data;
using Examen_3.Models;
using Examen_3.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Examen_3.Controllers;

[ApiController]
[Route("api/usuarios")]
public sealed class UsuariosController(AppDbContext dbContext) : ControllerBase
{
    [HttpGet]
    [RequireEpicAuth]
    [ProducesResponseType(typeof(IReadOnlyList<UsuarioResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<UsuarioResponse>>> GetAll(
        CancellationToken cancellationToken)
    {
        
        var usuarios = await dbContext.Usuarios
            .AsNoTracking()
            .Select(usuario => new UsuarioResponse(usuario.Id, usuario.Nombre, usuario.Correo))
            .ToListAsync(cancellationToken);

        return Ok(usuarios);
    }

    [HttpGet("{id:int}", Name = nameof(GetById))]
    [RequireEpicAuth]
    [ProducesResponseType(typeof(UsuarioResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UsuarioResponse>> GetById(
        int id,
        CancellationToken cancellationToken)
    {
        var usuario = await dbContext.Usuarios
            .AsNoTracking()
            .Where(usuario => usuario.Id == id)
            .Select(usuario => new UsuarioResponse(usuario.Id, usuario.Nombre, usuario.Correo))
            .FirstOrDefaultAsync(cancellationToken);

        return usuario is null ? NotFound() : Ok(usuario);
    }

    [HttpPost]
    [ProducesResponseType(typeof(UsuarioResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<UsuarioResponse>> Create(
        CreateUsuarioRequest request,
        CancellationToken cancellationToken)
    {
        var usuario = new Usuario
        {
            Nombre = request.Nombre,
            Correo = request.Correo
        };

        dbContext.Usuarios.Add(usuario);
        await dbContext.SaveChangesAsync(cancellationToken);

        var response = new UsuarioResponse(usuario.Id, usuario.Nombre, usuario.Correo);
        return CreatedAtAction(nameof(GetById), new { id = usuario.Id }, response);
    }
}
