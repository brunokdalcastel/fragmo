using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace Fragmo.Editor;

/// <summary>Utilidades comuns às anotações: caneta arredondada e limites com margem.</summary>
internal static class AnnotationDrawing
{
    public const int BoundsMargin = 2;

    public static Pen CreatePen(StrokeStyle style) => new(style.Color, style.Thickness)
    {
        StartCap = LineCap.Round,
        EndCap = LineCap.Round,
        LineJoin = LineJoin.Round,
    };

    /// <summary>Retângulo que envolve os pontos, aumentado em <paramref name="pad"/> de cada lado.</summary>
    public static Rectangle Around(IEnumerable<Point> points, int pad)
    {
        int left = int.MaxValue, top = int.MaxValue, right = int.MinValue, bottom = int.MinValue;
        foreach (Point p in points)
        {
            left = Math.Min(left, p.X);
            top = Math.Min(top, p.Y);
            right = Math.Max(right, p.X);
            bottom = Math.Max(bottom, p.Y);
        }
        if (left == int.MaxValue)
        {
            return Rectangle.Empty;
        }
        return Rectangle.FromLTRB(left - pad, top - pad, right + pad + 1, bottom + pad + 1);
    }

    public static int Pad(StrokeStyle style) => style.Thickness + BoundsMargin;
}

/// <summary>Traço livre.</summary>
public sealed class PenAnnotation(StrokeStyle style, List<Point> points) : IAnnotation
{
    public StrokeStyle Style { get; } = style;

    public IReadOnlyList<Point> Points => points;

    public Rectangle Bounds => AnnotationDrawing.Around(points, AnnotationDrawing.Pad(Style));

    public void Draw(Graphics g, Bitmap source)
    {
        if (points.Count == 0)
        {
            return;
        }

        g.SmoothingMode = SmoothingMode.AntiAlias;
        if (points.Count == 1)
        {
            float d = Style.Thickness;
            using var brush = new SolidBrush(Style.Color);
            g.FillEllipse(brush, points[0].X - d / 2f, points[0].Y - d / 2f, d, d);
            return;
        }

        using Pen pen = AnnotationDrawing.CreatePen(Style);
        g.DrawLines(pen, points.ToArray());
    }
}

/// <summary>Linha reta.</summary>
public sealed class LineAnnotation(StrokeStyle style, Point a, Point b) : IAnnotation
{
    public StrokeStyle Style { get; } = style;

    public Rectangle Bounds => AnnotationDrawing.Around([a, b], AnnotationDrawing.Pad(Style));

    public void Draw(Graphics g, Bitmap source)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using Pen pen = AnnotationDrawing.CreatePen(Style);
        g.DrawLine(pen, a, b);
    }
}

/// <summary>Seta com ponta triangular preenchida em <c>b</c>.</summary>
public sealed class ArrowAnnotation(StrokeStyle style, Point a, Point b) : IAnnotation
{
    public StrokeStyle Style { get; } = style;

    private int HeadSize => Math.Max(10, Style.Thickness * 4);

    public Rectangle Bounds => AnnotationDrawing.Around([a, b], AnnotationDrawing.Pad(Style) + HeadSize);

    public void Draw(Graphics g, Bitmap source)
    {
        float dx = b.X - a.X, dy = b.Y - a.Y;
        float length = MathF.Sqrt(dx * dx + dy * dy);
        if (length < 1f)
        {
            return;
        }

        g.SmoothingMode = SmoothingMode.AntiAlias;
        float ux = dx / length, uy = dy / length;
        float head = Math.Min(HeadSize, length);
        float half = head * 0.45f;
        var baseCenter = new PointF(b.X - ux * head, b.Y - uy * head);

        // A linha para na base da ponta, para a espessura não "vazar" pela ponta.
        using (Pen pen = AnnotationDrawing.CreatePen(Style))
        {
            g.DrawLine(pen, a.X, a.Y, baseCenter.X + ux, baseCenter.Y + uy);
        }

        PointF[] triangle =
        [
            new(b.X, b.Y),
            new(baseCenter.X - uy * half, baseCenter.Y + ux * half),
            new(baseCenter.X + uy * half, baseCenter.Y - ux * half),
        ];
        using var brush = new SolidBrush(Style.Color);
        g.FillPolygon(brush, triangle);
    }
}

/// <summary>Contorno de retângulo.</summary>
public sealed class RectangleAnnotation(StrokeStyle style, Rectangle rect) : IAnnotation
{
    public StrokeStyle Style { get; } = style;

    public Rectangle Rect { get; } = rect;

    public Rectangle Bounds => Rectangle.Inflate(Rect, AnnotationDrawing.Pad(Style), AnnotationDrawing.Pad(Style));

    public void Draw(Graphics g, Bitmap source)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using Pen pen = AnnotationDrawing.CreatePen(Style);
        g.DrawRectangle(pen, Rect);
    }
}

/// <summary>Texto em Segoe UI semibold com contorno de 1px na cor contrastante.</summary>
public sealed class TextAnnotation : IAnnotation
{
    private const float OutlineWidth = 2f; // centrado no contorno: 1px para fora

    public TextAnnotation(Color color, float fontSize, Point origin, string text)
    {
        Color = color;
        FontSize = fontSize;
        Origin = origin;
        Text = text.Replace("\r\n", "\n");

        using GraphicsPath path = BuildPath();
        RectangleF b = path.GetBounds();
        int pad = (int)OutlineWidth + AnnotationDrawing.BoundsMargin;
        Bounds = b.IsEmpty
            ? Rectangle.Empty
            : Rectangle.Inflate(Rectangle.Round(b), pad, pad);
    }

    public Color Color { get; }

    public float FontSize { get; }

    public Point Origin { get; }

    public string Text { get; }

    public Rectangle Bounds { get; }

    /// <summary>Preto sobre cores claras, branco sobre escuras.</summary>
    public static Color ContrastColor(Color c) =>
        0.299 * c.R + 0.587 * c.G + 0.114 * c.B > 140 ? Color.Black : Color.White;

    public static FontFamily CreateFamily()
    {
        try
        {
            return new FontFamily("Segoe UI Semibold");
        }
        catch (ArgumentException)
        {
            return new FontFamily(GenericFontFamilies.SansSerif);
        }
    }

    public void Draw(Graphics g, Bitmap source)
    {
        using GraphicsPath path = BuildPath();
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using (var outline = new Pen(ContrastColor(Color), OutlineWidth) { LineJoin = LineJoin.Round })
        {
            g.DrawPath(outline, path);
        }
        using var brush = new SolidBrush(Color);
        g.FillPath(brush, path);
    }

    private GraphicsPath BuildPath()
    {
        var path = new GraphicsPath();
        using FontFamily family = CreateFamily();
        using var format = (StringFormat)StringFormat.GenericTypographic.Clone();
        path.AddString(Text, family, (int)FontStyle.Regular, FontSize, Origin, format);
        return path;
    }
}

/// <summary>Marca-texto: traço largo e translúcido, com pontas retas.</summary>
public sealed class MarkerAnnotation(Color color, int thickness, List<Point> points) : IAnnotation
{
    private const int Alpha = 90;

    public Color Color { get; } = color;

    public int Thickness { get; } = thickness;

    public IReadOnlyList<Point> Points => points;

    private int Width => Thickness * 4;

    public Rectangle Bounds => AnnotationDrawing.Around(points, PadFor(Thickness));

    /// <summary>Margem para invalidar um trecho do traço com essa espessura.</summary>
    public static int PadFor(int thickness) => thickness * 4 + AnnotationDrawing.BoundsMargin;

    public void Draw(Graphics g, Bitmap source)
    {
        if (points.Count == 0)
        {
            return;
        }

        Color color = Color.FromArgb(Alpha, Color);
        if (points.Count == 1)
        {
            float half = Width / 2f;
            using var brush = new SolidBrush(color);
            g.FillRectangle(brush, points[0].X - half, points[0].Y - half, Width, Width);
            return;
        }

        g.SmoothingMode = SmoothingMode.AntiAlias;
        // Um único DrawLines: os trechos que se cruzam não escurecem.
        using var pen = new Pen(color, Width)
        {
            StartCap = LineCap.Square,
            EndCap = LineCap.Square,
            LineJoin = LineJoin.Round,
        };
        g.DrawLines(pen, points.ToArray());
    }
}

/// <summary>
/// Pixelização de uma área. Sempre lê o bitmap <b>base</b>, então nunca revela o conteúdo original
/// (nem o que estiver anotado por baixo).
/// </summary>
public sealed class BlurAnnotation(Rectangle r, int blockSize = 12) : IAnnotation
{
    public Rectangle Rect { get; } = r;

    public int BlockSize { get; } = Math.Max(2, blockSize);

    public Rectangle Bounds => Rect;

    public void Draw(Graphics g, Bitmap source)
    {
        Rectangle area = Rectangle.Intersect(Rect, new Rectangle(Point.Empty, source.Size));
        if (area.Width <= 0 || area.Height <= 0)
        {
            return;
        }

        int smallWidth = (area.Width + BlockSize - 1) / BlockSize;
        int smallHeight = (area.Height + BlockSize - 1) / BlockSize;

        using var small = new Bitmap(smallWidth, smallHeight, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using (var sg = Graphics.FromImage(small))
        using (var attributes = new System.Drawing.Imaging.ImageAttributes())
        {
            // Reduz com média dos pixels; o espelhamento evita bordas "puxando" transparência.
            attributes.SetWrapMode(WrapMode.TileFlipXY);
            sg.InterpolationMode = InterpolationMode.HighQualityBilinear;
            sg.PixelOffsetMode = PixelOffsetMode.HighQuality;
            sg.DrawImage(source, new Rectangle(0, 0, smallWidth, smallHeight),
                area.X, area.Y, area.Width, area.Height, GraphicsUnit.Pixel, attributes);
        }

        GraphicsState state = g.Save();
        g.InterpolationMode = InterpolationMode.NearestNeighbor;
        g.PixelOffsetMode = PixelOffsetMode.Half;
        g.CompositingMode = CompositingMode.SourceCopy;
        g.DrawImage(small, area, 0, 0, smallWidth, smallHeight, GraphicsUnit.Pixel);
        g.Restore(state);
    }
}

/// <summary>Passo numerado: círculo preenchido com o número em branco no centro.</summary>
public sealed class StepAnnotation(Color color, int number, Point center, int radius) : IAnnotation
{
    public Color Color { get; } = color;

    public int Number { get; } = number;

    public Point Center { get; } = center;

    public int Radius { get; } = radius;

    public Rectangle Bounds => Rectangle.Inflate(Circle, AnnotationDrawing.BoundsMargin, AnnotationDrawing.BoundsMargin);

    private Rectangle Circle => new(Center.X - Radius, Center.Y - Radius, 2 * Radius, 2 * Radius);

    public void Draw(Graphics g, Bitmap source)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using (var brush = new SolidBrush(Color))
        {
            g.FillEllipse(brush, Circle);
        }

        string text = Number.ToString(System.Globalization.CultureInfo.InvariantCulture);
        float size = Radius * (text.Length > 1 ? 0.95f : 1.2f);
        using FontFamily family = TextAnnotation.CreateFamily();
        using var path = new GraphicsPath();
        using var format = (StringFormat)StringFormat.GenericTypographic.Clone();
        path.AddString(text, family, (int)FontStyle.Regular, size, Point.Empty, format);

        // Centraliza pela forma real dos algarismos (sem o espaço do topo da fonte).
        RectangleF b = path.GetBounds();
        using (var move = new Matrix())
        {
            move.Translate(Center.X - (b.X + b.Width / 2f), Center.Y - (b.Y + b.Height / 2f));
            path.Transform(move);
        }
        g.FillPath(Brushes.White, path);
    }
}

/// <summary>Várias anotações que entram e saem do histórico juntas (uma etapa de desfazer).</summary>
public sealed class AnnotationGroup(IReadOnlyList<IAnnotation> items) : IAnnotation
{
    public IReadOnlyList<IAnnotation> Items { get; } = items;

    public Rectangle Bounds => Items.Count == 0
        ? Rectangle.Empty
        : Items.Skip(1).Aggregate(Items[0].Bounds, (acc, item) => Rectangle.Union(acc, item.Bounds));

    public void Draw(Graphics g, Bitmap source)
    {
        foreach (IAnnotation item in Items)
        {
            item.Draw(g, source);
        }
    }
}

/// <summary>
/// Tarja: retângulo sólido e opaco gravado na imagem. Diferente do desfoque (pixelização), não guarda
/// nada do conteúdo original, então é a forma segura de esconder senhas, documentos e dados pessoais.
/// </summary>
public sealed class RedactAnnotation(Rectangle r) : IAnnotation
{
    public static readonly Color Fill = Color.Black;

    public Rectangle Rect { get; } = r;

    public Rectangle Bounds => Rect;

    public void Draw(Graphics g, Bitmap source)
    {
        GraphicsState state = g.Save();
        g.SmoothingMode = SmoothingMode.None;
        g.CompositingMode = CompositingMode.SourceCopy;
        using var brush = new SolidBrush(Fill);
        g.FillRectangle(brush, Rect);
        g.Restore(state);
    }
}
