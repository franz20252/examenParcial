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
    IAlgoliaSearchService algolia,
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

        if (!algolia.EstaConfigurado)
        {
            modelo.Error = "La búsqueda no está disponible: Algolia no está configurado en el servidor.";
            return View(modelo);
        }

        IReadOnlyList<int> ids;
        try
        {
            ids = await algolia.BuscarIdsAsync(busqueda, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Error al consultar Algolia");
            modelo.Error = "No se pudo completar la búsqueda. Inténtalo de nuevo más tarde.";
            return View(modelo);
        }

        // La base de datos local es la fuente de verdad: solo incidencias existentes y abiertas.
        var encontradas = await abiertas
            .Where(i => ids.Contains(i.Id))
            .ToListAsync(cancellationToken);

        // Se conserva el orden de relevancia de Algolia.
        var posicion = ids.Select((id, indice) => (id, indice)).ToDictionary(x => x.id, x => x.indice);
        modelo.Incidencias = encontradas.OrderBy(i => posicion[i.Id]).ToList();
        return View(modelo);
    }

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
            TempData["Mensaje"] = $"Incidencia #{incidencia.Id} cerrada.";
        }

        return RedirectToAction(nameof(Incidencias), new { q });
    }
}
