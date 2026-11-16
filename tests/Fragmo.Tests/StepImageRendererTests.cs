using Fragmo.Editor;
using Fragmo.Tutorial;
using Fragmo.Tutorial.Export;

namespace Fragmo.Tests;

public sealed class StepImageRendererTests
{
    private static readonly int White = Color.White.ToArgb();
    private static readonly int Black = Color.Black.ToArgb();

    private static Bitmap WhiteImage()
    {
        var bmp = new Bitmap(200, 120);
        using Graphics g = Graphics.FromImage(bmp);
        g.Clear(Color.White);
        return bmp;
    }

    private static TutorialItem FullStep(bool edited = false, bool showHighlight = true) => ExportTestData.StepItem() with
    {
        Edited = edited,
        ShowHighlight = showHighlight,
        Redactions = [new RectDto(10, 10, 30, 20)],
        Highlight = new RectDto(80, 40, 60, 40),
        ClickPoint = new PointDto(170, 100),
    };

    private static bool AllWhite(Bitmap bmp, Rectangle area)
    {
        for (int y = area.Top; y < area.Bottom; y++)
        {
            for (int x = area.Left; x < area.Right; x++)
            {
                if (bmp.GetPixel(x, y).ToArgb() != White)
                {
                    return false;
                }
            }
        }
        return true;
    }

    [Fact]
    public void NotEdited_DrawsRedactionHighlightAndMarker()
    {
        using Bitmap source = WhiteImage();
        using Bitmap result = StepImageRenderer.Compose(source, FullStep(), 3);

        Assert.Equal(Black, result.GetPixel(25, 20).ToArgb());          // tarja
        Assert.NotEqual(White, result.GetPixel(80, 60).ToArgb());       // borda do destaque
        Assert.True(AllWhite(result, new Rectangle(95, 55, 30, 10)));   // miolo do destaque intacto
        Assert.NotEqual(White, result.GetPixel(160, 100).ToArgb());     // marcador (fora do número)
        Assert.Equal(source.Size, result.Size);
    }

    [Fact]
    public void Edited_DrawsNothingOnTop()
    {
        using Bitmap source = WhiteImage();
        using Bitmap result = StepImageRenderer.Compose(source, FullStep(edited: true), 3);

        Assert.True(AllWhite(result, new Rectangle(0, 0, 200, 120)));
    }

    [Fact]
    public void HideHighlight_KeepsRedactionsOnly()
    {
        using Bitmap source = WhiteImage();
        using Bitmap result = StepImageRenderer.Compose(source, FullStep(showHighlight: false), 3);

        Assert.Equal(Black, result.GetPixel(25, 20).ToArgb());
        Assert.True(AllWhite(result, new Rectangle(50, 30, 150, 90)));
    }

    [Fact]
    public void Annotations_OrderIsRedactionsThenRectangleThenMarker()
    {
        var items = StepImageRenderer.Annotations(FullStep(), 7);

        Assert.Collection(items,
            a => Assert.IsType<RedactAnnotation>(a),
            a => Assert.IsType<RectangleAnnotation>(a),
            a => Assert.Equal(7, Assert.IsType<StepAnnotation>(a).Number));
    }

    [Fact]
    public void Render_ReadsFromFolder_AndDoesNotLockTheFile()
    {
        string folder = ExportTestData.TempFolder();
        try
        {
            Directory.CreateDirectory(Path.Combine(folder, "images"));
            string file = Path.Combine(folder, "images", "0001.png");
            File.WriteAllBytes(file, ExportTestData.Png(50, 20, Color.White));

            using (Bitmap result = StepImageRenderer.Render(ExportTestData.StepItem(), 1, folder))
            {
                Assert.Equal(new Size(50, 20), result.Size);
                File.Delete(file); // falharia se o GDI+ estivesse segurando o arquivo
            }
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }

    [Fact]
    public void Render_RejectsPathOutsideImagesFolder()
    {
        string folder = ExportTestData.TempFolder();
        try
        {
            Assert.Throws<TutorialFormatException>(() =>
                StepImageRenderer.Render(ExportTestData.StepItem("../segredo.png"), 1, folder));
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }

    [Fact]
    public void Thumbnail_HasFixedWidthAndKeepsRatio()
    {
        string folder = ExportTestData.TempFolder();
        try
        {
            Directory.CreateDirectory(Path.Combine(folder, "images"));
            File.WriteAllBytes(Path.Combine(folder, "images", "0001.png"), ExportTestData.Png(800, 400, Color.White));

            using Bitmap thumb = StepImageRenderer.RenderThumbnail(ExportTestData.StepItem(), 1, folder);

            Assert.Equal(new Size(StepImageRenderer.ThumbnailWidth, 80), thumb.Size);
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }

    [Fact]
    public void Build_NumbersOnlySteps()
    {
        string folder = ExportTestData.TempFolder();
        try
        {
            Directory.CreateDirectory(Path.Combine(folder, "images"));
            File.WriteAllBytes(Path.Combine(folder, "images", "0001.png"), ExportTestData.Png(20, 10, Color.White));
            File.WriteAllBytes(Path.Combine(folder, "images", "0002.png"), ExportTestData.Png(20, 10, Color.White));
            var doc = new TutorialDocument
            {
                Title = "T",
                Items =
                [
                    new TutorialItem { Id = "n", Kind = TutorialItemKind.Note, Text = "Nota" },
                    ExportTestData.StepItem("images/0001.png"),
                    new TutorialItem { Id = "m", Kind = TutorialItemKind.Note, Text = "Outra" },
                    ExportTestData.StepItem("images/0002.png"),
                ],
            };

            ExportDocument export = ExportDocument.Build(doc, folder);

            Assert.Equal([1, 2], export.Steps.Select(s => s.Number));
            Assert.All(export.Steps, s => Assert.True(s.HasImage));
            Assert.Equal(2, export.Entries.OfType<ExportNote>().Count());
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }
}
