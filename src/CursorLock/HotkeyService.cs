using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Interop;
namespace CursorLock;
internal sealed class HotkeyService : IDisposable
{
    private readonly HwndSource source;
    private int activeId;
    private Hotkey? current;
    public bool IsRegistered => activeId != 0;
    public event Action? Pressed;
    public event Action? DisplayChanged;
    public HotkeyService()
    {
        source = new HwndSource(new HwndSourceParameters("CursorLock.Messages") { Width = 0, Height = 0, WindowStyle = 0 });
        source.AddHook(WindowProc);
    }
    public void Set(Hotkey hotkey)
    {
        if (!hotkey.IsValid) throw new ArgumentException("Use Ctrl, Alt or Shift with a letter, number or F1–F11.");
        if (hotkey == current && IsRegistered) return;
        int newId = activeId == 1 ? 2 : 1;
        if (!NativeMethods.RegisterHotKey(source.Handle, newId, hotkey.Modifiers | NativeMethods.ModNoRepeat, hotkey.VirtualKey))
            throw new Win32Exception(Marshal.GetLastWin32Error(), "This shortcut could not be registered. Choose another key combination.");
        if (activeId != 0) NativeMethods.UnregisterHotKey(source.Handle, activeId);
        activeId = newId;
        current = hotkey;
    }
    private IntPtr WindowProc(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message == NativeMethods.WmHotkey && wParam.ToInt32() == activeId) { handled = true; Pressed?.Invoke(); }
        else if (message == NativeMethods.WmDisplayChange) DisplayChanged?.Invoke();
        return IntPtr.Zero;
    }
    public void Dispose()
    {
        if (activeId != 0) NativeMethods.UnregisterHotKey(source.Handle, activeId);
        activeId = 0;
        source.RemoveHook(WindowProc);
        source.Dispose();
    }
}
