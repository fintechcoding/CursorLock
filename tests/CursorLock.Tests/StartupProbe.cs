using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using CursorLock;
internal static class StartupProbe
{
    internal static int Run()
    {
        Trace.Listeners.Add(new TextWriterTraceListener(Console.Error));
        Trace.AutoFlush = true;
        using var watchdog = new Timer(_ => { Console.Error.WriteLine("FAIL: application startup timed out."); Environment.Exit(2); }, null, 12000, Timeout.Infinite);
        var app = new App();
        app.InitializeComponent();
        app.Startup += (_, _) => app.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
        {
            try
            {
                if (!app.CanToggle) throw new InvalidOperationException("Global shortcut was not registered.");
                Console.WriteLine("PASS: complete tray and hotkey startup");
                app.ShowSettings();
                var window = app.Windows.OfType<MainWindow>().Single();
                if (!window.IsVisible) throw new InvalidOperationException("Settings did not open.");
                Console.WriteLine("PASS: settings open after startup");
                window.Close();
                if (window.IsVisible) throw new InvalidOperationException("Closing settings did not hide the window.");
                Console.WriteLine("PASS: closing settings keeps the tray app alive");
                app.ShowSettings();
                if (!window.IsVisible) throw new InvalidOperationException("Settings did not reopen.");
                Console.WriteLine("PASS: settings reopen after closing");
                app.ExitApplication();
            }
            catch (Exception error) { Console.Error.WriteLine(error); app.Shutdown(1); }
        }));
        return app.Run();
    }
}
