namespace examenParcial.Models;

public class IncidenciasViewModel
{
    public string? Busqueda { get; set; }

    public IReadOnlyList<Incidencia> Incidencias { get; set; } = [];

    public string? Error { get; set; }

    public bool EsBusqueda => !string.IsNullOrWhiteSpace(Busqueda);
}
