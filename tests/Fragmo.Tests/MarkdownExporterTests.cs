using System.Text.RegularExpressions;
using Fragmo.Tutorial.Export;

namespace Fragmo.Tests;

public sealed class MarkdownExporterTests
{
    [Fact]
    public void Write_CreatesImagesFolderAndValidRelativeLinks()
    {
        string folder = ExportTestData.TempFolder();
        try
        {
            string md = Path.Combine(folder, "Meu tutorial (v2).md");

            MarkdownExporter.Write(ExportTestData.Sample(), md);

            string text = File.ReadAllText(md);
            var links = Regex.Matches(text, @"!\[[^\]]*\]\(([^)]+)\)").Select(m => m.Groups[1].Value).ToList();
            Assert.Equal(2, links.Count);
            foreach (string link in links)
            {
                Assert.DoesNotContain(' ', link);
                Assert.DoesNotContain('\\', link);
                string local = Uri.UnescapeDataString(link).Replace('/', Path.DirectorySeparatorChar);
                Assert.True(File.Exists(Path.Combine(folder, local)), $"link quebrado: {link}");
            }
            Assert.Contains("Meu%20tutorial%20%28v2%29_imagens/passo-01.png", text, StringComparison.Ordinal);
            Assert.True(File.Exists(Path.Combine(folder, "Meu tutorial (v2)_imagens", "passo-02.png")));
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }

    [Fact]
    public void Write_TitleNotesAndNumberedSteps()
    {
        string folder = ExportTestData.TempFolder();
        try
        {
            string md = Path.Combine(folder, "t.md");

            MarkdownExporter.Write(ExportTestData.Sample(), md);

            string text = File.ReadAllText(md);
            Assert.StartsWith("# Como instalar\n", text, StringComparison.Ordinal);
            Assert.Contains("## Antes de começar\n", text, StringComparison.Ordinal);
            Assert.Contains("**1.** Clique no botão “Avançar”", text, StringComparison.Ordinal);
            Assert.Contains("**2.** ", text, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }

    [Fact]
    public void Write_RemovesStaleImagesOfAPreviousExportOnly()
    {
        string folder = ExportTestData.TempFolder();
        try
        {
            string images = Path.Combine(folder, "t_imagens");
            Directory.CreateDirectory(images);
            File.WriteAllText(Path.Combine(images, "passo-09.png"), "velha");
            File.WriteAllText(Path.Combine(images, "minha-foto.png"), "do usuário");

            MarkdownExporter.Write(ExportTestData.Sample(), Path.Combine(folder, "t.md"));

            Assert.False(File.Exists(Path.Combine(images, "passo-09.png")));
            Assert.True(File.Exists(Path.Combine(images, "minha-foto.png")));
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }

    [Theory]
    [InlineData("a*b_c", @"a\*b\_c")]
    [InlineData("[link](x)", @"\[link\](x)")]
    [InlineData("# título", @"\# título")]
    [InlineData("<b>", @"\<b\>")]
    [InlineData(@"C:\pasta", @"C:\\pasta")]
    public void Escape_MarkdownSyntax(string input, string expected) =>
        Assert.Equal(expected, MarkdownExporter.Escape(input));

    [Theory]
    [InlineData(5, 2, "passo-05.png")]
    [InlineData(99, 2, "passo-99.png")]
    [InlineData(7, 3, "passo-007.png")]
    public void ImageFileName_PadsNumber(int number, int digits, string expected) =>
        Assert.Equal(expected, MarkdownExporter.ImageFileName(number, digits));

    [Fact]
    public void MoreThan99Steps_UsesThreeDigits()
    {
        string folder = ExportTestData.TempFolder();
        try
        {
            var entries = Enumerable.Range(1, 100).Select(i => (ExportEntry)ExportTestData.Step(i, "p", 4, 4)).ToList();

            MarkdownExporter.Write(new ExportDocument("x", entries), Path.Combine(folder, "t.md"));

            Assert.True(File.Exists(Path.Combine(folder, "t_imagens", "passo-001.png")));
            Assert.True(File.Exists(Path.Combine(folder, "t_imagens", "passo-100.png")));
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }

    [Fact]
    public void EncodePathSegment_KeepsAccents_EncodesLinkBreakers()
    {
        Assert.Equal("Instalação", MarkdownExporter.EncodePathSegment("Instalação"));
        Assert.Equal("a%20b%23c%3Fd%25e", MarkdownExporter.EncodePathSegment("a b#c?d%e"));
    }
}
