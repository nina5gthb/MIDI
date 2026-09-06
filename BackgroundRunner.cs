using System.Threading;

namespace MidiShortcuts;

/// <summary>
/// Equivalente a BackgroundService de la version Python: corre sin
/// ventana, se reconecta solo si el dispositivo se cae (ej. Bluetooth) y
/// recarga la config en cada pulsacion por si se edito mientras corria.
/// </summary>
internal sealed class BackgroundRunner
{
    private const int ReconnectDelayMs = 3000;

    public int Run()
    {
        var stopEvent = BackgroundSignal.CreateListener();
        var cfg = AppConfig.Load();
        var cfgLoadedAt = DateTime.UtcNow;

        Logger.Log("Servicio MIDI en segundo plano iniciado.");

        while (!stopEvent.WaitOne(0))
        {
            var names = MidiInputDevice.GetDeviceNames();
            var chosenName = DeviceFinder.FindPreferredDevice(names, cfg.PortName);
            if (chosenName == null)
            {
                Logger.Log("Esperando dispositivo MIDI...");
                if (stopEvent.WaitOne(ReconnectDelayMs)) break;
                continue;
            }
            int deviceId = Array.IndexOf(names, chosenName);

            Logger.Log($"Conectado a: {chosenName}");
            using var listener = new MidiListener();
            using var disconnected = new ManualResetEventSlim(false);
            listener.Disconnected += () => disconnected.Set();

            listener.Trigger += key =>
            {
                // Solo se relee config.json si de verdad cambio desde la
                // ultima vez (no en cada pulsacion), y si falla la lectura
                // se sigue usando la copia que ya esta en memoria en vez
                // de reemplazarla por una vacia.
                try
                {
                    if (File.Exists(AppConfig.ConfigPath))
                    {
                        var lastWrite = File.GetLastWriteTimeUtc(AppConfig.ConfigPath);
                        if (lastWrite > cfgLoadedAt)
                        {
                            cfg = AppConfig.Load();
                            cfgLoadedAt = DateTime.UtcNow;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Log($"No se pudo revisar cambios en config.json, se sigue usando la copia en memoria: {ex.Message}");
                }

                var action = MappingHelper.FindMapping(cfg.Mappings, key);
                if (action == null)
                {
                    Logger.Log($"Pad pulsado sin atajo: {key}");
                    return;
                }
                try
                {
                    action.Execute();
                    Logger.Log($"Accion ejecutada: {MappingHelper.DescribeKey(key)} -> {action.Value}");
                }
                catch (Exception ex)
                {
                    Logger.Log($"Error ejecutando accion ({key}): {ex.Message}");
                }
            };

            if (!listener.Start(deviceId))
            {
                Logger.Log("No se pudo abrir el dispositivo MIDI. Reintentando...");
                if (stopEvent.WaitOne(ReconnectDelayMs)) break;
                continue;
            }

            // Espera hasta que llegue la señal de detener el proceso o el
            // dispositivo se desconecte solo (ej. corte de Bluetooth), y de
            // paso revisa cada tanto si sigue en la lista de dispositivos.
            int idx = WaitHandle.WaitAny(new WaitHandle[] { stopEvent, disconnected.WaitHandle }, ReconnectDelayMs);
            while (idx == WaitHandle.WaitTimeout)
            {
                var currentNames = MidiInputDevice.GetDeviceNames();
                if (Array.IndexOf(currentNames, chosenName) < 0) break;
                idx = WaitHandle.WaitAny(new WaitHandle[] { stopEvent, disconnected.WaitHandle }, ReconnectDelayMs);
            }

            if (stopEvent.WaitOne(0)) break;

            Logger.Log("Dispositivo desconectado. Reintentando...");
            if (stopEvent.WaitOne(ReconnectDelayMs)) break;
        }

        Logger.Log("Servicio MIDI detenido.");
        return 0;
    }
}
