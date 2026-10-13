using System;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CursorLock;
internal static class Program
{
    private static int passed, skipped;
    [STAThread]
    private static int Main(string[] args)
    {
        if (Array.IndexOf(args, "--startup-probe") >= 0) return StartupProbe.Run();
        string root = args.Length > 0 ? Path.GetFullPath(args[0]) : Directory.GetCurrentDirectory();
        try
        {
            string settingsDirectory = Path.Combine(root, "artifacts", "test-settings");
            Directory.CreateDirectory(settingsDirectory);
            Check("default shortcut", Hotkey.Default.ToString() == "Ctrl + Alt + L");
            Check("reject modifierless keys", !new Hotkey(0, 0x4C).IsValid);
            Check("reject F12", !new Hotkey(3, 0x7B).IsValid);
            Check("reject Windows modifier", !new Hotkey(8, 0x4C).IsValid);
            Check("allow F11", new Hotkey(6, 0x7A).IsValid);
            File.WriteAllText(Path.Combine(settingsDirectory, "settings.json"), "invalid json");
            Check("corrupt settings fallback", Settings.Load(settingsDirectory).Hotkey == Hotkey.Default);
            var settings = new Settings(new Hotkey(6, 0x4B));
            settings.Save(settingsDirectory);
            Check("settings round trip", Settings.Load(settingsDirectory) == settings);
            Check("atomic write leaves no temporary file", !File.Exists(Path.Combine(settingsDirectory, "settings.json.tmp")));
            File.WriteAllText(Path.Combine(settingsDirectory, "settings.json"), "{\"Hotkey\":{\"Modifiers\":0,\"VirtualKey\":76}}");
            Check("invalid stored shortcut fallback", Settings.Load(settingsDirectory).Hotkey == Hotkey.Default);
            TestNativeLock();
            TestHotkeyConflict();
            var app = new App();
            app.InitializeComponent();
            var window = new MainWindow(app);
            var content = (FrameworkElement)window.Content;
            content.Measure(new Size(620, 750));
            content.Arrange(new Rect(0, 0, 620, 750));
            content.UpdateLayout();
            var preview = new RenderTargetBitmap(620, 750, 96, 96, PixelFormats.Pbgra32);
            preview.Render(content);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(preview));
            Directory.CreateDirectory(Path.Combine(root, "docs"));
            using (var output = File.Create(Path.Combine(root, "docs", "preview.png"))) encoder.Save(output);
            Check("settings window renders", content.ActualWidth == 620);
            using (var icon = TrayIconFactory.Create(false))
            using (var output = File.Create(Path.Combine(root, "src", "CursorLock", "Assets", "CursorLock.ico"))) icon.Save(output);
            Console.WriteLine($"PASS: {passed} checks; SKIP: {skipped} environment-dependent checks.");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
    private static void TestNativeLock()
    {
        if (!NativeMethods.GetClipCursor(out var original) || !NativeMethods.GetCursorPos(out _))
        { Skip("interactive desktop is unavailable for cursor tests"); return; }
        var service = new CursorLockService();
        try
        {
            service.Toggle();
            Check("native cursor lock", service.IsLocked && !string.IsNullOrWhiteSpace(service.Device));
            NativeMethods.GetClipCursor(out var bounds);
            Check("monitor has nonempty physical bounds", bounds.Right > bounds.Left && bounds.Bottom > bounds.Top);
            service.Toggle();
            Check("native cursor unlock", !service.IsLocked && service.Device is null);
            service.Toggle();
            var other = new NativeMethods.Rect(bounds.Left + 1, bounds.Top + 1, bounds.Right - 1, bounds.Bottom - 1);
            if (!NativeMethods.ClipCursor(ref other)) throw new Win32Exception();
            service.Unlock();
            NativeMethods.GetClipCursor(out var preserved);
            Check("do not clear another application's clip", preserved.Left == other.Left && preserved.Right == other.Right);
            service.Toggle();
            if (!NativeMethods.ClipCursor(ref other)) throw new Win32Exception();
            service.CheckOwnership();
            Check("detect external clip replacement", !service.IsLocked);
        }
        catch (Win32Exception error) when (error.NativeErrorCode == 5)
        { Skip("cursor APIs denied by the current desktop"); }
        finally { service.Unlock(); NativeMethods.ClipCursor(ref original); }
    }
    private static void TestHotkeyConflict()
    {
        using var a = new HotkeyService();
        using var b = new HotkeyService();
        var first = new Hotkey(7, 0x78);
        var second = new Hotkey(7, 0x79);
        try { a.Set(first); b.Set(second); }
        catch (Win32Exception) { Skip("native test shortcuts are unavailable"); return; }
        bool conflict = false;
        try { a.Set(second); } catch (Win32Exception) { conflict = true; }
        Check("shortcut conflict is reported", conflict);
        conflict = false;
        try { b.Set(first); } catch (Win32Exception) { conflict = true; }
        Check("old shortcut survives a conflict", conflict && a.IsRegistered);
        a.Set(first);
        Check("same shortcut is an idempotent update", a.IsRegistered);
    }
    private static void Check(string name, bool result)
    {
        if (!result) throw new InvalidOperationException("FAIL: " + name);
        passed++; Console.WriteLine("PASS: " + name);
    }
    private static void Skip(string reason) { skipped++; Console.WriteLine("SKIP: " + reason); }
}

