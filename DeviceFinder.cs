using System.Linq;

namespace MidiShortcuts;

internal static class DeviceFinder
{
    /// <summary>
    /// Elige el dispositivo MIDI preferido de la lista: primero el guardado
    /// en la config si sigue disponible, luego uno que contenga "star" o
    /// "pad" en el nombre (evitando los que digan "daw"), y si no hay nada
    /// de eso, el primero disponible.
    /// </summary>
    public static string? FindPreferredDevice(string[] names, string? preferredName)
    {
        if (names.Length == 0) return null;

        if (preferredName != null && Array.IndexOf(names, preferredName) >= 0)
        {
            return preferredName;
        }

        var starry = names.Where(n => n.Contains("star", StringComparison.OrdinalIgnoreCase)
                                    || n.Contains("pad", StringComparison.OrdinalIgnoreCase)).ToArray();
        var nonDaw = starry.Where(n => !n.Contains("daw", StringComparison.OrdinalIgnoreCase)).ToArray();
        if (nonDaw.Length > 0) return nonDaw[0];
        if (starry.Length > 0) return starry[0];

        return names[0];
    }
}
