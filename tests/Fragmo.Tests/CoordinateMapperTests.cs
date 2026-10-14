using Fragmo.Capture;

namespace Fragmo.Tests;

public sealed class CoordinateMapperTests
{
    // Monitor à esquerda e um pouco acima do principal.
    private static readonly Rectangle Virtual = new(-1920, -200, 3840, 1280);

    [Fact]
    public void ScreenToBitmap_VirtualOrigin_IsZero()
    {
        Assert.Equal(new Point(0, 0), CoordinateMapper.ScreenToBitmap(new Point(-1920, -200), Virtual));
    }

    [Fact]
    public void ScreenToBitmap_PrimaryOrigin_IsOffset()
    {
        Assert.Equal(new Point(1920, 200), CoordinateMapper.ScreenToBitmap(new Point(0, 0), Virtual));
    }

    [Fact]
    public void BitmapToScreen_RoundTrips()
    {
        var screenRect = new Rectangle(-500, 50, 300, 200);
        Point p = CoordinateMapper.ScreenToBitmap(screenRect.Location, Virtual);
        var bitmapRect = new Rectangle(p, screenRect.Size);

        Assert.Equal(new Rectangle(1420, 250, 300, 200), bitmapRect);
        Assert.Equal(screenRect, CoordinateMapper.BitmapToScreen(bitmapRect, Virtual));
    }
}
