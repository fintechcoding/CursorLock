using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
namespace CursorLock;
// Intercepts Win+D with a low-level keyboard hook so we can replace the
// system-wide "show desktop" with a per-monitor minimize. Windows reserves
// Win-key combinations, so RegisterHotKey cannot claim Win+D; a WH_KEYBOARD_LL
// hook sees the keystroke before the shell and can suppress it. Install and
// Uninstall run on the UI thread, which owns the message loop the hook needs.
internal sealed class ShowDesktopHook : IDisposable
{
    private readonly NativeMethods.LowLevelKeyboardProc proc;
    private IntPtr hook;
    private bool dDown;
    public event Action? Triggered;
    public bool IsInstalled => hook != IntPtr.Zero;
    public ShowDesktopHook() => proc = HookProc;
    public void Install()
    {
        if (hook != IntPtr.Zero) return;
        using var process = Process.GetCurrentProcess();
        using var module = process.MainModule;
        hook = NativeMethods.SetWindowsHookEx(NativeMethods.WhKeyboardLl, proc, NativeMethods.GetModuleHandle(module?.ModuleName), 0);
        if (hook == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
    }
    public void Uninstall()
    {
        if (hook == IntPtr.Zero) return;
        NativeMethods.UnhookWindowsHookEx(hook);
        hook = IntPtr.Zero;
        dDown = false;
    }
    private IntPtr HookProc(int code, IntPtr wParam, IntPtr lParam)
    {
        if (code >= 0)
        {
            var data = Marshal.PtrToStructure<NativeMethods.KbdLlHookStruct>(lParam);
            // Act on the D key. The masking keystroke below is Ctrl, so it never
            // re-enters here as a match - no need to filter injected input, which
            // also lets on-screen keyboards and remote sessions trigger it.
            if (data.VkCode == NativeMethods.VkD)
            {
                int message = wParam.ToInt32();
                bool winHeld = (NativeMethods.GetAsyncKeyState(NativeMethods.VkLWin) & 0x8000) != 0
                            || (NativeMethods.GetAsyncKeyState(NativeMethods.VkRWin) & 0x8000) != 0;
                if (winHeld)
                {
                    if (message is NativeMethods.WmKeyDown or NativeMethods.WmSysKeyDown)
                    {
                        if (!dDown)
                        {
                            dDown = true;
                            MaskWindowsKey();
                            try { Triggered?.Invoke(); } catch { }
                        }
                        return 1; // Suppress: do not let the shell show the desktop.
                    }
                    if (message is NativeMethods.WmKeyUp or NativeMethods.WmSysKeyUp)
                    {
                        dDown = false;
                        return 1;
                    }
                }
                else if (message is NativeMethods.WmKeyUp or NativeMethods.WmSysKeyUp)
                    dDown = false; // Win released first; let a plain D through.
            }
        }
        return NativeMethods.CallNextHookEx(IntPtr.Zero, code, wParam, lParam);
    }
    // Suppressing D removes the only key activity between pressing and releasing
    // the Windows key, which would otherwise make the shell open the Start menu.
    // A throwaway Ctrl tap counts as activity and prevents that. It is ignored on
    // re-entry because SendInput marks it injected.
    private static void MaskWindowsKey()
    {
        var inputs = new[] { NativeMethods.Input.Key(NativeMethods.VkControl, false), NativeMethods.Input.Key(NativeMethods.VkControl, true) };
        NativeMethods.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<NativeMethods.Input>());
    }
    public void Dispose() => Uninstall();
}
