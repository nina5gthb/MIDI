using System.Runtime.InteropServices;
using static MidiShortcuts.NativeInput;
using INPUT = MidiShortcuts.NativeInput.INPUT;
using InputUnion = MidiShortcuts.NativeInput.InputUnion;
using KEYBDINPUT = MidiShortcuts.NativeInput.KEYBDINPUT;

namespace MidiShortcuts;

/// <summary>
/// Simula combinaciones de teclas (ej. "ctrl+c", "alt+tab", "win+d",
/// "ctrl+shift+esc") y escritura de texto, usando SendInput de Windows.
///
/// Nota: igual que con la libreria "keyboard" de Python, esto no puede
/// enviar teclas a una ventana que corre como Administrador si esta app
/// no corre tambien como Administrador (restriccion UIPI de Windows).
/// </summary>
internal static class KeySender
{
    private static readonly Dictionary<string, ushort> VkMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ctrl"] = 0x11, ["control"] = 0x11,
        ["shift"] = 0x10,
        ["alt"] = 0x12, ["menu"] = 0x12,
        ["win"] = 0x5B, ["windows"] = 0x5B, ["super"] = 0x5B,
        ["esc"] = 0x1B, ["escape"] = 0x1B,
        ["tab"] = 0x09,
        ["enter"] = 0x0D, ["return"] = 0x0D,
        ["space"] = 0x20, ["espacio"] = 0x20,
        ["backspace"] = 0x08,
        ["delete"] = 0x2E, ["del"] = 0x2E, ["suprimir"] = 0x2E,
        ["insert"] = 0x2D, ["ins"] = 0x2D,
        ["home"] = 0x24, ["inicio"] = 0x24,
        ["end"] = 0x23, ["fin"] = 0x23,
        ["pageup"] = 0x21, ["pgup"] = 0x21, ["repag"] = 0x21,
        ["pagedown"] = 0x22, ["pgdn"] = 0x22, ["avpag"] = 0x22,
        ["up"] = 0x26, ["arriba"] = 0x26,
        ["down"] = 0x28, ["abajo"] = 0x28,
        ["left"] = 0x25, ["izquierda"] = 0x25,
        ["right"] = 0x27, ["derecha"] = 0x27,
        ["capslock"] = 0x14,
        ["numlock"] = 0x90,
        ["scrolllock"] = 0x91,
        ["printscreen"] = 0x2C, ["prtsc"] = 0x2C, ["impr"] = 0x2C,
        ["pause"] = 0x13,
        ["plus"] = 0xBB, ["minus"] = 0xBD,
        ["comma"] = 0xBC, ["period"] = 0xBE,
        ["apps"] = 0x5D, ["menukey"] = 0x5D,
    };

    private static readonly HashSet<ushort> ExtendedKeys = new()
    {
        0x2E, 0x2D, 0x24, 0x23, 0x21, 0x22, // delete, insert, home, end, pageup, pagedown
        0x26, 0x28, 0x25, 0x27,             // flechas
        0x5B, 0x5D,                          // teclas Windows / menu contextual
        0x90, 0x2C,                          // numlock, printscreen
    };

    static KeySender()
    {
        for (int i = 1; i <= 24; i++) VkMap[$"f{i}"] = (ushort)(0x70 + (i - 1));
        for (char c = '0'; c <= '9'; c++) VkMap[c.ToString()] = c;
        for (char c = 'a'; c <= 'z'; c++) VkMap[c.ToString()] = (ushort)char.ToUpperInvariant(c);
    }

    public static void SendCombo(string combo)
    {
        if (string.IsNullOrWhiteSpace(combo)) return;

        var parts = combo.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var vks = new List<ushort>();
        foreach (var part in parts)
        {
            if (VkMap.TryGetValue(part, out var vk)) vks.Add(vk);
        }
        if (vks.Count == 0) return;

        var inputs = new List<INPUT>();
        foreach (var vk in vks) inputs.Add(MakeKeyInput(vk, keyUp: false));
        for (int i = vks.Count - 1; i >= 0; i--) inputs.Add(MakeKeyInput(vks[i], keyUp: true));

        SendInput((uint)inputs.Count, inputs.ToArray(), Marshal.SizeOf<INPUT>());
    }

    public static void TypeText(string text)
    {
        if (string.IsNullOrEmpty(text)) return;

        var inputs = new List<INPUT>();
        foreach (var ch in text)
        {
            inputs.Add(MakeUnicodeInput(ch, keyUp: false));
            inputs.Add(MakeUnicodeInput(ch, keyUp: true));
        }
        SendInput((uint)inputs.Count, inputs.ToArray(), Marshal.SizeOf<INPUT>());
    }

    private static INPUT MakeKeyInput(ushort vk, bool keyUp)
    {
        uint flags = keyUp ? KEYEVENTF_KEYUP : 0;
        if (ExtendedKeys.Contains(vk)) flags |= KEYEVENTF_EXTENDEDKEY;
        return new INPUT
        {
            type = INPUT_KEYBOARD,
            U = new InputUnion { ki = new KEYBDINPUT { wVk = vk, wScan = 0, dwFlags = flags, time = 0, dwExtraInfo = IntPtr.Zero } }
        };
    }

    private static INPUT MakeUnicodeInput(char ch, bool keyUp)
    {
        uint flags = KEYEVENTF_UNICODE | (keyUp ? KEYEVENTF_KEYUP : 0);
        return new INPUT
        {
            type = INPUT_KEYBOARD,
            U = new InputUnion { ki = new KEYBDINPUT { wVk = 0, wScan = ch, dwFlags = flags, time = 0, dwExtraInfo = IntPtr.Zero } }
        };
    }
}
