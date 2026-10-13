# CursorLock 1.0.2

- Optional per-monitor Win+D: turn it on in Settings and Win+D minimizes only the application windows on the monitor your cursor is on, press again to bring them back, instead of showing the whole desktop. Off by default.

## 1.0.1

- Keep the lock through Win+D (Show desktop) and other foreground changes. Windows resets the cursor confine to the whole desktop in those cases; CursorLock now reasserts the lock instead of dropping it, so you no longer have to press the shortcut again. It still yields when another application sets its own cursor confine.

## 1.0.0

- Lock the cursor to the current monitor with Ctrl + Alt + L; press again to unlock.
- Configurable global shortcut with conflict handling.
- Hold your cross-monitor key (Ctrl by default; configurable) while locked to cross to another monitor and carry a window across.
- English light settings window with a live monitor layout.
- Tray controls and optional Windows autostart.
- Per-user installer and portable Windows x64 package with the runtime included.
- Unlock on display changes, session lock/disconnect and suspend/resume.

Download CursorLock-Setup.exe for installation, or extract CursorLock-Portable-win-x64.zip for portable use.
