using System.Runtime.Versioning;

namespace WMPlayer;

[SupportedOSPlatform("windows")]
internal static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        OpenWithRegistry.EnsureRegistered();

        using var mainForm = new MainForm(args.FirstOrDefault());
        Application.Run(mainForm);
    }
}
