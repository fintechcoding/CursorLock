using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Win32;
using Forms = System.Windows.Forms;
namespace CursorLock;
public partial class App : Application
{
    internal const string MutexName = @"Local\CursorLock.Application";
    private Mutex? instance;
    private bool ownsMutex;
    private EventWaitHandle? showEvent, quitEvent;
    private RegisteredWaitHandle? showWait, quitWait;
    private HotkeyService? hotkeys;
    private Forms.NotifyIcon? tray;
    private Forms.ToolStripMenuItem? toggleItem, startupItem;
    private Icon? unlockedIcon, lockedIcon;
    private MainWindow? settingsWindow;
    private DispatcherTimer? ownershipTimer;
    private bool exiting;
    internal CursorLockService Cursor { get; } = new();
    internal Settings Preferences { get; private set; } = Settings.Load();
    internal bool CanToggle => hotkeys?.IsRegistered == true || Cursor.IsLocked;
    internal event Action? StateChanged;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        bool quit = e.Args.Contains("--quit", StringComparer.OrdinalIgnoreCase);
        instance = new Mutex(false, MutexName);
        try { ownsMutex = instance.WaitOne(0); }
        catch (AbandonedMutexException) { ownsMutex = true; }
        if (!ownsMutex)
        {
            try
            {
                using var signal = EventWaitHandle.OpenExisting(quit ? @"Local\CursorLock.Quit" : @"Local\CursorLock.Show");
                signal.Set();
                if (quit)
                {
                    try { ownsMutex = instance.WaitOne(TimeSpan.FromSeconds(12)); }
                    catch (AbandonedMutexException) { ownsMutex = true; }
                }
            }
            catch (WaitHandleCannotBeOpenedException) { }
            Shutdown(quit && !ownsMutex ? 1 : 0);
            return;
        }
        if (quit) { Shutdown(); return; }
        DispatcherUnhandledException += HandleUnhandledException;
        AppDomain.CurrentDomain.ProcessExit += ProcessExit;
        try
        {
            showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\CursorLock.Show");
            quitEvent = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\CursorLock.Quit");
            showWait = ThreadPool.RegisterWaitForSingleObject(showEvent, (_, _) => Dispatch(ShowSettings), null, Timeout.Infinite, false);
            quitWait = ThreadPool.RegisterWaitForSingleObject(quitEvent, (_, _) => Dispatch(ExitApplication), null, Timeout.Infinite, false);
            hotkeys = new HotkeyService();
            hotkeys.Pressed += ToggleLock;
            hotkeys.DisplayChanged += DisplayChanged;
            Cursor.Changed += UpdateState;
            CreateTray();
            SystemEvents.SessionSwitch += SessionSwitch;
            SystemEvents.PowerModeChanged += PowerChanged;
            ownershipTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(750) };
            ownershipTimer.Tick += (_, _) => Cursor.CheckOwnership();
            ownershipTimer.Start();
            try { hotkeys.Set(Preferences.Hotkey); }
            catch (Win32Exception error)
            {
                ShowSettings();
                settingsWindow!.ShowFeedback(error.Message, true);
            }
            UpdateState();
            if (!e.Args.Contains("--background", StringComparer.OrdinalIgnoreCase)) ShowSettings();
        }
        catch (Exception error)
        {
            Trace.TraceError(error.ToString());
            MessageBox.Show("CursorLock could not start: " + error.Message, "CursorLock", MessageBoxButton.OK, MessageBoxImage.Error);
            ExitApplication();
        }
    }

    private void CreateTray()
    {
        unlockedIcon = TrayIconFactory.Create(false);
        lockedIcon = TrayIconFactory.Create(true);
        var menu = new Forms.ContextMenuStrip { ShowImageMargin = false, BackColor = Color.FromArgb(25, 30, 42), ForeColor = Color.White };
        toggleItem = new Forms.ToolStripMenuItem("Lock", null, (_, _) => ToggleLock());
        menu.Items.Add(toggleItem);
        menu.Items.Add("Settings", null, (_, _) => ShowSettings());
        startupItem = new Forms.ToolStripMenuItem("Start with Windows") { CheckOnClick = false };
        startupItem.Click += (_, _) => SetStartup(!StartupRegistration.IsEnabled);
        menu.Items.Add(startupItem);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => ExitApplication());
        menu.Opening += (_, _) => startupItem.Checked = StartupRegistration.IsEnabled;
        tray = new Forms.NotifyIcon { Icon = unlockedIcon, Text = "CursorLock • Unlocked", ContextMenuStrip = menu, Visible = true };
        tray.DoubleClick += (_, _) => ShowSettings();
    }

    internal void ShowSettings()
    {
        if (exiting) return;
        settingsWindow ??= new MainWindow(this);
        settingsWindow.RefreshState();
        settingsWindow.Show();
        if (settingsWindow.WindowState == WindowState.Minimized) settingsWindow.WindowState = WindowState.Normal;
        settingsWindow.Activate();
    }

    internal void ToggleLock()
    {
        if (!CanToggle) { ShowSettings(); return; }
        try { Cursor.Toggle(); }
        catch (Exception error) { ReportError("Could not change the cursor lock: " + error.Message); }
    }

    internal void SetHotkey(Hotkey proposed)
    {
        var previous = Preferences;
        hotkeys!.Set(proposed);
        try
        {
            var next = new Settings(proposed);
            next.Save();
            Preferences = next;
        }
        catch
        {
            hotkeys.Set(previous.Hotkey);
            throw;
        }
        UpdateState();
    }

    internal void SetStartup(bool enabled)
    {
        try { StartupRegistration.SetEnabled(enabled); UpdateState(); }
        catch (Exception error) { ReportError("Could not change the startup setting: " + error.Message); UpdateState(); }
    }

    private void UpdateState()
    {
        if (tray is not null)
        {
            tray.Icon = Cursor.IsLocked ? lockedIcon : unlockedIcon;
            tray.Text = Cursor.IsLocked ? "CursorLock • Locked" : "CursorLock • Unlocked";
            toggleItem!.Text = Cursor.IsLocked ? "Unlock" : "Lock current monitor";
            toggleItem.Enabled = CanToggle;
            startupItem!.Checked = StartupRegistration.IsEnabled;
        }
        StateChanged?.Invoke();
    }

    private void DisplayChanged() { SafeUnlock(); UpdateState(); }
    private void SessionSwitch(object sender, SessionSwitchEventArgs e)
    {
        if (e.Reason is SessionSwitchReason.SessionLock or SessionSwitchReason.SessionLogoff or SessionSwitchReason.ConsoleDisconnect or SessionSwitchReason.RemoteDisconnect)
            Dispatch(SafeUnlock);
    }
    private void PowerChanged(object sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode is PowerModes.Suspend or PowerModes.Resume) Dispatch(SafeUnlock);
    }
    private void Dispatch(Action action)
    {
        if (!exiting && !Dispatcher.HasShutdownStarted) Dispatcher.BeginInvoke(action);
    }
    private void SafeUnlock()
    {
        try { Cursor.Unlock(); }
        catch (Exception error) { ReportError("Could not release the cursor: " + error.Message); }
    }
    private void ReportError(string message)
    {
        if (exiting) return;
        ShowSettings();
        settingsWindow!.ShowFeedback(message, true);
    }
    private void HandleUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        try { Cursor.Unlock(); } catch { }
        e.Handled = true;
        Trace.TraceError(e.Exception.ToString());
        MessageBox.Show("An unexpected error occurred. CursorLock will close.\n" + e.Exception.Message, "CursorLock", MessageBoxButton.OK, MessageBoxImage.Error);
        ExitApplication();
    }
    private void ProcessExit(object? sender, EventArgs e) { try { Cursor.Unlock(); } catch { } }
    internal void ExitApplication()
    {
        if (exiting) return;
        exiting = true;
        try { Cursor.Unlock(); } catch { }
        Shutdown();
    }
    protected override void OnExit(ExitEventArgs e)
    {
        exiting = true;
        SystemEvents.SessionSwitch -= SessionSwitch;
        SystemEvents.PowerModeChanged -= PowerChanged;
        DispatcherUnhandledException -= HandleUnhandledException;
        AppDomain.CurrentDomain.ProcessExit -= ProcessExit;
        try { Cursor.Unlock(); } catch { }
        ownershipTimer?.Stop();
        showWait?.Unregister(null);
        quitWait?.Unregister(null);
        hotkeys?.Dispose();
        if (tray is not null) { tray.Visible = false; tray.ContextMenuStrip?.Dispose(); tray.Dispose(); }
        unlockedIcon?.Dispose(); lockedIcon?.Dispose();
        showEvent?.Dispose(); quitEvent?.Dispose();
        if (ownsMutex) instance?.ReleaseMutex();
        instance?.Dispose();
        base.OnExit(e);
    }
}
