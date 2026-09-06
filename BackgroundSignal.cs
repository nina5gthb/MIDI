using System.Threading;

namespace MidiShortcuts;

/// <summary>
/// En la version Python, "stop_background_instances" mataba el proceso
/// pythonw.exe en segundo plano con un comando de PowerShell. Aca se usa
/// un EventWaitHandle con nombre (mecanismo nativo de Windows) para
/// pedirle de forma limpia a la instancia en segundo plano que se detenga
/// y libere el puerto MIDI, sin tener que matar el proceso.
/// </summary>
internal static class BackgroundSignal
{
    private const string EventName = "Local\\MidiShortcuts_StopBackground";

    /// <summary>Usado por la GUI: le pide a la instancia --background (si existe) que se detenga.</summary>
    public static void RequestStop()
    {
        try
        {
            using var handle = EventWaitHandle.OpenExisting(EventName);
            handle.Set();
            Thread.Sleep(400); // le da tiempo a cerrar el puerto antes de que la GUI intente abrirlo
        }
        catch (WaitHandleCannotBeOpenedException)
        {
            // No habia ninguna instancia en segundo plano corriendo, no hay nada que hacer.
        }
        catch (Exception ex)
        {
            Logger.Log($"No se pudo señalizar a la instancia en segundo plano: {ex.Message}");
        }
    }

    /// <summary>Usado por el modo --background: crea el evento y devuelve el handle para esperarlo.</summary>
    public static EventWaitHandle CreateListener()
    {
        return new EventWaitHandle(false, EventResetMode.ManualReset, EventName);
    }
}
