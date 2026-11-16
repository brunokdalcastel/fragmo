using Fragmo.Tutorial.Export;

namespace Fragmo.Tests;

public sealed class PdfLayoutTests
{
    /// <summary>Medida previsível: cada caractere ocupa metade do tamanho da fonte.</summary>
    private static float Measure(string text, bool bold, float size) => text.Length * size * 0.5f;

    private static ExportStep Step(int n, int width, int height) => new(n, "Passo " + n, [1], new Size(width, height));

    [Fact]
    public void TitleOnlyOnFirstPage()
    {
        var entries = Enumerable.Range(1, 6).Select(i => (ExportEntry)Step(i, 800, 600)).ToList();

        var pages = PdfLayout.Plan(new ExportDocument("Meu título", entries), Measure);

        Assert.True(pages.Count > 1);
        Assert.Contains(pages[0].Lines, l => l.Bold && l.Text == "Meu título");
        Assert.All(pages.Skip(1), p => Assert.DoesNotContain(p.Lines, l => l.Text == "Meu título"));
    }

    [Fact]
    public void StepThatDoesNotFit_GoesToNextPage_WithItsText()
    {
        // Imagem de 600 px de altura = 450 pt; duas não cabem numa página (762 pt úteis).
        var doc = new ExportDocument("", [Step(1, 400, 600), Step(2, 400, 600)]);

        var pages = PdfLayout.Plan(doc, Measure);

        Assert.Equal(2, pages.Count);
        Assert.Single(pages[0].Images);
        Assert.Single(pages[1].Images);
        Assert.Equal("2. Passo 2", Assert.Single(pages[1].Lines).Text);
        Assert.True(pages[1].Lines[0].Baseline < pages[1].Images[0].Rect.Top);
    }

    [Fact]
    public void GiantImage_IsReducedToUsableWidthAnd60PercentHeight()
    {
        var doc = new ExportDocument("", [Step(1, 4000, 12000)]);

        RectangleF rect = Assert.Single(Assert.Single(PdfLayout.Plan(doc, Measure)).Images).Rect;

        Assert.True(rect.Width <= PdfLayout.ContentWidth);
        Assert.True(rect.Height <= PdfLayout.MaxImageHeight);
        Assert.Equal(4000.0 / 12000.0, rect.Width / rect.Height, 2);
        Assert.True(rect.Bottom <= PdfLayout.ContentBottom);
    }

    [Fact]
    public void SmallImage_IsNotEnlarged()
    {
        SizeF size = PdfLayout.ImageSize(new Size(200, 100));

        Assert.Equal(new SizeF(150, 75), size);
    }

    [Fact]
    public void EverythingStaysInsideMargins()
    {
        var entries = new List<ExportEntry> { new ExportNote(new string('x', 400)) };
        entries.AddRange(Enumerable.Range(1, 20).Select(i => (ExportEntry)Step(i, 1920, 1080)));

        var pages = PdfLayout.Plan(new ExportDocument(new string('T', 200), entries), Measure);

        foreach (PdfPagePlan page in pages)
        {
            Assert.All(page.Images, i =>
            {
                Assert.True(i.Rect.Left >= PdfLayout.Margin && i.Rect.Right <= PdfLayout.PageWidth - PdfLayout.Margin + 0.01);
                Assert.True(i.Rect.Top >= PdfLayout.Margin && i.Rect.Bottom <= PdfLayout.ContentBottom + 0.01);
            });
            Assert.All(page.Lines, l =>
            {
                Assert.True(Measure(l.Text, l.Bold, l.Size) <= PdfLayout.ContentWidth + 0.01);
                Assert.True(l.Baseline <= PdfLayout.ContentBottom);
            });
        }
        Assert.Equal(20, pages.Sum(p => p.Images.Count));
    }

    [Fact]
    public void Wrap_BreaksOnWords_AndCutsLongWords()
    {
        var lines = PdfLayout.Wrap("aaa bbb ccc " + new string('z', 25), false, 2, 10, Measure);

        Assert.Equal(["aaa bbb", "ccc", "zzzzzzzzzz", "zzzzzzzzzz", "zzzzz"], lines);
    }

    [Fact]
    public void EmptyDocument_HasOnePage()
    {
        Assert.Single(PdfLayout.Plan(new ExportDocument("", []), Measure));
    }
}
