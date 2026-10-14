using System.Drawing.Drawing2D;
using Fragmo.Capture;
using Fragmo.Editor;
using Fragmo.Native;
using Fragmo.Settings;
using Fragmo.Ui;
using Grip = Fragmo.Overlay.SelectionGeometry.Handle;

namespace Fragmo.Overlay;

internal enum OverlayResult { Cancel, Copy, Save, Pin, Print }

/// <summary>
/// Janela de tela cheia sobre a tela congelada, onde o usuário escolhe a área a capturar.
/// Tudo em pixels físicos: coordenadas do cliente = coordenadas do bitmap.
/// Depois de selecionar, vira o editor: barras de ferramentas, anotações e desfazer/refazer.
/// </summary>
internal sealed class SelectionOverlay : Form
{
    private const int VeilAlpha = 110;
    private const int GripSize = 6;
    private const int MagnifierRadius = 7;
    private const int MagnifierZoom = 8;
    private const int MagnifierSize = (2 * MagnifierRadius + 1) * MagnifierZoom; // 120 px
    private const int MagnifierInfoHeight = 34;
    private const int CursorGap = 20;
    private const int LabelPadding = 4;
    private const int DirtyMargin = 4;
    private const int ToolbarGap = 8;
    private const int MaxThickness = 20;
    private const int MinTextBoxWidth = 40;

    private static readonly Color Accent = Color.FromArgb(0xFF, 0x6A, 0x1A);
    private static readonly Color FallbackColor = Color.FromArgb(0xFF, 0x3B, 0x30);

    private enum State { Idle, Dragging, Selected }

    private readonly CapturedScreen _screen;
    private readonly AppSettings _settings;
    private readonly Font _font = new("Segoe UI", 9f);
    private readonly SolidBrush _veilBrush = new(Color.FromArgb(VeilAlpha, Color.Black));
    private readonly SolidBrush _accentBrush = new(Accent);
    private readonly SolidBrush _labelBrush = new(Color.FromArgb(200, 20, 20, 20));
    private readonly Pen _accentPen = new(Accent, 1);
    private readonly Theme _theme;
    private readonly AnnotationHistory _history = new();
    private readonly string _initialColor;
    private readonly int _initialThickness;

    private State _state = State.Idle;
    private Rectangle _selection;
    private Grip _activeGrip = Grip.None;
    private Point _dragStart;
    private Rectangle _dragOriginal;
    private Point _cursor;
    private Rectangle _lastDirty;

    // Editor
    private ToolKind _tool = ToolKind.Select;
    private StrokeStyle _style;
    private EditorToolbar? _toolsBar;
    private EditorToolbar? _actionsBar;
    private bool _drawing;
    private Point _drawStart;
    private List<Point>? _penPoints;
    private IAnnotation? _pending;
    private TextBox? _textBox; // criada uma vez e reaproveitada; é descartada junto com o form
    private Font? _textFont;
    private bool _editingText;
    private Point _textOrigin;

    /// <param name="initialSelection">
    /// Área já escolhida (em coordenadas do bitmap), usada por "repetir última área" e "janela ativa":
    /// o overlay abre direto no editor.
    /// </param>
    public SelectionOverlay(CapturedScreen screen, AppSettings settings, Rectangle? initialSelection = null)
    {
        _screen = screen;
        _settings = settings;
        _theme = Theme.Resolve(settings.Theme);
        _initialColor = settings.LastColor;
        _initialThickness = settings.LastThickness;
        _style = new StrokeStyle(ParseColor(settings.LastColor), Math.Clamp(settings.LastThickness, 1, MaxThickness));
        _history.Changed += OnHistoryChanged;

        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        AutoScaleMode = AutoScaleMode.None;
        KeyPreview = true;
        Cursor = Cursors.Cross;
        Bounds = screen.VirtualBounds;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);

        if (User32.GetCursorPos(out User32.POINT p))
        {
            _cursor = CoordinateMapper.ScreenToBitmap(new Point(p.X, p.Y), screen.VirtualBounds);
        }

        if (initialSelection is Rectangle initial)
        {
            _selection = SelectionGeometry.Clamp(initial, ImageSize);
            if (HasSelection)
            {
                _state = State.Selected;
            }
        }
    }

    public OverlayResult Result { get; private set; } = OverlayResult.Cancel;

    /// <summary>Área escolhida, em coordenadas do bitmap.</summary>
    public Rectangle Selection => _state == State.Selected ? _selection : Rectangle.Empty;

    /// <summary>A cor ou a espessura mudaram em relação às configurações (para o fluxo gravar).</summary>
    public bool StrokeStyleChanged =>
        !string.Equals(ToHex(_style.Color), _initialColor, StringComparison.OrdinalIgnoreCase) ||
        _style.Thickness != _initialThickness;

    /// <summary>Imagem final: área selecionada com as anotações, sem véu, borda nem barras.</summary>
    public Bitmap ComposeResult() => ImageComposer.Compose(_screen.Image, _history.Items, _selection);

    private bool HasSelection => _selection.Width > 0 && _selection.Height > 0;

    private Size ImageSize => _screen.Image.Size;

    private bool IsFreehand => _tool is ToolKind.Pen or ToolKind.Marker;

    /// <summary>Raio do círculo dos passos, acompanhando a espessura escolhida.</summary>
    private int StepRadius => 10 + _style.Thickness * 2;

    /// <summary>Tamanho do texto em pixels, acompanhando a espessura escolhida.</summary>
    private float TextSize => 12 + _style.Thickness * 3;

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Rectangle vs = _screen.VirtualBounds;
        User32.SetWindowPos(Handle, IntPtr.Zero, vs.X, vs.Y, vs.Width, vs.Height,
            User32.SWP_NOZORDER | User32.SWP_NOACTIVATE);
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        if (_state == State.Selected)
        {
            ShowToolbars();
            UpdateCursor(_cursor);
        }
        Activate();
        Focus();
    }

    protected override void WndProc(ref Message m)
    {
        // Não deixa o WinForms redimensionar o overlay ao cruzar monitores com escalas diferentes.
        if (m.Msg == User32.WM_DPICHANGED)
        {
            return;
        }
        base.WndProc(ref m);
    }

    // ----- Teclado -----

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        // Na caixa de texto: Enter confirma e Esc descarta (Shift+Enter quebra a linha normalmente).
        if (_editingText)
        {
            if (keyData == Keys.Enter)
            {
                CloseTextBox(commit: true);
                return true;
            }
            if (keyData == Keys.Escape)
            {
                CloseTextBox(commit: false);
                return true;
            }
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (_editingText || !HandleKey(e.KeyData))
        {
            return;
        }
        e.Handled = true;
        e.SuppressKeyPress = true;
    }

    private bool HandleKey(Keys keyData)
    {
        if (keyData == Keys.Escape)
        {
            Finish(OverlayResult.Cancel);
            return true;
        }
        if (_state != State.Selected)
        {
            return false;
        }

        switch (keyData)
        {
            case Keys.Enter:
            case Keys.Control | Keys.C:
                Finish(OverlayResult.Copy);
                return true;
            case Keys.Control | Keys.S:
                Finish(OverlayResult.Save);
                return true;
            case Keys.Control | Keys.T:
                Finish(OverlayResult.Pin);
                return true;
            case Keys.Control | Keys.P:
                Finish(OverlayResult.Print);
                return true;
            case Keys.Control | Keys.Z:
                if (!_drawing) _history.Undo();
                return true;
            case Keys.Control | Keys.Y:
            case Keys.Control | Keys.Shift | Keys.Z:
                if (!_drawing) _history.Redo();
                return true;
        }

        ToolKind? tool = keyData switch
        {
            Keys.P => ToolKind.Pen,
            Keys.L => ToolKind.Line,
            Keys.A => ToolKind.Arrow,
            Keys.R => ToolKind.Rectangle,
            Keys.T => ToolKind.Text,
            Keys.M => ToolKind.Marker,
            Keys.B => ToolKind.Blur,
            Keys.N => ToolKind.Step,
            Keys.V => ToolKind.Select,
            _ => null,
        };
        if (tool is null || _drawing)
        {
            return false;
        }
        SelectTool(tool.Value);
        return true;
    }

    // ----- Mouse -----

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        _cursor = e.Location;

        // Clicar fora da caixa de texto confirma o texto.
        if (_editingText)
        {
            CloseTextBox(commit: true);
            return;
        }

        if (_drawing)
        {
            return;
        }

        if (e.Button == MouseButtons.Right)
        {
            if (_state == State.Idle)
            {
                Finish(OverlayResult.Cancel);
                return;
            }
            ClearSelection();
            return;
        }

        if (e.Button != MouseButtons.Left)
        {
            return;
        }

        bool editing = _state == State.Selected && _tool != ToolKind.Select;
        if (editing && _selection.Contains(e.Location))
        {
            BeginAnnotation(e.Location);
            return;
        }

        Grip grip = _state == State.Selected ? SelectionGeometry.HitTest(_selection, e.Location, GripSize) : Grip.None;
        if (grip != Grip.None)
        {
            HideToolbars();
            _activeGrip = grip;
            _dragStart = e.Location;
            _dragOriginal = _selection;
            return;
        }

        if (editing)
        {
            return; // com uma ferramenta ativa, clicar fora da seleção não começa outra
        }

        HideToolbars();
        _state = State.Dragging;
        _dragStart = e.Location;
        _selection = Rectangle.Empty;
        UpdateDirty();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        _cursor = e.Location;

        if (_drawing)
        {
            ContinueAnnotation(e.Location);
            return;
        }

        if (_state == State.Dragging)
        {
            _selection = SelectionGeometry.Clamp(SelectionGeometry.FromPoints(_dragStart, e.Location), ImageSize);
        }
        else if (_activeGrip != Grip.None)
        {
            var delta = new Point(e.X - _dragStart.X, e.Y - _dragStart.Y);
            _selection = _activeGrip == Grip.Move
                ? SelectionGeometry.Move(_dragOriginal, delta, ImageSize)
                : SelectionGeometry.Clamp(SelectionGeometry.Resize(_dragOriginal, _activeGrip, delta), ImageSize);
        }
        else if (_state == State.Selected)
        {
            UpdateCursor(e.Location);
        }

        UpdateDirty();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.Button != MouseButtons.Left)
        {
            return;
        }

        if (_drawing)
        {
            EndAnnotation(e.Location);
            return;
        }

        bool wasDragging = _state == State.Dragging || _activeGrip != Grip.None;
        _activeGrip = Grip.None;
        if (!wasDragging)
        {
            return;
        }

        if (HasSelection)
        {
            _state = State.Selected;
            UpdateCursor(e.Location);
            UpdateDirty();
            ShowToolbars();
        }
        else
        {
            ClearSelection();
        }
    }

    protected override void OnMouseDoubleClick(MouseEventArgs e)
    {
        base.OnMouseDoubleClick(e);
        // Com uma ferramenta de desenho, o duplo clique faz parte do desenho e não copia.
        if (e.Button == MouseButtons.Left && _state == State.Selected && _tool == ToolKind.Select &&
            _selection.Contains(e.Location))
        {
            Finish(OverlayResult.Copy);
        }
    }

    private void ClearSelection()
    {
        _state = State.Idle;
        _activeGrip = Grip.None;
        _selection = Rectangle.Empty;
        Cursor = Cursors.Cross;
        HideToolbars();
        UpdateDirty();
    }

    private void Finish(OverlayResult result)
    {
        if (_editingText)
        {
            CloseTextBox(commit: result != OverlayResult.Cancel);
        }
        Result = result;
        DialogResult = result == OverlayResult.Cancel ? DialogResult.Cancel : DialogResult.OK;
    }

    private void UpdateCursor(Point p)
    {
        if (_tool != ToolKind.Select && _selection.Contains(p))
        {
            Cursor = _tool == ToolKind.Text ? Cursors.IBeam : Cursors.Cross;
            return;
        }
        Cursor = CursorFor(SelectionGeometry.HitTest(_selection, p, GripSize));
    }

    private static Cursor CursorFor(Grip grip) => grip switch
    {
        Grip.N or Grip.S => Cursors.SizeNS,
        Grip.E or Grip.W => Cursors.SizeWE,
        Grip.NE or Grip.SW => Cursors.SizeNESW,
        Grip.NW or Grip.SE => Cursors.SizeNWSE,
        Grip.Move => Cursors.SizeAll,
        _ => Cursors.Cross,
    };

    // ----- Anotações -----

    private void BeginAnnotation(Point p)
    {
        if (_tool == ToolKind.Text)
        {
            OpenTextBox(p);
            return;
        }
        if (_tool == ToolKind.Step)
        {
            _history.Add(new StepAnnotation(_style.Color, StepCounter.Next(_history.Items), p, StepRadius));
            return;
        }

        _drawing = true;
        _drawStart = p;
        _penPoints = IsFreehand ? [p] : null;
        _pending = BuildPending(p);
        if (_pending is not null)
        {
            Invalidate(_pending.Bounds);
        }
    }

    private void ContinueAnnotation(Point p)
    {
        if (_penPoints is not null)
        {
            Point last = _penPoints[^1];
            if (last == p)
            {
                return;
            }
            _penPoints.Add(p);
            // Só o trecho novo precisa ser repintado.
            int pad = _tool == ToolKind.Marker ? MarkerAnnotation.PadFor(_style.Thickness) : AnnotationDrawing.Pad(_style);
            Invalidate(AnnotationDrawing.Around([last, p], pad));
            return;
        }

        Rectangle old = _pending?.Bounds ?? Rectangle.Empty;
        _pending = BuildPending(p);
        Invalidate(UnionNonEmpty(old, _pending?.Bounds ?? Rectangle.Empty));
    }

    private void EndAnnotation(Point p)
    {
        IAnnotation? done = _pending;
        bool trivial = !IsFreehand && p == _drawStart; // clique sem arrastar
        _drawing = false;
        _pending = null;
        _penPoints = null;

        if (done is null)
        {
            return;
        }
        if (trivial)
        {
            Invalidate(done.Bounds);
            return;
        }
        _history.Add(done);
    }

    /// <summary>Anotação "em andamento" até o ponto atual; Shift trava ângulo (linha/seta) ou faz quadrado.</summary>
    private IAnnotation? BuildPending(Point current)
    {
        bool shift = (ModifierKeys & Keys.Shift) == Keys.Shift;
        return _tool switch
        {
            ToolKind.Pen => new PenAnnotation(_style, _penPoints!),
            ToolKind.Marker => new MarkerAnnotation(_style.Color, _style.Thickness, _penPoints!),
            ToolKind.Line => new LineAnnotation(_style, _drawStart, shift ? SnapAngle(_drawStart, current) : current),
            ToolKind.Arrow => new ArrowAnnotation(_style, _drawStart, shift ? SnapAngle(_drawStart, current) : current),
            ToolKind.Rectangle => new RectangleAnnotation(_style,
                shift ? SquareFrom(_drawStart, current) : SelectionGeometry.FromPoints(_drawStart, current)),
            ToolKind.Blur => new BlurAnnotation(
                shift ? SquareFrom(_drawStart, current) : SelectionGeometry.FromPoints(_drawStart, current)),
            _ => null,
        };
    }

    /// <summary>Trava a direção em múltiplos de 45°, mantendo o comprimento.</summary>
    private static Point SnapAngle(Point a, Point b)
    {
        int dx = b.X - a.X, dy = b.Y - a.Y;
        double step = Math.PI / 4;
        double angle = Math.Round(Math.Atan2(dy, dx) / step) * step;
        double length = Math.Sqrt((double)dx * dx + (double)dy * dy);
        return new Point(a.X + (int)Math.Round(length * Math.Cos(angle)), a.Y + (int)Math.Round(length * Math.Sin(angle)));
    }

    private static Rectangle SquareFrom(Point a, Point b)
    {
        int side = Math.Max(Math.Abs(b.X - a.X), Math.Abs(b.Y - a.Y));
        var corner = new Point(a.X + Math.Sign(b.X - a.X) * side, a.Y + Math.Sign(b.Y - a.Y) * side);
        return SelectionGeometry.FromPoints(a, corner);
    }

    private void OnHistoryChanged()
    {
        _actionsBar?.SetUndoRedo(_history.CanUndo, _history.CanRedo);
        if (HasSelection)
        {
            Invalidate(Rectangle.Inflate(_selection, GripSize + DirtyMargin, GripSize + DirtyMargin));
        }
    }

    private void SelectTool(ToolKind tool)
    {
        if (_editingText)
        {
            CloseTextBox(commit: true);
        }
        _tool = tool;
        _toolsBar?.SetTool(tool);
        UpdateCursor(_cursor);
    }

    private void OnToolbarStyleChanged(StrokeStyle style)
    {
        _style = style;
        if (_editingText && _textBox is not null)
        {
            _textBox.ForeColor = style.Color;
            ApplyTextFont();
            ResizeTextBox();
        }
    }

    // ----- Texto -----

    private void OpenTextBox(Point p)
    {
        _textOrigin = p;
        if (_textBox is null)
        {
            _textBox = new TextBox
            {
                BorderStyle = BorderStyle.None,
                Multiline = true,
                AcceptsReturn = true,
                WordWrap = false,
                ScrollBars = ScrollBars.None,
                Visible = false,
            };
            _textBox.TextChanged += (_, _) => ResizeTextBox();
            Controls.Add(_textBox);
        }

        // Fundo "transparente" simulado: a cor do pixel sob o ponto clicado.
        Color under = _screen.Image.GetPixel(
            Math.Clamp(p.X, 0, ImageSize.Width - 1),
            Math.Clamp(p.Y, 0, ImageSize.Height - 1));
        _textBox.BackColor = Color.FromArgb(255, under);
        _textBox.ForeColor = _style.Color;
        ApplyTextFont();
        _textBox.Text = string.Empty;
        _textBox.Location = p;
        ResizeTextBox();

        _editingText = true;
        _textBox.Visible = true;
        _textBox.BringToFront();
        _textBox.Focus();
    }

    private void ApplyTextFont()
    {
        Font? old = _textFont;
        _textFont = new Font("Segoe UI Semibold", TextSize, FontStyle.Regular, GraphicsUnit.Pixel);
        _textBox!.Font = _textFont;
        old?.Dispose();
    }

    private void ResizeTextBox()
    {
        if (_textBox is null || _textFont is null)
        {
            return;
        }
        // O "W" final reserva espaço para o próximo caractere e para uma linha nova vazia.
        Size text = TextRenderer.MeasureText(_textBox.Text + "W", _textFont);
        _textBox.Size = new Size(Math.Max(MinTextBoxWidth, text.Width + 4), text.Height + 4);
    }

    private void CloseTextBox(bool commit)
    {
        if (!_editingText || _textBox is null)
        {
            return;
        }

        _editingText = false;
        string text = _textBox.Text;
        _textBox.Visible = false;
        Focus();

        if (commit && !string.IsNullOrWhiteSpace(text))
        {
            _history.Add(new TextAnnotation(_style.Color, TextSize, _textOrigin, text));
        }
    }

    // ----- Barras de ferramentas -----

    private void ShowToolbars()
    {
        if (_toolsBar is null || _actionsBar is null)
        {
            _toolsBar = new EditorToolbar(_theme, Orientation.Vertical, _style) { Visible = false };
            _actionsBar = new EditorToolbar(_theme, Orientation.Horizontal, _style) { Visible = false };

            _toolsBar.SetTool(_tool);
            _toolsBar.ToolChanged += SelectTool;
            _toolsBar.StrokeStyleChanged += OnToolbarStyleChanged;

            _actionsBar.SetUndoRedo(_history.CanUndo, _history.CanRedo);
            _actionsBar.UndoClicked += () => { CloseTextBox(commit: true); _history.Undo(); };
            _actionsBar.RedoClicked += () => { CloseTextBox(commit: true); _history.Redo(); };
            _actionsBar.PinClicked += () => Finish(OverlayResult.Pin);
            _actionsBar.PrintClicked += () => Finish(OverlayResult.Print);
            _actionsBar.CopyClicked += () => Finish(OverlayResult.Copy);
            _actionsBar.SaveClicked += () => Finish(OverlayResult.Save);
            _actionsBar.CloseClicked += () => Finish(OverlayResult.Cancel);

            Controls.Add(_toolsBar);
            Controls.Add(_actionsBar);
        }

        PositionToolbars(_toolsBar, _actionsBar);
        _toolsBar.Visible = true;
        _actionsBar.Visible = true;
    }

    private void HideToolbars()
    {
        if (_toolsBar is not null) _toolsBar.Visible = false;
        if (_actionsBar is not null) _actionsBar.Visible = false;
    }

    /// <summary>
    /// Horizontal abaixo da seleção (à direita) e vertical à direita dela; quando não cabem no
    /// monitor, trocam de lado e, em último caso, vão para dentro da seleção.
    /// </summary>
    private void PositionToolbars(EditorToolbar tools, EditorToolbar actions)
    {
        Rectangle vs = _screen.VirtualBounds;
        var corner = new Point(_selection.Right - 1 + vs.X, _selection.Bottom - 1 + vs.Y);
        Rectangle monitor = Screen.FromPoint(corner).Bounds;
        monitor.Offset(-vs.X, -vs.Y); // para coordenadas do bitmap

        Size h = actions.Size;
        int hx = _selection.Right - h.Width;
        int hy = _selection.Bottom + ToolbarGap;
        if (hy + h.Height > monitor.Bottom)
        {
            hy = _selection.Top - ToolbarGap - h.Height;
            if (hy < monitor.Top)
            {
                hy = _selection.Bottom - ToolbarGap - h.Height;
            }
        }
        actions.Location = new Point(
            ClampToRange(hx, monitor.Left, monitor.Right - h.Width),
            ClampToRange(hy, monitor.Top, monitor.Bottom - h.Height));

        Size v = tools.Size;
        int vx = _selection.Right + ToolbarGap;
        int vy = _selection.Top;
        if (vx + v.Width > monitor.Right)
        {
            vx = _selection.Left - ToolbarGap - v.Width;
            if (vx < monitor.Left)
            {
                vx = _selection.Right - ToolbarGap - v.Width;
            }
        }
        tools.Location = new Point(
            ClampToRange(vx, monitor.Left, monitor.Right - v.Width),
            ClampToRange(vy, monitor.Top, monitor.Bottom - v.Height));
    }

    private static int ClampToRange(int value, int min, int max) => Math.Max(min, Math.Min(value, max));

    // ----- Invalidação -----

    /// <summary>Invalida só a união do que mudou (seleção + rótulo + lupa), para não piscar.</summary>
    private void UpdateDirty()
    {
        Rectangle now = Rectangle.Empty;
        if (HasSelection)
        {
            now = UnionNonEmpty(now, Rectangle.Inflate(_selection, GripSize + DirtyMargin, GripSize + DirtyMargin));
            now = UnionNonEmpty(now, Rectangle.Inflate(LabelBounds(), DirtyMargin, DirtyMargin));
        }
        if (MagnifierVisible)
        {
            now = UnionNonEmpty(now, Rectangle.Inflate(MagnifierBounds(), DirtyMargin, DirtyMargin));
        }

        Rectangle dirty = UnionNonEmpty(_lastDirty, now);
        if (!dirty.IsEmpty)
        {
            Invalidate(dirty);
        }
        _lastDirty = now;
    }

    private static Rectangle UnionNonEmpty(Rectangle a, Rectangle b)
    {
        if (a.IsEmpty) return b;
        if (b.IsEmpty) return a;
        return Rectangle.Union(a, b);
    }

    // ----- Pintura -----

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        // Tudo é pintado em OnPaint.
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        Rectangle clip = e.ClipRectangle;

        g.CompositingMode = CompositingMode.SourceCopy;
        g.DrawImage(_screen.Image, clip, clip, GraphicsUnit.Pixel);
        g.CompositingMode = CompositingMode.SourceOver;

        if (HasSelection && (_history.Items.Count > 0 || _pending is not null))
        {
            // Anotações recortadas à seleção; o estado do Graphics volta ao normal depois.
            GraphicsState state = g.Save();
            g.SetClip(_selection, CombineMode.Intersect);
            foreach (IAnnotation item in _history.Items)
            {
                if (item.Bounds.IntersectsWith(clip))
                {
                    item.Draw(g, _screen.Image);
                }
            }
            _pending?.Draw(g, _screen.Image);
            g.Restore(state);
        }

        using (var veil = new Region(ClientRectangle))
        {
            if (HasSelection)
            {
                veil.Exclude(_selection);
            }
            g.FillRegion(_veilBrush, veil);
        }

        if (HasSelection)
        {
            DrawSelectionFrame(g);
            DrawSizeLabel(g);
        }

        if (MagnifierVisible)
        {
            DrawMagnifier(g);
        }
    }

    private void DrawSelectionFrame(Graphics g)
    {
        // Borda por fora da seleção, para não cobrir pixels que serão copiados.
        g.DrawRectangle(_accentPen, _selection.X - 1, _selection.Y - 1, _selection.Width + 1, _selection.Height + 1);

        int l = _selection.Left, t = _selection.Top, r = _selection.Right, b = _selection.Bottom;
        int cx = l + _selection.Width / 2, cy = t + _selection.Height / 2;
        Point[] points = [new(l, t), new(cx, t), new(r, t), new(r, cy), new(r, b), new(cx, b), new(l, b), new(l, cy)];
        foreach (Point p in points)
        {
            g.FillRectangle(_accentBrush, p.X - GripSize / 2, p.Y - GripSize / 2, GripSize, GripSize);
        }
    }

    private string SizeText => $"{_selection.Width} × {_selection.Height}";

    private Rectangle LabelBounds()
    {
        Size text = TextRenderer.MeasureText(SizeText, _font);
        var size = new Size(text.Width + 2 * LabelPadding, text.Height + LabelPadding);
        int y = _selection.Top - size.Height - GripSize;
        if (y < 0)
        {
            y = _selection.Top + GripSize; // não cabe acima: vai para dentro
        }
        int x = Math.Clamp(_selection.Left, 0, Math.Max(0, ClientSize.Width - size.Width));
        return new Rectangle(new Point(x, y), size);
    }

    private void DrawSizeLabel(Graphics g)
    {
        Rectangle box = LabelBounds();
        g.FillRectangle(_labelBrush, box);
        TextRenderer.DrawText(g, SizeText, _font, box, Color.White,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
    }

    private bool MagnifierVisible =>
        _settings.ShowMagnifier && _state != State.Selected && ClientRectangle.Contains(_cursor);

    private Rectangle MagnifierBounds()
    {
        int totalHeight = MagnifierSize + MagnifierInfoHeight;
        int x = _cursor.X + CursorGap;
        int y = _cursor.Y + CursorGap;
        if (x + MagnifierSize > ClientSize.Width) x = _cursor.X - CursorGap - MagnifierSize;
        if (y + totalHeight > ClientSize.Height) y = _cursor.Y - CursorGap - totalHeight;
        return new Rectangle(Math.Max(0, x), Math.Max(0, y), MagnifierSize, totalHeight);
    }

    private void DrawMagnifier(Graphics g)
    {
        Rectangle bounds = MagnifierBounds();
        var zoom = new Rectangle(bounds.X, bounds.Y, MagnifierSize, MagnifierSize);
        var info = new Rectangle(bounds.X, zoom.Bottom, MagnifierSize, MagnifierInfoHeight);

        g.FillRectangle(Brushes.Black, zoom);
        InterpolationMode oldInterpolation = g.InterpolationMode;
        PixelOffsetMode oldOffset = g.PixelOffsetMode;
        g.InterpolationMode = InterpolationMode.NearestNeighbor;
        g.PixelOffsetMode = PixelOffsetMode.Half;
        g.DrawImage(_screen.Image, zoom, SelectionGeometry.MagnifierSource(_cursor, MagnifierRadius), GraphicsUnit.Pixel);
        g.InterpolationMode = oldInterpolation;
        g.PixelOffsetMode = oldOffset;

        // Mira no pixel central.
        int c = MagnifierRadius * MagnifierZoom;
        g.DrawRectangle(_accentPen, zoom.X + c, zoom.Y + c, MagnifierZoom - 1, MagnifierZoom - 1);
        g.DrawRectangle(_accentPen, zoom.X, zoom.Y, zoom.Width - 1, zoom.Height - 1);

        Color pixel = _screen.Image.GetPixel(
            Math.Clamp(_cursor.X, 0, ImageSize.Width - 1),
            Math.Clamp(_cursor.Y, 0, ImageSize.Height - 1));
        Point screenPos = new(_cursor.X + _screen.VirtualBounds.X, _cursor.Y + _screen.VirtualBounds.Y);
        string text = $"#{pixel.R:X2}{pixel.G:X2}{pixel.B:X2}\n{screenPos.X}, {screenPos.Y}";

        g.FillRectangle(_labelBrush, info);
        TextRenderer.DrawText(g, text, _font, info, Color.White,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
    }

    // ----- Cor -----

    private static string ToHex(Color c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";

    private static Color ParseColor(string text)
    {
        try
        {
            Color c = ColorTranslator.FromHtml(text);
            return c.IsEmpty ? FallbackColor : Color.FromArgb(255, c);
        }
        catch (Exception)
        {
            return FallbackColor;
        }
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        base.OnFormClosed(e);
        // Grava a cor e a espessura usadas; o CaptureWorkflow persiste as configurações.
        if (StrokeStyleChanged)
        {
            _settings.LastColor = ToHex(_style.Color);
            _settings.LastThickness = _style.Thickness;
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _textFont?.Dispose();
            _font.Dispose();
            _veilBrush.Dispose();
            _accentBrush.Dispose();
            _labelBrush.Dispose();
            _accentPen.Dispose();
        }
        base.Dispose(disposing);
    }
}
