using System.Text.RegularExpressions;

namespace examenParcial.Services;

// Se rellena desde las variables de entorno PIESOCKET_CLUSTER_ID, PIESOCKET_API_KEY,
// PIESOCKET_API_SECRET y PIESOCKET_CHANNEL (ver Program.cs).
public partial class PieSocketOptions
{
    public string? ClusterId { get; set; }

    // Pública: el navegador la necesita para abrir el WebSocket.
    public string? ApiKey { get; set; }

    // Privada: solo se usa en el servidor para publicar.
    public string? ApiSecret { get; set; }

    public string? Channel { get; set; }

    // El Cluster ID forma parte del host; se valida para no construir URLs arbitrarias.
    public bool ClienteConfigurado =>
        !string.IsNullOrWhiteSpace(ClusterId) && ClusterIdValido().IsMatch(ClusterId)
        && !string.IsNullOrWhiteSpace(ApiKey)
        && !string.IsNullOrWhiteSpace(Channel);

    public bool PublicacionConfigurada => ClienteConfigurado && !string.IsNullOrWhiteSpace(ApiSecret);

    // Acepta el Cluster ID ("s12345.nyc1") o el host/URL que muestra el panel
    // ("https://s12345.nyc1.piesocket.com/") y lo reduce al Cluster ID.
    public static string? NormalizarClusterId(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return null;
        }

        var cluster = valor.Trim();
        if (Uri.TryCreate(cluster, UriKind.Absolute, out var uri) && !string.IsNullOrEmpty(uri.Host))
        {
            cluster = uri.Host;
        }

        cluster = cluster.TrimEnd('/').ToLowerInvariant();
        const string dominio = ".piesocket.com";
        return cluster.EndsWith(dominio) ? cluster[..^dominio.Length] : cluster;
    }

    // Los clusters de PieHost pueden contener puntos, p. ej. "s12345.nyc1" o "free.blr2".
    [GeneratedRegex("^[a-z0-9]([a-z0-9.-]*[a-z0-9])?$")]
    private static partial Regex ClusterIdValido();
}
