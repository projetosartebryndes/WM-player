using System.Runtime.Versioning;
using System.Text;

namespace WMPlayer;

[SupportedOSPlatform("windows")]
internal static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        Application.ThreadException += (_, eventArgs) => HandleFatalError(eventArgs.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, eventArgs) =>
        {
            if (eventArgs.ExceptionObject is Exception exception)
            {
                HandleFatalError(exception);
            }
        };

        try
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            OpenWithRegistry.EnsureRegistered();

            using var mainForm = new MainForm(args.FirstOrDefault());
            Application.Run(mainForm);
        }
        catch (Exception exception)
        {
            HandleFatalError(exception);
        }
    }

    private static void HandleFatalError(Exception exception)
    {
        try
        {
            var logDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "WM-player");

            Directory.CreateDirectory(logDirectory);
            var logPath = Path.Combine(logDirectory, "startup-error.log");

            var content = new StringBuilder()
                .AppendLine($"Data: {DateTime.Now:yyyy-MM-dd HH:mm:ss}")
                .AppendLine(exception.ToString())
                .AppendLine(new string('-', 80))
                .ToString();

            File.AppendAllText(logPath, content);

            MessageBox.Show(
                $"Não foi possível abrir o WM-player.{Environment.NewLine}{Environment.NewLine}" +
                $"Detalhes salvos em:{Environment.NewLine}{logPath}",
                "Erro ao iniciar o WM-player",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        catch
        {
            MessageBox.Show(
                $"Não foi possível abrir o WM-player.{Environment.NewLine}{Environment.NewLine}{exception.Message}",
                "Erro ao iniciar o WM-player",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }
}
