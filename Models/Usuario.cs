using System.ComponentModel.DataAnnotations;

namespace Examen_3.Models;

public class Usuario
{
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string Nombre { get; set; } = string.Empty;

    [Required]
    [MaxLength(255)]
    public string Correo { get; set; } = string.Empty;
}
