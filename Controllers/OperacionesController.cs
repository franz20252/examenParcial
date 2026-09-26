using examenParcial.Data;
using examenParcial.Models;
using examenParcial.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace examenParcial.Controllers;

[Authorize]
public class OperacionesController(
    ApplicationDbContext db,
    IIncidenciasCacheService cache,
    ILogger<OperacionesController> logger) : Controller
{
    private const int LongitudMaximaBusqueda = 200;

    [HttpGet]
    public async Task<IActionResult> Incidencias(string? q, CancellationToken cancellationToken)
    {
        var busqueda = q?.Trim();
        if (busqueda?.Length > LongitudMaximaBusqueda)
        {
            busqueda = busqueda[..LongitudMaximaBusqueda];
        }

        var modelo = new IncidenciasViewModel { Busqueda = busqueda };
        var abiertas = db.Incidencias.AsNoTracking().Where(i => i.Estado == EstadoIncidencia.Abierta);

        if (string.IsNullOrWhiteSpace(busqueda))
        {
            // Listado general: Redis durante 60 s, con SQLite como fuente de verdad.
            modelo.Incidencias = await cache.ObtenerAbiertasAsync(cancellationToken);
            return View(modelo);
        }

        // Búsqueda con texto: nunca usa la caché del listado general.
        logger.LogInformation("Búsqueda con texto: consulta directa a SQLite, sin caché de Redis.");
        var patron = $"%{EscaparLike(busqueda)}%";
        modelo.Incidencias = await abiertas
            .Where(i => EF.Functions.Like(i.Estacion, patron, "\\") || EF.Functions.Like(i.Descripcion, patron, "\\"))
            .OrderBy(i => i.Id)
            .ToListAsync(cancellationToken);
        return View(modelo);
    }

    private static string EscaparLike(string texto) =>
        texto.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cerrar(int id, string? q, CancellationToken cancellationToken)
    {
        var incidencia = await db.Incidencias.FirstOrDefaultAsync(i => i.Id == id, cancellationToken);
        if (incidencia is null)
        {
            return NotFound();
        }

        if (incidencia.Estado == EstadoIncidencia.Abierta)
        {
            incidencia.Estado = EstadoIncidencia.Cerrada;
            await db.SaveChangesAsync(cancellationToken);
            // Solo se invalida cuando SQLite ya guardó el nuevo estado.
            await cache.InvalidarAbiertasAsync();
            TempData["Mensaje"] = $"Incidencia #{incidencia.Id} cerrada.";
        }

        return RedirectToAction(nameof(Incidencias), new { q });
    }
}
