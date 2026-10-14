using Fragmo.Settings;

namespace Fragmo;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        // O mutex é verificado antes de carregar as configurações, então a mensagem usa o idioma do Windows.
        Strings.Apply(UiLanguage.System);

        using var mutex = new Mutex(true, @"Local\Fragmo.SingleInstance", out bool created);
        if (!created)
        {
            MessageBox.Show(Strings.AlreadyRunning, Strings.AppName,
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        ApplicationConfiguration.Initialize();

        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) => ReportError(e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            ReportError(e.ExceptionObject as Exception ?? new Exception(e.ExceptionObject?.ToString()));

        var store = SettingsStore.CreateDefault();
        var settings = store.Load();
        Strings.Apply(settings.Language);

        Application.Run(new TrayContext(store, settings));
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
