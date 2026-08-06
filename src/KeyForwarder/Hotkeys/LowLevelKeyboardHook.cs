using System.Runtime.InteropServices;
using KeyForwarder.Native;
using KeyForwarder.Settings;

namespace KeyForwarder.Hotkeys;

/// <summary>
/// Detects hotkeys through a WH_KEYBOARD_LL hook rather than RegisterHotKey.
/// Remote desktop clients install their own low-level hook and forward every keystroke to
/// the remote session, so WM_HOTKEY never arrives while their window has focus. A hook is
/// the only place the combination can still be seen — and swallowed before it is forwarded.
/// </summary>
/// <remarks>
/// Everything here runs on the thread that installed the hook (the UI thread), because the
/// system dispatches low-level hooks through that thread's message queue. The callback must
/// stay short: exceeding <c>LowLevelHooksTimeout</c> makes Windows silently drop the hook.
/// </remarks>
internal sealed class LowLevelKeyboardHook : IDisposable
{
    /// <summary>
    /// Hooks are invoked newest-first, so a client that hooks after us would take our keys.
    /// Re-installing periodically puts us back at the head of the chain.
    /// </summary>
    private const int RearmIntervalMs = 2000;

    /// <summary>
    /// How long a swallowed key stays "held" without a key-up. Longer than the slowest auto-repeat
    /// delay, so a key-up that another hook eats cannot wedge the hotkey permanently.
    /// </summary>
    private const int HeldKeyTimeoutMs = 1500;

    private readonly NativeMethods.LowLevelKeyboardProc _proc;
    private readonly Func<HotkeyId, bool> _onHotkey;
    private readonly Dictionary<HotkeyId, HotkeyBinding> _bindings = new();
    private readonly Dictionary<uint, long> _swallowedAt = new();
    private readonly System.Windows.Forms.Timer _rearmTimer;

    private IntPtr _hook;
    private bool _disposed;

    /// <param name="onHotkey">
    /// Returns true when the app acted on the hotkey, in which case the key is swallowed.
    /// Runs inside the hook callback, so it must not block.
    /// </param>
    public LowLevelKeyboardHook(Func<HotkeyId, bool> onHotkey)
    {
        _onHotkey = onHotkey;
        // Held in a field: the delegate is the unmanaged callback and must outlive the hook.
        _proc = HookProc;
        _rearmTimer = new System.Windows.Forms.Timer { Interval = RearmIntervalMs };
        _rearmTimer.Tick += (_, _) => Rearm();
    }

    public bool IsInstalled => _hook != IntPtr.Zero;

    public bool EnsureInstalled(out int win32Error)
    {
        win32Error = 0;
        if (_hook != IntPtr.Zero)
        {
            return true;
        }

        _hook = InstallHook();
        if (_hook == IntPtr.Zero)
        {
            win32Error = Marshal.GetLastWin32Error();
            return false;
        }

        _rearmTimer.Start();
        return true;
    }

    /// <summary>Assigns or (with a null binding) clears the combination for <paramref name="id"/>.</summary>
    public void SetBinding(HotkeyId id, HotkeyBinding? binding)
    {
        if (binding is null)
        {
            _bindings.Remove(id);
        }
        else
        {
            _bindings[id] = binding;
        }

        if (_bindings.Count == 0)
        {
            Uninstall();
        }
    }

    public void Uninstall()
    {
        _rearmTimer.Stop();
        _swallowedAt.Clear();

        if (_hook == IntPtr.Zero)
        {
            return;
        }

        NativeMethods.UnhookWindowsHookEx(_hook);
        _hook = IntPtr.Zero;
    }

    private IntPtr InstallHook() =>
        NativeMethods.SetWindowsHookEx(
            NativeMethods.WH_KEYBOARD_LL,
            _proc,
            // The callback lives in this process, so the executable's own module handle applies.
            NativeMethods.GetModuleHandle(null),
            0);

    private void Rearm()
    {
        if (_hook == IntPtr.Zero)
        {
            return;
        }

        // Install the replacement first so no keystroke slips through an unhooked gap.
        var replacement = InstallHook();
        if (replacement == IntPtr.Zero)
        {
            return;
        }

        NativeMethods.UnhookWindowsHookEx(_hook);
        _hook = replacement;
    }

    private IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode != NativeMethods.HC_ACTION)
        {
            return NativeMethods.CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
        }

        var data = Marshal.PtrToStructure<NativeMethods.KBDLLHOOKSTRUCT>(lParam);

        // Our own SendInput output travels the same path; it must never re-trigger a hotkey.
        if ((data.flags & NativeMethods.LLKHF_INJECTED) != 0)
        {
            return NativeMethods.CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
        }

        var message = (int)wParam;

        if (message is NativeMethods.WM_KEYUP or NativeMethods.WM_SYSKEYUP
            && _swallowedAt.Remove(data.vkCode))
        {
            // The key-down never reached the foreground window, so its key-up must not either.
            return 1;
        }

        if (message is NativeMethods.WM_KEYDOWN or NativeMethods.WM_SYSKEYDOWN
            && TryMatch(data.vkCode, out var id))
        {
            var now = Environment.TickCount64;
            var repeating = _swallowedAt.TryGetValue(data.vkCode, out var pressedAt)
                            && now - pressedAt < HeldKeyTimeoutMs;

            _swallowedAt[data.vkCode] = now;

            if (repeating)
            {
                return 1;
            }

            if (_onHotkey(id))
            {
                return 1;
            }

            _swallowedAt.Remove(data.vkCode);
        }

        return NativeMethods.CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
    }

    private bool TryMatch(uint vkCode, out HotkeyId id)
    {
        id = default;

        if (IsModifier(vkCode) || _bindings.Count == 0)
        {
            return false;
        }

        var control = NativeMethods.IsKeyDown(NativeMethods.VK_CONTROL);
        var alt = NativeMethods.IsKeyDown(NativeMethods.VK_MENU);
        var shift = NativeMethods.IsKeyDown(NativeMethods.VK_SHIFT);
        var win = NativeMethods.IsKeyDown(NativeMethods.VK_LWIN)
                  || NativeMethods.IsKeyDown(NativeMethods.VK_RWIN);

        foreach (var (candidate, binding) in _bindings)
        {
            if (binding.VirtualKey == (int)vkCode
                && binding.Control == control
                && binding.Alt == alt
                && binding.Shift == shift
                && binding.Win == win)
            {
                id = candidate;
                return true;
            }
        }

        return false;
    }

    private static bool IsModifier(uint vkCode) => (int)vkCode
        is NativeMethods.VK_CONTROL or NativeMethods.VK_LCONTROL or NativeMethods.VK_RCONTROL
        or NativeMethods.VK_SHIFT or NativeMethods.VK_LSHIFT or NativeMethods.VK_RSHIFT
        or NativeMethods.VK_MENU or NativeMethods.VK_LMENU or NativeMethods.VK_RMENU
        or NativeMethods.VK_LWIN or NativeMethods.VK_RWIN;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Uninstall();
        _rearmTimer.Dispose();
    }
}
