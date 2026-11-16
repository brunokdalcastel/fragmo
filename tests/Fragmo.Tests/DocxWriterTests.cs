using System.IO.Compression;
using System.Xml.Linq;
using Fragmo.Tutorial.Export;

namespace Fragmo.Tests;

public sealed class DocxWriterTests
{
    private static readonly XNamespace W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    private static readonly XNamespace R = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly XNamespace Wp = "http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing";
    private static readonly XNamespace A = "http://schemas.openxmlformats.org/drawingml/2006/main";
    private static readonly XNamespace Rel = "http://schemas.openxmlformats.org/package/2006/relationships";

    private static ZipArchive WriteDocx(ExportDocument doc)
    {
        var stream = new MemoryStream();
        DocxWriter.Write(doc, stream);
        stream.Position = 0;
        return new ZipArchive(stream, ZipArchiveMode.Read);
    }

    private static XDocument Xml(ZipArchive zip, string name)
    {
        ZipArchiveEntry entry = zip.GetEntry(name) ?? throw new Xunit.Sdk.XunitException($"falta {name}");
        using Stream s = entry.Open();
        return XDocument.Load(s); // lança se não for XML válido
    }

    private static ExportDocument Tricky() => new("T\u00EDtulo <&> \"aspas\"",
    [
        new ExportNote("Nota com & e <tags>"),
        ExportTestData.Step(1, "Clique em \u201COK\u201D & siga\nsegunda linha", 1920, 1080),
        ExportTestData.Step(2, "Controle\u0001 removido", 100, 50),
        ExportTestData.Step(3, "Emoji \U0001F600 fica", 3000, 9000),
    ]);

    [Fact]
    public void AllPartsAreValidXml()
    {
        using ZipArchive zip = WriteDocx(Tricky());

        Xml(zip, "[Content_Types].xml");
        Xml(zip, "_rels/.rels");
        Xml(zip, "word/_rels/document.xml.rels");
        XDocument doc = Xml(zip, "word/document.xml");

        string text = string.Concat(doc.Descendants(W + "t").Select(t => t.Value));
        Assert.Contains("T\u00EDtulo <&> \"aspas\"", text, StringComparison.Ordinal);
        Assert.Contains("Emoji \U0001F600 fica", text, StringComparison.Ordinal);
        Assert.Contains("Controle removido", text, StringComparison.Ordinal);
        Assert.NotNull(doc.Descendants(W + "sectPr").Single().Element(W + "pgSz"));
    }

    [Fact]
    public void EveryEmbed_HasRelationship_AndEveryTargetExists()
    {
        using ZipArchive zip = WriteDocx(Tricky());

        var rels = Xml(zip, "word/_rels/document.xml.rels").Descendants(Rel + "Relationship")
            .ToDictionary(r => (string)r.Attribute("Id")!, r => (string)r.Attribute("Target")!);
        var embeds = Xml(zip, "word/document.xml").Descendants(A + "blip")
            .Select(b => (string)b.Attribute(R + "embed")!).ToList();

        Assert.Equal(3, embeds.Count);
        Assert.Equal(embeds.Count, embeds.Distinct().Count());
        foreach (string id in embeds)
        {
            Assert.True(rels.ContainsKey(id), $"{id} sem relationship");
        }
        foreach (string target in rels.Values)
        {
            Assert.NotNull(zip.GetEntry("word/" + target));
        }
    }

    [Fact]
    public void DocPrIds_AreUnique()
    {
        using ZipArchive zip = WriteDocx(Tricky());

        var ids = Xml(zip, "word/document.xml").Descendants(Wp + "docPr").Select(d => (string)d.Attribute("id")!).ToList();

        Assert.Equal(3, ids.Count);
        Assert.Equal(ids.Count, ids.Distinct().Count());
    }

    [Fact]
    public void Extents_FitUsableWidth_AndKeepRatio()
    {
        using ZipArchive zip = WriteDocx(Tricky());

        var extents = Xml(zip, "word/document.xml").Descendants(Wp + "extent")
            .Select(e => (Cx: (long)e.Attribute("cx")!, Cy: (long)e.Attribute("cy")!)).ToList();

        Assert.All(extents, e => Assert.True(e.Cx <= DocxWriter.MaxImageWidthEmu && e.Cy <= DocxWriter.MaxImageHeightEmu));
        Assert.Equal(1920.0 / 1080.0, (double)extents[0].Cx / extents[0].Cy, 2);
        Assert.Equal((100 * DocxWriter.EmuPerPixel, 50 * DocxWriter.EmuPerPixel), extents[1]); // pequena: tamanho real
        Assert.Equal(3000.0 / 9000.0, (double)extents[2].Cx / extents[2].Cy, 2);
    }

    [Fact]
    public void ContentTypes_DeclarePngAndMainDocument()
    {
        using ZipArchive zip = WriteDocx(ExportTestData.Sample());

        string types = Xml(zip, "[Content_Types].xml").ToString();

        Assert.Contains("Extension=\"png\"", types, StringComparison.Ordinal);
        Assert.Contains("PartName=\"/word/document.xml\"", types, StringComparison.Ordinal);
    }

    [Fact]
    public void WriteToFile_ReplacesAtomically()
    {
        string folder = ExportTestData.TempFolder();
        try
        {
            string path = Path.Combine(folder, "t.docx");
            File.WriteAllText(path, "antigo");

            DocxWriter.Write(ExportTestData.Sample(), path);

            using ZipArchive zip = ZipFile.OpenRead(path);
            Assert.NotNull(zip.GetEntry("word/document.xml"));
            Assert.False(File.Exists(path + ".tmp"));
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }
}
