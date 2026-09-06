using System.Diagnostics;
using System.Text.Json.Serialization;

namespace MidiShortcuts;

/// <summary>
/// Type se guarda como string ("tecla" / "texto" / "app"), igual que en el
/// config.json de la version Python, para mantener compatibilidad total.
/// </summary>
public class ShortcutAction
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "tecla";

    [JsonPropertyName("value")]
    public string Value { get; set; } = string.Empty;

    public void Execute()
    {
        switch (Type)
        {
            case "tecla":
                KeySender.SendCombo(Value);
                break;
            case "texto":
                KeySender.TypeText(Value);
                break;
            case "app":
            case "carpeta":
                Process.Start(new ProcessStartInfo(Value) { UseShellExecute = true });
                break;
            default:
                throw new InvalidOperationException($"Tipo de accion desconocido: {Type}");
        }
    }
}
