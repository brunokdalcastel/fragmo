using System.Diagnostics;
using System.Globalization;
using Fragmo.Hotkeys;
using Fragmo.Settings;
using Fragmo.Tutorial.Editor;
using Fragmo.Tutorial.Recording;
using Fragmo.Ui;

namespace Fragmo.Tutorial;

/// <summary>
/// Liga a bandeja ao gravador e ao editor: começa e para a gravação, mostra a barra, abre o editor ao parar,
/// abre arquivos <c>.fragmotut</c> e cuida das sessões que ficaram sem terminar (processo encerrado no meio).
/// Roda na thread da UI.
/// </summary>
internal sealed class TutorialWorkflow(AppSettings settings, TrayContext tray) : IDisposable
{
    private TutorialRecorder? _recorder;
    private RecordingBar? _bar;
    private bool _finishing;
    private readonly List<TutorialEditorForm> _editors = [];

    /// <summary>Disparado quando a gravação começa ou termina (o menu e o atalho de passo manual acompanham).</summary>
    public event Action<bool>? RecordingChanged;

    /// <summary>Gravando ou ainda terminando (esperando a fila e o OCR).</summary>
    public bool IsRecording => _recorder is not null;

    /// <summary>Há editor de tutorial aberto (o instalador não fecha o app nesse caso).</summary>
    public bool HasOpenEditors => _editors.Count > 0;

    public void ToggleRecording()
    {
        if (_recorder is null)
        {
            Start();
        }
        else
        {
            _ = StopAsync();
        }
    }

    public void ManualStep()
    {
        if (!_finishing)
        {
            _recorder?.ManualStep();
        }
    }

    private void Start()
    {
        RecordingSession session;
        try
        {
            string title = string.Format(Strings.TutorialDefaultTitle,
                DateTime.Now.ToString("g", CultureInfo.CurrentCulture));
            session = RecordingSession.Create(RecordingSession.DefaultRoot, title, DateTimeOffset.Now);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            tray.ShowError(string.Format(Strings.TutorialStartFailed, ex.Message));
            return;
        }

        var recorder = new TutorialRecorder(settings, session, IgnoredShortcuts(settings.Hotkeys));
        if (!recorder.Start())
        {
            recorder.Dispose();
            TryDelete(session);
            tray.ShowError(Strings.TutorialHooksFailed);
            return;
        }

        var bar = new RecordingBar(Theme.Resolve(settings.Theme));
        // Os eventos chegam pela fila da UI e podem vir depois de a barra fechar.
        recorder.StepCountChanged += count =>
        {
            if (_bar == bar)
            {
                bar.SetStepCount(count);
            }
        };
        recorder.Failed += message => OnFailed(recorder, message);
        bar.StopClicked += (_, _) => _ = StopAsync();
        bar.PauseClicked += (_, _) => TogglePause();
        bar.DiscardClicked += (_, _) => _ = DiscardAsync();

        _recorder = recorder;
        _bar = bar;
        _finishing = false;
        bar.Show();
        RecordingChanged?.Invoke(true);
    }

    private void TogglePause()
    {
        if (_recorder is null || _bar is null || _finishing)
        {
            return;
        }
        if (_recorder.IsPaused)
        {
            _recorder.Resume();
        }
        else
        {
            _recorder.Pause();
        }
        _bar.SetPaused(_recorder.IsPaused);
    }

    private async Task StopAsync()
    {
        if (_recorder is not TutorialRecorder recorder || _finishing)
        {
            return;
        }
        _finishing = true;
        _bar?.SetProcessing();

        RecordingSession session;
        try
        {
            session = await recorder.StopAsync();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Finish();
            tray.ShowError(string.Format(Strings.TutorialRecordingFailed, ex.Message));
            return;
        }
        Finish();

        int steps = session.StepCount;
        if (steps == 0)
        {
            TryDelete(session);
            tray.ShowInfo(Strings.TutorialEmpty);
            return;
        }
        OpenEditor(session.Folder, session.Document, currentFile: null);
    }

    /// <summary>Menu "Abrir tutorial…": extrai o pacote numa pasta de trabalho nova e abre o editor.</summary>
    public void OpenTutorial()
    {
        string? file;
        using (var dialog = new OpenFileDialog { Filter = Strings.TutorialFilter, CheckFileExists = true })
        {
            file = dialog.ShowDialog() == DialogResult.OK ? dialog.FileName : null;
        }
        if (file is null)
        {
            return;
        }

        // O mesmo arquivo já aberto: traz o editor para a frente em vez de abrir outra cópia.
        TutorialEditorForm? open = _editors.FirstOrDefault(e =>
            e.CurrentFile is { } current && string.Equals(Path.GetFullPath(current), Path.GetFullPath(file), StringComparison.OrdinalIgnoreCase));
        if (open is not null)
        {
            open.Activate();
            return;
        }

        string folder = RecordingSession.NewFolderPath(RecordingSession.DefaultRoot, DateTimeOffset.Now);
        TutorialDocument doc;
        try
        {
            doc = TutorialSerializer.OpenPackage(file, folder);
        }
        catch (Exception ex) when (ex is TutorialFormatException or IOException or UnauthorizedAccessException)
        {
            TryDeleteFolder(folder);
            MessageBox.Show(string.Format(Strings.TutorialOpenFailed, ex.Message), Strings.AppName,
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }
        OpenEditor(folder, doc, file);
    }

    private void OpenEditor(string folder, TutorialDocument doc, string? currentFile)
    {
        var editor = new TutorialEditorForm(settings, folder, doc, currentFile, tray.ShowInfo);
        editor.FormClosed += (_, _) => _editors.Remove(editor); // não modal: o Close já descarta
        _editors.Add(editor);
        editor.Show();
        editor.Activate();
    }

    private async Task DiscardAsync()
    {
        if (_recorder is not TutorialRecorder recorder || _bar is null || _finishing)
        {
            return;
        }
        int steps = recorder.StepCount;
        if (steps > 0)
        {
            // Sem dono: a barra não pode ser ativada (WS_EX_NOACTIVATE).
            DialogResult answer = MessageBox.Show(string.Format(Strings.RecordingDiscardConfirm, steps), Strings.AppName,
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2,
                MessageBoxOptions.DefaultDesktopOnly);
            if (answer != DialogResult.Yes || _recorder != recorder || _finishing)
            {
                return;
            }
        }
        _finishing = true;
        _bar.SetProcessing();
        await recorder.DiscardAsync();
        Finish();
        tray.ShowInfo(Strings.TutorialDiscarded);
    }

    /// <summary>A sessão sumiu ou o disco encheu: a gravação já parou; guarda o que deu e avisa.</summary>
    private void OnFailed(TutorialRecorder recorder, string message)
    {
        if (_recorder != recorder)
        {
            return;
        }
        tray.ShowError(string.Format(Strings.TutorialRecordingFailed, message));
        if (!_finishing)
        {
            _finishing = true;
            _ = FinishAfterFailureAsync(recorder);
        }
    }

    private async Task FinishAfterFailureAsync(TutorialRecorder recorder)
    {
        try
        {
            await recorder.StopAsync();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Debug.WriteLine($"TutorialWorkflow: {ex.Message}");
        }
        Finish();
    }

    private void Finish()
    {
        _recorder?.Dispose();
        _recorder = null;
        _bar?.Close();
        _bar?.Dispose();
        _bar = null;
        _finishing = false;
        RecordingChanged?.Invoke(false);
    }

    /// <summary>
    /// Na abertura: pergunta o que fazer com cada gravação que ficou sem terminar. No modo <c>--tray</c>
    /// (<paramref name="quiet"/>) só avisa por balão, para não abrir janelas no login.
    /// </summary>
    public void CheckUnfinishedOnStartup(bool quiet)
    {
        IReadOnlyList<string> unfinished;
        try
        {
            unfinished = RecordingSession.FindUnfinished(RecordingSession.DefaultRoot);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Debug.WriteLine($"TutorialWorkflow: {ex.Message}");
            return;
        }
        if (unfinished.Count == 0)
        {
            return;
        }
        if (quiet)
        {
            tray.ShowInfo(Strings.TutorialUnfinishedTray);
            return;
        }

        foreach (string folder in unfinished)
        {
            RecordingSession session;
            try
            {
                session = RecordingSession.Resume(folder);
            }
            catch (TutorialFormatException ex)
            {
                Debug.WriteLine($"TutorialWorkflow: {ex.Message}");
                continue;
            }

            int steps = session.StepCount;
            if (steps == 0)
            {
                TryDelete(session); // nada a recuperar
                continue;
            }

            string when = session.Document.CreatedAt.ToLocalTime().ToString("g", CultureInfo.CurrentCulture);
            DialogResult answer = MessageBox.Show(string.Format(Strings.TutorialUnfinished, when, steps), Strings.AppName,
                MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
            try
            {
                switch (answer)
                {
                    case DialogResult.Yes:
                        OpenEditor(session.Folder, session.Document, currentFile: null);
                        break;
                    case DialogResult.No:
                        session.Delete();
                        break;
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                tray.ShowError(ex.Message);
            }
        }
    }

    /// <summary>Todos os atalhos do Fragmo: o agrupador não os transforma em passo.</summary>
    internal static IReadOnlySet<HotkeyBinding> IgnoredShortcuts(HotkeySettings hotkeys)
    {
        var set = new HashSet<HotkeyBinding>();
        IEnumerable<string> texts = SettingsValidator.HotkeyEntries(hotkeys).Select(e => e.Text)
            .Append(hotkeys.CaptureAreaFallback);
        foreach (string text in texts)
        {
            if (HotkeyBinding.TryParse(text, out HotkeyBinding binding))
            {
                set.Add(binding);
            }
        }
        return set;
    }

    private static void TryDelete(RecordingSession session)
    {
        try
        {
            session.Delete();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Debug.WriteLine($"TutorialWorkflow: {ex.Message}");
        }
    }

    private static void TryDeleteFolder(string folder)
    {
        try
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Debug.WriteLine($"TutorialWorkflow: {ex.Message}");
        }
    }

    /// <summary>
    /// Ao sair do app: tira os ganchos e fecha os editores sem perguntar. Gravação em andamento e editor com
    /// alterações não salvas ficam em disco e são oferecidos na próxima abertura.
    /// </summary>
    public void Dispose()
    {
        foreach (TutorialEditorForm editor in _editors.ToList())
        {
            editor.CloseForExit();
        }
        _recorder?.Dispose();
        _recorder = null;
        _bar?.Close();
        _bar?.Dispose();
        _bar = null;
    }
}
