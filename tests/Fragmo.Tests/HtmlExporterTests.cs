using System.Text.RegularExpressions;
using Fragmo.Tutorial.Export;

namespace Fragmo.Tests;

public sealed class HtmlExporterTests
{
    [Fact]
    public void EscapesScriptInTitleAndText()
    {
        var doc = new ExportDocument("<script>alert(1)</script>",
        [
            new ExportNote("<img src=x onerror=alert(1)>"),
            ExportTestData.Step(1, "Clique em <script>x</script> & \"y\""),
        ]);

        string html = HtmlExporter.Build(doc);

        Assert.DoesNotContain("<script", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<img src=x", html, StringComparison.Ordinal);
        Assert.Contains("&lt;script&gt;", html, StringComparison.Ordinal);
        Assert.Contains("&amp;", html, StringComparison.Ordinal);
    }

    [Fact]
    public void OneDataImagePerStep()
    {
        string html = HtmlExporter.Build(ExportTestData.Sample());

        Assert.Equal(2, Regex.Matches(html, "<img src=\"data:image/png;base64,").Count);
    }

    [Fact]
    public void NotesAreHeadingsWithoutNumber()
    {
        string html = HtmlExporter.Build(ExportTestData.Sample());

        Assert.Contains("<h2>" + System.Net.WebUtility.HtmlEncode("Antes de começar") + "</h2>", html, StringComparison.Ordinal);
        Assert.Contains("<span class=\"num\">1.</span>", html, StringComparison.Ordinal);
        Assert.Contains("<span class=\"num\">2.</span>", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<span class=\"num\">3.</span>", html, StringComparison.Ordinal);
    }

    [Fact]
    public void IsSelfContainedUtf8Document()
    {
        string html = HtmlExporter.Build(ExportTestData.Sample());

        Assert.StartsWith("<!DOCTYPE html>", html, StringComparison.Ordinal);
        Assert.Contains("<meta charset=\"utf-8\">", html, StringComparison.Ordinal);
        Assert.Contains("max-width: 960px", html, StringComparison.Ordinal);
        Assert.Contains("break-inside: avoid", html, StringComparison.Ordinal);
        Assert.DoesNotContain("http://", html, StringComparison.Ordinal);
        Assert.DoesNotContain("https://", html, StringComparison.Ordinal);
    }

    [Fact]
    public void StepWithoutImage_HasNoImgTag()
    {
        var doc = new ExportDocument("", [new ExportStep(1, "Passo", [], Size.Empty)]);

        string html = HtmlExporter.Build(doc);

        Assert.DoesNotContain("<img", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<h1>", html, StringComparison.Ordinal);
    }
}
