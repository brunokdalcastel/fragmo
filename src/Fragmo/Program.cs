using Fragmo.Settings;
using Fragmo.Ui;

namespace Fragmo;

internal static class Program
{
    private static readonly int[] IconSizes = [16, 24, 32, 48, 256];

    /// <summary>Instância única; o instalador também olha este nome para saber se o Fragmo está aberto.</summary>
    public const string InstanceMutexName = @"Local\Fragmo.SingleInstance";

    [STAThread]
    private static int Main(string[] args)
    {
        // --export-icon <caminho.ico>: grava o ícone do EXE e sai (não precisa de instância única).
        int exportAt = Array.IndexOf(args, "--export-icon");
        if (exportAt >= 0)
        {
            if (exportAt + 1 >= args.Length)
            {
                return 2;
            }
            string target = Path.GetFullPath(args[exportAt + 1]);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            IconFactory.SaveIco(target, IconSizes);
            return 0;
        }

        // --tray: modo do "iniciar com o Windows", abre direto na bandeja sem nenhuma mensagem.
        bool quiet = args.Contains("--tray");

        // O mutex é verificado antes de carregar as configurações, então a mensagem usa o idioma do Windows.
        Strings.Apply(UiLanguage.System);

        using var mutex = new Mutex(true, InstanceMutexName, out bool created);
        if (!created)
        {
            if (!quiet)
            {
                MessageBox.Show(Strings.AlreadyRunning, Strings.AppName,
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            return 0;
        }

        ApplicationConfiguration.Initialize();

        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) => ReportError(e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            ReportError(e.ExceptionObject as Exception ?? new Exception(e.ExceptionObject?.ToString()));

        var store = SettingsStore.CreateDefault();
        var settings = store.Load();
        Strings.Apply(settings.Language);

        Application.Run(new TrayContext(store, settings, quiet));
        return 0;
    }

    private static void ReportError(Exception ex)
    {
        string logPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Fragmo", "error.log");
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
            File.AppendAllText(logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {ex}{Environment.NewLine}{Environment.NewLine}");
        }
        catch (IOException)
        {
            // Sem log possível: ainda mostramos a mensagem.
        }
        catch (UnauthorizedAccessException)
        {
        }

        MessageBox.Show(string.Format(Strings.UnexpectedError, logPath, ex.Message), Strings.AppName,
            MessageBoxButtons.OK, MessageBoxIcon.Error);
    }
}
