using System.Text;
using System.Text.RegularExpressions;
using Fragmo.Tutorial.Export;

namespace Fragmo.Tests;

public sealed class PdfWriterTests
{
    private static byte[] WritePdf(ExportDocument doc)
    {
        using var stream = new MemoryStream();
        PdfWriter.Write(doc, stream);
        return stream.ToArray();
    }

    private static string Latin1(byte[] bytes) => Encoding.Latin1.GetString(bytes);

    [Fact]
    public void HasHeaderAndEof()
    {
        string pdf = Latin1(WritePdf(ExportTestData.Sample()));

        Assert.StartsWith("%PDF-1.4", pdf, StringComparison.Ordinal);
        Assert.EndsWith("%%EOF", pdf.TrimEnd(), StringComparison.Ordinal);
    }

    [Fact]
    public void EveryXrefOffset_PointsToItsObject()
    {
        byte[] bytes = WritePdf(ExportTestData.Sample());
        string pdf = Latin1(bytes);

        int startxref = int.Parse(Regex.Match(pdf, @"startxref\n(\d+)\n%%EOF").Groups[1].Value);
        Assert.StartsWith("xref\n", pdf[startxref..], StringComparison.Ordinal);

        Match header = Regex.Match(pdf[startxref..], @"^xref\n0 (\d+)\n");
        int count = int.Parse(header.Groups[1].Value);
        var entries = Regex.Matches(pdf[startxref..], @"(\d{10}) (\d{5}) ([nf]) \n").ToList();
        Assert.Equal(count, entries.Count);

        for (int id = 1; id < count; id++)
        {
            int offset = int.Parse(entries[id].Groups[1].Value);
            Assert.StartsWith($"{id} 0 obj", pdf[offset..], StringComparison.Ordinal);
        }
        Assert.Contains($"/Size {count}", pdf, StringComparison.Ordinal);
    }

    [Fact]
    public void PageCount_MatchesLayout()
    {
        var entries = Enumerable.Range(1, 7).Select(i => (ExportEntry)ExportTestData.Step(i, "p", 800, 600)).ToList();
        var doc = new ExportDocument("V\u00E1rios", entries);

        string pdf = Latin1(WritePdf(doc));

        int planned = PdfLayout.Plan(doc, HelveticaMetrics.Measure).Count;
        Assert.True(planned > 1);
        Assert.Equal(planned, Regex.Matches(pdf, @"/Type /Page(?!s)").Count);
        Assert.Contains($"/Count {planned}", pdf, StringComparison.Ordinal);
    }

    [Fact]
    public void OneJpegImagePerStep()
    {
        string pdf = Latin1(WritePdf(ExportTestData.Sample()));

        Assert.Equal(2, Regex.Matches(pdf, "/Filter /DCTDecode").Count);
        Assert.Equal(2, Regex.Matches(pdf, "stream\n\u00FF\u00D8").Count); // começo de JPEG (FF D8)
    }

    [Fact]
    public void Text_IsWinAnsi()
    {
        Assert.Equal(new byte[] { (byte)'(', (byte)'a', 0xE7, 0xE3, (byte)'o', (byte)')' }, PdfWriter.EncodeText("a\u00E7\u00E3o"));
    }

    [Fact]
    public void Text_OutsideWinAnsi_BecomesQuestionMark()
    {
        Assert.Equal("(ok ?)", Latin1(PdfWriter.EncodeText("ok \U0001F600")));
        Assert.Equal("(?)", Latin1(PdfWriter.EncodeText("\u4E2D")));
    }

    [Fact]
    public void Text_EscapesParenthesesAndBackslash()
    {
        Assert.Equal(@"(\(C:\\x\))", Latin1(PdfWriter.EncodeText(@"(C:\x)")));
    }

    [Fact]
    public void CurlyQuotes_UseWinAnsiCodes()
    {
        byte[] bytes = PdfWriter.EncodeText("\u201Cx\u201D");

        Assert.Equal(new byte[] { (byte)'(', 0x93, (byte)'x', 0x94, (byte)')' }, bytes);
    }

    [Fact]
    public void Metrics_UseAfmWidths()
    {
        Assert.Equal(5.56f, HelveticaMetrics.Measure("a", false, 10), 3);
        Assert.Equal(HelveticaMetrics.Measure("a", false, 10), HelveticaMetrics.Measure("\u00E1", false, 10), 3);
        Assert.Equal(6.11f, HelveticaMetrics.Measure("b", true, 10), 3);
    }
}
