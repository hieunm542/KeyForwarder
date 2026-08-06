# KeyForwarder

Windows tray app that types clipboard text into the focused window using keyboard simulation (`SendInput`). Useful when RDP, Citrix, or VMware sessions block paste from the host machine.

## Requirements

- Windows 10/11 **x64**
- No .NET runtime install needed for the self-contained publish build

## Usage

1. Run `KeyForwarder.exe` (it stays in the system tray).
2. Copy text on the local machine.
3. Focus the remote desktop window and the target text field.
4. Press the **Type** hotkey (default: `Ctrl+Shift+V`).
5. Press **Cancel** (default: `Esc`) to stop typing mid-flight.

Right-click the tray icon for **Settings**, **Enabled**, and **Exit**. Double-click opens Settings.

In Settings, click **Change** next to a hotkey, press the desired combination, then click **Save**.
(While capturing, Esc cancels capture only — it does not close the dialog.)

### Settings

| Setting | Default | Description |
|---------|---------|-------------|
| Type hotkey | `Ctrl+Shift+V` | Types clipboard Unicode text into the focused window |
| Cancel hotkey | `Esc` | Cancels an in-progress typing run |
| Delay (ms) | `15` | Pause between keystrokes (increase if remote drops keys) |
| Warn length | `5000` | Confirm before typing when clipboard exceeds this length |
| Start with Windows | off | Registers under HKCU Run |
| Enabled | on | Disables the Type hotkey when off |
| Work inside remote desktop sessions | on | Detects hotkeys with a keyboard hook instead of `RegisterHotKey` |

## Remote desktop sessions

Remote desktop clients (`mstsc.exe`, Citrix, VMware Horizon) install their own low-level
keyboard hook and forward every keystroke to the remote machine. That hook runs before
Windows evaluates registered hotkeys, so `RegisterHotKey` never fires while the session
window has focus — most visibly in full screen, or with *Apply Windows key combinations*
set to *On the remote computer*.

With **Work inside remote desktop sessions** enabled (the default) KeyForwarder installs its
own `WH_KEYBOARD_LL` hook, swallows the Type hotkey so the remote session never sees it, and
re-installs the hook periodically to stay ahead of the client's hook. Before typing it also
releases any modifier still held down, so the remote side receives plain characters rather
than `Ctrl+`/`Shift+` shortcuts.

Turn the option off to go back to `RegisterHotKey` if another tool conflicts with the hook.

## Project layout

- `src/KeyForwarder` — WinForms tray app (hotkeys, SendInput, UI)
- `src/KeyForwarder.Core` — settings + text normalization (testable without Windows)
- `tests/KeyForwarder.Tests` — unit tests

## Build / publish

Requires the .NET 8+ SDK (on Windows, or any OS with `EnableWindowsTargeting`).

```bash
dotnet test KeyForwarder.sln -c Release
dotnet publish src/KeyForwarder/KeyForwarder.csproj -c Release -r win-x64 --self-contained true -o publish
```

The published `publish/KeyForwarder.exe` is a single self-contained file (native libraries
bundled and compressed) — copy that one file to any Windows x64 machine and run.
No .NET runtime install is required.

## Notes / limitations

- **UAC:** If the remote client window runs elevated (Administrator) and KeyForwarder does not, `SendInput` may not reach that window — and the keyboard hook will not see its keystrokes either. Run KeyForwarder at the same privilege level.
- Typing goes to whichever window currently has focus — keep the remote session focused while typing.
- This does **not** use remote clipboard sync; it only simulates keystrokes on the local machine.
- Outbound copy (remote → local) is not supported in this version.
