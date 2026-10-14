using System.Drawing.Drawing2D;
using Fragmo.Capture;
using Fragmo.Native;
using Fragmo.Settings;
using Grip = Fragmo.Overlay.SelectionGeometry.Handle;

namespace Fragmo.Overlay;

internal enum OverlayResult { Cancel, Copy, Save }

/// <summary>
/// Janela de tela cheia sobre a tela congelada, onde o usuário escolhe a área a capturar.
/// Tudo em pixels físicos: coordenadas do cliente = coordenadas do bitmap.
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

    private static readonly Color Accent = Color.FromArgb(0xFF, 0x6A, 0x1A);

    private enum State { Idle, Dragging, Selected }

    private readonly CapturedScreen _screen;
    private readonly AppSettings _settings;
    private readonly Font _font = new("Segoe UI", 9f);
    private readonly SolidBrush _veilBrush = new(Color.FromArgb(VeilAlpha, Color.Black));
    private readonly SolidBrush _accentBrush = new(Accent);
    private readonly SolidBrush _labelBrush = new(Color.FromArgb(200, 20, 20, 20));
    private readonly Pen _accentPen = new(Accent, 1);

    private State _state = State.Idle;
    private Rectangle _selection;
    private Grip _activeGrip = Grip.None;
    private Point _dragStart;
    private Rectangle _dragOriginal;
    private Point _cursor;
    private Rectangle _lastDirty;

    public SelectionOverlay(CapturedScreen screen, AppSettings settings)
    {
        _screen = screen;
        _settings = settings;

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
    }

    public OverlayResult Result { get; private set; } = OverlayResult.Cancel;

    /// <summary>Área escolhida, em coordenadas do bitmap.</summary>
    public Rectangle Selection => _state == State.Selected ? _selection : Rectangle.Empty;

    private bool HasSelection => _selection.Width > 0 && _selection.Height > 0;

    private Size ImageSize => _screen.Image.Size;

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

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        switch (e.KeyData)
        {
            case Keys.Escape:
                Finish(OverlayResult.Cancel);
                break;
            case Keys.Enter:
            case Keys.Control | Keys.C:
                if (_state == State.Selected) Finish(OverlayResult.Copy);
                break;
            case Keys.Control | Keys.S:
                if (_state == State.Selected) Finish(OverlayResult.Save);
                break;
            default:
                return;
        }
        e.Handled = true;
        e.SuppressKeyPress = true;
    }

    // ----- Mouse -----

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        _cursor = e.Location;

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

        Grip grip = _state == State.Selected ? SelectionGeometry.HitTest(_selection, e.Location, GripSize) : Grip.None;
        if (grip != Grip.None)
        {
            _activeGrip = grip;
            _dragStart = e.Location;
            _dragOriginal = _selection;
            return;
        }

        _state = State.Dragging;
        _dragStart = e.Location;
        _selection = Rectangle.Empty;
        UpdateDirty();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        _cursor = e.Location;

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
            Cursor = CursorFor(SelectionGeometry.HitTest(_selection, e.Location, GripSize));
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

        bool wasDragging = _state == State.Dragging || _activeGrip != Grip.None;
        _activeGrip = Grip.None;
        if (!wasDragging)
        {
            return;
        }

        if (HasSelection)
        {
            _state = State.Selected;
            Cursor = CursorFor(SelectionGeometry.HitTest(_selection, e.Location, GripSize));
            UpdateDirty();
        }
        else
        {
            ClearSelection();
        }
    }

    protected override void OnMouseDoubleClick(MouseEventArgs e)
    {
        base.OnMouseDoubleClick(e);
        if (e.Button == MouseButtons.Left && _state == State.Selected && _selection.Contains(e.Location))
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
        UpdateDirty();
    }

    private void Finish(OverlayResult result)
    {
        Result = result;
        DialogResult = result == OverlayResult.Cancel ? DialogResult.Cancel : DialogResult.OK;
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

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _font.Dispose();
            _veilBrush.Dispose();
            _accentBrush.Dispose();
            _labelBrush.Dispose();
            _accentPen.Dispose();
        }
        base.Dispose(disposing);
    }
}
