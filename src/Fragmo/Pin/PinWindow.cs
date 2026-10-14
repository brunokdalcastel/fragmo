using System.Drawing.Drawing2D;
using Fragmo.Native;
using Fragmo.Output;

namespace Fragmo.Pin;

/// <summary>
/// Recorte "fixado" na tela: janela sem borda, sempre por cima, que pode ser movida, ampliada e
/// ficar translúcida. Passa a ser dona do bitmap e o descarta ao fechar.
/// </summary>
internal sealed class PinWindow : Form
{
    private const int BorderWidth = 1;
    private const double MinZoom = 0.25;
    private const double MaxZoom = 4.0;
    private const double ZoomStep = 0.10;
    private const double MinOpacity = 0.20;
    private const double OpacityStep = 0.10;

    private static readonly Color Accent = Color.FromArgb(0xFF, 0x6A, 0x1A);
    private static readonly Point AnchorTopLeft = Point.Empty;

    private readonly Bitmap _image;
    private readonly OutputService _output;
    private readonly ContextMenuStrip _menu;
    private readonly Pen _borderPen = new(Accent, BorderWidth);

    private double _zoom = 1.0;
    private bool _dragging;
    private Point _dragCursor;
    private Point _dragWindow;

    /// <param name="screenLocation">Canto superior esquerdo da imagem, em coordenadas de tela.</param>
    public PinWindow(Bitmap image, Point screenLocation, OutputService output)
    {
        _image = image;
        _output = output;

        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        AutoScaleMode = AutoScaleMode.None;
        KeyPreview = true;
        Text = Strings.AppName;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);

        // A borda fica por fora, para a imagem continuar exatamente onde estava na tela.
        Bounds = new Rectangle(
            screenLocation.X - BorderWidth,
            screenLocation.Y - BorderWidth,
            image.Width + 2 * BorderWidth,
            image.Height + 2 * BorderWidth);

        _menu = BuildMenu();
        ContextMenuStrip = _menu;
    }

    /// <summary>Mensagem para o balão da bandeja (copiado, salvo em…).</summary>
    public event Action<string>? Info;

    private ContextMenuStrip BuildMenu()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add(Strings.PinCopy, null, (_, _) =>
        {
            _output.CopyToClipboard(_image);
            Info?.Invoke(Strings.Copied);
        });
        menu.Items.Add(Strings.PinSave, null, (_, _) => SaveImage());
        menu.Items.Add(Strings.PinZoomReset, null, (_, _) => SetZoom(1.0, AnchorTopLeft));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(Strings.PinClose, null, (_, _) => Close());
        return menu;
    }

    private void SaveImage()
    {
        // TopMost cobriria o diálogo de salvar.
        TopMost = false;
        try
        {
            string? path = _output.Save(_image, this);
            if (path is not null)
            {
                Info?.Invoke(string.Format(Strings.SavedTo, path));
            }
        }
        catch (Exception ex)
        {
            Info?.Invoke(string.Format(Strings.CaptureError, ex.Message));
        }
        finally
        {
            TopMost = true;
        }
    }

    // ----- Teclado -----

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyCode == Keys.Escape)
        {
            e.Handled = true;
            Close();
        }
    }

    // ----- Mouse: arrastar, zoom e opacidade -----

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button == MouseButtons.Left)
        {
            _dragging = true;
            _dragCursor = Cursor.Position;
            _dragWindow = Location;
            Cursor = Cursors.SizeAll;
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_dragging)
        {
            // Posição do cursor na tela, para não "tremer" enquanto a janela anda.
            Point now = Cursor.Position;
            Location = new Point(_dragWindow.X + now.X - _dragCursor.X, _dragWindow.Y + now.Y - _dragCursor.Y);
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.Button == MouseButtons.Left)
        {
            _dragging = false;
            Cursor = Cursors.Default;
        }
    }

    protected override void OnMouseDoubleClick(MouseEventArgs e)
    {
        base.OnMouseDoubleClick(e);
        if (e.Button == MouseButtons.Left)
        {
            Close();
        }
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        int direction = Math.Sign(e.Delta);
        if (direction == 0)
        {
            return;
        }

        if ((ModifierKeys & Keys.Control) == Keys.Control)
        {
            Opacity = Math.Clamp(Math.Round(Opacity + direction * OpacityStep, 2), MinOpacity, 1.0);
            return;
        }
        SetZoom(Math.Round(_zoom + direction * ZoomStep, 2), e.Location);
    }

    /// <summary>Muda o zoom mantendo fixo o ponto <paramref name="anchor"/> (coordenadas do cliente).</summary>
    private void SetZoom(double zoom, Point anchor)
    {
        zoom = Math.Clamp(zoom, MinZoom, MaxZoom);
        if (Math.Abs(zoom - _zoom) < 0.001)
        {
            return;
        }

        double ratio = zoom / _zoom;
        _zoom = zoom;
        var size = new Size(
            Math.Max(1, (int)Math.Round(_image.Width * zoom)) + 2 * BorderWidth,
            Math.Max(1, (int)Math.Round(_image.Height * zoom)) + 2 * BorderWidth);
        var location = new Point(
            Left + anchor.X - (int)Math.Round(anchor.X * ratio),
            Top + anchor.Y - (int)Math.Round(anchor.Y * ratio));
        Bounds = new Rectangle(location, size);
        Invalidate();
    }

    // ----- Janela -----

    protected override void WndProc(ref Message m)
    {
        // O tamanho é sempre em pixels físicos: não deixa o WinForms reescalar ao trocar de monitor.
        if (m.Msg == User32.WM_DPICHANGED)
        {
            return;
        }
        base.WndProc(ref m);
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        // Tudo é pintado em OnPaint.
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        var dest = new Rectangle(BorderWidth, BorderWidth, ClientSize.Width - 2 * BorderWidth, ClientSize.Height - 2 * BorderWidth);
        if (_zoom >= 1.0)
        {
            // Ampliado: pixels nítidos, como numa lupa.
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = PixelOffsetMode.Half;
        }
        else
        {
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        }
        g.DrawImage(_image, dest, 0, 0, _image.Width, _image.Height, GraphicsUnit.Pixel);

        g.PixelOffsetMode = PixelOffsetMode.Default;
        g.DrawRectangle(_borderPen, 0, 0, ClientSize.Width - 1, ClientSize.Height - 1);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _menu.Dispose();
            _borderPen.Dispose();
            _image.Dispose();
        }
        base.Dispose(disposing);
    }
}
