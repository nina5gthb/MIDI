using System.Runtime.InteropServices;

namespace MidiShortcuts;

/// <summary>
/// Envoltorio delgado sobre la API de MIDI de Windows (winmm.dll) para
/// listar dispositivos de entrada y recibir mensajes cortos (note on/off,
/// control change), sin depender de ninguna libreria externa.
///
/// Nota sobre Bluetooth: un dispositivo BLE-MIDI ya emparejado en Windows
/// (10 version 1809+ / 11) aparece aqui igual que uno por USB, porque
/// Windows lo puentea al mismo API clasico que usa esta clase.
/// </summary>
internal sealed class MidiInputDevice : IDisposable
{
    private const int CALLBACK_FUNCTION = 0x00030000;
    private const int MIM_DATA = 0x3C3;
    private const int MIM_CLOSE = 0x3C4;
    private const int MMSYSERR_NOERROR = 0;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct MIDIINCAPS
    {
        public ushort wMid;
        public ushort wPid;
        public uint vDriverVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string szPname;
    }

    private delegate void MidiInProc(IntPtr hMidiIn, int wMsg, IntPtr dwInstance, IntPtr dwParam1, IntPtr dwParam2);

    [DllImport("winmm.dll")]
    private static extern int midiInGetNumDevs();

    [DllImport("winmm.dll", CharSet = CharSet.Unicode)]
    private static extern int midiInGetDevCaps(IntPtr uDeviceID, ref MIDIINCAPS caps, int cbMidiInCaps);

    [DllImport("winmm.dll")]
    private static extern int midiInOpen(out IntPtr lphMidiIn, int uDeviceID, MidiInProc dwCallback, IntPtr dwInstance, int dwFlags);

    [DllImport("winmm.dll")]
    private static extern int midiInStart(IntPtr hMidiIn);

    [DllImport("winmm.dll")]
    private static extern int midiInStop(IntPtr hMidiIn);

    [DllImport("winmm.dll")]
    private static extern int midiInClose(IntPtr hMidiIn);

    /// <summary>Evento con (status, data1, data2) de cada mensaje MIDI corto recibido.</summary>
    public event Action<byte, byte, byte>? MessageReceived;

    /// <summary>Se dispara si Windows cierra el dispositivo (ej. se desconecta el Bluetooth).</summary>
    public event Action? DeviceClosed;

    /// <summary>True mientras el dispositivo esta abierto y escuchando.</summary>
    public bool IsOpen { get; private set; }

    private IntPtr _handle = IntPtr.Zero;
    private readonly MidiInProc _callback; // referencia fuerte: evita que el GC la recoja

    public MidiInputDevice()
    {
        _callback = Callback;
    }

    public static string[] GetDeviceNames()
    {
        int count = midiInGetNumDevs();
        var names = new string[count];
        for (int i = 0; i < count; i++)
        {
            var caps = new MIDIINCAPS();
            midiInGetDevCaps((IntPtr)i, ref caps, Marshal.SizeOf<MIDIINCAPS>());
            names[i] = caps.szPname;
        }
        return names;
    }

    public bool Open(int deviceId)
    {
        int result = midiInOpen(out _handle, deviceId, _callback, IntPtr.Zero, CALLBACK_FUNCTION);
        if (result != MMSYSERR_NOERROR)
        {
            _handle = IntPtr.Zero;
            return false;
        }
        midiInStart(_handle);
        IsOpen = true;
        return true;
    }

    private void Callback(IntPtr hMidiIn, int wMsg, IntPtr dwInstance, IntPtr dwParam1, IntPtr dwParam2)
    {
        if (wMsg == MIM_DATA)
        {
            int packed = dwParam1.ToInt32();
            byte status = (byte)(packed & 0xFF);
            byte data1 = (byte)((packed >> 8) & 0xFF);
            byte data2 = (byte)((packed >> 16) & 0xFF);
            MessageReceived?.Invoke(status, data1, data2);
        }
        else if (wMsg == MIM_CLOSE)
        {
            IsOpen = false;
            DeviceClosed?.Invoke();
        }
    }

    public void Dispose()
    {
        if (_handle != IntPtr.Zero)
        {
            IsOpen = false;
            midiInStop(_handle);
            midiInClose(_handle);
            _handle = IntPtr.Zero;
        }
    }
}
