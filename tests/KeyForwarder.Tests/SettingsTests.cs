using KeyForwarder.Settings;

namespace KeyForwarder.Tests;

public class AppSettingsTests
{
    [Fact]
    public void CreateDefault_HasExpectedDefaults()
    {
        var s = AppSettings.CreateDefault();

        Assert.Equal(15, s.DelayMs);
        Assert.Equal(5000, s.WarnLength);
        Assert.True(s.Enabled);
        Assert.False(s.StartWithWindows);
        Assert.Equal("Ctrl+Shift+V", s.TypeHotkey.ToString());
        Assert.Equal("Esc", s.CancelHotkey.ToString());
    }
}

public class SettingsStoreTests
{
    [Fact]
    public void SaveAndLoad_RoundTrips()
    {
        var path = Path.Combine(Path.GetTempPath(), "KeyForwarderTests", Guid.NewGuid().ToString("N"), "settings.json");
        try
        {
            var store = new SettingsStore(path);
            var original = new AppSettings
            {
                DelayMs = 42,
                WarnLength = 1000,
                Enabled = false,
                StartWithWindows = true,
                TypeHotkey = new HotkeyBinding { Control = true, VirtualKey = 0x43 }, // Ctrl+C
                CancelHotkey = new HotkeyBinding { VirtualKey = 0x1B }
            };

            store.Save(original);
            var loaded = store.Load();

            Assert.Equal(42, loaded.DelayMs);
            Assert.Equal(1000, loaded.WarnLength);
            Assert.False(loaded.Enabled);
            Assert.True(loaded.StartWithWindows);
            Assert.True(loaded.TypeHotkey.Control);
            Assert.Equal(0x43, loaded.TypeHotkey.VirtualKey);
            Assert.Equal(0x1B, loaded.CancelHotkey.VirtualKey);
        }
        finally
        {
            var dir = Path.GetDirectoryName(path);
            if (dir is not null && Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }

    [Fact]
    public void Load_MissingFile_ReturnsDefaults()
    {
        var path = Path.Combine(Path.GetTempPath(), "KeyForwarderTests", Guid.NewGuid().ToString("N"), "missing.json");
        var store = new SettingsStore(path);
        var loaded = store.Load();

        Assert.Equal(AppSettings.DefaultDelayMs, loaded.DelayMs);
        Assert.True(loaded.Enabled);
    }

    [Fact]
    public void Normalize_ClampsDelay()
    {
        var s = new AppSettings { DelayMs = 999 };
        SettingsStore.Normalize(s);
        Assert.Equal(500, s.DelayMs);

        s.DelayMs = -5;
        SettingsStore.Normalize(s);
        Assert.Equal(0, s.DelayMs);
    }
}
