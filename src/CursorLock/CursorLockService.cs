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
    // True while Ctrl is held and the clip has been temporarily released so the
    // cursor can cross to another monitor (and carry a dragged window with it).
    private bool bypassing;
    public void Toggle() { if (IsLocked) Unlock(); else LockCurrentMonitor(); }
    private void LockCurrentMonitor()
    {
        ApplyLockToCursorMonitor();
        IsLocked = true;
        bypassing = false;
        Changed?.Invoke();
    }
    private void ApplyLockToCursorMonitor()
    {
        if (!NativeMethods.GetCursorPos(out var point)) ThrowNativeError();
        var monitor = NativeMethods.MonitorFromPoint(point, 2);
        var info = new NativeMethods.MonitorInfo { Size = Marshal.SizeOf<NativeMethods.MonitorInfo>(), Device = "" };
        if (monitor == IntPtr.Zero || !NativeMethods.GetMonitorInfo(monitor, ref info)) ThrowNativeError();
        var rect = info.Monitor;
        if (!NativeMethods.ClipCursor(ref rect)) ThrowNativeError();
        lockedBounds = rect;
        Device = info.Device;
    }
    public void Unlock()
    {
        if (!IsLocked) return;
        // Avoid clearing a constraint now owned by another application. While
        // bypassing the clip is already released, so there is nothing to clear.
        if (!bypassing && NativeMethods.GetClipCursor(out var current) && BoundsEqual(current, lockedBounds))
            if (!NativeMethods.ReleaseCursor(IntPtr.Zero)) ThrowNativeError();
        IsLocked = false;
        Device = null;
        bypassing = false;
        Changed?.Invoke();
    }
    // Called frequently while locked. Holding Ctrl lifts the clip so the cursor
    // can move to another monitor; releasing Ctrl re-locks to whichever monitor
    // the cursor now sits on. Otherwise it behaves like CheckOwnership.
    public void Tick(int crossKey)
    {
        if (!IsLocked) return;
        bool held = crossKey != 0 && (NativeMethods.GetAsyncKeyState(crossKey) & 0x8000) != 0;
        if (held)
        {
            if (!bypassing)
            {
                if (NativeMethods.GetClipCursor(out var current) && BoundsEqual(current, lockedBounds))
                    NativeMethods.ReleaseCursor(IntPtr.Zero);
                bypassing = true;
            }
            return;
        }
        if (bypassing)
        {
            bypassing = false;
            var previous = Device;
            try { ApplyLockToCursorMonitor(); }
            catch { IsLocked = false; Device = null; Changed?.Invoke(); return; }
            if (previous != Device) Changed?.Invoke();
            return;
        }
        CheckOwnership();
    }
    public void CheckOwnership()
    {
        if (!IsLocked || bypassing) return;
        if (!NativeMethods.GetClipCursor(out var current) || BoundsEqual(current, lockedBounds)) return;
        // The clip changed under us. Windows resets it to the whole virtual
        // desktop whenever the confine is simply dropped - showing the desktop
        // (Win+D) or a foreground change does this - so reassert our lock rather
        // than giving up. Only yield when another application has set its own
        // clip to a different, specific rect.
        if (BoundsEqual(current, NativeMethods.GetVirtualScreen()) && NativeMethods.ClipCursor(ref lockedBounds)) return;
        IsLocked = false;
        Device = null;
        Changed?.Invoke();
    }
    private static bool BoundsEqual(NativeMethods.Rect a, NativeMethods.Rect b) => a.Left == b.Left && a.Top == b.Top && a.Right == b.Right && a.Bottom == b.Bottom;
    private static void ThrowNativeError() => throw new Win32Exception(Marshal.GetLastWin32Error());
}
