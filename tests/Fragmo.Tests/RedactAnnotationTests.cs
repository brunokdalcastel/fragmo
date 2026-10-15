using Fragmo.Editor;

namespace Fragmo.Tests;

public sealed class RedactAnnotationTests
{
    private static Bitmap TextImage(string text)
    {
        var bmp = new Bitmap(200, 40);
        using var g = Graphics.FromImage(bmp);
        g.Clear(Color.White);
        using var font = new Font("Segoe UI", 14f);
        g.DrawString(text, font, Brushes.Black, 5, 5);
        return bmp;
    }

    [Fact]
    public void CoveredArea_IsSolidOpaqueBlack()
    {
        using Bitmap source = TextImage("529.982.247-25");
        var area = new Rectangle(0, 0, 200, 40);

        using Bitmap result = ImageComposer.Compose(source, [new RedactAnnotation(new Rectangle(10, 5, 150, 30))], area);

        for (int y = 5; y < 35; y++)
        {
            for (int x = 10; x < 160; x++)
            {
                Assert.Equal(Color.Black.ToArgb(), result.GetPixel(x, y).ToArgb());
            }
        }
    }

    [Fact]
    public void CoveredArea_KeepsNothingFromTheOriginal()
    {
        // Ao contrário da pixelização, textos diferentes ficam idênticos debaixo da tarja.
        using Bitmap a = TextImage("529.982.247-25");
        using Bitmap b = TextImage("123.456.789-09");
        var redact = new RedactAnnotation(new Rectangle(0, 0, 200, 40));
        var area = new Rectangle(0, 0, 200, 40);

        using Bitmap ra = ImageComposer.Compose(a, [redact], area);
        using Bitmap rb = ImageComposer.Compose(b, [redact], area);

        for (int y = 0; y < 40; y++)
        {
            for (int x = 0; x < 200; x++)
            {
                Assert.Equal(ra.GetPixel(x, y).ToArgb(), rb.GetPixel(x, y).ToArgb());
            }
        }
    }

    [Fact]
    public void OutsideArea_IsUnchanged()
    {
        using Bitmap source = TextImage("ana@exemplo.com");
        var area = new Rectangle(0, 0, 200, 40);

        using Bitmap result = ImageComposer.Compose(source, [new RedactAnnotation(new Rectangle(100, 0, 100, 40))], area);

        for (int y = 0; y < 40; y++)
        {
            for (int x = 0; x < 100; x++)
            {
                Assert.Equal(source.GetPixel(x, y).ToArgb(), result.GetPixel(x, y).ToArgb());
            }
        }
    }

    [Fact]
    public void Group_OfRedactions_IsUndoneInOneStep()
    {
        var history = new AnnotationHistory();
        history.Add(new AnnotationGroup([new RedactAnnotation(new Rectangle(0, 0, 5, 5)), new RedactAnnotation(new Rectangle(9, 9, 5, 5))]));

        history.Undo();

        Assert.Empty(history.Items);
    }
}
