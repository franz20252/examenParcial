using System.ComponentModel.DataAnnotations;

namespace examenParcial.Models;

public enum EstadoIncidencia
{
    Abierta = 0,
    Cerrada = 1
}

public enum PrioridadIncidencia
{
    Baja = 0,
    Media = 1,
    Alta = 2
}

public class Incidencia
{
    // Coincide con el objectID del registro en el índice de Algolia.
    public int Id { get; set; }

    [Required, StringLength(100)]
    [Display(Name = "Estación")]
    public string Estacion { get; set; } = string.Empty;

    [Required, StringLength(500)]
    [Display(Name = "Descripción")]
    public string Descripcion { get; set; } = string.Empty;

    public PrioridadIncidencia Prioridad { get; set; } = PrioridadIncidencia.Media;

    public EstadoIncidencia Estado { get; set; } = EstadoIncidencia.Abierta;
}
