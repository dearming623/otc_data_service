using Microsoft.Win32;

namespace OtcDataService.Services;

public sealed class StartupService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "OtcDataService";
    public const string MinimizedArgument = "--minimized";

    public bool IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
            var value = key?.GetValue(ValueName) as string;
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var exePath = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(exePath))
            {
                return true;
            }

            return value.Contains(exePath, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    public bool Enable(out string? errorMessage)
    {
        var exePath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(exePath))
        {
            errorMessage = "Unable to determine the application executable path.";
            return false;
        }

        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true)
                ?? Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
            key.SetValue(ValueName, $"\"{exePath}\" {MinimizedArgument}");
            errorMessage = null;
            return true;
        }
        catch (Exception ex)
        {
            errorMessage = $"Failed to enable startup: {ex.Message}";
            return false;
        }
    }

    public bool Disable(out string? errorMessage)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
            key?.DeleteValue(ValueName, throwOnMissingValue: false);
            errorMessage = null;
            return true;
        }
        catch (Exception ex)
        {
            errorMessage = $"Failed to disable startup: {ex.Message}";
            return false;
        }
    }

    public bool Sync(bool shouldRun, out string? errorMessage)
    {
        if (shouldRun)
        {
            if (IsEnabled())
            {
                errorMessage = null;
                return true;
            }

            return Enable(out errorMessage);
        }

        if (!IsEnabled())
        {
            errorMessage = null;
            return true;
        }

        return Disable(out errorMessage);
    }
}
