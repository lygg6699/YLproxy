using Microsoft.Win32;

namespace YLproxy.Core.PreFlight;

[System.Runtime.Versioning.SupportedOSPlatform("windows")]
public static class AutoStartService
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string EntryName = "YLproxy";

    public static bool IsAutoStartEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: false);
            return key?.GetValue(EntryName) is not null;
        }
        catch
        {
            return false;
        }
    }

    public static void EnableAutoStart(string executablePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);

        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true)
            ?? Registry.CurrentUser.CreateSubKey(RunKey);

        key.SetValue(EntryName, $"\"{executablePath}\"", RegistryValueKind.String);
    }

    public static void DisableAutoStart()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
            if (key is not null)
            {
                key.DeleteValue(EntryName, throwOnMissingValue: false);
            }
        }
        catch
        {
            // Non-critical: silently ignore if we can't remove
        }
    }

    public static void SetAutoStart(bool enable, string? executablePath = null)
    {
        if (enable)
        {
            var path = executablePath ?? Environment.ProcessPath
                ?? Path.Combine(AppContext.BaseDirectory, "YLproxy.GUI.exe");
            EnableAutoStart(path);
        }
        else
        {
            DisableAutoStart();
        }
    }
}
