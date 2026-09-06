using System.Globalization;

namespace MidiShortcuts;

internal static class MappingHelper
{
    /// <summary>
    /// Busca la accion mapeada para una clave tipo "note_60" o "cc_20".
    /// Tambien soporta el formato viejo con canal "note_0_60" por si
    /// vienes de una config anterior, igual que el find_mapping original.
    /// </summary>
    public static ShortcutAction? FindMapping(Dictionary<string, ShortcutAction> mappings, string key)
    {
        if (mappings.TryGetValue(key, out var direct)) return direct;

        var parts = key.Split('_');
        if (parts.Length == 3 && mappings.TryGetValue($"{parts[0]}_{parts[2]}", out var short3))
        {
            return short3;
        }
        if (parts.Length == 2)
        {
            foreach (var kv in mappings)
            {
                var oldParts = kv.Key.Split('_');
                if (oldParts.Length == 3 && $"{oldParts[0]}_{oldParts[2]}" == key)
                {
                    return kv.Value;
                }
            }
        }
        return null;
    }

    public static string DescribeKey(string key)
    {
        var parts = key.Split('_');
        if (parts.Length >= 2 && parts[0] == "note")
        {
            if (parts.Length == 3 && int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var ch))
            {
                return $"Pad/nota {parts[2]} (canal {ch + 1})";
            }
            return $"Pad/nota {parts[1]}";
        }
        if (parts.Length >= 2 && parts[0] == "cc")
        {
            if (parts.Length == 3 && int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var ch))
            {
                return $"Control {parts[2]} (canal {ch + 1})";
            }
            return $"Control {parts[1]}";
        }
        return key;
    }
}
