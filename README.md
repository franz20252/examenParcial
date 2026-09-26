# examenParcial

Aplicación ASP.NET Core MVC (.NET 10) con Identity, EF Core + SQLite y gestión de incidencias en `/Operaciones/Incidencias`.

## Tiempo real con PieSocket (PieHost)

Al cerrar una incidencia, el servidor guarda `Estado = Cerrada` en SQLite y, **solo después**, publica el evento `IncidenciaActualizada` en el canal de PieSocket. Las pantallas abiertas lo reciben por WebSocket y retiran la incidencia sin recargar la página. Si la conexión se pierde, el navegador se reconecta con espera progresiva (1 s → 30 s) y vuelve a pedir el listado actual.

- Publicación (servidor): `POST https://{PIESOCKET_CLUSTER_ID}.piesocket.com/api/publish`
- Suscripción (navegador): `wss://{PIESOCKET_CLUSTER_ID}.piesocket.com/v3/{PIESOCKET_CHANNEL}?api_key=...`
- Mensaje: `{"event":"IncidenciaActualizada","data":{"type":"IncidenciaActualizada","id":5,"estado":"Cerrada"}}`

## Variables de entorno (Render)

Configúralas en Render → _Environment_. No guardes valores reales en el repositorio.

| Variable                                 | Obligatoria          | Descripción                                                                                                               |
| ---------------------------------------- | -------------------- | ------------------------------------------------------------------------------------------------------------------------- |
| `PIESOCKET_CLUSTER_ID`                   | Sí, para tiempo real | Cluster de la API key (p. ej. `s12345.nyc1`). También se acepta la URL del cluster (`https://s12345.nyc1.piesocket.com/`) |
| `PIESOCKET_API_KEY`                      | Sí, para tiempo real | API key. Es pública: el navegador la usa para abrir el WebSocket                                                          |
| `PIESOCKET_API_SECRET`                   | Sí, para publicar    | API secret. Solo se usa en el servidor; nunca llega al navegador                                                          |
| `PIESOCKET_CHANNEL`                      | Sí, para tiempo real | Canal/room donde se publican los eventos                                                                                  |
| `ConnectionStrings__DefaultConnection`   | No                   | SQLite; por defecto `Data Source=examenParcial.db`                                                                        |
| `SEED_USER_EMAIL` / `SEED_USER_PASSWORD` | No                   | Crea un usuario de prueba al arrancar                                                                                     |

Sin las variables de PieSocket la aplicación funciona igual, pero sin actualización en tiempo real.

## Desarrollo local (User Secrets)

```powershell
dotnet user-secrets set "PIESOCKET_CLUSTER_ID" "<cluster-id>"
dotnet user-secrets set "PIESOCKET_API_KEY" "<api-key>"
dotnet user-secrets set "PIESOCKET_API_SECRET" "<api-secret>"
dotnet user-secrets set "PIESOCKET_CHANNEL" "<canal>"
dotnet user-secrets set "SEED_USER_EMAIL" "<email>"
dotnet user-secrets set "SEED_USER_PASSWORD" "<contraseña>"
dotnet run
```

USUARIO:profe@usmp.pe
Contraseña:TuClave#2026
