using System.ComponentModel.DataAnnotations;

namespace Examen_3.Models;

/// <summary>Datos necesarios para crear un usuario.</summary>
public sealed record CreateUsuarioRequest
{
    [Required, MaxLength(100)]
    public required string Nombre { get; init; }

    [Required, EmailAddress, MaxLength(255)]
    public required string Correo { get; init; }
}

/// <summary>Datos públicos de un usuario.</summary>
public sealed record UsuarioResponse(int Id, string Nombre, string Correo);
