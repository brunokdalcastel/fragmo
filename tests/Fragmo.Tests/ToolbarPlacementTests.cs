using Fragmo.Overlay;

namespace Fragmo.Tests;

public sealed class ToolbarPlacementTests
{
    private const int Gap = ToolbarPlacement.DefaultGap;
    private static readonly Rectangle Monitor = new(0, 0, 1920, 1080);
    private static readonly Size Bar = new(600, 44);

    private static Point Place(Rectangle selection, params Rectangle[] monitors) =>
        ToolbarPlacement.Place(selection, Bar, monitors.Length == 0 ? [Monitor] : monitors);

    [Fact]
    public void SpaceBelow_CentersBelowSelection()
    {
        var sel = new Rectangle(500, 200, 800, 400);

        Point p = Place(sel);

        Assert.Equal(new Point(500 + (800 - 600) / 2, 600 + Gap), p);
    }

    [Fact]
    public void NoSpaceBelow_GoesAbove()
    {
        var sel = new Rectangle(500, 600, 800, 450); // termina em 1050: a barra não cabe embaixo

        Point p = Place(sel);

        Assert.Equal(600 - Gap - Bar.Height, p.Y);
        Assert.Equal(600, p.X);
    }

    [Fact]
    public void FullScreen_GoesInsideAtBottom()
    {
        Point p = Place(Monitor);

        Assert.Equal(1080 - Gap - Bar.Height, p.Y);
        Assert.Equal((1920 - 600) / 2, p.X);
    }

    [Fact]
    public void TallSelection_NoRoomAboveOrBelow_GoesInside()
    {
        var sel = new Rectangle(300, 20, 400, 1040);

        Point p = Place(sel);

        Assert.Equal(sel.Bottom - Gap - Bar.Height, p.Y);
    }

    [Fact]
    public void SelectionAtLeftEdge_StaysOnMonitor()
    {
        var sel = new Rectangle(0, 100, 100, 100); // centralizar levaria a barra para x negativo

        Point p = Place(sel);

        Assert.Equal(0, p.X);
        Assert.Equal(200 + Gap, p.Y);
    }

    [Fact]
    public void SelectionAtRightEdge_StaysOnMonitor()
    {
        var sel = new Rectangle(1820, 100, 100, 100);

        Point p = Place(sel);

        Assert.Equal(1920 - 600, p.X);
    }

    [Fact]
    public void BarWiderThanMonitor_AlignsToMonitorLeft()
    {
        var small = new Rectangle(0, 0, 400, 300);

        Point p = ToolbarPlacement.Place(new Rectangle(50, 50, 100, 100), Bar, [small]);

        Assert.Equal(0, p.X);
    }

    [Fact]
    public void SecondMonitor_StaysOnSelectionMonitor()
    {
        var right = new Rectangle(1920, 0, 1920, 1080);
        var sel = new Rectangle(1925, 900, 200, 170); // no canto inferior esquerdo do monitor da direita

        Point p = Place(sel, Monitor, right);

        Assert.True(p.X >= right.Left, $"x={p.X} saiu para o monitor da esquerda");
        Assert.True(p.Y + Bar.Height <= right.Bottom);
        Assert.Equal(900 - Gap - Bar.Height, p.Y); // embaixo não cabe: vai para cima
    }

    [Fact]
    public void MonitorAboveAndBelow_DoesNotSpillIntoOtherMonitor()
    {
        var top = new Rectangle(0, -1080, 1920, 1080);
        var sel = new Rectangle(500, -400, 600, 395); // termina 5 px acima da divisa

        Point p = Place(sel, top, Monitor);

        Assert.True(p.Y + Bar.Height <= top.Bottom, $"y={p.Y} invadiu o monitor de baixo");
        Assert.Equal(-400 - Gap - Bar.Height, p.Y);
    }

    [Fact]
    public void SelectionAcrossMonitors_UsesMonitorWithLargestPart()
    {
        var right = new Rectangle(1920, 0, 1920, 1080);
        var sel = new Rectangle(1800, 300, 600, 300); // 120 px no da esquerda, 480 px no da direita

        Rectangle chosen = ToolbarPlacement.MonitorFor(sel, [Monitor, right]);

        Assert.Equal(right, chosen);
    }

    [Fact]
    public void MonitorsWithNegativeOrigin_AreRespected()
    {
        var left = new Rectangle(-1280, 0, 1280, 1024);
        var sel = new Rectangle(-1280, 0, 1280, 1024);

        Point p = Place(sel, left, Monitor);

        Assert.Equal(-1280 + (1280 - 600) / 2, p.X);
        Assert.Equal(1024 - Gap - Bar.Height, p.Y);
    }
}
