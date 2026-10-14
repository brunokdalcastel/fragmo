using System.Drawing.Imaging;

namespace Fragmo.Capture;

/// <summary>Imagem da tela virtual inteira e a posição dela (a origem pode ser negativa).</summary>
internal sealed record CapturedScreen(Bitmap Image, Rectangle VirtualBounds) : IDisposable
{
    public void Dispose() => Image.Dispose();
}

internal static class ScreenCapturer
{
    public static CapturedScreen CaptureVirtualScreen()
    {
        Rectangle vs = SystemInformation.VirtualScreen;
        var bmp = new Bitmap(vs.Width, vs.Height, PixelFormat.Format32bppArgb);
        try
        {
            using (var g = Graphics.FromImage(bmp))
            {
                g.CopyFromScreen(vs.Left, vs.Top, 0, 0, vs.Size, CopyPixelOperation.SourceCopy);
            }
            return new CapturedScreen(bmp, vs);
        }
        catch
        {
            bmp.Dispose();
            throw;
        }
    }

    public static Bitmap Crop(Bitmap source, Rectangle bitmapRect)
    {
        Rectangle rect = Rectangle.Intersect(bitmapRect, new Rectangle(Point.Empty, source.Size));
        if (rect.Width <= 0 || rect.Height <= 0)
        {
            throw new ArgumentException("A área selecionada está fora da imagem.", nameof(bitmapRect));
        }
        return source.Clone(rect, PixelFormat.Format32bppArgb);
    }
}
