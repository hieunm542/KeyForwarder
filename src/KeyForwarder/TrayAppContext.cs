using KeyForwarder.Hotkeys;
using KeyForwarder.Settings;
using KeyForwarder.Startup;
using KeyForwarder.Typing;

namespace KeyForwarder;

internal sealed class TrayAppContext : ApplicationContext
{
    private readonly NotifyIcon _trayIcon;
    private readonly SettingsStore _store;
    private readonly HotkeyService _hotkeys;
    private readonly UnicodeTypeEngine _typeEngine;
    private readonly SynchronizationContext _ui;
    private AppSettings _settings;
    private ToolStripMenuItem _enableItem = null!;

    public TrayAppContext()
    {
        _ui = SynchronizationContext.Current
              ?? new WindowsFormsSynchronizationContext();
        SynchronizationContext.SetSynchronizationContext(_ui);

        _store = new SettingsStore();
        _settings = _store.Load();
        _typeEngine = new UnicodeTypeEngine();
        _hotkeys = new HotkeyService();
        _hotkeys.HotkeyPressed += OnHotkeyPressed;

        _trayIcon = new NotifyIcon
        {
            Text = "KeyForwarder",
            Icon = AppIcon.Get(),
            Visible = true,
            ContextMenuStrip = BuildMenu()
        };

        _trayIcon.DoubleClick += (_, _) => OpenSettings();

        var ok = ApplyHotkeys(showErrors: true);
        SyncStartupRegistration();
        UpdateTrayTooltip();

        if (ok)
        {
            Balloon($"Ready — press {_settings.TypeHotkey} to type clipboard.");
        }
    }

    private ContextMenuStrip BuildMenu()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("Type clipboard now", null, (_, _) => HandleTypeRequest(fromMenu: true));
        menu.Items.Add("Settings…", null, (_, _) => OpenSettings());
        _enableItem = new ToolStripMenuItem("Enabled", null, (_, _) => ToggleEnabled())
        {
            Checked = _settings.Enabled,
            CheckOnClick = false
        };
        menu.Items.Add(_enableItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => Exit());
        return menu;
    }

    private void ToggleEnabled()
    {
        _settings.Enabled = !_settings.Enabled;
        _store.Save(_settings);
        _enableItem.Checked = _settings.Enabled;
        ApplyHotkeys(showErrors: true);
        UpdateTrayTooltip();
        Balloon(_settings.Enabled ? "Typing hotkey enabled." : "Typing hotkey disabled.");
    }

    private void OpenSettings()
    {
        _hotkeys.UnregisterAll();

        using var form = new SettingsForm(_settings);
        var result = form.ShowDialog();

        if (result != DialogResult.OK)
        {
            ApplyHotkeys(showErrors: false);
            return;
        }

        _settings = form.ResultSettings;
        try
        {
            _store.Save(_settings);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Could not write settings file:\n{_store.FilePath}\n\n{ex.Message}",
                "KeyForwarder",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }

        var registerOk = ApplyHotkeys(showErrors: true);
        SyncStartupRegistration();
        _enableItem.Checked = _settings.Enabled;
        UpdateTrayTooltip();

        if (registerOk)
        {
            Balloon($"Saved — Type: {_settings.TypeHotkey}");
        }
    }

    private bool ApplyHotkeys(bool showErrors)
    {
        _hotkeys.UnregisterAll();
        _hotkeys.UseLowLevelHook = _settings.UseLowLevelHook;

        if (!_settings.Enabled)
        {
            var cancelOnly = _hotkeys.TryRegister(HotkeyId.Cancel, _settings.CancelHotkey, out var cancelErr);
            if (!cancelOnly && showErrors)
            {
                MessageBox.Show(
                    $"Could not register Cancel hotkey ({_settings.CancelHotkey}).\nWin32 error: {cancelErr}",
                    "KeyForwarder",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }

            return cancelOnly;
        }

        var typeOk = _hotkeys.TryRegister(HotkeyId.Type, _settings.TypeHotkey, out var typeErr);
        var cancelOk = _hotkeys.TryRegister(HotkeyId.Cancel, _settings.CancelHotkey, out var cancelErr2);

        if (showErrors && (!typeOk || !cancelOk))
        {
            var parts = new List<string>();
            if (!typeOk)
            {
                parts.Add($"Type ({_settings.TypeHotkey}) — Win32 {typeErr}");
            }

            if (!cancelOk)
            {
                parts.Add($"Cancel ({_settings.CancelHotkey}) — Win32 {cancelErr2}");
            }

            MessageBox.Show(
                "Hotkeys could not be registered (often already in use):\n\n" +
                string.Join("\n", parts) +
                "\n\nPick different hotkeys in Settings, or use tray → Type clipboard now.",
                "KeyForwarder",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }

        if (showErrors && _settings.UseLowLevelHook && !_hotkeys.HookActive)
        {
            Balloon("Keyboard hook unavailable — hotkeys may not work inside remote desktop.");
        }

        return typeOk && cancelOk;
    }

    private void SyncStartupRegistration()
    {
        try
        {
            StartupRegistration.SetEnabled(_settings.StartWithWindows);
        }
        catch
        {
            Balloon("Could not update Start with Windows.");
        }
    }

    /// <summary>
    /// Runs inside the keyboard hook, so it only decides whether to claim the key and hands the
    /// actual work to the UI queue. Leaving <see cref="HotkeyEventArgs.Handled"/> false lets the
    /// keystroke continue to the foreground window — important for Cancel, which is Esc by default.
    /// </summary>
    private void OnHotkeyPressed(object? sender, HotkeyEventArgs e)
    {
        if (e.Id == HotkeyId.Cancel)
        {
            if (!_typeEngine.IsTyping)
            {
                return;
            }

            _typeEngine.Cancel();
            e.Handled = true;
            _ui.Post(_ => Balloon("Typing cancelled."), null);
            return;
        }

        if (e.Id == HotkeyId.Type)
        {
            if (!_settings.Enabled)
            {
                return;
            }

            e.Handled = true;
            _ui.Post(_ => HandleTypeRequest(fromMenu: false), null);
        }
    }

    private void HandleTypeRequest(bool fromMenu)
    {
        if (!_settings.Enabled && !fromMenu)
        {
            return;
        }

        if (_typeEngine.IsTyping)
        {
            Balloon("Already typing…");
            return;
        }

        var text = ClipboardTextReader.TryReadText();
        if (string.IsNullOrEmpty(text))
        {
            Balloon("Clipboard is empty or has no text.");
            return;
        }

        if (_settings.WarnLength > 0 && text.Length > _settings.WarnLength)
        {
            var answer = MessageBox.Show(
                $"Clipboard has {text.Length} characters (warn threshold {_settings.WarnLength}). Type anyway?",
                "KeyForwarder",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);
            if (answer != DialogResult.Yes)
            {
                return;
            }
        }

        if (fromMenu)
        {
            Balloon("Focus the target window… typing in 1.5s");
            var textCopy = text;
            var delay = _settings.DelayMs;
            _ = Task.Run(async () =>
            {
                await Task.Delay(1500).ConfigureAwait(false);
                _ui.Post(_ => StartTyping(textCopy, delay), null);
            });
            return;
        }

        StartTyping(text, _settings.DelayMs);
    }

    private void StartTyping(string text, int delayMs)
    {
        if (!_typeEngine.TryStart(text, delayMs, out var task))
        {
            Balloon("Could not start typing.");
            return;
        }

        Balloon($"Typing {text.Length} characters…");

        _ = task.ContinueWith(t =>
        {
            _ui.Post(_ =>
            {
                if (t.IsCanceled || t.Exception?.GetBaseException() is OperationCanceledException)
                {
                    return;
                }

                if (t.IsFaulted)
                {
                    var msg = t.Exception?.GetBaseException().Message ?? "Unknown error";
                    Balloon("Typing failed: " + Truncate(msg, 80));
                    return;
                }

                Balloon("Done.");
            }, null);
        }, TaskScheduler.Default);
    }

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..max] + "…";

    private void UpdateTrayTooltip()
    {
        var state = _settings.Enabled ? "on" : "off";
        var tip = $"KeyForwarder ({state}) — {_settings.TypeHotkey}";
        _trayIcon.Text = tip.Length <= 63 ? tip : tip[..63];
    }

    private void Balloon(string message)
    {
        try
        {
            _trayIcon.BalloonTipTitle = "KeyForwarder";
            _trayIcon.BalloonTipText = message.Length <= 250 ? message : message[..250];
            _trayIcon.ShowBalloonTip(2500);
        }
        catch
        {
            // ignored
        }
    }

    private void Exit()
    {
        _hotkeys.Dispose();
        _trayIcon.Visible = false;
        _trayIcon.Dispose();
        ExitThread();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _hotkeys.Dispose();
            _trayIcon.Dispose();
        }

        base.Dispose(disposing);
    }
}
