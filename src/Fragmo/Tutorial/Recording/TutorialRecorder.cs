using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Channels;
using Fragmo.Capture;
using Fragmo.Hotkeys;
using Fragmo.Native;
using Fragmo.Ocr;
using Fragmo.Settings;
using Fragmo.Tutorial.Capture;
using Fragmo.Tutorial.Input;
using Fragmo.Tutorial.Text;

namespace Fragmo.Tutorial.Recording;

/// <summary>
/// Junta ganchos, agrupador, UI Automation e sessão em disco. O callback dos ganchos só enfileira; captura,
/// UIA, disco e OCR rodam em tarefas próprias, nunca na thread da UI nem na do gancho. Os eventos
/// para a UI passam pelo <see cref="SynchronizationContext"/> de quem criou o gravador.
/// </summary>
internal sealed class TutorialRecorder : IDisposable
{
    private const int TickMs = 250;
    private const int OcrPadding = 3;

    private abstract record Work;
    private sealed record InputWork(RawInputEvent Event) : Work;
    private sealed record TickWork : Work;
    private sealed record FlushWork : Work;
    private sealed record ManualWork : Work;

    private sealed record OcrWork(string ItemId, Bitmap Image);

    /// <summary>Contexto guardado quando a digitação começa.</summary>
    private sealed record TypingContext(ElementInfo? Focused, IntPtr Window);

    private readonly AppSettings _settings;
    private readonly RecordingSession _session;
    private readonly SynchronizationContext _ui;
    private readonly InputHookThread _hooks;
    private readonly ElementInspector _inspector = new();
    private readonly InputEventGrouper _grouper;
    private readonly Channel<Work> _work = Channel.CreateUnbounded<Work>(new UnboundedChannelOptions { SingleReader = true });
    private readonly Channel<OcrWork> _ocr = Channel.CreateUnbounded<OcrWork>(new UnboundedChannelOptions { SingleReader = true });
    private readonly System.Threading.Timer _tick;
    private Task? _worker;
    private Task? _ocrWorker;

    private volatile bool _paused;
    private volatile bool _stopped;
    private int _pendingOcr;
    private volatile bool _failed;
    private bool _disposed;

    // Só a thread de trabalho mexe nestes.
    private TypingContext? _typing;
    private string? _lastWindowTitle;
    private LastClick? _lastClick;

    /// <summary>O que o duplo clique precisa para reescrever o texto do último clique.</summary>
    private sealed record LastClick(string ItemId, string? Name, string? Title, bool MentionWindow);

    public event Action<int>? StepCountChanged;
    public event Action<string>? Failed;

    public TutorialRecorder(AppSettings settings, RecordingSession session, IReadOnlySet<HotkeyBinding> ignoredShortcuts)
    {
        _settings = settings;
        _session = session;
        _ui = SynchronizationContext.Current ?? new SynchronizationContext();
        _grouper = new InputEventGrouper(
            (uint)SystemInformation.DoubleClickTime, SystemInformation.DoubleClickSize, ignoredShortcuts);
        _hooks = new InputHookThread(OnRawInput);
        _tick = new System.Threading.Timer(_ => _work.Writer.TryWrite(new TickWork()));
    }

    public RecordingSession Session => _session;

    public int StepCount => _session.StepCount;

    public bool IsPaused => _paused;

    /// <summary>OCRs de tarja ainda na fila.</summary>
    public int PendingOcr => Volatile.Read(ref _pendingOcr);

    /// <summary>Liga os ganchos. Devolve <c>false</c> se o Windows recusou (aí nada fica rodando).</summary>
    public bool Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _worker = Task.Run(RunWorkerAsync);
        _ocrWorker = Task.Run(RunOcrAsync);
        _hooks.Start();
        if (!_hooks.HooksInstalled)
        {
            Shutdown();
            return false;
        }
        _tick.Change(TickMs, TickMs);
        return true;
    }

    /// <summary>Os ganchos continuam instalados (D11); os eventos só são ignorados.</summary>
    public void Pause()
    {
        if (_paused)
        {
            return;
        }
        _paused = true;
        _work.Writer.TryWrite(new FlushWork()); // fecha a digitação aberta antes da pausa
    }

    public void Resume() => _paused = false;

    /// <summary>Passo manual (atalho): print da janela em primeiro plano.</summary>
    public void ManualStep()
    {
        if (!_paused && !_stopped)
        {
            _work.Writer.TryWrite(new ManualWork());
        }
    }

    /// <summary>Para os ganchos, fecha o que estava aberto, espera a fila e o OCR e marca a sessão como terminada.</summary>
    public async Task<RecordingSession> StopAsync()
    {
        await ShutdownAndWaitAsync(flush: true).ConfigureAwait(false);
        if (!_failed)
        {
            _session.MarkFinished();
        }
        return _session;
    }

    /// <summary>Para tudo e apaga a pasta da sessão.</summary>
    public async Task DiscardAsync()
    {
        await ShutdownAndWaitAsync(flush: false).ConfigureAwait(false);
        try
        {
            _session.Delete();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Debug.WriteLine($"TutorialRecorder: falha ao apagar a sessão: {ex.Message}");
        }
    }

    private async Task ShutdownAndWaitAsync(bool flush)
    {
        bool wasStopped = _stopped;
        Shutdown();
        if (!wasStopped && flush)
        {
            _work.Writer.TryWrite(new FlushWork());
        }
        _work.Writer.TryComplete();
        if (_worker is not null)
        {
            await _worker.ConfigureAwait(false);
        }
        _ocr.Writer.TryComplete();
        if (_ocrWorker is not null)
        {
            await _ocrWorker.ConfigureAwait(false);
        }
    }

    /// <summary>Tira os ganchos e o timer; a fila ainda pode ser esvaziada depois.</summary>
    private void Shutdown()
    {
        _stopped = true;
        if (!_disposed)
        {
            _tick.Change(Timeout.Infinite, Timeout.Infinite);
        }
        _hooks.Dispose();
    }

    // ---- thread do gancho: só enfileirar ----

    private void OnRawInput(RawInputEvent e)
    {
        if (!_paused && !_stopped)
        {
            _work.Writer.TryWrite(new InputWork(e));
        }
    }

    // ---- thread de trabalho ----

    private async Task RunWorkerAsync()
    {
        await foreach (Work work in _work.Reader.ReadAllAsync().ConfigureAwait(false))
        {
            if (_failed)
            {
                continue; // esvazia a fila sem fazer nada
            }
            try
            {
                await HandleAsync(work).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Fail(ex.Message);
            }
            catch (Exception ex) when (ex is Win32Exception or ExternalException or ArgumentException)
            {
                // Tela bloqueada, UAC ou janela que sumiu no meio: perde-se este passo, a gravação segue.
                Debug.WriteLine($"TutorialRecorder: passo ignorado ({ex.GetType().Name}: {ex.Message})");
            }
        }
    }

    private async Task HandleAsync(Work work)
    {
        switch (work)
        {
            case InputWork { Event.Kind: RawInputKind.LeftDown or RawInputKind.RightDown } input:
                await HandleClickAsync(input.Event).ConfigureAwait(false);
                break;
            case InputWork input:
                if (WindowLocator.IsOwnWindow(User32.GetForegroundWindow()))
                {
                    break; // tecla digitada numa janela do Fragmo (D10)
                }
                await ProcessAsync(_grouper.Push(input.Event), clickShot: null).ConfigureAwait(false);
                break;
            case TickWork:
                await ProcessAsync(_grouper.Tick((uint)Environment.TickCount), clickShot: null).ConfigureAwait(false);
                break;
            case FlushWork:
                await ProcessAsync(_grouper.Flush(), clickShot: null).ConfigureAwait(false);
                break;
            case ManualWork:
                // Fecha a digitação aberta antes, para manter a ordem dos passos.
                await ProcessAsync(_grouper.Flush(), clickShot: null).ConfigureAwait(false);
                AddForegroundStep(StepAction.Manual, element: null, shortcut: null);
                break;
        }
    }

    /// <summary>Print do clique feito no momento em que o evento chega (D6), antes de qualquer outra coisa.</summary>
    private sealed record ClickShot(Bitmap Image, Rectangle Bounds, string WindowTitle) : IDisposable
    {
        public void Dispose() => Image.Dispose();
    }

    private async Task HandleClickAsync(RawInputEvent e)
    {
        Rectangle bounds = WindowLocator.GetCaptureBounds(e.ScreenPoint, out string title, out bool own);
        if (own)
        {
            return; // clique na barra, no editor ou nas configurações (D10): nem o agrupador vê
        }

        using var shot = new ClickShot(ScreenCapturer.CaptureRect(bounds), bounds, title);
        await ProcessAsync(_grouper.Push(e), shot).ConfigureAwait(false);
    }

    private async Task ProcessAsync(IReadOnlyList<GroupedAction> actions, ClickShot? clickShot)
    {
        foreach (GroupedAction action in actions)
        {
            switch (action.Kind)
            {
                case GroupedKind.NewClick:
                case GroupedKind.NewRightClick:
                    if (clickShot is not null)
                    {
                        StepAction kind = action.Kind == GroupedKind.NewClick ? StepAction.Click : StepAction.RightClick;
                        await AddClickStepAsync(kind, action.Source.ScreenPoint, clickShot).ConfigureAwait(false);
                    }
                    break;

                case GroupedKind.PromoteLastToDoubleClick:
                    PromoteLastToDoubleClick();
                    break;

                case GroupedKind.TypingStarted:
                    IntPtr window = User32.GetForegroundWindow();
                    ElementInfo? focused = await _inspector.FocusedAsync().ConfigureAwait(false);
                    _typing = new TypingContext(focused, window);
                    break;

                case GroupedKind.TypingFinished:
                    TypingContext? typing = _typing;
                    _typing = null;
                    StepAction typeAction = typing?.Focused?.IsPassword == true ? StepAction.TypePassword : StepAction.Type;
                    AddForegroundStep(typeAction, typing?.Focused, shortcut: null);
                    break;

                case GroupedKind.Shortcut:
                    AddForegroundStep(StepAction.Shortcut, element: null, action.Shortcut?.ToString());
                    break;
            }
        }
    }

    private async Task AddClickStepAsync(StepAction action, Point screenPoint, ClickShot shot)
    {
        ElementInfo? element = await _inspector.FromPointAsync(screenPoint).ConfigureAwait(false);
        Rectangle imageArea = new(Point.Empty, shot.Bounds.Size);
        Point click = new(screenPoint.X - shot.Bounds.X, screenPoint.Y - shot.Bounds.Y);

        bool mention = WindowChanged(shot.WindowTitle);
        (string? name, string? title) = Masked(element?.Name, shot.WindowTitle);
        var item = new TutorialItem
        {
            Id = TutorialItem.NewId(),
            Kind = TutorialItemKind.Step,
            Action = action,
            Text = Generate(action, element, name, title, shortcut: null, mention),
            Highlight = ToImageRect(element, shot.Bounds),
            ClickPoint = imageArea.Contains(click) ? new PointDto(click.X, click.Y) : null,
        };
        _lastClick = null;
        SaveStep(new Bitmap(shot.Image), item);
        if (action == StepAction.Click)
        {
            _lastClick = new LastClick(item.Id, name, title, mention);
        }
    }

    private void AddForegroundStep(StepAction action, ElementInfo? element, string? shortcut)
    {
        Rectangle bounds = WindowLocator.GetForegroundCaptureBounds(out string title, out bool own);
        if (own)
        {
            return;
        }
        Bitmap image = ScreenCapturer.CaptureRect(bounds);
        var item = new TutorialItem
        {
            Id = TutorialItem.NewId(),
            Kind = TutorialItemKind.Step,
            Action = action,
            Text = BuildText(action, element, title, shortcut),
            Highlight = ToImageRect(element, bounds),
        };
        _lastClick = null;
        SaveStep(image, item);
    }

    private void PromoteLastToDoubleClick()
    {
        // Só se o 1º clique virou mesmo o último passo (não falhou nem foi seguido de outro).
        if (_lastClick is not LastClick last || _session.LastStep?.Id != last.ItemId)
        {
            return;
        }
        _lastClick = null;
        string text = StepTextGenerator.Generate(new StepTextInput(
            StepAction.DoubleClick, ElementKind.Other, last.Name, last.Title, null, null, last.MentionWindow, Strings.Current));
        _session.UpdateItem(last.ItemId, i => i with { Action = StepAction.DoubleClick, Text = text });
    }

    /// <summary>Grava o passo (fica com a imagem) e manda o OCR de tarjas para a fila, se ligado.</summary>
    private void SaveStep(Bitmap image, TutorialItem item)
    {
        TutorialItem saved;
        try
        {
            saved = _session.AddStep(image, item);
        }
        catch
        {
            image.Dispose();
            throw;
        }

        Post(() => StepCountChanged?.Invoke(_session.StepCount));

        if (_settings.RedactTutorials)
        {
            Interlocked.Increment(ref _pendingOcr);
            if (!_ocr.Writer.TryWrite(new OcrWork(saved.Id, image)))
            {
                Interlocked.Decrement(ref _pendingOcr);
                image.Dispose();
            }
        }
        else
        {
            image.Dispose();
        }
    }

    private string BuildText(StepAction action, ElementInfo? element, string windowTitle, string? shortcut)
    {
        bool mention = WindowChanged(windowTitle);
        (string? name, string? title) = Masked(element?.Name, windowTitle);
        return Generate(action, element, name, title, shortcut, mention);
    }

    /// <summary>"na janela …" só quando a janela mudou desde o passo anterior.</summary>
    private bool WindowChanged(string windowTitle)
    {
        bool changed = !string.Equals(windowTitle, _lastWindowTitle, StringComparison.Ordinal);
        _lastWindowTitle = windowTitle;
        return changed;
    }

    /// <summary>RF9: nome e título passam pelo mascarador antes de virar texto.</summary>
    private (string? Name, string? Title) Masked(string? name, string title) =>
        _settings.RedactTutorials
            ? (name is null ? null : SensitiveDataDetector.MaskText(name), SensitiveDataDetector.MaskText(title))
            : (name, title);

    private static string Generate(StepAction action, ElementInfo? element, string? name, string? title, string? shortcut, bool mention) =>
        StepTextGenerator.Generate(new StepTextInput(
            action, element?.Kind ?? ElementKind.Other, name, title, element?.IsToggledOn, shortcut, mention, Strings.Current));

    /// <summary>Retângulo do elemento (tela) → coordenadas da imagem, cortado na imagem; vazio vira <c>null</c>.</summary>
    internal static RectDto? ToImageRect(ElementInfo? element, Rectangle captureBounds)
    {
        if (element is null || element.ScreenBounds.IsEmpty)
        {
            return null;
        }
        Rectangle r = element.ScreenBounds;
        r.Offset(-captureBounds.X, -captureBounds.Y);
        r = Rectangle.Intersect(r, new Rectangle(Point.Empty, captureBounds.Size));
        // Elemento do tamanho da janela inteira (ex.: o documento) não ajuda como destaque.
        if (r.Width <= 0 || r.Height <= 0 || r.Size == captureBounds.Size)
        {
            return null;
        }
        return new RectDto(r.X, r.Y, r.Width, r.Height);
    }

    // ---- OCR de tarjas ----

    private async Task RunOcrAsync()
    {
        await foreach (OcrWork work in _ocr.Reader.ReadAllAsync().ConfigureAwait(false))
        {
            try
            {
                if (_failed)
                {
                    continue;
                }
                OcrText? text = await OcrService.RecognizeAsync(work.Image).ConfigureAwait(false);
                if (text is null)
                {
                    continue; // sem idioma de OCR no Windows: o texto do passo já foi mascarado
                }
                IReadOnlyList<Rectangle> targets = SensitiveRedactor.Targets(
                    text, new Rectangle(Point.Empty, work.Image.Size), OcrPadding);
                if (targets.Count > 0)
                {
                    _session.UpdateItem(work.ItemId, i => i with
                    {
                        Redactions = targets.Select(r => new RectDto(r.X, r.Y, r.Width, r.Height)).ToList(),
                    });
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Fail(ex.Message);
            }
            catch (Exception ex) when (ex is COMException or ArgumentException or InvalidOperationException or ExternalException)
            {
                Debug.WriteLine($"TutorialRecorder: OCR falhou ({ex.GetType().Name}: {ex.Message})");
            }
            finally
            {
                work.Image.Dispose();
                Interlocked.Decrement(ref _pendingOcr);
            }
        }
    }

    // ---- falhas e UI ----

    private void Fail(string message)
    {
        if (_failed)
        {
            return;
        }
        _failed = true;
        Shutdown();
        Post(() => Failed?.Invoke(message));
    }

    private void Post(Action action) => _ui.Post(_ => action(), null);

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        Shutdown();
        _work.Writer.TryComplete();
        _ocr.Writer.TryComplete();
        _tick.Dispose();
        _inspector.Dispose();
    }
}
