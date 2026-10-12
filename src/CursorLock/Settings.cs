using System;
using System.IO;
using System.Text.Json;
using System.Windows.Input;
using Microsoft.Win32;
namespace CursorLock;
internal sealed record Hotkey(uint Modifiers, uint VirtualKey)
{
    public static Hotkey Default => new(3, 0x4C);
    public bool IsValid => Modifiers is > 0 and <= 7 && (VirtualKey is >= 0x30 and <= 0x39 or >= 0x41 and <= 0x5A or >= 0x70 and <= 0x7A);
    public override string ToString()
    {
        string value = "";
        if ((Modifiers & 2) != 0) value += "Ctrl + ";
        if ((Modifiers & 1) != 0) value += "Alt + ";
        if ((Modifiers & 4) != 0) value += "Shift + ";
        return value + KeyInterop.KeyFromVirtualKey((int)VirtualKey);
    }
}
internal sealed record Settings(Hotkey Hotkey)
{
    internal static readonly string DirectoryPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CursorLock");
    private static string FilePath(string? directory) => Path.Combine(directory ?? DirectoryPath, "settings.json");
    public static Settings Load(string? directory = null)
    {
        try
        {
            var settings = JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath(directory)));
            if (settings?.Hotkey is { IsValid: true }) return settings;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException) { }
        return new Settings(Hotkey.Default);
    }
    public void Save(string? directory = null)
    {
        Directory.CreateDirectory(directory ?? DirectoryPath);
        string target = FilePath(directory);
        string temporary = target + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temporary, target, true);
    }
}
internal static class StartupRegistration
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    public static bool IsEnabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            return key?.GetValue("CursorLock") is string value && !string.IsNullOrWhiteSpace(value);
        }
    }
    public static void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey);
        if (enabled)
        {
            string executable = Environment.ProcessPath ?? throw new InvalidOperationException("The application path could not be found.");
            key.SetValue("CursorLock", "\"" + executable + "\" --background", RegistryValueKind.String);
        }
        else key.DeleteValue("CursorLock", false);
    }
}
