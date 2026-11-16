using System.Diagnostics;
using Fragmo.Native;
using Fragmo.Ui;

namespace Fragmo.Tutorial;

/// <summary>
/// Barra pequena da gravação: sempre por cima, excluída dos prints (SetWindowDisplayAffinity) e sem roubar o foco
/// da janela gravada (WS_EX_NOACTIVATE). Arrastável pelo rótulo de status.
/// </summary>
internal sealed class RecordingBar : Form
{
    private const int WS_EX_TOPMOST = 0x00000008;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int WS_EX_NOACTIVATE = 0x08000000;

    private readonly Theme _theme;
    private readonly Label _status;
    private readonly Button _pause;
    private readonly Button _stop;
    private readonly Button _discard;
    private readonly ToolTip _tip = new();
    private int _steps;
    private bool _paused;
    private bool _processing;
    private Point? _dragOffset;

    public event EventHandler? StopClicked;
    public event EventHandler? PauseClicked;
    public event EventHandler? DiscardClicked;

    public RecordingBar(Theme theme)
    {
        _theme = theme;
        FormBorderStyle = FormBorderStyle.None;
        TopMost = true;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = theme.Background;
        ForeColor = theme.Foreground;
        Padding = new Padding(8, 4, 4, 4);
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Text = Strings.AppName;

        var layout = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            WrapContents = false,
            Dock = DockStyle.Fill,
            BackColor = theme.Background,
        };

        _status = new Label
        {
            AutoSize = true,
            ForeColor = theme.AccentText,
            Margin = new Padding(0, 6, 12, 0),
            Cursor = Cursors.SizeAll,
        };
        _tip.SetToolTip(_status, Strings.RecordingDragHint);
        _status.MouseDown += OnDragStart;
        _status.MouseMove += OnDragMove;
        _status.MouseUp += (_, _) => _dragOffset = null;

        _pause = NewButton(Strings.RecordingPause, accent: false);
        _pause.Click += (_, _) => PauseClicked?.Invoke(this, EventArgs.Empty);
        _stop = NewButton(Strings.RecordingStop, accent: true);
        _stop.Click += (_, _) => StopClicked?.Invoke(this, EventArgs.Empty);
        _discard = NewButton(Strings.RecordingDiscard, accent: false);
        _discard.Click += (_, _) => DiscardClicked?.Invoke(this, EventArgs.Empty);

        layout.Controls.Add(_status);
        layout.Controls.Add(_pause);
        layout.Controls.Add(_stop);
        layout.Controls.Add(_discard);
        Controls.Add(layout);
        UpdateStatus();
    }

    private Button NewButton(string text, bool accent)
    {
        var button = new Button
        {
            AutoSize = true,
            Text = text,
            FlatStyle = FlatStyle.Flat,
            BackColor = accent ? _theme.Accent : _theme.Surface,
            ForeColor = accent ? _theme.AccentForeground : _theme.Foreground,
            Margin = new Padding(2),
        };
        button.FlatAppearance.BorderSize = accent ? 0 : 1;
        button.FlatAppearance.BorderColor = _theme.Border;
        return button;
    }

    /// <summary>Não rouba o foco da janela que está sendo gravada.</summary>
    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams cp = base.CreateParams;
            cp.ExStyle |= WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW | WS_EX_TOPMOST;
            return cp;
        }
    }

    public int StepCount => _steps;

    public void SetStepCount(int count)
    {
        _steps = count;
        UpdateStatus();
    }

    public void SetPaused(bool paused)
    {
        _paused = paused;
        _pause.Text = paused ? Strings.RecordingResume : Strings.RecordingPause;
        UpdateStatus();
    }

    /// <summary>Esperando a fila e o OCR depois do Parar: botões desligados.</summary>
    public void SetProcessing()
    {
        _processing = true;
        _pause.Enabled = _stop.Enabled = _discard.Enabled = false;
        UpdateStatus();
    }

    private void UpdateStatus() => _status.Text = _processing
        ? Strings.RecordingProcessing
        : string.Format(_paused ? Strings.RecordingPausedStatus : Strings.RecordingStatus, _steps);

    private void OnDragStart(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            _dragOffset = new Point(Cursor.Position.X - Left, Cursor.Position.Y - Top);
        }
    }

    private void OnDragMove(object? sender, MouseEventArgs e)
    {
        if (_dragOffset is Point offset && e.Button == MouseButtons.Left)
        {
            Location = new Point(Cursor.Position.X - offset.X, Cursor.Position.Y - offset.Y);
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        if (!User32.SetWindowDisplayAffinity(Handle, User32.WDA_EXCLUDEFROMCAPTURE))
        {
            Debug.WriteLine($"RecordingBar: SetWindowDisplayAffinity falhou ({System.Runtime.InteropServices.Marshal.GetLastPInvokeError()}); a barra pode aparecer nos prints.");
        }
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        Rectangle area = Screen.PrimaryScreen!.WorkingArea;
        Location = new Point(area.Left + (area.Width - Width) / 2, area.Top + 8);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _tip.Dispose();
        }
        base.Dispose(disposing);
    }
}
