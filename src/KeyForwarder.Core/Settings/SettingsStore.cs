using System.Text.Json;
using System.Text.Json.Serialization;

namespace KeyForwarder.Settings;

public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never
    };

    private readonly string _filePath;

    public SettingsStore(string? filePath = null)
    {
        _filePath = filePath ?? GetDefaultPath();
    }

    public string FilePath => _filePath;

    public static string GetDefaultPath()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "KeyForwarder");
        return Path.Combine(dir, "settings.json");
    }

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(_filePath))
            {
                return AppSettings.CreateDefault();
            }

            var json = File.ReadAllText(_filePath);
            var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
            return Normalize(settings ?? AppSettings.CreateDefault());
        }
        catch
        {
            return AppSettings.CreateDefault();
        }
    }

    public void Save(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        settings = Normalize(settings);

        var dir = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var json = JsonSerializer.Serialize(settings, JsonOptions);
        File.WriteAllText(_filePath, json);
    }

    public static AppSettings Normalize(AppSettings settings)
    {
        if (settings.DelayMs < 0)
        {
            settings.DelayMs = 0;
        }

        if (settings.DelayMs > 500)
        {
            settings.DelayMs = 500;
        }

        if (settings.WarnLength < 0)
        {
            settings.WarnLength = 0;
        }

        settings.TypeHotkey ??= HotkeyBinding.CtrlShiftV;
        settings.CancelHotkey ??= HotkeyBinding.Escape;

        if (settings.TypeHotkey.VirtualKey == 0)
        {
            settings.TypeHotkey = HotkeyBinding.CtrlShiftV;
        }

        if (settings.CancelHotkey.VirtualKey == 0)
        {
            settings.CancelHotkey = HotkeyBinding.Escape;
        }

        return settings;
    }
}
