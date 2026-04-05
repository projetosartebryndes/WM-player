using Microsoft.Win32;

namespace WMPlayer;

public static class OpenWithRegistry
{
    private const string AppName = "WM-player";

    public static void EnsureRegistered()
    {
        try
        {
            var exePath = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(exePath)) return;

            RegisterApplication(exePath);
            RegisterOpenWith(exePath);
        }
        catch
        {
            // Ignora falhas de registro para não bloquear a reprodução.
        }
    }

    private static void RegisterApplication(string exePath)
    {
        using var appKey = Registry.CurrentUser.CreateSubKey($@"Software\\Classes\\Applications\\{Path.GetFileName(exePath)}");
        appKey?.SetValue("FriendlyAppName", AppName);

        using var commandKey = appKey?.CreateSubKey("shell\\open\\command");
        commandKey?.SetValue(string.Empty, $"\"{exePath}\" \"%1\"");

        using var supportedTypes = appKey?.CreateSubKey("SupportedTypes");
        string[] extensions = [".mp4", ".mkv", ".avi", ".mov", ".wmv", ".flv", ".m4v", ".webm", ".ts"];
        foreach (var ext in extensions)
        {
            supportedTypes?.SetValue(ext, string.Empty);
        }
    }

    private static void RegisterOpenWith(string exePath)
    {
        using var shellKey = Registry.CurrentUser.CreateSubKey(@"Software\\Classes\\SystemFileAssociations\\video\\shell\\OpenWithWMPlayer");
        shellKey?.SetValue(string.Empty, $"Abrir com {AppName}");
        shellKey?.SetValue("Icon", exePath);

        using var commandKey = shellKey?.CreateSubKey("command");
        commandKey?.SetValue(string.Empty, $"\"{exePath}\" \"%1\"");
    }
}
