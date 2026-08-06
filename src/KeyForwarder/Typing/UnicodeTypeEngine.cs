using System.Runtime.InteropServices;
using KeyForwarder.Native;

namespace KeyForwarder.Typing;

public sealed class UnicodeTypeEngine
{
    private readonly object _gate = new();
    private CancellationTokenSource? _cts;
    private Task? _running;

    public bool IsTyping
    {
        get
        {
            lock (_gate)
            {
                return _running is { IsCompleted: false };
            }
        }
    }

    /// <summary>
    /// Types <paramref name="text"/> into the focused window. No-ops if already typing.
    /// </summary>
    public bool TryStart(string text, int delayMs, out Task typingTask)
    {
        text = TextNormalizer.Normalize(text);
        if (text.Length == 0)
        {
            typingTask = Task.CompletedTask;
            return false;
        }

        lock (_gate)
        {
            if (_running is { IsCompleted: false })
            {
                typingTask = _running;
                return false;
            }

            _cts?.Dispose();
            _cts = new CancellationTokenSource();
            var token = _cts.Token;
            var delay = Math.Clamp(delayMs, 0, 500);
            _running = Task.Run(() => TypeCore(text, delay, token), CancellationToken.None);
            typingTask = _running;
            return true;
        }
    }

    public void Cancel()
    {
        lock (_gate)
        {
            try
            {
                _cts?.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // ignored
            }
        }
    }

    private static readonly (int Vk, bool Extended)[] ModifierKeys =
    {
        (NativeMethods.VK_LSHIFT, false),
        (NativeMethods.VK_RSHIFT, true),
        (NativeMethods.VK_LCONTROL, false),
        (NativeMethods.VK_RCONTROL, true),
        (NativeMethods.VK_LMENU, false),
        (NativeMethods.VK_RMENU, true),
        (NativeMethods.VK_LWIN, true),
        (NativeMethods.VK_RWIN, true)
    };

    private static void TypeCore(string text, int delayMs, CancellationToken token)
    {
        ClearHotkeyModifiers(token);

        var inputSize = NativeMethods.InputSize;
        if (inputSize < 28)
        {
            throw new InvalidOperationException($"INPUT struct size looks wrong: {inputSize}");
        }

        for (var i = 0; i < text.Length; i++)
        {
            token.ThrowIfCancellationRequested();

            var ch = text[i];

            if (ch == '\n')
            {
                SendOrThrow(new[]
                {
                    NativeMethods.CreateVirtualKeyDown((ushort)NativeMethods.VK_RETURN),
                    NativeMethods.CreateVirtualKeyUp((ushort)NativeMethods.VK_RETURN)
                }, inputSize);
            }
            else if (ch == '\t')
            {
                SendOrThrow(new[]
                {
                    NativeMethods.CreateVirtualKeyDown((ushort)NativeMethods.VK_TAB),
                    NativeMethods.CreateVirtualKeyUp((ushort)NativeMethods.VK_TAB)
                }, inputSize);
            }
            else if (char.IsHighSurrogate(ch))
            {
                if (i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
                {
                    SendUnicodePair(ch, text[i + 1], inputSize);
                    i++;
                }
            }
            else if (!char.IsLowSurrogate(ch))
            {
                // Prefer Unicode injection (layout-independent). Works for most local apps
                // and modern RDP; remote clients that strip Unicode still get the attempt.
                SendOrThrow(new[]
                {
                    NativeMethods.CreateUnicodeKeyDown(ch),
                    NativeMethods.CreateUnicodeKeyUp(ch)
                }, inputSize);
            }

            if (delayMs > 0 && token.WaitHandle.WaitOne(delayMs))
            {
                token.ThrowIfCancellationRequested();
            }
        }
    }

    /// <summary>
    /// Waits briefly for the hotkey modifiers to be let go, then forces a key-up for whatever is
    /// still held. A remote session only learns about the modifiers we forward to it, so without
    /// this the injected characters arrive there as Ctrl+/Shift+ shortcuts instead of text.
    /// </summary>
    private static void ClearHotkeyModifiers(CancellationToken token)
    {
        const int WaitForReleaseMs = 800;
        const int PollMs = 20;
        const int SettleMs = 60;

        var deadline = Environment.TickCount64 + WaitForReleaseMs;
        while (Environment.TickCount64 < deadline && AnyModifierDown())
        {
            if (token.WaitHandle.WaitOne(PollMs))
            {
                token.ThrowIfCancellationRequested();
            }
        }

        var stuck = ModifierKeys
            .Where(m => NativeMethods.IsKeyDown(m.Vk))
            .Select(m => NativeMethods.CreateVirtualKeyUp((ushort)m.Vk, m.Extended))
            .ToArray();

        if (stuck.Length > 0)
        {
            SendOrThrow(stuck, NativeMethods.InputSize);
        }

        if (token.WaitHandle.WaitOne(SettleMs))
        {
            token.ThrowIfCancellationRequested();
        }
    }

    private static bool AnyModifierDown() =>
        ModifierKeys.Any(m => NativeMethods.IsKeyDown(m.Vk));

    private static void SendUnicodePair(char high, char low, int inputSize)
    {
        SendOrThrow(new[]
        {
            NativeMethods.CreateUnicodeKeyDown(high),
            NativeMethods.CreateUnicodeKeyUp(high),
            NativeMethods.CreateUnicodeKeyDown(low),
            NativeMethods.CreateUnicodeKeyUp(low)
        }, inputSize);
    }

    private static void SendOrThrow(NativeMethods.INPUT[] inputs, int inputSize)
    {
        var sent = NativeMethods.SendInput((uint)inputs.Length, inputs, inputSize);
        if (sent != inputs.Length)
        {
            var err = Marshal.GetLastWin32Error();
            throw new InvalidOperationException($"SendInput failed (sent {sent}/{inputs.Length}, win32={err}).");
        }
    }
}
