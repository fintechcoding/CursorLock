using System;
using System.Collections.Generic;
namespace CursorLock;
// Minimizes the application windows on the monitor under the cursor, and
// restores them on the next request - a per-monitor stand-in for Win+D's
// "show desktop" toggle.
internal sealed class DesktopService
{
    private readonly List<IntPtr> minimized = new();
    public void Toggle()
    {
        if (minimized.Count > 0) { Restore(); return; }
        MinimizeCursorMonitor();
    }
    private void Restore()
    {
        foreach (var hwnd in minimized)
            if (NativeMethods.IsWindow(hwnd) && NativeMethods.IsIconic(hwnd))
                NativeMethods.ShowWindow(hwnd, NativeMethods.SwRestore);
        minimized.Clear();
    }
    private void MinimizeCursorMonitor()
    {
        if (!NativeMethods.GetCursorPos(out var point)) return;
        var target = NativeMethods.MonitorFromPoint(point, NativeMethods.MonitorDefaultToNearest);
        if (target == IntPtr.Zero) return;
        var pending = new List<IntPtr>();
        NativeMethods.EnumWindows((hwnd, _) => { if (ShouldMinimize(hwnd, target)) pending.Add(hwnd); return true; }, IntPtr.Zero);
        foreach (var hwnd in pending)
            if (NativeMethods.ShowWindow(hwnd, NativeMethods.SwMinimize)) minimized.Add(hwnd);
    }
    // The alt-tab heuristic: a visible, titled, non-minimized top-level window on
    // the target monitor that the shell does not hide. Tool windows, DWM-cloaked
    // windows (other virtual desktops, suspended UWP) and owned helper windows
    // are skipped so only real application windows are touched.
    private static bool ShouldMinimize(IntPtr hwnd, IntPtr target)
    {
        if (!NativeMethods.IsWindowVisible(hwnd) || NativeMethods.IsIconic(hwnd)) return false;
        if (NativeMethods.MonitorFromWindow(hwnd, NativeMethods.MonitorDefaultToNearest) != target) return false;
        if (NativeMethods.GetWindowTextLength(hwnd) == 0) return false;
        long exStyle = NativeMethods.GetWindowLongPtr(hwnd, NativeMethods.GwlExStyle).ToInt64();
        if ((exStyle & NativeMethods.WsExToolWindow) != 0) return false;
        if (NativeMethods.DwmGetWindowAttribute(hwnd, NativeMethods.DwmwaCloaked, out int cloaked, sizeof(int)) == 0 && cloaked != 0) return false;
        if (NativeMethods.GetWindow(hwnd, NativeMethods.GwOwner) != IntPtr.Zero && (exStyle & NativeMethods.WsExAppWindow) == 0) return false;
        return true;
    }
}
