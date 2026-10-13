using System;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Forms = System.Windows.Forms;
namespace CursorLock;
public partial class MainWindow : Window
{
    private readonly App app;
    private Hotkey proposed;
    private bool loadingUi;
    internal MainWindow(App app)
    {
        InitializeComponent();
        this.app = app;
        proposed = app.Preferences.Hotkey;
        HotkeyBox.Text = proposed.ToString();
        SelectCrossKey(app.Preferences.CrossKey);
        app.StateChanged += RefreshState;
        using var icon = TrayIconFactory.Create(false);
        Icon = Imaging.CreateBitmapSourceFromHIcon(icon.Handle, Int32Rect.Empty, System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions());
        Icon.Freeze();
        RefreshState();
    }
    internal void RefreshState()
    {
        bool locked = app.Cursor.IsLocked;
        StatusText.Text = locked ? "Locked to monitor" : "Unlocked";
        StatusText.Foreground = new SolidColorBrush(locked ? Color.FromRgb(5, 150, 105) : Color.FromRgb(27, 27, 27));
        string? cross = CrossKeyName(app.Preferences.CrossKey);
        StatusDescription.Text = locked
            ? "Press " + app.Preferences.Hotkey + " to toggle the lock." + (cross is null ? "" : " Hold " + cross + " to cross to another monitor.")
            : "Move to the monitor you want to lock and press " + app.Preferences.Hotkey + " to toggle the lock.";
        ToggleButton.Content = locked ? "Unlock" : "Lock monitor";
        ToggleButton.IsEnabled = app.CanToggle;
        StartupCheck.IsChecked = StartupRegistration.IsEnabled;
        DrawMonitors();
    }
    internal void ShowFeedback(string message, bool error)
    {
        FeedbackText.Text = message;
        FeedbackText.Foreground = new SolidColorBrush(error ? Color.FromRgb(196, 43, 28) : Color.FromRgb(15, 118, 60));
        FeedbackText.Visibility = Visibility.Visible;
    }
    private void CaptureHotkey(object sender, KeyEventArgs e)
    {
        e.Handled = true;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin) return;
        if (key == Key.Escape) { proposed = app.Preferences.Hotkey; HotkeyBox.Text = proposed.ToString(); return; }
        var modifiers = Keyboard.Modifiers;
        if ((modifiers & ModifierKeys.Windows) != 0) { ShowFeedback("Shortcuts containing the Windows key are not supported.", true); return; }
        uint native = 0;
        if ((modifiers & ModifierKeys.Control) != 0) native |= 2;
        if ((modifiers & ModifierKeys.Alt) != 0) native |= 1;
        if ((modifiers & ModifierKeys.Shift) != 0) native |= 4;
        var candidate = new Hotkey(native, (uint)KeyInterop.VirtualKeyFromKey(key));
        if (!candidate.IsValid) { ShowFeedback("Use Ctrl, Alt or Shift with a letter, number or F1–F11.", true); return; }
        proposed = candidate;
        HotkeyBox.Text = proposed.ToString();
        ShowFeedback("Click Save to activate your new shortcut.", false);
    }
    private void SaveHotkeyClick(object sender, RoutedEventArgs e)
    {
        try { app.SetHotkey(proposed); ShowFeedback("Shortcut saved: " + proposed, false); }
        catch (Exception error) { ShowFeedback(error.Message, true); }
    }
    private static string? CrossKeyName(int virtualKey) => virtualKey switch { 0x11 => "Ctrl", 0x12 => "Alt", 0x10 => "Shift", _ => null };
    private void SelectCrossKey(int virtualKey)
    {
        loadingUi = true;
        foreach (ComboBoxItem item in CrossKeyBox.Items)
            if (item.Tag is string tag && int.TryParse(tag, out int value) && value == virtualKey) { CrossKeyBox.SelectedItem = item; break; }
        if (CrossKeyBox.SelectedItem is null) CrossKeyBox.SelectedIndex = 0;
        loadingUi = false;
    }
    private void CrossKeyChanged(object sender, SelectionChangedEventArgs e)
    {
        if (loadingUi || app is null) return;
        if (CrossKeyBox.SelectedItem is ComboBoxItem item && item.Tag is string tag && int.TryParse(tag, out int value))
            app.SetCrossKey(value);
    }
    private void ToggleClick(object sender, RoutedEventArgs e) => app.ToggleLock();
    private void StartupClick(object sender, RoutedEventArgs e) => app.SetStartup(StartupCheck.IsChecked == true);
    private void ExitClick(object sender, RoutedEventArgs e) => app.ExitApplication();
    protected override void OnClosing(CancelEventArgs e)
    {
        if (!Dispatcher.HasShutdownStarted) { e.Cancel = true; Hide(); }
        base.OnClosing(e);
    }
    private void MonitorSizeChanged(object sender, SizeChangedEventArgs e) { if (app is not null) DrawMonitors(); }
    private void DrawMonitors()
    {
        MonitorCanvas.Children.Clear();
        var screens = Forms.Screen.AllScreens;
        if (screens.Length == 0 || MonitorCanvas.ActualWidth < 20) return;
        int left = screens.Min(s => s.Bounds.Left), top = screens.Min(s => s.Bounds.Top);
        int right = screens.Max(s => s.Bounds.Right), bottom = screens.Max(s => s.Bounds.Bottom);
        double scale = Math.Min((MonitorCanvas.ActualWidth - 24) / (right - left), 105.0 / (bottom - top));
        double offsetX = (MonitorCanvas.ActualWidth - (right - left) * scale) / 2;
        double offsetY = (125 - (bottom - top) * scale) / 2;
        for (int i = 0; i < screens.Length; i++)
        {
            var screen = screens[i];
            bool locked = app.Cursor.IsLocked && screen.DeviceName == app.Cursor.Device;
            var label = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            label.Children.Add(new TextBlock { Text = (i + 1).ToString(), FontSize = 20, FontWeight = FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Center });
            label.Children.Add(new TextBlock { Text = locked ? "LOCKED" : screen.Primary ? "PRIMARY" : "MONITOR", FontSize = 9, HorizontalAlignment = HorizontalAlignment.Center, Foreground = new SolidColorBrush(locked ? Color.FromRgb(5, 150, 105) : Color.FromRgb(91, 102, 114)) });
            var border = new Border
            {
                Width = Math.Max(20, screen.Bounds.Width * scale - 5), Height = Math.Max(20, screen.Bounds.Height * scale - 5),
                Background = new SolidColorBrush(locked ? Color.FromRgb(220, 247, 238) : Color.FromRgb(240, 242, 246)),
                BorderBrush = new SolidColorBrush(locked ? Color.FromRgb(5, 150, 105) : Color.FromRgb(200, 205, 213)),
                BorderThickness = new Thickness(locked ? 2 : 1), CornerRadius = new CornerRadius(7), Child = label,
                ToolTip = screen.DeviceName + " • " + screen.Bounds.Width + " × " + screen.Bounds.Height
            };
            Canvas.SetLeft(border, offsetX + (screen.Bounds.Left - left) * scale);
            Canvas.SetTop(border, offsetY + (screen.Bounds.Top - top) * scale);
            MonitorCanvas.Children.Add(border);
        }
    }
}
