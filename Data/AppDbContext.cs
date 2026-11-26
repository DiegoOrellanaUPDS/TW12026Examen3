using Examen_3.Models;
using Microsoft.EntityFrameworkCore;

namespace Examen_3.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Usuario> Usuarios => Set<Usuario>();
}
