using System.Drawing.Drawing2D;
using Fragmo.Ui;

namespace Fragmo.Editor;

/// <summary>
/// Barra do editor desenhada à mão com o <see cref="Theme"/>. A vertical tem as ferramentas, a cor
/// e a espessura; a horizontal tem desfazer/refazer e copiar/salvar/fechar.
/// </summary>
internal sealed class EditorToolbar : Control
{
    private const int ButtonSize = 32;
    private const int IconInset = 8;
    private const int BarPadding = 4;
    private const int SeparatorSize = 9;
    private const int TooltipDelayMs = 400;
    private const int TooltipDurationMs = 4000;
    private const int SwatchSize = 14;

    private static readonly Color[] QuickColors =
    [
        ColorTranslator.FromHtml("#FF3B30"), ColorTranslator.FromHtml("#FF6A1A"),
        ColorTranslator.FromHtml("#FFCC00"), ColorTranslator.FromHtml("#34C759"),
        ColorTranslator.FromHtml("#007AFF"), ColorTranslator.FromHtml("#AF52DE"),
        Color.FromArgb(0, 0, 0), Color.FromArgb(255, 255, 255),
    ];

    private static readonly int[] Thicknesses = [1, 2, 3, 5, 8];

    private enum ButtonId { Pen, Line, Arrow, Rectangle, Text, Color, Thickness, Undo, Redo, Copy, Save, Close }

    private sealed record Item(ButtonId Id, Rectangle Bounds);

    private readonly Theme _theme;
    private readonly List<Item> _items = [];
    private readonly List<Rectangle> _separators = [];
    private readonly ToolTip _toolTip = new();
    private readonly System.Windows.Forms.Timer _tooltipTimer = new() { Interval = TooltipDelayMs };
    private readonly ContextMenuStrip _colorMenu;
    private readonly ContextMenuStrip _thicknessMenu;
    private readonly List<Bitmap> _swatches = [];

    private ToolKind _tool = ToolKind.Select;
    private StrokeStyle _style;
    private bool _canUndo;
    private bool _canRedo;
    private int _hover = -1;
    private int _pressed = -1;

    public EditorToolbar(Theme theme, Orientation orientation, StrokeStyle style)
    {
        _theme = theme;
        _style = style;
        Orientation = orientation;

        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        SetStyle(ControlStyles.Selectable, false); // o teclado fica sempre no overlay
        TabStop = false;
        Cursor = Cursors.Default;
        BackColor = theme.Background;

        ButtonId?[] layout = orientation == Orientation.Vertical
            ? [ButtonId.Pen, ButtonId.Line, ButtonId.Arrow, ButtonId.Rectangle, ButtonId.Text, null,
               ButtonId.Color, ButtonId.Thickness]
            : [ButtonId.Undo, ButtonId.Redo, null, ButtonId.Copy, ButtonId.Save, ButtonId.Close];
        BuildLayout(layout);

        _colorMenu = BuildColorMenu();
        _thicknessMenu = BuildThicknessMenu();
        _tooltipTimer.Tick += OnTooltipTick;
    }

    public event Action<ToolKind>? ToolChanged;
    public event Action<StrokeStyle>? StrokeStyleChanged;
    public event Action? UndoClicked;
    public event Action? RedoClicked;
    public event Action? CopyClicked;
    public event Action? SaveClicked;
    public event Action? CloseClicked;

    public Orientation Orientation { get; }

    public void SetUndoRedo(bool canUndo, bool canRedo)
    {
        _canUndo = canUndo;
        _canRedo = canRedo;
        Invalidate();
    }

    /// <summary>Marca a ferramenta ativa sem disparar <see cref="ToolChanged"/>.</summary>
    public void SetTool(ToolKind tool)
    {
        _tool = tool;
        Invalidate();
    }

    // ----- Layout -----

    private void BuildLayout(ButtonId?[] layout)
    {
        bool vertical = Orientation == Orientation.Vertical;
        int pos = BarPadding;
        foreach (ButtonId? id in layout)
        {
            if (id is null)
            {
                _separators.Add(vertical
                    ? new Rectangle(BarPadding, pos, ButtonSize, SeparatorSize)
                    : new Rectangle(pos, BarPadding, SeparatorSize, ButtonSize));
                pos += SeparatorSize;
                continue;
            }
            _items.Add(new Item(id.Value, vertical
                ? new Rectangle(BarPadding, pos, ButtonSize, ButtonSize)
                : new Rectangle(pos, BarPadding, ButtonSize, ButtonSize)));
            pos += ButtonSize;
        }
        pos += BarPadding;
        int thickness = ButtonSize + 2 * BarPadding;
        Size = vertical ? new Size(thickness, pos) : new Size(pos, thickness);
    }

    private int IndexAt(Point p) => _items.FindIndex(i => i.Bounds.Contains(p));

    private bool IsEnabled(ButtonId id) => id switch
    {
        ButtonId.Undo => _canUndo,
        ButtonId.Redo => _canRedo,
        _ => true,
    };

    private static ToolKind? ToolFor(ButtonId id) => id switch
    {
        ButtonId.Pen => ToolKind.Pen,
        ButtonId.Line => ToolKind.Line,
        ButtonId.Arrow => ToolKind.Arrow,
        ButtonId.Rectangle => ToolKind.Rectangle,
        ButtonId.Text => ToolKind.Text,
        _ => null,
    };

    private static string TooltipFor(ButtonId id) => id switch
    {
        ButtonId.Pen => Strings.ToolPen,
        ButtonId.Line => Strings.ToolLine,
        ButtonId.Arrow => Strings.ToolArrow,
        ButtonId.Rectangle => Strings.ToolRectangle,
        ButtonId.Text => Strings.ToolText,
        ButtonId.Color => Strings.ToolColor,
        ButtonId.Thickness => Strings.ToolThickness,
        ButtonId.Undo => Strings.ActionUndo,
        ButtonId.Redo => Strings.ActionRedo,
        ButtonId.Copy => Strings.ActionCopy,
        ButtonId.Save => Strings.ActionSave,
        _ => Strings.ActionClose,
    };

    // ----- Mouse -----

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        SetHover(IndexAt(e.Location));
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        SetHover(-1);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        HideTooltip();
        _pressed = e.Button == MouseButtons.Left ? IndexAt(e.Location) : -1;
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        int index = IndexAt(e.Location);
        bool clicked = e.Button == MouseButtons.Left && index >= 0 && index == _pressed;
        _pressed = -1;
        if (clicked && IsEnabled(_items[index].Id))
        {
            Activate(_items[index]);
        }
    }

    private void SetHover(int index)
    {
        if (index == _hover)
        {
            return;
        }
        _hover = index;
        Invalidate();
        HideTooltip();
        if (index >= 0)
        {
            _tooltipTimer.Start();
        }
    }

    private void HideTooltip()
    {
        _tooltipTimer.Stop();
        _toolTip.Hide(this);
    }

    private void OnTooltipTick(object? sender, EventArgs e)
    {
        _tooltipTimer.Stop();
        if (_hover < 0)
        {
            return;
        }
        Rectangle b = _items[_hover].Bounds;
        Point at = Orientation == Orientation.Vertical
            ? new Point(Width + 4, b.Top + 6)
            : new Point(b.Left, Height + 4);
        _toolTip.Show(TooltipFor(_items[_hover].Id), this, at, TooltipDurationMs);
    }

    private void Activate(Item item)
    {
        if (ToolFor(item.Id) is ToolKind tool)
        {
            // Clicar na ferramenta ativa volta para a seleção.
            SetTool(_tool == tool ? ToolKind.Select : tool);
            ToolChanged?.Invoke(_tool);
            return;
        }

        switch (item.Id)
        {
            case ButtonId.Color:
                _colorMenu.Show(this, MenuPoint(item));
                break;
            case ButtonId.Thickness:
                _thicknessMenu.Show(this, MenuPoint(item));
                break;
            case ButtonId.Undo:
                UndoClicked?.Invoke();
                break;
            case ButtonId.Redo:
                RedoClicked?.Invoke();
                break;
            case ButtonId.Copy:
                CopyClicked?.Invoke();
                break;
            case ButtonId.Save:
                SaveClicked?.Invoke();
                break;
            case ButtonId.Close:
                CloseClicked?.Invoke();
                break;
        }
    }

    private Point MenuPoint(Item item) => Orientation == Orientation.Vertical
        ? new Point(Width, item.Bounds.Top)
        : new Point(item.Bounds.Left, Height);

    private void ApplyStyle(StrokeStyle style)
    {
        _style = style;
        Invalidate();
        StrokeStyleChanged?.Invoke(style);
    }

    // ----- Menus de cor e espessura -----

    private ContextMenuStrip BuildColorMenu()
    {
        ContextMenuStrip menu = CreateMenu();
        foreach (Color color in QuickColors)
        {
            var swatch = new Bitmap(SwatchSize, SwatchSize);
            using (var g = Graphics.FromImage(swatch))
            using (var border = new Pen(_theme.Border))
            using (var brush = new SolidBrush(color))
            {
                g.FillRectangle(brush, 0, 0, SwatchSize, SwatchSize);
                g.DrawRectangle(border, 0, 0, SwatchSize - 1, SwatchSize - 1);
            }
            _swatches.Add(swatch);

            Color picked = color;
            var item = new ToolStripMenuItem(ToHex(color), swatch, (_, _) => ApplyStyle(_style with { Color = picked }))
            {
                ForeColor = _theme.Foreground,
                ImageScaling = ToolStripItemImageScaling.None,
            };
            menu.Items.Add(item);
        }

        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem(Strings.MoreColors, null, (_, _) => PickCustomColor())
        {
            ForeColor = _theme.Foreground,
        });
        return menu;
    }

    private void PickCustomColor()
    {
        using var dialog = new ColorDialog { Color = _style.Color, FullOpen = true, AnyColor = true };
        if (dialog.ShowDialog(FindForm()) == DialogResult.OK)
        {
            ApplyStyle(_style with { Color = Color.FromArgb(255, dialog.Color) });
        }
    }

    private ContextMenuStrip BuildThicknessMenu()
    {
        ContextMenuStrip menu = CreateMenu();
        foreach (int value in Thicknesses)
        {
            int picked = value;
            menu.Items.Add(new ToolStripMenuItem($"{value} px", null, (_, _) => ApplyStyle(_style with { Thickness = picked }))
            {
                ForeColor = _theme.Foreground,
                Tag = value,
            });
        }
        menu.Opening += (_, _) =>
        {
            foreach (ToolStripMenuItem item in menu.Items.OfType<ToolStripMenuItem>())
            {
                item.Checked = item.Tag is int t && t == _style.Thickness;
            }
        };
        return menu;
    }

    private ContextMenuStrip CreateMenu() => new()
    {
        BackColor = _theme.Background,
        ForeColor = _theme.Foreground,
        ShowCheckMargin = false,
        Renderer = new ToolStripProfessionalRenderer(new ThemedColorTable(_theme)) { RoundedEdges = false },
    };

    private static string ToHex(Color c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";

    // ----- Pintura -----

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        // Tudo é pintado em OnPaint.
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        using (var background = new SolidBrush(_theme.Background))
        {
            g.FillRectangle(background, ClientRectangle);
        }
        using (var border = new Pen(_theme.Border))
        {
            g.DrawRectangle(border, 0, 0, Width - 1, Height - 1);
            foreach (Rectangle s in _separators)
            {
                if (Orientation == Orientation.Vertical)
                {
                    int y = s.Top + s.Height / 2;
                    g.DrawLine(border, s.Left + 4, y, s.Right - 5, y);
                }
                else
                {
                    int x = s.Left + s.Width / 2;
                    g.DrawLine(border, x, s.Top + 4, x, s.Bottom - 5);
                }
            }
        }

        g.SmoothingMode = SmoothingMode.AntiAlias;
        for (int i = 0; i < _items.Count; i++)
        {
            Item item = _items[i];
            bool enabled = IsEnabled(item.Id);
            bool active = ToolFor(item.Id) is ToolKind t && t == _tool;

            Color back = active ? _theme.Accent
                : enabled && (i == _hover) ? _theme.Surface
                : _theme.Background;
            Color fore = active ? _theme.AccentForeground
                : enabled ? _theme.Foreground
                : Blend(_theme.Foreground, _theme.Background, 0.35);

            if (back != _theme.Background)
            {
                using var brush = new SolidBrush(back);
                g.FillRectangle(brush, Rectangle.Inflate(item.Bounds, -1, -1));
            }
            DrawIcon(g, item.Id, Rectangle.Inflate(item.Bounds, -IconInset, -IconInset), fore, back);
        }
    }

    private static Color Blend(Color a, Color b, double amountOfA) => Color.FromArgb(
        (int)(a.R * amountOfA + b.R * (1 - amountOfA)),
        (int)(a.G * amountOfA + b.G * (1 - amountOfA)),
        (int)(a.B * amountOfA + b.B * (1 - amountOfA)));

    /// <summary>Ícones vetoriais de 16×16 px.</summary>
    private void DrawIcon(Graphics g, ButtonId id, Rectangle r, Color fore, Color back)
    {
        using var pen = new Pen(fore, 2f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        using var brush = new SolidBrush(fore);
        int l = r.Left, t = r.Top, rt = r.Right, b = r.Bottom;
        int cx = l + r.Width / 2;

        switch (id)
        {
            case ButtonId.Pen:
                using (var body = new Pen(fore, 4f) { StartCap = LineCap.Flat, EndCap = LineCap.Round })
                {
                    g.DrawLine(body, l + 5, b - 5, rt - 2, t + 2);
                }
                g.FillPolygon(brush, new PointF[] { new(l + 1, b - 1), new(l + 2.5f, b - 6.5f), new(l + 6.5f, b - 2.5f) });
                break;

            case ButtonId.Line:
                g.DrawLine(pen, l + 1, b - 1, rt - 1, t + 1);
                break;

            case ButtonId.Arrow:
                g.DrawLine(pen, l + 1, b - 1, rt - 4, t + 4);
                g.FillPolygon(brush, new PointF[] { new(rt, t), new(rt - 8, t + 1), new(rt - 1, t + 8) });
                break;

            case ButtonId.Rectangle:
                g.DrawRectangle(pen, l + 1, t + 3, r.Width - 2, r.Height - 6);
                break;

            case ButtonId.Text:
                g.DrawLine(pen, l + 2, t + 2, rt - 2, t + 2);
                g.DrawLine(pen, cx, t + 2, cx, b - 1);
                break;

            case ButtonId.Color:
                using (var swatch = new SolidBrush(_style.Color))
                using (var outline = new Pen(fore, 1f))
                {
                    g.FillRectangle(swatch, l, t, r.Width, r.Height);
                    g.DrawRectangle(outline, l, t, r.Width, r.Height);
                }
                break;

            case ButtonId.Thickness:
            {
                float d = Math.Clamp(_style.Thickness + 2, 3, r.Width);
                g.FillEllipse(brush, cx - d / 2f, t + r.Height / 2f - d / 2f, d, d);
                break;
            }

            case ButtonId.Undo:
            case ButtonId.Redo:
                using (var arrowPen = new Pen(fore, 2f))
                using (var cap = new AdjustableArrowCap(3f, 3f, true))
                {
                    arrowPen.CustomEndCap = cap;
                    var arc = new Rectangle(l + 2, t + 3, r.Width - 4, r.Height - 4);
                    if (id == ButtonId.Undo)
                    {
                        g.DrawArc(arrowPen, arc, 0, -180); // da direita, por cima, até a esquerda
                    }
                    else
                    {
                        g.DrawArc(arrowPen, arc, 180, 180); // da esquerda, por cima, até a direita
                    }
                }
                break;

            case ButtonId.Copy:
                using (var thin = new Pen(fore, 1.5f))
                using (var cover = new SolidBrush(back))
                {
                    g.DrawRectangle(thin, l + 5, t + 1, 10, 10);
                    g.FillRectangle(cover, l + 1, t + 5, 10, 10);
                    g.DrawRectangle(thin, l + 1, t + 5, 10, 10);
                }
                break;

            case ButtonId.Save:
                g.DrawLine(pen, cx, t + 1, cx, b - 6);
                g.DrawLines(pen, new Point[] { new(cx - 4, b - 9), new(cx, b - 5), new(cx + 4, b - 9) });
                g.DrawLines(pen, new Point[] { new(l + 1, b - 5), new(l + 1, b - 1), new(rt - 1, b - 1), new(rt - 1, b - 5) });
                break;

            case ButtonId.Close:
                g.DrawLine(pen, l + 2, t + 2, rt - 2, b - 2);
                g.DrawLine(pen, rt - 2, t + 2, l + 2, b - 2);
                break;
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _tooltipTimer.Dispose();
            _toolTip.Dispose();
            _colorMenu.Dispose();
            _thicknessMenu.Dispose();
            foreach (Bitmap swatch in _swatches)
            {
                swatch.Dispose();
            }
        }
        base.Dispose(disposing);
    }

    /// <summary>Cores dos menus de cor/espessura seguindo o tema.</summary>
    private sealed class ThemedColorTable(Theme theme) : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground => theme.Background;
        public override Color ImageMarginGradientBegin => theme.Background;
        public override Color ImageMarginGradientMiddle => theme.Background;
        public override Color ImageMarginGradientEnd => theme.Background;
        public override Color MenuBorder => theme.Border;
        public override Color MenuItemBorder => theme.Accent;
        public override Color MenuItemSelected => theme.Surface;
        public override Color MenuItemSelectedGradientBegin => theme.Surface;
        public override Color MenuItemSelectedGradientEnd => theme.Surface;
        public override Color SeparatorDark => theme.Border;
        public override Color SeparatorLight => theme.Border;
        public override Color CheckBackground => theme.Accent;
        public override Color CheckSelectedBackground => theme.Accent;
        public override Color CheckPressedBackground => theme.Accent;
    }
}
