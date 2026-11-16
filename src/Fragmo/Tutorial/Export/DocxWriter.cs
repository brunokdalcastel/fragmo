using System.Globalization;
using System.IO.Compression;
using System.Security;
using System.Text;

namespace Fragmo.Tutorial.Export;

/// <summary>
/// DOCX mínimo montado à mão (ZIP + WordprocessingML), sem OpenXML SDK: título, passos com a imagem inline
/// e notas em negrito. A4 com margens de 1 pol.
/// </summary>
public static class DocxWriter
{
    /// <summary>EMU por pixel a 96 DPI.</summary>
    public const long EmuPerPixel = 9525;

    /// <summary>Largura útil usada pelas imagens: 6 pol (a página A4 com margens de 1 pol tem ~6,27).</summary>
    public const long MaxImageWidthEmu = 5_486_400;

    /// <summary>Altura máxima da imagem: 8 pol, para nunca passar da altura útil (~9,69 pol).</summary>
    public const long MaxImageHeightEmu = 7_315_200;

    private const string ContentTypes = """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
        <Default Extension="png" ContentType="image/png"/>
        <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
        <Default Extension="xml" ContentType="application/xml"/>
        <Override PartName="/word/document.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml"/>
        </Types>
        """;

    private const string PackageRels = """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
        <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="word/document.xml"/>
        </Relationships>
        """;

    private const string DocumentStart =
        "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>\n" +
        "<w:document xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"" +
        " xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"" +
        " xmlns:wp=\"http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing\"" +
        " xmlns:a=\"http://schemas.openxmlformats.org/drawingml/2006/main\"" +
        " xmlns:pic=\"http://schemas.openxmlformats.org/drawingml/2006/picture\">\n<w:body>\n";

    private const string SectionProperties =
        "<w:sectPr><w:pgSz w:w=\"11906\" w:h=\"16838\"/>" +
        "<w:pgMar w:top=\"1440\" w:right=\"1440\" w:bottom=\"1440\" w:left=\"1440\" w:header=\"708\" w:footer=\"708\" w:gutter=\"0\"/>" +
        "</w:sectPr>\n";

    public static void Write(ExportDocument doc, string path) =>
        ExportText.WriteAtomic(path, stream => Write(doc, stream));

    public static void Write(ExportDocument doc, Stream output)
    {
        using var zip = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true);
        AddText(zip, "[Content_Types].xml", ContentTypes);
        AddText(zip, "_rels/.rels", PackageRels);

        var body = new StringBuilder(DocumentStart);
        var rels = new StringBuilder(
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>\n" +
            "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">\n");

        string title = ExportText.SingleLine(doc.Title);
        if (title.Length > 0)
        {
            body.Append("<w:p><w:pPr><w:spacing w:after=\"240\"/></w:pPr>")
                .Append(Run(title, bold: true, size: 40)).Append("</w:p>\n");
        }

        int image = 0;
        foreach (ExportEntry entry in doc.Entries)
        {
            switch (entry)
            {
                case ExportNote note:
                    body.Append("<w:p><w:pPr><w:keepNext/><w:spacing w:before=\"240\" w:after=\"120\"/></w:pPr>")
                        .Append(Run(note.Text, bold: true, size: 28)).Append("</w:p>\n");
                    break;

                case ExportStep step:
                    body.Append("<w:p><w:pPr><w:keepNext/><w:spacing w:after=\"80\"/></w:pPr>")
                        .Append(Run(step.Number.ToString(CultureInfo.InvariantCulture) + ". ", bold: true, size: null))
                        .Append(Run(step.Text, bold: false, size: null)).Append("</w:p>\n");

                    if (step.HasImage)
                    {
                        image++;
                        string relId = "rId" + image.ToString(CultureInfo.InvariantCulture);
                        string media = $"media/image{image}.png";
                        AddBytes(zip, "word/" + media, step.Png);
                        rels.Append("<Relationship Id=\"").Append(relId)
                            .Append("\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/image\" Target=\"")
                            .Append(media).Append("\"/>\n");
                        body.Append("<w:p><w:pPr><w:spacing w:after=\"240\"/></w:pPr>")
                            .Append(Drawing(image, relId, step)).Append("</w:p>\n");
                    }
                    break;
            }
        }

        body.Append(SectionProperties).Append("</w:body>\n</w:document>\n");
        rels.Append("</Relationships>\n");
        AddText(zip, "word/document.xml", body.ToString());
        AddText(zip, "word/_rels/document.xml.rels", rels.ToString());
    }

    /// <summary>Tamanho em EMU: 96 DPI, sem ampliar, limitado à largura e à altura útil mantendo a proporção.</summary>
    public static (long Cx, long Cy) ExtentFor(Size pixels)
    {
        long cx = pixels.Width * EmuPerPixel;
        long cy = pixels.Height * EmuPerPixel;
        double scale = Math.Min(1.0, Math.Min((double)MaxImageWidthEmu / cx, (double)MaxImageHeightEmu / cy));
        return (Math.Max(1, (long)(cx * scale)), Math.Max(1, (long)(cy * scale)));
    }

    private static string Drawing(int id, string relId, ExportStep step)
    {
        (long cx, long cy) = ExtentFor(step.PixelSize);
        string name = SecurityElement.Escape(ExportText.SingleLine(Sanitize(step.Text)));
        string n = id.ToString(CultureInfo.InvariantCulture);
        string ext = $"cx=\"{cx}\" cy=\"{cy}\"";
        return "<w:r><w:drawing><wp:inline distT=\"0\" distB=\"0\" distL=\"0\" distR=\"0\">" +
               $"<wp:extent {ext}/><wp:effectExtent l=\"0\" t=\"0\" r=\"0\" b=\"0\"/>" +
               $"<wp:docPr id=\"{n}\" name=\"Picture {n}\" descr=\"{name}\"/>" +
               "<wp:cNvGraphicFramePr><a:graphicFrameLocks noChangeAspect=\"1\"/></wp:cNvGraphicFramePr>" +
               "<a:graphic><a:graphicData uri=\"http://schemas.openxmlformats.org/drawingml/2006/picture\">" +
               $"<pic:pic><pic:nvPicPr><pic:cNvPr id=\"{n}\" name=\"image{n}.png\"/><pic:cNvPicPr/></pic:nvPicPr>" +
               $"<pic:blipFill><a:blip r:embed=\"{relId}\"/><a:stretch><a:fillRect/></a:stretch></pic:blipFill>" +
               $"<pic:spPr><a:xfrm><a:off x=\"0\" y=\"0\"/><a:ext {ext}/></a:xfrm>" +
               "<a:prstGeom prst=\"rect\"><a:avLst/></a:prstGeom></pic:spPr></pic:pic>" +
               "</a:graphicData></a:graphic></wp:inline></w:drawing></w:r>";
    }

    /// <summary>Um run de texto; quebras de linha do usuário viram <c>w:br</c>.</summary>
    private static string Run(string text, bool bold, int? size)
    {
        var sb = new StringBuilder("<w:r>");
        if (bold || size is not null)
        {
            sb.Append("<w:rPr>");
            if (bold)
            {
                sb.Append("<w:b/>");
            }
            if (size is int s)
            {
                sb.Append("<w:sz w:val=\"").Append(s).Append("\"/>");
            }
            sb.Append("</w:rPr>");
        }

        string[] lines = ExportText.Lines(Sanitize(text));
        for (int i = 0; i < lines.Length; i++)
        {
            if (i > 0)
            {
                sb.Append("<w:br/>");
            }
            sb.Append("<w:t xml:space=\"preserve\">").Append(SecurityElement.Escape(lines[i])).Append("</w:t>");
        }
        return sb.Append("</w:r>").ToString();
    }

    /// <summary>Tira caracteres que o XML 1.0 não aceita (controles, surrogates soltos); mantém tab e quebras.</summary>
    private static string Sanitize(string text)
    {
        var sb = new StringBuilder(text.Length);
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (char.IsHighSurrogate(c) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
            {
                sb.Append(c).Append(text[++i]);
            }
            else if (c is '\t' or '\n' or '\r' || (c >= ' ' && !char.IsSurrogate(c) && c is not '\uFFFE' and not '\uFFFF'))
            {
                sb.Append(c);
            }
        }
        return sb.ToString();
    }

    private static void AddText(ZipArchive zip, string name, string content) =>
        AddBytes(zip, name, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(content));

    private static void AddBytes(ZipArchive zip, string name, byte[] content)
    {
        ZipArchiveEntry entry = zip.CreateEntry(name, CompressionLevel.Optimal);
        using Stream s = entry.Open();
        s.Write(content);
    }
}
