# CursorLock

A small Windows tray app that keeps your cursor on the monitor you are using.

**Ctrl + Alt + L** locks your cursor to its current monitor. Press it again to unlock. The cursor moves normally inside the monitor, including its taskbar. While locked, hold **Ctrl** to let the cursor cross to another monitor — handy for dragging a window across. Release Ctrl and the lock follows to the monitor the cursor is on.

## Install and use

Run **CursorLock-Setup.exe**. Installation is per user and needs no administrator privileges. Start with Windows is enabled by default and can be turned off in Settings or the tray menu.

For portable use, extract the entire **CursorLock-Portable-win-x64.zip** and run **CursorLock.exe**. Keep all files together. The runtime is included. Portable mode does not enable autostart until you select it; disable autostart before moving or deleting the folder.

- Double-click the tray icon to open Settings.
- To change the shortcut, select its field, press the new combination and click **Save**.
- Use Ctrl, Alt or Shift with a letter, digit or F1–F11. Windows key shortcuts and F12 are excluded.
- Closing Settings leaves the app in the tray. Use **Exit** to release the cursor and quit.
- A second copy opens the existing Settings window.
- Autostart launches quietly, with the cursor unlocked.

The app unlocks when the display layout changes, the session locks/disconnects, or the PC suspends/resumes. If another app replaces the cursor constraint, CursorLock stops reporting its own lock rather than fighting that app. Games can override the constraint.

Target: Windows 10/11 x64. Monitor labels are local to CursorLock and may differ from Windows Settings. All UI and installer text is English.

## Build

Install the **.NET 10 SDK** and **Inno Setup 6.7 or newer** from their official sources.

```powershell
.\scripts\test.ps1
.\scripts\build.ps1
# Portable package only:
.\scripts\build.ps1 -SkipInstaller
# Explicit compiler location:
.\scripts\build.ps1 -InnoCompiler "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
```

Output: **artifacts/CursorLock-Setup.exe**, **artifacts/CursorLock-Portable-win-x64.zip**, and **artifacts/SHA256SUMS.txt**.

The installer closes the app before upgrades/uninstall. Hotkey settings are preserved on upgrade; autostart changes made in the app are preserved on reinstall. Uninstall removes shortcuts and the startup registration. Preferences remain at **%LocalAppData%\CursorLock\settings.json**.

## Implementation

C# / WPF on .NET 10, WinForms NotifyIcon, and Win32 ClipCursor / RegisterHotKey. Per-monitor DPI awareness uses physical monitor bounds. Settings are written atomically. A conflicting new shortcut leaves the previous one active.

See [manual acceptance checks](docs/acceptance.md). The test runner uses isolated settings and restores the original cursor constraint after native tests.
