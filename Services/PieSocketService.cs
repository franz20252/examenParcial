using System.Net.Http.Json;
using System.Text.Json;
using examenParcial.Models;
using Microsoft.Extensions.Options;

namespace examenParcial.Services;

public interface IPieSocketService
{
    // Publica IncidenciaActualizada en el canal configurado. Nunca lanza excepción:
    // devuelve false si no se pudo publicar (el cambio en SQLite ya está guardado).
    Task<bool> PublicarIncidenciaActualizadaAsync(int id, EstadoIncidencia estado);
}

public class PieSocketService(
    HttpClient http,
    IOptions<PieSocketOptions> options,
    ILogger<PieSocketService> logger) : IPieSocketService
{
    public const string EventoIncidenciaActualizada = "IncidenciaActualizada";

    private readonly PieSocketOptions _options = options.Value;

    public async Task<bool> PublicarIncidenciaActualizadaAsync(int id, EstadoIncidencia estado)
    {
        if (!_options.PublicacionConfigurada)
        {
            logger.LogWarning("PieSocket no configurado: no se publicó {Evento} de la incidencia #{Id}.",
                EventoIncidenciaActualizada, id);
            return false;
        }

        // API REST de PieSocket: POST https://{cluster}.piesocket.com/api/publish
        // El mensaje sigue el formato de eventos del SDK oficial: { event, data }.
        var url = $"https://{_options.ClusterId}.piesocket.com/api/publish";
        var cuerpo = new
        {
            key = _options.ApiKey,
            secret = _options.ApiSecret,
            roomId = _options.Channel,
            message = new
            {
                @event = EventoIncidenciaActualizada,
                data = new { type = EventoIncidenciaActualizada, id, estado = estado.ToString() }
            }
        };

        try
        {
            // Sin CancellationToken de la petición: el cambio ya está en SQLite y el evento debe salir.
            using var respuesta = await http.PostAsJsonAsync(url, cuerpo);
            var texto = await respuesta.Content.ReadAsStringAsync();

            // PieSocket responde {"success":true} al publicar y {"error":"..."} (HTTP 403) si las credenciales fallan.
            if (!respuesta.IsSuccessStatusCode || !IndicaExito(texto))
            {
                logger.LogError("PieSocket rechazó {Evento} de la incidencia #{Id}: HTTP {Status} {Respuesta}",
                    EventoIncidenciaActualizada, id, (int)respuesta.StatusCode, Recortar(texto));
                return false;
            }

            logger.LogInformation("Evento {Evento} publicado en PieSocket: incidencia #{Id} → {Estado}. Respuesta: {Respuesta}",
                EventoIncidenciaActualizada, id, estado, Recortar(texto));
            return true;
        }
        catch (Exception ex)
        {
            // Solo el tipo y el mensaje de red; la petición (que contiene el secret) nunca se registra.
            logger.LogError("Error de red al publicar {Evento} de la incidencia #{Id} en PieSocket: {Tipo}: {Mensaje}",
                EventoIncidenciaActualizada, id, ex.GetType().Name, ex.Message);
            return false;
        }
    }

    private static bool IndicaExito(string texto)
    {
        try
        {
            using var json = JsonDocument.Parse(texto);
            var raiz = json.RootElement;
            if (raiz.ValueKind != JsonValueKind.Object)
            {
                return true;
            }
            if (raiz.TryGetProperty("error", out _))
            {
                return false;
            }
            return !raiz.TryGetProperty("success", out var exito) || exito.ValueKind != JsonValueKind.False;
        }
        catch (JsonException)
        {
            // Respuesta 2xx sin JSON: se acepta el código HTTP.
            return true;
        }
    }

    // Recorta la respuesta y oculta el secret por si el servicio lo devolviera en un error.
    private string Recortar(string texto)
    {
        if (!string.IsNullOrEmpty(_options.ApiSecret))
        {
            texto = texto.Replace(_options.ApiSecret, "***");
        }
        return texto.Length > 300 ? texto[..300] : texto;
    }
}
