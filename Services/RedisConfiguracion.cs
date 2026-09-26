using System.Text.RegularExpressions;
using StackExchange.Redis;

namespace examenParcial.Services;

public static partial class RedisConfiguracion
{
    // Acepta el formato de StackExchange.Redis ("host:6379,password=...") y también
    // URLs redis:// o rediss:// como las que entregan Render u otros proveedores.
    public static ConfigurationOptions CrearOpciones(string conexion)
    {
        conexion = conexion.Trim();

        var opciones = conexion.StartsWith("redis://", StringComparison.OrdinalIgnoreCase)
            || conexion.StartsWith("rediss://", StringComparison.OrdinalIgnoreCase)
            ? DesdeUrl(conexion)
            : ConfigurationOptions.Parse(conexion);

        // Si Redis no está disponible, la app sigue arrancando y las operaciones fallan rápido.
        opciones.AbortOnConnectFail = false;
        opciones.BacklogPolicy = BacklogPolicy.FailFast;
        opciones.ConnectTimeout = 3000;
        opciones.SyncTimeout = 2000;
        opciones.AsyncTimeout = 2000;

        return opciones;
    }

    // No usa System.Uri: la contraseña puede contener caracteres como '@', '#' o '/' sin codificar.
    private static ConfigurationOptions DesdeUrl(string url)
    {
        var m = UrlRedis().Match(url);
        if (!m.Success)
        {
            // El mensaje nunca incluye la cadena de conexión.
            throw new FormatException("ConnectionStrings:Redis no tiene un formato redis://[usuario:contraseña@]host[:puerto][/db] válido.");
        }

        var host = m.Groups["host"].Value.Trim();
        var puertoTexto = m.Groups["puerto"].Value.Trim();
        var puerto = 6379;
        if (host.Length == 0 || (puertoTexto.Length > 0 && !int.TryParse(puertoTexto, out puerto)))
        {
            throw new FormatException("ConnectionStrings:Redis tiene un host o puerto no válido.");
        }

        var opciones = new ConfigurationOptions
        {
            Ssl = m.Groups["esquema"].Value.Equals("rediss", StringComparison.OrdinalIgnoreCase)
        };
        opciones.EndPoints.Add(host, puerto);

        if (m.Groups["userinfo"].Success)
        {
            var partes = m.Groups["userinfo"].Value.Split(':', 2);
            var usuario = partes.Length == 2 ? Uri.UnescapeDataString(partes[0].Trim()) : "";
            var password = Uri.UnescapeDataString(partes[^1].Trim());
            if (usuario.Length > 0)
            {
                opciones.User = usuario;
            }
            if (password.Length > 0)
            {
                opciones.Password = password;
            }
        }

        if (m.Groups["db"].Success)
        {
            opciones.DefaultDatabase = int.Parse(m.Groups["db"].Value);
        }

        return opciones;
    }

    // userinfo es codicioso para quedarse con la última '@'; el puerto es lo que sigue a la última ':'.
    [GeneratedRegex(@"^(?<esquema>rediss?)://(?:(?<userinfo>.*)@)?(?<host>[^@:/]+?)\s*(?::(?<puerto>\s*\d+))?\s*(?:/(?<db>\d+))?/?$",
        RegexOptions.IgnoreCase)]
    private static partial Regex UrlRedis();
}
