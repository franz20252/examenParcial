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
    IPieSocketService pieSocket,
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
            // Prioridad se guarda como texto; se ordena en memoria para respetar el orden del enum.
            var lista = await abiertas.ToListAsync(cancellationToken);
            modelo.Incidencias = lista
                .OrderByDescending(i => i.Prioridad)
                .ThenBy(i => i.Id)
                .ToList();
            return View(modelo);
        }

        // Búsqueda con texto: consulta directa a SQLite.
        logger.LogInformation("Búsqueda con texto: consulta directa a SQLite.");
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
            // Solo se publica cuando SQLite ya guardó el nuevo estado; si la publicación falla, el cierre se mantiene.
            await pieSocket.PublicarIncidenciaActualizadaAsync(incidencia.Id, incidencia.Estado);
            TempData["Mensaje"] = $"Incidencia #{incidencia.Id} cerrada.";
        }

        return RedirectToAction(nameof(Incidencias), new { q });
    }
}
