using System.Drawing.Drawing2D;
using Fragmo.Native;

namespace Fragmo.Ui;

/// <summary>
/// Desenha o ícone do app em GDI+: quadrado arredondado laranja com um colchete de recorte branco.
/// </summary>
internal static class IconFactory
{
    public static readonly Color Accent = ColorTranslator.FromHtml("#FF6A1A");

    public static Icon CreateAppIcon(int size)
    {
        using var bmp = new Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            Draw(g, size);
        }

        IntPtr hIcon = bmp.GetHicon();
        try
        {
            using var temp = Icon.FromHandle(hIcon);
            return (Icon)temp.Clone();
        }
        finally
        {
            User32.DestroyIcon(hIcon);
        }
    }

    private static void Draw(Graphics g, int size)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.Clear(Color.Transparent);

        float s = size;
        float radius = s * 0.22f;
        var body = new RectangleF(0.5f, 0.5f, s - 1f, s - 1f);
        using (var path = RoundedRect(body, radius))
        using (var fill = new SolidBrush(Accent))
        {
            g.FillPath(fill, path);
        }

        // Colchete de recorte: dois cantos em L (superior esquerdo e inferior direito).
        float stroke = Math.Max(1.5f, s * 0.11f);
        float inset = s * 0.24f;
        float arm = s * 0.30f;
        float near = inset;
        float far = s - inset;

        using var pen = new Pen(Color.White, stroke)
        {
            StartCap = LineCap.Square,
            EndCap = LineCap.Square,
            LineJoin = LineJoin.Miter,
        };
        g.DrawLines(pen, new[]
        {
            new PointF(near, near + arm),
            new PointF(near, near),
            new PointF(near + arm, near),
        });
        g.DrawLines(pen, new[]
        {
            new PointF(far - arm, far),
            new PointF(far, far),
            new PointF(far, far - arm),
        });
    }

    private static GraphicsPath RoundedRect(RectangleF r, float radius)
    {
        float d = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(r.Left, r.Top, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Top, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.Left, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}
