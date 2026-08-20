using Microsoft.Win32;

namespace NovaConnect.App;

internal static class StartupRegistration
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "NovaConnect";

    internal static bool IsEnabled()
    {
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
        string? executablePath = Environment.ProcessPath;
        return executablePath is not null
            && IsExpectedCommand(key?.GetValue(ValueName) as string, executablePath);
    }

    internal static bool IsExpectedCommand(string? command, string executablePath) =>
        string.Equals(
            command,
            $"\"{executablePath}\" --background",
            StringComparison.OrdinalIgnoreCase);

    internal static void SetEnabled(bool enabled)
    {
        using RegistryKey key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
        if (enabled)
        {
            string executablePath = Environment.ProcessPath
                ?? throw new InvalidOperationException("Cannot determine the NovaConnect executable path.");
            key.SetValue(ValueName, $"\"{executablePath}\" --background");
        }
        else
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
        }
    }
}
