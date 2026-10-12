using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
namespace CursorLock;
internal sealed class CursorLockService
{
    public bool IsLocked { get; private set; }
    public string? Device { get; private set; }
    public event Action? Changed;
    private NativeMethods.Rect lockedBounds;
    public void Toggle() { if (IsLocked) Unlock(); else LockCurrentMonitor(); }
    private void LockCurrentMonitor()
    {
        if (!NativeMethods.GetCursorPos(out var point)) ThrowNativeError();
        var monitor = NativeMethods.MonitorFromPoint(point, 2);
        var info = new NativeMethods.MonitorInfo { Size = Marshal.SizeOf<NativeMethods.MonitorInfo>(), Device = "" };
        if (monitor == IntPtr.Zero || !NativeMethods.GetMonitorInfo(monitor, ref info)) ThrowNativeError();
        var rect = info.Monitor;
        if (!NativeMethods.ClipCursor(ref rect)) ThrowNativeError();
        lockedBounds = rect;
        IsLocked = true;
        Device = info.Device;
        Changed?.Invoke();
    }
    public void Unlock()
    {
        if (!IsLocked) return;
        // Avoid clearing a constraint now owned by another application.
        if (NativeMethods.GetClipCursor(out var current) && BoundsEqual(current, lockedBounds))
            if (!NativeMethods.ReleaseCursor(IntPtr.Zero)) ThrowNativeError();
        IsLocked = false;
        Device = null;
        Changed?.Invoke();
    }
    public void CheckOwnership()
    {
        if (IsLocked && NativeMethods.GetClipCursor(out var current) && !BoundsEqual(current, lockedBounds))
        {
            IsLocked = false;
            Device = null;
            Changed?.Invoke();
        }
    }
    private static bool BoundsEqual(NativeMethods.Rect a, NativeMethods.Rect b) => a.Left == b.Left && a.Top == b.Top && a.Right == b.Right && a.Bottom == b.Bottom;
    private static void ThrowNativeError() => throw new Win32Exception(Marshal.GetLastWin32Error());
}
