using examenParcial.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace examenParcial.Data;

public static class SeedData
{
    public static async Task InicializarAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<ApplicationDbContext>();
        var config = services.GetRequiredService<IConfiguration>();
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(SeedData));

        await db.Database.MigrateAsync();

        if (!await db.Incidencias.AnyAsync())
        {
            db.Incidencias.AddRange(
                new Incidencia { Id = 1, Estacion = "Estación Central", Descripcion = "Escalera mecánica detenida en el andén 2", Prioridad = PrioridadIncidencia.Alta },
                new Incidencia { Id = 2, Estacion = "Estación Central", Descripcion = "Luminaria fundida en el acceso norte", Prioridad = PrioridadIncidencia.Baja },
                new Incidencia { Id = 3, Estacion = "Estación Grau", Descripcion = "Torniquete no lee tarjetas", Prioridad = PrioridadIncidencia.Media },
                new Incidencia { Id = 4, Estacion = "Estación Grau", Descripcion = "Fuga de agua en el baño público", Prioridad = PrioridadIncidencia.Media },
                new Incidencia { Id = 5, Estacion = "Estación Gamarra", Descripcion = "Pantalla informativa sin señal", Prioridad = PrioridadIncidencia.Baja },
                new Incidencia { Id = 6, Estacion = "Estación Gamarra", Descripcion = "Ascensor fuera de servicio", Prioridad = PrioridadIncidencia.Alta },
                new Incidencia { Id = 7, Estacion = "Estación La Cultura", Descripcion = "Puerta de andén no cierra correctamente", Prioridad = PrioridadIncidencia.Alta },
                new Incidencia { Id = 8, Estacion = "Estación La Cultura", Descripcion = "Máquina expendedora de tarjetas atascada", Prioridad = PrioridadIncidencia.Media },
                new Incidencia { Id = 9, Estacion = "Estación Angamos", Descripcion = "Cámara de seguridad desconectada", Prioridad = PrioridadIncidencia.Media },
                new Incidencia { Id = 10, Estacion = "Estación Angamos", Descripcion = "Luminaria parpadeando en el andén 1", Prioridad = PrioridadIncidencia.Baja, Estado = EstadoIncidencia.Cerrada });
            await db.SaveChangesAsync();
        }

        // Usuario de prueba: solo se crea si las variables están configuradas.
        var email = config["SEED_USER_EMAIL"];
        var password = config["SEED_USER_PASSWORD"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        var userManager = services.GetRequiredService<UserManager<IdentityUser>>();
        if (await userManager.FindByEmailAsync(email) is null)
        {
            var user = new IdentityUser { UserName = email, Email = email, EmailConfirmed = true };
            var result = await userManager.CreateAsync(user, password);
            if (!result.Succeeded)
            {
                logger.LogWarning("No se pudo crear el usuario de prueba: {Errores}",
                    string.Join("; ", result.Errors.Select(e => e.Description)));
            }
        }
    }
}
