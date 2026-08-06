using System.Runtime.InteropServices;
using KeyForwarder.Native;
using KeyForwarder.Settings;

namespace KeyForwarder.Hotkeys;

public enum HotkeyId
{
    Type = 1,
    Cancel = 2
}

public sealed class HotkeyEventArgs : EventArgs
{
    public HotkeyEventArgs(HotkeyId id) => Id = id;

    public HotkeyId Id { get; }

    /// <summary>
    /// Set by the handler when it acted on the hotkey. In hook mode the key is then swallowed
    /// instead of reaching the foreground window; ignored in RegisterHotKey mode.
    /// </summary>
    public bool Handled { get; set; }
}

/// <summary>
/// Global hotkeys, delivered either by a low-level keyboard hook or by RegisterHotKey on a
/// hidden form. Hook mode is the one that survives remote desktop clients, which swallow
/// keystrokes before Windows gets a chance to post WM_HOTKEY.
/// </summary>
public sealed class HotkeyService : IDisposable
{
    private readonly HotkeyHostForm _host;
    private readonly LowLevelKeyboardHook _hook;
    private bool _disposed;

    public event EventHandler<HotkeyEventArgs>? HotkeyPressed;

    public HotkeyService()
    {
        _host = new HotkeyHostForm(id => OnHotkey(id));
        // Create handle immediately so RegisterHotKey has a valid HWND.
        _ = _host.Handle;
        _hook = new LowLevelKeyboardHook(OnHotkey);
    }

    public IntPtr WindowHandle => _host.Handle;

    /// <summary>Prefer the low-level hook. Takes effect on the next registration.</summary>
    public bool UseLowLevelHook { get; set; } = true;

    /// <summary>True once the hook is actually in place (it can fail and fall back).</summary>
    public bool HookActive => _hook.IsInstalled;

    public bool TryRegister(HotkeyId id, HotkeyBinding binding, out int win32Error)
    {
        ArgumentNullException.ThrowIfNull(binding);
        Unregister(id);

        if (UseLowLevelHook && _hook.EnsureInstalled(out win32Error))
        {
            _hook.SetBinding(id, binding);
            return true;
        }

        var mods = ToModifiers(binding) | NativeMethods.MOD_NOREPEAT;
        var ok = NativeMethods.RegisterHotKey(_host.Handle, (int)id, mods, (uint)binding.VirtualKey);
        win32Error = ok ? 0 : Marshal.GetLastWin32Error();
        return ok;
    }

    public bool TryRegister(HotkeyId id, HotkeyBinding binding) =>
        TryRegister(id, binding, out _);

    public void Unregister(HotkeyId id)
    {
        _hook.SetBinding(id, null);

        if (_host.IsHandleCreated)
        {
            NativeMethods.UnregisterHotKey(_host.Handle, (int)id);
        }
    }

    public void UnregisterAll()
    {
        Unregister(HotkeyId.Type);
        Unregister(HotkeyId.Cancel);
    }

    private bool OnHotkey(HotkeyId id)
    {
        var args = new HotkeyEventArgs(id);
        HotkeyPressed?.Invoke(this, args);
        return args.Handled;
    }

    private static uint ToModifiers(HotkeyBinding b)
    {
        uint mods = 0;
        if (b.Control) mods |= NativeMethods.MOD_CONTROL;
        if (b.Alt) mods |= NativeMethods.MOD_ALT;
        if (b.Shift) mods |= NativeMethods.MOD_SHIFT;
        if (b.Win) mods |= NativeMethods.MOD_WIN;
        return mods;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        UnregisterAll();
        _hook.Dispose();
        _host.Close();
        _host.Dispose();
    }

    private sealed class HotkeyHostForm : Form
    {
        private readonly Action<HotkeyId> _onHotkey;

        public HotkeyHostForm(Action<HotkeyId> onHotkey)
        {
            _onHotkey = onHotkey;
            Text = "KeyForwarderHotkeys";
            ShowInTaskbar = false;
            FormBorderStyle = FormBorderStyle.None;
            Opacity = 0;
            Width = 0;
            Height = 0;
            StartPosition = FormStartPosition.Manual;
            Location = new Point(-10000, -10000);
            // Do not Show() — handle is created via Handle getter; keeps a real HWND on the UI thread.
        }

        protected override CreateParams CreateParams
        {
            get
            {
                const int WS_EX_TOOLWINDOW = 0x00000080;
                var cp = base.CreateParams;
                cp.ExStyle |= WS_EX_TOOLWINDOW;
                return cp;
            }
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == NativeMethods.WM_HOTKEY)
            {
                _onHotkey((HotkeyId)m.WParam.ToInt32());
                return;
            }

            base.WndProc(ref m);
        }

        protected override void SetVisibleCore(bool value)
        {
            // Never become visible; still allow handle creation.
            base.SetVisibleCore(false);
        }
    }
}
