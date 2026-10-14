using Fragmo.Editor;

namespace Fragmo.Tests;

public sealed class ImageComposerTests
{
    private static Bitmap WhiteBitmap(int size)
    {
        var bmp = new Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        g.Clear(Color.White);
        return bmp;
    }

    private static bool IsRed(Color c) => c.R > 230 && c.G < 40 && c.B < 40;

    [Fact]
    public void Compose_DrawsAnnotationAndCropsToSelection()
    {
        using Bitmap source = WhiteBitmap(100);
        // Contorno de 4px em x = 20..40, y = 20..40 (coordenadas do bitmap base).
        var rect = new RectangleAnnotation(new StrokeStyle(Color.Red, 4), new Rectangle(20, 20, 20, 20));

        using Bitmap result = ImageComposer.Compose(source, [rect], new Rectangle(10, 10, 50, 50));

        Assert.Equal(new Size(50, 50), result.Size);
        Assert.True(IsRed(result.GetPixel(10, 20)), "borda esquerda (x = 20 no base) deveria estar vermelha");
        Assert.True(IsRed(result.GetPixel(30, 20)), "borda direita (x = 40 no base) deveria estar vermelha");
        Assert.Equal(Color.White.ToArgb(), result.GetPixel(20, 20).ToArgb()); // interior continua branco
        Assert.Equal(Color.White.ToArgb(), result.GetPixel(0, 0).ToArgb());
    }

    [Fact]
    public void Compose_DoesNotChangeBaseImage()
    {
        using Bitmap source = WhiteBitmap(100);
        var rect = new RectangleAnnotation(new StrokeStyle(Color.Red, 4), new Rectangle(20, 20, 20, 20));

        using Bitmap result = ImageComposer.Compose(source, [rect], new Rectangle(0, 0, 100, 100));

        Assert.Equal(Color.White.ToArgb(), source.GetPixel(20, 30).ToArgb());
    }

    [Fact]
    public void Compose_SelectionOutsideBitmap_Throws()
    {
        using Bitmap source = WhiteBitmap(100);

        Assert.Throws<ArgumentException>(() =>
            ImageComposer.Compose(source, [], new Rectangle(200, 200, 50, 50)));
    }
}
