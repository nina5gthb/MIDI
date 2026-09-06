using System.Windows.Forms;

namespace MidiShortcuts;

/// <summary>
/// MIDI Shortcuts - Starrypad mini (version C# / WinForms)
/// ---------------------------------------------------------
/// Escucha los mensajes MIDI de un controlador (ej. Starrypad mini) y
/// ejecuta atajos personalizados: combinacion de teclas, escribir texto,
/// o abrir un programa/archivo/carpeta.
///
/// Ejecutar (interfaz grafica):
///     MidiShortcuts.exe
///
/// Ejecutar en segundo plano (sin ventana):
///     MidiShortcuts.exe --background
/// </summary>
internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        // Si algo revienta antes de que se abra cualquier ventana (ej. un
        // problema con el manifest, con una DllImport, etc.), sin esto el
        // proceso simplemente se cierra sin avisar nada. Con esto al menos
        // queda un mensaje y una linea en el log.
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            var ex = e.ExceptionObject as Exception;
            Logger.Log($"Excepcion no controlada: {ex}");
            MessageBox.Show(
                $"La aplicacion encontro un error y debe cerrarse:\n\n{ex?.Message}\n\nRevisa midi_shortcuts.log para mas detalles.",
                "MIDI Shortcuts - Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        };

        try
        {
            bool background = Array.Exists(args, a => a.Equals("--background", StringComparison.OrdinalIgnoreCase));

            if (background)
            {
                return new BackgroundRunner().Run();
            }

            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
            Application.ThreadException += (_, e) =>
            {
                Logger.Log($"Excepcion en hilo de UI: {e.Exception}");
                MessageBox.Show(
                    $"Ocurrio un error:\n\n{e.Exception.Message}\n\nRevisa midi_shortcuts.log para mas detalles.",
                    "MIDI Shortcuts - Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            };
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            bool minimized = Array.Exists(args, a => a.Equals("--minimized", StringComparison.OrdinalIgnoreCase));
            Application.Run(new MainForm(startHidden: minimized));
            return 0;
        }
        catch (Exception ex)
        {
            Logger.Log($"Fallo al iniciar: {ex}");
            MessageBox.Show(
                $"No se pudo iniciar MIDI Shortcuts:\n\n{ex.Message}\n\nRevisa midi_shortcuts.log para mas detalles.",
                "MIDI Shortcuts - Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 1;
        }
    }
}
