using Algolia.Search.Clients;
using Algolia.Search.Models.Search;
using Microsoft.Extensions.Options;

namespace examenParcial.Services;

public interface IAlgoliaSearchService
{
    bool EstaConfigurado { get; }

    // Devuelve los Ids de incidencia encontrados en Algolia, en orden de relevancia.
    Task<IReadOnlyList<int>> BuscarIdsAsync(string texto, CancellationToken cancellationToken = default);
}

public class AlgoliaSearchService : IAlgoliaSearchService
{
    private const int MaxResultados = 100;

    private readonly AlgoliaOptions _options;
    private readonly SearchClient? _client;
    private readonly ILogger<AlgoliaSearchService> _logger;

    public AlgoliaSearchService(IOptions<AlgoliaOptions> options, ILogger<AlgoliaSearchService> logger)
    {
        _options = options.Value;
        _logger = logger;

        if (_options.EstaConfigurado)
        {
            if (string.IsNullOrWhiteSpace(_options.SearchApiKey))
            {
                _logger.LogWarning("ALGOLIA_SEARCH_API_KEY no está definida; se usará la Admin API Key solo en el servidor.");
            }

            _client = new SearchClient(new SearchConfig(_options.AppId!, _options.ApiKeyParaBusqueda!));
        }
    }

    public bool EstaConfigurado => _client is not null;

    public async Task<IReadOnlyList<int>> BuscarIdsAsync(string texto, CancellationToken cancellationToken = default)
    {
        if (_client is null)
        {
            throw new InvalidOperationException("Algolia no está configurado.");
        }

        var parametros = new SearchParams(new SearchParamsObject
        {
            Query = texto,
            HitsPerPage = MaxResultados,
            AttributesToRetrieve = ["objectID", "id", "Id"]
        });

        var respuesta = await _client.SearchSingleIndexAsync<Hit>(
            _options.IndexName!, parametros, cancellationToken: cancellationToken);

        var ids = new List<int>();
        foreach (var hit in respuesta.Hits)
        {
            if (!TryObtenerId(hit, out var id))
            {
                _logger.LogWarning("Hit de Algolia sin Id numérico reconocible: {ObjectID}", hit.ObjectID);
            }
            else if (!ids.Contains(id))
            {
                ids.Add(id);
            }
        }

        return ids;
    }

    // Prioriza un atributo explícito "id"/"Id" del registro; si no existe, usa el objectID.
    // Así un objectID autogenerado por Algolia (que puede ser numérico) no se confunde con el Id local.
    private static bool TryObtenerId(Hit hit, out int id)
    {
        if (hit.AdditionalProperties is not null)
        {
            foreach (var clave in new[] { "id", "Id" })
            {
                if (hit.AdditionalProperties.TryGetValue(clave, out var valor)
                    && int.TryParse(valor?.ToString(), out id))
                {
                    return true;
                }
            }
        }

        return int.TryParse(hit.ObjectID, out id);
    }
}
