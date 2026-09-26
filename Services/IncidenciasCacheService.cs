using System.Text.Json;
using examenParcial.Data;
using examenParcial.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.StackExchangeRedis;

namespace examenParcial.Services;

public interface IIncidenciasCacheService
{
    // Listado general de incidencias abiertas: Redis (60 s) con respaldo en SQLite.
    Task<IReadOnlyList<Incidencia>> ObtenerAbiertasAsync(CancellationToken cancellationToken = default);

    // Debe llamarse después de guardar en SQLite un cambio que afecte al listado.
    Task InvalidarAbiertasAsync();
}

public class IncidenciasCacheService : IIncidenciasCacheService
{
    public const string ClaveListadoAbiertas = "incidencias:abiertas:listado-general";
    public static readonly TimeSpan DuracionListadoAbiertas = TimeSpan.FromSeconds(60);

    private readonly ApplicationDbContext _db;
    private readonly IDistributedCache? _redis;
    private readonly ILogger<IncidenciasCacheService> _logger;

    public IncidenciasCacheService(
        ApplicationDbContext db,
        ILogger<IncidenciasCacheService> logger,
        IDistributedCache? cache = null)
    {
        _db = db;
        _logger = logger;
        // Solo se usa Redis real; nunca una caché en memoria registrada por otro componente.
        _redis = cache as RedisCache;
    }

    public async Task<IReadOnlyList<Incidencia>> ObtenerAbiertasAsync(CancellationToken cancellationToken = default)
    {
        var redisDisponible = _redis is not null;

        if (_redis is null)
        {
            _logger.LogInformation("Redis no configurado: el listado de incidencias abiertas se lee desde SQLite.");
        }
        else
        {
            string? json = null;
            try
            {
                json = await _redis.GetStringAsync(ClaveListadoAbiertas, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                redisDisponible = false;
                _logger.LogError(ex, "Error al consultar Redis ({Clave}); se usa SQLite como respaldo.", ClaveListadoAbiertas);
            }

            if (json is not null)
            {
                var cacheadas = Deserializar(json);
                if (cacheadas is not null)
                {
                    _logger.LogInformation("Redis HIT para el listado de incidencias abiertas ({Clave}): {Cantidad} incidencias.",
                        ClaveListadoAbiertas, cacheadas.Count);
                    return cacheadas;
                }

                _logger.LogWarning("Valor inválido en Redis para {Clave}; se ignora y se lee SQLite.", ClaveListadoAbiertas);
            }
            else if (redisDisponible)
            {
                _logger.LogInformation("Redis MISS para el listado de incidencias abiertas ({Clave}).", ClaveListadoAbiertas);
            }
        }

        var abiertas = await LeerAbiertasDeSqliteAsync(cancellationToken);
        _logger.LogInformation("Listado de incidencias abiertas obtenido desde SQLite: {Cantidad} incidencias.", abiertas.Count);

        if (_redis is not null && redisDisponible)
        {
            try
            {
                var opciones = new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = DuracionListadoAbiertas };
                await _redis.SetStringAsync(ClaveListadoAbiertas, Serializar(abiertas), opciones, cancellationToken);
                _logger.LogInformation("Listado de incidencias abiertas guardado en Redis ({Clave}) durante {Segundos} s.",
                    ClaveListadoAbiertas, DuracionListadoAbiertas.TotalSeconds);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Error al guardar en Redis ({Clave}); el listado se sirvió desde SQLite.", ClaveListadoAbiertas);
            }
        }

        return abiertas;
    }

    public async Task InvalidarAbiertasAsync()
    {
        if (_redis is null)
        {
            return;
        }

        try
        {
            // Sin CancellationToken: si la petición se cancela tras guardar en SQLite, la invalidación debe completarse igual.
            await _redis.RemoveAsync(ClaveListadoAbiertas);
            _logger.LogInformation("Caché de Redis invalidada ({Clave}) después de cerrar una incidencia.", ClaveListadoAbiertas);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se pudo invalidar la clave {Clave} en Redis; expirará en como máximo {Segundos} s.",
                ClaveListadoAbiertas, DuracionListadoAbiertas.TotalSeconds);
        }
    }

    private async Task<List<Incidencia>> LeerAbiertasDeSqliteAsync(CancellationToken cancellationToken)
    {
        var lista = await _db.Incidencias
            .AsNoTracking()
            .Where(i => i.Estado == EstadoIncidencia.Abierta)
            .ToListAsync(cancellationToken);

        // Prioridad se guarda como texto; se ordena en memoria para respetar el orden del enum.
        return lista
            .OrderByDescending(i => i.Prioridad)
            .ThenBy(i => i.Id)
            .ToList();
    }

    // Se guarda un DTO plano, independiente del DbContext.
    private sealed record IncidenciaCacheDto(
        int Id, string Estacion, string Descripcion, PrioridadIncidencia Prioridad, EstadoIncidencia Estado);

    private static string Serializar(IEnumerable<Incidencia> incidencias) =>
        JsonSerializer.Serialize(incidencias
            .Select(i => new IncidenciaCacheDto(i.Id, i.Estacion, i.Descripcion, i.Prioridad, i.Estado))
            .ToList());

    private static List<Incidencia>? Deserializar(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<List<IncidenciaCacheDto>>(json)?
                .Select(d => new Incidencia
                {
                    Id = d.Id,
                    Estacion = d.Estacion,
                    Descripcion = d.Descripcion,
                    Prioridad = d.Prioridad,
                    Estado = d.Estado
                })
                .ToList();
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
