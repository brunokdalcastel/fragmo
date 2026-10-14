using Fragmo.Output;

namespace Fragmo.Tests;

public sealed class PrintLayoutTests
{
    private static readonly Rectangle Page = new(100, 100, 650, 900);

    [Fact]
    public void Fit_LargerImage_ShrinksKeepingRatioAndCenters()
    {
        Rectangle r = PrintLayout.Fit(new Size(1300, 1000), Page);

        Assert.Equal(new Size(650, 500), r.Size);
        Assert.Equal(new Point(100, 300), r.Location); // (900 - 500) / 2 = 200 de folga em cima
    }

    [Fact]
    public void Fit_SmallerImage_KeepsOriginalSizeCentered()
    {
        Rectangle r = PrintLayout.Fit(new Size(200, 100), Page);

        Assert.Equal(new Size(200, 100), r.Size);
        Assert.Equal(new Point(100 + 225, 100 + 400), r.Location);
    }

    [Fact]
    public void Fit_VeryWideImage_FitsWidth()
    {
        Rectangle r = PrintLayout.Fit(new Size(6500, 100), Page);

        Assert.Equal(650, r.Width);
        Assert.Equal(10, r.Height);
        Assert.Equal(Page.X, r.X);
        Assert.Equal(Page.Y + (900 - 10) / 2, r.Y);
        Assert.True(Page.Contains(r));
    }

    [Fact]
    public void Fit_EmptyImage_ReturnsEmpty()
    {
        Assert.Equal(Rectangle.Empty, PrintLayout.Fit(Size.Empty, Page));
    }
}
