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
    IAlgoliaSearchService algolia,
    IAlgoliaIndexService algoliaIndex,
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
            // Listado general: Redis durante 60 s, con SQLite como fuente de verdad.
            modelo.Incidencias = await cache.ObtenerAbiertasAsync(cancellationToken);
            return View(modelo);
        }

        // Búsqueda con texto: Algolia, nunca la caché del listado general.
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
            // Solo se invalida cuando SQLite ya guardó el nuevo estado.
            await cache.InvalidarAbiertasAsync();
            // Se publica después de guardar en SQLite y de invalidar Redis: si un cliente vuelve a pedir el
            // listado al recibir el evento, ya no obtiene la versión cacheada. Si falla, el cierre se mantiene.
            await pieSocket.PublicarIncidenciaActualizadaAsync(incidencia.Id, incidencia.Estado);
            // Mantiene el índice de Algolia coherente; la búsqueda sigue filtrando por SQLite.
            await algoliaIndex.ActualizarEstadoAsync(incidencia.Id, incidencia.Estado);
            TempData["Mensaje"] = $"Incidencia #{incidencia.Id} cerrada.";
        }

        return RedirectToAction(nameof(Incidencias), new { q });
    }
}
