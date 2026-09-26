using Algolia.Search.Clients;
using Algolia.Search.Models.Search;
using examenParcial.Data;
using examenParcial.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace examenParcial.Services;

public interface IAlgoliaIndexService
{
    // Copia al índice de Algolia las incidencias de SQLite (SQLite sigue siendo la fuente de verdad).
    Task SincronizarDesdeSqliteAsync(ApplicationDbContext db, CancellationToken cancellationToken = default);

    // Refleja en el índice el nuevo estado de una incidencia. Nunca lanza excepción.
    Task ActualizarEstadoAsync(int id, EstadoIncidencia estado);
}

public class AlgoliaIndexService : IAlgoliaIndexService
{
    private readonly AlgoliaOptions _options;
    private readonly SearchClient? _client;
    private readonly ILogger<AlgoliaIndexService> _logger;

    public AlgoliaIndexService(IOptions<AlgoliaOptions> options, ILogger<AlgoliaIndexService> logger)
    {
        _options = options.Value;
        _logger = logger;

        // Escribir en el índice requiere la Admin API Key; solo se usa en el servidor.
        if (_options.IndexacionConfigurada)
        {
            _client = new SearchClient(new SearchConfig(_options.AppId!, _options.AdminApiKey!));
        }
    }

    public async Task SincronizarDesdeSqliteAsync(ApplicationDbContext db, CancellationToken cancellationToken = default)
    {
        if (_client is null)
        {
            _logger.LogWarning("Indexación de Algolia desactivada: faltan ALGOLIA_APP_ID, ALGOLIA_ADMIN_API_KEY o ALGOLIA_INDEX_NAME.");
            return;
        }

        try
        {
            var incidencias = await db.Incidencias.AsNoTracking().OrderBy(i => i.Id).ToListAsync(cancellationToken);
            var indice = _options.IndexName!;

            // Crea el índice si no existe y fija los atributos buscables: estación y descripción.
            var ajustes = await _client.SetSettingsAsync(indice, new IndexSettings
            {
                SearchableAttributes = ["estacion", "descripcion"]
            }, cancellationToken: cancellationToken);
            await _client.WaitForTaskAsync(indice, ajustes.TaskID, ct: cancellationToken);

            // Sustituye el contenido del índice por el de SQLite de forma atómica.
            await _client.ReplaceAllObjectsAsync(indice, incidencias.Select(ARegistro), cancellationToken: cancellationToken);

            _logger.LogInformation("Algolia sincronizado con SQLite: {Cantidad} incidencias en el índice {Indice}.",
                incidencias.Count, indice);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "No se pudo sincronizar Algolia con SQLite; la búsqueda puede devolver datos incompletos.");
        }
    }

    public async Task ActualizarEstadoAsync(int id, EstadoIncidencia estado)
    {
        if (_client is null)
        {
            return;
        }

        try
        {
            await _client.PartialUpdateObjectAsync(_options.IndexName!, id.ToString(), new { estado = estado.ToString() });
            _logger.LogInformation("Algolia actualizado: incidencia #{Id} → {Estado}.", id, estado);
        }
        catch (Exception ex)
        {
            // El cambio ya está en SQLite y la búsqueda filtra por SQLite: un fallo aquí no afecta al resultado.
            _logger.LogError(ex, "No se pudo actualizar la incidencia #{Id} en Algolia.", id);
        }
    }

    // objectID = Id de SQLite, para poder cruzar los resultados de la búsqueda con la base local.
    private static object ARegistro(Incidencia i) => new
    {
        objectID = i.Id.ToString(),
        id = i.Id,
        estacion = i.Estacion,
        descripcion = i.Descripcion,
        prioridad = i.Prioridad.ToString(),
        estado = i.Estado.ToString()
    };
}
