using System.Drawing;
using Fragmo.Tutorial;
using Fragmo.Tutorial.Editor;

namespace Fragmo.Tests;

public sealed class ThumbnailCacheTests
{
    private static TutorialItem Step(string id) => new() { Id = id, Kind = TutorialItemKind.Step, Text = id };

    [Fact]
    public void Get_SameItemTwice_RendersOnce()
    {
        int renders = 0;
        using var cache = new ThumbnailCache(4, (_, _) => { renders++; return new Bitmap(2, 2); });
        TutorialItem a = Step("a");

        Bitmap? first = cache.Get(a, 1);
        Bitmap? second = cache.Get(a, 1);

        Assert.Same(first, second);
        Assert.Equal(1, renders);
    }

    [Fact]
    public void Get_EditedItemOrOtherNumber_RendersAgain()
    {
        int renders = 0;
        using var cache = new ThumbnailCache(4, (_, _) => { renders++; return new Bitmap(2, 2); });
        TutorialItem a = Step("a");

        cache.Get(a, 1);
        cache.Get(a with { ShowHighlight = false }, 1);
        cache.Get(a, 2);

        Assert.Equal(3, renders);
    }

    [Fact]
    public void Get_OverCapacity_EvictsLeastRecentAndDisposesIt()
    {
        var made = new List<Bitmap>();
        using var cache = new ThumbnailCache(2, (_, _) => { var b = new Bitmap(2, 2); made.Add(b); return b; });
        TutorialItem a = Step("a"), b = Step("b"), c = Step("c");

        cache.Get(a, 1);
        cache.Get(b, 2);
        cache.Get(a, 1); // a volta a ser o mais recente
        cache.Get(c, 3); // sai o b

        Assert.Equal(2, cache.Count);
        Assert.Equal(3, made.Count);
        Assert.Throws<ArgumentException>(() => made[1].Width); // b descartado
        Assert.Equal(2, made[0].Width);                          // a vivo
        cache.Get(a, 1);
        Assert.Equal(3, made.Count);
    }

    [Fact]
    public void Get_RenderFailure_IsCachedAsNull()
    {
        int renders = 0;
        using var cache = new ThumbnailCache(4, (_, _) => { renders++; return null; });
        TutorialItem a = Step("a");

        Assert.Null(cache.Get(a, 1));
        Assert.Null(cache.Get(a, 1));
        Assert.Equal(1, renders);
    }

    [Fact]
    public void Clear_DisposesEverything()
    {
        var made = new List<Bitmap>();
        var cache = new ThumbnailCache(4, (_, _) => { var b = new Bitmap(2, 2); made.Add(b); return b; });
        cache.Get(Step("a"), 1);
        cache.Get(Step("b"), 2);

        cache.Dispose();

        Assert.Equal(0, cache.Count);
        Assert.All(made, b => Assert.Throws<ArgumentException>(() => b.Width));
    }
}

public sealed class StepImageEditingTests
{
    [Fact]
    public void Canvas_SmallImage_IsTheMonitor_WithImageCentered()
    {
        var monitor = new Rectangle(-1920, 0, 1920, 1080);
        Rectangle canvas = StepImageEditing.CanvasBounds(monitor, new Size(800, 600));
        Rectangle image = StepImageEditing.ImagePlacement(canvas.Size, new Size(800, 600));

        Assert.Equal(monitor, canvas);
        Assert.Equal(new Rectangle(560, 240, 800, 600), image);
    }

    [Fact]
    public void Canvas_ImageLargerThanMonitor_GrowsFromTheMonitorOrigin()
    {
        var monitor = new Rectangle(0, 0, 1366, 768);
        Rectangle canvas = StepImageEditing.CanvasBounds(monitor, new Size(1920, 700));
        Rectangle image = StepImageEditing.ImagePlacement(canvas.Size, new Size(1920, 700));

        Assert.Equal(new Rectangle(0, 0, 1920, 768), canvas);
        Assert.Equal(new Rectangle(0, 34, 1920, 700), image);
    }
}
