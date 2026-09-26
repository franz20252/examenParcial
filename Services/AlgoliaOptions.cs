namespace examenParcial.Services;

// Se rellena desde las variables de entorno ALGOLIA_APP_ID, ALGOLIA_SEARCH_API_KEY,
// ALGOLIA_ADMIN_API_KEY y ALGOLIA_INDEX_NAME (ver Program.cs). Solo se usa en el servidor.
public class AlgoliaOptions
{
    public string? AppId { get; set; }

    public string? SearchApiKey { get; set; }

    public string? AdminApiKey { get; set; }

    public string? IndexName { get; set; }

    // Para buscar basta la Search API Key; la Admin solo se usa como respaldo en el servidor.
    public string? ApiKeyParaBusqueda =>
        !string.IsNullOrWhiteSpace(SearchApiKey) ? SearchApiKey : AdminApiKey;

    public bool EstaConfigurado =>
        !string.IsNullOrWhiteSpace(AppId)
        && !string.IsNullOrWhiteSpace(ApiKeyParaBusqueda)
        && !string.IsNullOrWhiteSpace(IndexName);

    // Cargar datos en el índice requiere la Admin API Key.
    public bool IndexacionConfigurada =>
        !string.IsNullOrWhiteSpace(AppId)
        && !string.IsNullOrWhiteSpace(AdminApiKey)
        && !string.IsNullOrWhiteSpace(IndexName);
}
