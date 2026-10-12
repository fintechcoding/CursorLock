# Acceptance checks

## Hardware and lifecycle

- Lock and unlock on each of two monitors; verify all edges and access to the taskbar.
- Repeat with a monitor left/above the primary monitor and mixed 100%/150% scaling.
- Hold the shortcut; only one toggle should occur until it is released.
- Change the shortcut and relaunch; the saved shortcut must work.
- Occupy the proposed shortcut in another app; saving must show an English error and preserve the previous shortcut.
- Close Settings and reopen through the tray. Launch a second copy and verify there is only one tray icon.
- While locked, change display layout, disconnect a monitor, lock the session and suspend/resume; the app must return unlocked.
- Exit while locked and verify the cursor is free.

## Installation

- Install under a standard user; verify no elevation prompt and an English setup.
- Verify the default startup option launches quietly at sign-in, with the cursor unlocked.
- Disable startup in Settings, then reinstall; it must remain disabled.
- Upgrade while locked; setup must release/close the old instance and preserve preferences.
- Uninstall while running; remove the process, tray icon, shortcuts and startup value.
- Run the portable package on a machine without .NET installed.

## Distribution

- Verify the repository excludes settings, credentials, compiler downloads and caches.
- Attach the tested installer, portable ZIP and SHA256SUMS.txt to release v1.0.0.
- Executables are unsigned unless a signing certificate is explicitly supplied.
