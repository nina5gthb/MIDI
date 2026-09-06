using System.Text.Json;
using System.Text.Json.Serialization;

namespace MidiShortcuts;

/// <summary>
/// Usa exactamente los mismos nombres de campo que el config.json de la
/// version en Python ("port_name", "mappings" con "type"/"value"), asi
/// puedes copiar tu config.json existente junto al .exe y se sigue leyendo
/// igual, sin tener que rehacer los atajos.
/// </summary>
public class AppConfig
{
    [JsonPropertyName("port_name")]
    public string? PortName { get; set; }

    [JsonPropertyName("mappings")]
    public Dictionary<string, ShortcutAction> Mappings { get; set; } = new();

    public static readonly string ConfigPath = Path.Combine(AppContext.BaseDirectory, "config.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    /// <summary>
    /// Lee config.json. Si el archivo esta corrupto (JSON invalido), NO lo
    /// pisa con uno vacio: lo respalda como config.json.corrupto-<fecha>
    /// y avisa, para no perder los atajos guardados por un problema
    /// pasajero de lectura/escritura.
    /// </summary>
    public static AppConfig Load()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                var json = File.ReadAllText(ConfigPath);
                if (string.IsNullOrWhiteSpace(json))
                {
                    Logger.Log("config.json esta vacio, se usa uno nuevo (no se sobreescribe nada todavia).");
                    return new AppConfig();
                }

                var cfg = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions);
                if (cfg != null)
                {
                    cfg.Mappings ??= new Dictionary<string, ShortcutAction>();
                    return cfg;
                }
            }
        }
        catch (Exception ex)
        {
            // No se descarta el archivo: se guarda una copia para poder
            // recuperar los atajos a mano si hace falta, y se avisa fuerte
            // en el log en vez de perderlo en silencio.
            try
            {
                if (File.Exists(ConfigPath))
                {
                    var backupPath = ConfigPath + $".corrupto-{DateTime.Now:yyyyMMdd-HHmmss}.bak";
                    File.Copy(ConfigPath, backupPath, overwrite: true);
                    Logger.Log($"config.json parecia corrupto ({ex.Message}). Se guardo una copia en: {backupPath}");
                }
            }
            catch (Exception backupEx)
            {
                Logger.Log($"config.json parecia corrupto ({ex.Message}) y ademas fallo el respaldo: {backupEx.Message}");
            }
        }
        return new AppConfig();
    }

    /// <summary>
    /// Guarda de forma atomica (escribe a un archivo temporal y recien al
    /// final reemplaza el config.json real), para que un cierre inesperado
    /// a media escritura no deje un archivo corrupto o vacio.
    /// </summary>
    public void Save()
    {
        try
        {
            var json = JsonSerializer.Serialize(this, JsonOptions);
            var tempPath = ConfigPath + ".tmp";
            File.WriteAllText(tempPath, json);

            if (File.Exists(ConfigPath))
            {
                File.Replace(tempPath, ConfigPath, null);
            }
            else
            {
                File.Move(tempPath, ConfigPath);
            }
        }
        catch (Exception ex)
        {
            Logger.Log($"No se pudo guardar config.json: {ex.Message}");
        }
    }
}
