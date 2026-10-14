using Fragmo.Overlay;
using Grip = Fragmo.Overlay.SelectionGeometry.Handle;

namespace Fragmo.Tests;

public sealed class SelectionGeometryTests
{
    private static readonly Rectangle Sel = new(100, 100, 200, 100); // right = 300, bottom = 200
    private static readonly Size Bounds = new(1000, 800);

    [Theory]
    [InlineData(10, 20, 50, 80)]   // para baixo e à direita
    [InlineData(50, 80, 10, 20)]   // para cima e à esquerda
    [InlineData(50, 20, 10, 80)]   // para baixo e à esquerda
    [InlineData(10, 80, 50, 20)]   // para cima e à direita
    public void FromPoints_AnyDirection_IsNormalized(int ax, int ay, int bx, int by)
    {
        Rectangle r = SelectionGeometry.FromPoints(new Point(ax, ay), new Point(bx, by));
        Assert.Equal(new Rectangle(10, 20, 40, 60), r);
    }

    [Fact]
    public void Clamp_PartiallyOutside_IsCut()
    {
        Rectangle r = SelectionGeometry.Clamp(new Rectangle(-50, 700, 200, 300), Bounds);
        Assert.Equal(new Rectangle(0, 700, 150, 100), r);
    }

    [Fact]
    public void Clamp_FullyOutside_IsEmpty()
    {
        Assert.True(SelectionGeometry.Clamp(new Rectangle(2000, 2000, 10, 10), Bounds).IsEmpty);
    }

    [Theory]
    [InlineData(100, 100, Grip.NW)]
    [InlineData(300, 100, Grip.NE)]
    [InlineData(100, 200, Grip.SW)]
    [InlineData(300, 200, Grip.SE)]
    [InlineData(103, 97, Grip.NW)]
    [InlineData(200, 100, Grip.N)]
    [InlineData(200, 200, Grip.S)]
    [InlineData(100, 150, Grip.W)]
    [InlineData(300, 150, Grip.E)]
    [InlineData(200, 150, Grip.Move)]
    [InlineData(50, 50, Grip.None)]
    [InlineData(400, 150, Grip.None)]
    public void HitTest_ReturnsHandleUnderPoint(int x, int y, Grip expected)
    {
        Assert.Equal(expected, SelectionGeometry.HitTest(Sel, new Point(x, y)));
    }

    [Fact]
    public void HitTest_EmptySelection_IsNone()
    {
        Assert.Equal(Grip.None, SelectionGeometry.HitTest(Rectangle.Empty, Point.Empty));
    }

    [Fact]
    public void Resize_East_GrowsWidth()
    {
        Assert.Equal(new Rectangle(100, 100, 250, 100), SelectionGeometry.Resize(Sel, Grip.E, new Point(50, 0)));
    }

    [Fact]
    public void Resize_CrossingOppositeEdge_KeepsPositiveSize()
    {
        // Arrastar a borda direita 300 px para a esquerda: passa da esquerda (100) e vai até 0.
        Rectangle r = SelectionGeometry.Resize(Sel, Grip.E, new Point(-300, 0));
        Assert.Equal(new Rectangle(0, 100, 100, 100), r);

        Rectangle r2 = SelectionGeometry.Resize(Sel, Grip.NW, new Point(250, 150));
        Assert.True(r2.Width > 0 && r2.Height > 0);
        Assert.Equal(new Rectangle(300, 200, 50, 50), r2);
    }

    [Fact]
    public void Move_AgainstEdge_StaysInside()
    {
        Assert.Equal(new Rectangle(0, 0, 200, 100), SelectionGeometry.Move(Sel, new Point(-500, -500), Bounds));
        Assert.Equal(new Rectangle(800, 700, 200, 100), SelectionGeometry.Move(Sel, new Point(5000, 5000), Bounds));
        Assert.Equal(new Rectangle(110, 120, 200, 100), SelectionGeometry.Move(Sel, new Point(10, 20), Bounds));
    }

    [Fact]
    public void MagnifierSource_IsSquareAroundPoint()
    {
        Assert.Equal(new Rectangle(43, 93, 15, 15), SelectionGeometry.MagnifierSource(new Point(50, 100), 7));
    }
}
