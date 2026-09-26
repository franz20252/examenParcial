using examenParcial.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace examenParcial.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext(options)
{
    public DbSet<Incidencia> Incidencias => Set<Incidencia>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Incidencia>(e =>
        {
            // Los Ids se asignan explícitamente para que coincidan con los objectID de Algolia.
            e.Property(i => i.Id).ValueGeneratedNever();
            e.Property(i => i.Estado).HasConversion<string>().HasMaxLength(20);
            e.Property(i => i.Prioridad).HasConversion<string>().HasMaxLength(20);
            e.HasIndex(i => i.Estado);
        });
    }
}
