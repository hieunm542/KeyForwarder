using System.Text.Json.Serialization;

namespace KeyForwarder.Settings;

public sealed class AppSettings
{
    public const int DefaultDelayMs = 15;
    public const int DefaultWarnLength = 5000;

    /// <summary>Modifiers + key for typing clipboard (e.g. Control+Shift+V).</summary>
    public HotkeyBinding TypeHotkey { get; set; } = HotkeyBinding.CtrlShiftV;

    /// <summary>Modifiers + key to cancel an in-progress type operation.</summary>
    public HotkeyBinding CancelHotkey { get; set; } = HotkeyBinding.Escape;

    /// <summary>Delay in milliseconds between keystrokes.</summary>
    public int DelayMs { get; set; } = DefaultDelayMs;

    /// <summary>Warn (confirm) before typing when clipboard length exceeds this.</summary>
    public int WarnLength { get; set; } = DefaultWarnLength;

    /// <summary>When false, Type hotkey is ignored.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Register app in HKCU Run for login startup.</summary>
    public bool StartWithWindows { get; set; }

    /// <summary>
    /// Detect hotkeys with a low-level keyboard hook instead of RegisterHotKey. Required for
    /// remote desktop clients, which forward keystrokes to the remote session before Windows
    /// can post WM_HOTKEY. Turn off if another tool conflicts with the hook.
    /// </summary>
    public bool UseLowLevelHook { get; set; } = true;

    public static AppSettings CreateDefault() => new();
}

public sealed class HotkeyBinding
{
    public bool Control { get; set; }
    public bool Alt { get; set; }
    public bool Shift { get; set; }
    public bool Win { get; set; }

    /// <summary>Virtual-key code (Windows VK_*).</summary>
    public int VirtualKey { get; set; }

    [JsonIgnore]
    public static HotkeyBinding CtrlShiftV => new()
    {
        Control = true,
        Shift = true,
        VirtualKey = 0x56 // 'V'
    };

    [JsonIgnore]
    public static HotkeyBinding Escape => new()
    {
        VirtualKey = 0x1B // VK_ESCAPE
    };

    public override string ToString()
    {
        var parts = new List<string>();
        if (Control) parts.Add("Ctrl");
        if (Alt) parts.Add("Alt");
        if (Shift) parts.Add("Shift");
        if (Win) parts.Add("Win");
        parts.Add(FormatVirtualKey(VirtualKey));
        return string.Join("+", parts);
    }

    private static string FormatVirtualKey(int vk) => vk switch
    {
        0x1B => "Esc",
        0x0D => "Enter",
        0x09 => "Tab",
        0x20 => "Space",
        >= 0x30 and <= 0x39 => ((char)vk).ToString(),
        >= 0x41 and <= 0x5A => ((char)vk).ToString(),
        >= 0x70 and <= 0x7B => $"F{vk - 0x6F}",
        _ => $"VK_{vk:X2}"
    };
}
