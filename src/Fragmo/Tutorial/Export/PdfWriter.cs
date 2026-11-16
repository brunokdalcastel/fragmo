using System.Drawing.Imaging;
using System.Globalization;
using System.Text;

namespace Fragmo.Tutorial.Export;

/// <summary>
/// Escritor PDF 1.4 mínimo, sem biblioteca: Helvetica/Helvetica-Bold padrão (WinAnsi, D4), imagens JPEG
/// (<c>/DCTDecode</c>) e uma página A4 por <see cref="PdfPagePlan"/>. As imagens são convertidas uma por vez
/// enquanto o arquivo é escrito, para não segurar todas na memória.
/// </summary>
public static class PdfWriter
{
    public const long JpegQuality = 85;

    private const int CatalogId = 1;
    private const int PagesId = 2;
    private const int FontRegularId = 3;
    private const int FontBoldId = 4;
    private const int InfoId = 5;
    private const int FirstDynamicId = 6;

    public static void Write(ExportDocument doc, string path) =>
        ExportText.WriteAtomic(path, stream => Write(doc, stream));

    public static void Write(ExportDocument doc, Stream output)
    {
        IReadOnlyList<PdfPagePlan> pages = PdfLayout.Plan(doc, HelveticaMetrics.Measure);
        var images = pages.SelectMany(p => p.Images).Select(i => i.Step).Distinct().ToList();

        // Numeração fixa antes de escrever: imagens, depois (página, conteúdo) por página.
        var imageIds = new Dictionary<ExportStep, int>();
        int next = FirstDynamicId;
        foreach (ExportStep step in images)
        {
            imageIds[step] = next++;
        }
        var pageIds = new List<(int Page, int Content)>();
        foreach (PdfPagePlan _ in pages)
        {
            pageIds.Add((next, next + 1));
            next += 2;
        }

        var w = new PdfOutput(output, next);
        w.Raw("%PDF-1.4\n%\u00E2\u00E3\u00CF\u00D3\n");

        w.BeginObject(CatalogId);
        w.Raw($"<< /Type /Catalog /Pages {PagesId} 0 R >>\n");
        w.EndObject();

        w.BeginObject(PagesId);
        string kids = string.Join(' ', pageIds.Select(p => $"{p.Page} 0 R"));
        w.Raw($"<< /Type /Pages /Kids [{kids}] /Count {pageIds.Count} >>\n");
        w.EndObject();

        WriteFont(w, FontRegularId, "Helvetica");
        WriteFont(w, FontBoldId, "Helvetica-Bold");

        w.BeginObject(InfoId);
        w.Raw($"<< /Title {Utf16String(ExportText.SingleLine(doc.Title))} /Producer (Fragmo) >>\n");
        w.EndObject();

        foreach (ExportStep step in images)
        {
            WriteImage(w, imageIds[step], step);
        }

        for (int i = 0; i < pages.Count; i++)
        {
            PdfPagePlan page = pages[i];
            (int pageId, int contentId) = pageIds[i];
            string xobjects = string.Join(' ', page.Images.Select(img => $"/Im{imageIds[img.Step]} {imageIds[img.Step]} 0 R"));

            w.BeginObject(pageId);
            w.Raw($"<< /Type /Page /Parent {PagesId} 0 R /MediaBox [0 0 {Num(PdfLayout.PageWidth)} {Num(PdfLayout.PageHeight)}] " +
                  $"/Resources << /Font << /F1 {FontRegularId} 0 R /F2 {FontBoldId} 0 R >>" +
                  (xobjects.Length > 0 ? $" /XObject << {xobjects} >>" : "") +
                  $" >> /Contents {contentId} 0 R >>\n");
            w.EndObject();

            byte[] content = PageContent(page, imageIds);
            w.BeginObject(contentId);
            w.Raw($"<< /Length {content.Length} >>\nstream\n");
            w.Bytes(content);
            w.Raw("\nendstream\n");
            w.EndObject();
        }

        w.Finish(CatalogId, InfoId);
    }

    /// <summary>Texto em WinAnsi entre parênteses, com <c>\ ( )</c> escapados e controles trocados por espaço.</summary>
    public static byte[] EncodeText(string text)
    {
        var result = new List<byte>(text.Length + 8) { (byte)'(' };
        foreach (byte b in HelveticaMetrics.ToWinAnsi(text))
        {
            if (b is (byte)'\\' or (byte)'(' or (byte)')')
            {
                result.Add((byte)'\\');
                result.Add(b);
            }
            else
            {
                result.Add(b < 32 ? (byte)' ' : b);
            }
        }
        result.Add((byte)')');
        return result.ToArray();
    }

    private static void WriteFont(PdfOutput w, int id, string baseFont)
    {
        w.BeginObject(id);
        w.Raw($"<< /Type /Font /Subtype /Type1 /BaseFont /{baseFont} /Encoding /WinAnsiEncoding >>\n");
        w.EndObject();
    }

    private static void WriteImage(PdfOutput w, int id, ExportStep step)
    {
        (byte[] jpeg, Size size) = ToJpeg(step.Png);
        w.BeginObject(id);
        w.Raw($"<< /Type /XObject /Subtype /Image /Width {size.Width} /Height {size.Height} " +
              $"/ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /DCTDecode /Length {jpeg.Length} >>\nstream\n");
        w.Bytes(jpeg);
        w.Raw("\nendstream\n");
        w.EndObject();
    }

    /// <summary>PNG → JPEG RGB sobre fundo branco (o JPEG não tem transparência).</summary>
    private static (byte[] Jpeg, Size Size) ToJpeg(byte[] png)
    {
        using var input = new MemoryStream(png);
        using var source = Image.FromStream(input);
        using var rgb = new Bitmap(source.Width, source.Height, PixelFormat.Format24bppRgb);
        using (Graphics g = Graphics.FromImage(rgb))
        {
            g.Clear(Color.White);
            g.DrawImage(source, new Rectangle(0, 0, source.Width, source.Height));
        }

        ImageCodecInfo codec = ImageCodecInfo.GetImageEncoders().First(c => c.FormatID == ImageFormat.Jpeg.Guid);
        using var parameters = new EncoderParameters(1);
        parameters.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, JpegQuality);
        using var output = new MemoryStream();
        rgb.Save(output, codec, parameters);
        return (output.ToArray(), rgb.Size);
    }

    private static byte[] PageContent(PdfPagePlan page, Dictionary<ExportStep, int> imageIds)
    {
        using var content = new MemoryStream();
        void Ascii(string s) => content.Write(Encoding.ASCII.GetBytes(s));

        foreach (PdfImagePlacement img in page.Images)
        {
            RectangleF r = img.Rect;
            float bottom = PdfLayout.PageHeight - r.Bottom;
            Ascii($"q {Num(r.Width)} 0 0 {Num(r.Height)} {Num(r.X)} {Num(bottom)} cm /Im{imageIds[img.Step]} Do Q\n");
            Ascii($"q 0.85 G 0.5 w {Num(r.X)} {Num(bottom)} {Num(r.Width)} {Num(r.Height)} re S Q\n");
        }

        if (page.Lines.Count > 0)
        {
            Ascii("BT 0 g\n");
            foreach (PdfTextLine line in page.Lines)
            {
                Ascii($"/{(line.Bold ? "F2" : "F1")} {Num(line.Size)} Tf 1 0 0 1 {Num(line.X)} {Num(PdfLayout.PageHeight - line.Baseline)} Tm ");
                content.Write(EncodeText(line.Text));
                Ascii(" Tj\n");
            }
            Ascii("ET\n");
        }
        return content.ToArray();
    }

    /// <summary>Texto Unicode para o dicionário Info (UTF-16BE com BOM, em hexadecimal).</summary>
    private static string Utf16String(string text) =>
        "<FEFF" + Convert.ToHexString(Encoding.BigEndianUnicode.GetBytes(text)) + ">";

    private static string Num(float value) => Math.Round(value, 2).ToString("0.##", CultureInfo.InvariantCulture);

    /// <summary>Saída com contagem de bytes para a tabela <c>xref</c>.</summary>
    private sealed class PdfOutput(Stream stream, int objectCount)
    {
        private readonly long[] _offsets = new long[objectCount];
        private long _position;

        public void Raw(string text) => Bytes(Encoding.Latin1.GetBytes(text));

        public void Bytes(byte[] bytes)
        {
            stream.Write(bytes);
            _position += bytes.Length;
        }

        public void BeginObject(int id)
        {
            _offsets[id] = _position;
            Raw($"{id} 0 obj\n");
        }

        public void EndObject() => Raw("endobj\n");

        public void Finish(int rootId, int infoId)
        {
            long xref = _position;
            var sb = new StringBuilder();
            sb.Append("xref\n0 ").Append(_offsets.Length).Append('\n');
            sb.Append("0000000000 65535 f \n");
            for (int i = 1; i < _offsets.Length; i++)
            {
                sb.Append(_offsets[i].ToString("D10", CultureInfo.InvariantCulture)).Append(" 00000 n \n");
            }
            sb.Append($"trailer\n<< /Size {_offsets.Length} /Root {rootId} 0 R /Info {infoId} 0 R >>\n");
            sb.Append("startxref\n").Append(xref.ToString(CultureInfo.InvariantCulture)).Append("\n%%EOF\n");
            Raw(sb.ToString());
        }
    }
}
