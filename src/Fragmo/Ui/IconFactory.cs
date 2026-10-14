using System.Drawing.Drawing2D;
using Fragmo.Native;

namespace Fragmo.Ui;

/// <summary>
/// Desenha o ícone do app em GDI+: quadrado arredondado na cor de destaque com um colchete de recorte branco.
/// </summary>
internal static class IconFactory
{
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

    /// <summary>Grava um <c>.ico</c> com um PNG por tamanho (ICONDIR + ICONDIRENTRY + bytes de cada PNG).</summary>
    public static void SaveIco(string path, params int[] sizes)
    {
        var images = new List<byte[]>(sizes.Length);
        foreach (int size in sizes)
        {
            using var bmp = new Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                Draw(g, size);
            }
            using var png = new MemoryStream();
            bmp.Save(png, System.Drawing.Imaging.ImageFormat.Png);
            images.Add(png.ToArray());
        }

        const int HeaderSize = 6;
        const int EntrySize = 16;
        using var file = File.Create(path);
        using var w = new BinaryWriter(file);
        w.Write((ushort)0);            // reservado
        w.Write((ushort)1);            // tipo: ícone
        w.Write((ushort)sizes.Length);

        int offset = HeaderSize + EntrySize * sizes.Length;
        for (int i = 0; i < sizes.Length; i++)
        {
            byte dim = sizes[i] >= 256 ? (byte)0 : (byte)sizes[i]; // 0 significa 256
            w.Write(dim);                  // largura
            w.Write(dim);                  // altura
            w.Write((byte)0);              // cores na paleta
            w.Write((byte)0);              // reservado
            w.Write((ushort)1);            // planos
            w.Write((ushort)32);           // bits por pixel
            w.Write(images[i].Length);
            w.Write(offset);
            offset += images[i].Length;
        }

        foreach (byte[] image in images)
        {
            w.Write(image);
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
        using (var fill = new SolidBrush(Palette.Accent))
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
