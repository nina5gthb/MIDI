namespace MidiShortcuts;

internal static class Logger
{
    private static readonly string LogPath = Path.Combine(AppContext.BaseDirectory, "midi_shortcuts.log");
    private static readonly object Lock = new();

    public static void Log(string text)
    {
        var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {text}";
        try
        {
            lock (Lock)
            {
                File.AppendAllText(LogPath, line + Environment.NewLine);
            }
        }
        catch
        {
            // Si no se puede escribir el log (permisos, disco, etc.) no se
            // interrumpe la app por esto.
        }
    }
}
