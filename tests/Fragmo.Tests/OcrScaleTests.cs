using Fragmo.Ocr;

namespace Fragmo.Tests;

public sealed class OcrScaleTests
{
    private const int Max = 10000;

    [Fact]
    public void SmallImage_IsEnlarged()
    {
        Assert.Equal(2.0, OcrService.ChooseScale(new Size(800, 300), Max));
    }

    [Fact]
    public void LargeImage_KeepsSize()
    {
        Assert.Equal(1.0, OcrService.ChooseScale(new Size(2560, 1440), Max));
    }

    [Fact]
    public void ImageAboveLimit_IsReducedToFit()
    {
        double scale = OcrService.ChooseScale(new Size(20000, 1000), Max);

        Assert.Equal(0.5, scale);
    }

    [Fact]
    public void ImageThatWouldPassLimitWhenEnlarged_KeepsSize()
    {
        Assert.Equal(1.0, OcrService.ChooseScale(new Size(6000, 100), Max));
    }

    [Fact]
    public void EmptyImage_KeepsScale()
    {
        Assert.Equal(1.0, OcrService.ChooseScale(Size.Empty, Max));
    }
}
