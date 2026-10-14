using System.Drawing.Imaging;
using Fragmo.Editor;

namespace Fragmo.Tests;

public sealed class BlurAnnotationTests
{
    private const int Block = 12;

    /// <summary>48×24: metade esquerda preta (x &lt; 24) e metade direita branca.</summary>
    private static Bitmap HalfBlackHalfWhite()
    {
        var bmp = new Bitmap(48, 24, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        g.Clear(Color.White);
        g.FillRectangle(Brushes.Black, 0, 0, 24, 24);
        return bmp;
    }

    [Fact]
    public void Draw_OverTheBorder_MakesEachBlockUniform()
    {
        using Bitmap source = HalfBlackHalfWhite();
        using var target = new Bitmap(source);
        // Blocos em x = 6..18, 18..30 (este atravessa a divisa em x = 24) e 30..42.
        var blur = new BlurAnnotation(new Rectangle(6, 0, 36, 24), Block);

        using (var g = Graphics.FromImage(target))
        {
            blur.Draw(g, source);
        }

        for (int bx = 6; bx < 42; bx += Block)
        {
            for (int by = 0; by < 24; by += Block)
            {
                int expected = target.GetPixel(bx, by).ToArgb();
                for (int x = bx; x < bx + Block; x++)
                {
                    for (int y = by; y < by + Block; y++)
                    {
                        Assert.Equal(expected, target.GetPixel(x, y).ToArgb());
                    }
                }
            }
        }

        // O bloco sobre a divisa vira uma mistura: nem preto nem branco.
        Color mixed = target.GetPixel(20, 5);
        Assert.NotEqual(Color.Black.ToArgb(), mixed.ToArgb());
        Assert.NotEqual(Color.White.ToArgb(), mixed.ToArgb());
    }

    [Fact]
    public void Draw_LeavesPixelsOutsideTheAreaUntouched()
    {
        using Bitmap source = HalfBlackHalfWhite();
        using var target = new Bitmap(source);
        var blur = new BlurAnnotation(new Rectangle(6, 0, 36, 24), Block);

        using (var g = Graphics.FromImage(target))
        {
            blur.Draw(g, source);
        }

        Assert.Equal(Color.Black.ToArgb(), target.GetPixel(5, 10).ToArgb());
        Assert.Equal(Color.White.ToArgb(), target.GetPixel(42, 10).ToArgb());
    }
}
