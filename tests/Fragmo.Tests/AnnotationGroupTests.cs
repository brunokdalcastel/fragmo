using Fragmo.Editor;

namespace Fragmo.Tests;

public sealed class AnnotationGroupTests
{
    [Fact]
    public void Bounds_IsUnionOfItems()
    {
        var group = new AnnotationGroup([new BlurAnnotation(new Rectangle(10, 10, 20, 20)), new BlurAnnotation(new Rectangle(100, 50, 10, 10))]);

        Assert.Equal(Rectangle.FromLTRB(10, 10, 110, 60), group.Bounds);
    }

    [Fact]
    public void Empty_HasEmptyBounds()
    {
        Assert.Equal(Rectangle.Empty, new AnnotationGroup([]).Bounds);
    }

    [Fact]
    public void Group_IsUndoneInOneStep()
    {
        var history = new AnnotationHistory();
        history.Add(new BlurAnnotation(new Rectangle(0, 0, 5, 5)));
        history.Add(new AnnotationGroup([new BlurAnnotation(new Rectangle(10, 10, 5, 5)), new BlurAnnotation(new Rectangle(20, 20, 5, 5))]));

        history.Undo();

        IAnnotation remaining = Assert.Single(history.Items);
        Assert.IsType<BlurAnnotation>(remaining);
    }

    [Fact]
    public void Draw_PaintsEveryItem()
    {
        using var source = new Bitmap(40, 20);
        using (var g = Graphics.FromImage(source))
        {
            g.Clear(Color.White);
            g.FillRectangle(Brushes.Black, 0, 0, 2, 20);   // detalhe fino na esquerda
            g.FillRectangle(Brushes.Black, 20, 0, 2, 20);  // e na direita
        }
        var group = new AnnotationGroup([new BlurAnnotation(new Rectangle(0, 0, 20, 20), 20), new BlurAnnotation(new Rectangle(20, 0, 20, 20), 20)]);

        using var result = ImageComposer.Compose(source, [group], new Rectangle(0, 0, 40, 20));

        // Depois do desfoque, as faixas pretas viram cinza nas duas metades.
        Assert.NotEqual(Color.Black.ToArgb(), result.GetPixel(0, 10).ToArgb());
        Assert.NotEqual(Color.Black.ToArgb(), result.GetPixel(20, 10).ToArgb());
    }
}
