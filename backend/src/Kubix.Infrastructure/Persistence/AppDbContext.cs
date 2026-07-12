using Microsoft.EntityFrameworkCore;

namespace Kubix.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        // Entidades de dominio y filtros de tenant aterrizan en KBX-2 / KBX-4.
    }
}
