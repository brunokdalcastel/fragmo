using System.Globalization;
using System.Text;
using Fragmo.Output;

namespace Fragmo.Tutorial.Export;

/// <summary>Linha de texto já posicionada. Coordenadas em pontos, a partir do canto superior esquerdo da página.</summary>
public sealed record PdfTextLine(string Text, bool Bold, float Size, float X, float Baseline);

/// <summary>Imagem de um passo encaixada na página (pontos, canto superior esquerdo).</summary>
public sealed record PdfImagePlacement(ExportStep Step, RectangleF Rect);

public sealed record PdfPagePlan(IReadOnlyList<PdfTextLine> Lines, IReadOnlyList<PdfImagePlacement> Images);

/// <summary>Mede a largura de um texto em pontos: (texto, negrito, tamanho da fonte).</summary>
public delegate float PdfTextMeasure(string text, bool bold, float size);

/// <summary>
/// Distribui título, notas e passos em páginas A4. Puro (sem GDI+ nem arquivo) para ser testado.
/// Um passo (texto + imagem) só começa numa página onde cabe inteiro; se nem numa página nova cabe, o texto
/// segue para as próximas e a imagem vai junto da última linha.
/// </summary>
public static class PdfLayout
{
    public const float PageWidth = 595;
    public const float PageHeight = 842;
    public const float Margin = 40;
    public const float ContentWidth = PageWidth - 2 * Margin;
    public const float ContentBottom = PageHeight - Margin;
    public const float MaxImageHeight = PageHeight * 0.6f;

    public const float TitleSize = 18;
    public const float NoteSize = 14;
    public const float TextSize = 11;
    public const float LineFactor = 1.3f;
    public const float GapAfterText = 6;
    public const float GapAfterStep = 18;
    public const float GapAfterTitle = 14;
    public const float GapBeforeNote = 8;

    /// <summary>Pontos por pixel da imagem (tela a 96 DPI = 72 pt/pol).</summary>
    public const float PointsPerPixel = 0.75f;

    public static IReadOnlyList<PdfPagePlan> Plan(ExportDocument doc, PdfTextMeasure measure)
    {
        var builder = new PageBuilder();
        string title = ExportText.SingleLine(doc.Title);
        if (title.Length > 0)
        {
            builder.AddLines(Wrap(title, true, TitleSize, ContentWidth, measure), true, TitleSize);
            builder.Y += GapAfterTitle;
        }

        foreach (ExportEntry entry in doc.Entries)
        {
            switch (entry)
            {
                case ExportNote note:
                {
                    var lines = WrapAll(note.Text, true, NoteSize, measure);
                    builder.Y += GapBeforeNote;
                    builder.KeepTogether(lines.Count * NoteSize * LineFactor);
                    builder.AddLines(lines, true, NoteSize);
                    builder.Y += GapAfterText;
                    break;
                }

                case ExportStep step:
                {
                    string label = step.Number.ToString(CultureInfo.InvariantCulture) + ". ";
                    var lines = WrapAll(label + step.Text, false, TextSize, measure);
                    float textHeight = lines.Count * TextSize * LineFactor;
                    SizeF image = step.HasImage ? ImageSize(step.PixelSize) : SizeF.Empty;
                    float imageHeight = step.HasImage ? GapAfterText + image.Height : 0;

                    builder.KeepTogether(textHeight + imageHeight);
                    builder.AddLines(lines, false, TextSize);
                    if (step.HasImage)
                    {
                        builder.Y += GapAfterText;
                        builder.KeepTogether(image.Height);
                        var rect = new RectangleF(Margin + (ContentWidth - image.Width) / 2, builder.Y, image.Width, image.Height);
                        builder.Images.Add(new PdfImagePlacement(step, rect));
                        builder.Y += image.Height;
                    }
                    builder.Y += GapAfterStep;
                    break;
                }
            }
        }

        return builder.Finish();
    }

    /// <summary>Tamanho da imagem na página: sem ampliar, na largura útil e até 60% da altura da página.</summary>
    public static SizeF ImageSize(Size pixels)
    {
        var natural = new Size(
            Math.Max(1, (int)Math.Round(pixels.Width * PointsPerPixel)),
            Math.Max(1, (int)Math.Round(pixels.Height * PointsPerPixel)));
        Rectangle fit = PrintLayout.Fit(natural, new Rectangle(0, 0, (int)ContentWidth, (int)MaxImageHeight));
        return fit.Size;
    }

    /// <summary>Quebra por palavras; uma palavra mais larga que a linha é cortada por caractere.</summary>
    public static List<string> Wrap(string text, bool bold, float size, float width, PdfTextMeasure measure)
    {
        var lines = new List<string>();
        var current = new StringBuilder();
        foreach (string word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            string candidate = current.Length == 0 ? word : current + " " + word;
            if (measure(candidate, bold, size) <= width)
            {
                current.Clear().Append(candidate);
                continue;
            }

            if (current.Length > 0)
            {
                lines.Add(current.ToString());
                current.Clear();
            }

            string rest = word;
            while (measure(rest, bold, size) > width && rest.Length > 1)
            {
                int take = rest.Length - 1;
                while (take > 1 && measure(rest[..take], bold, size) > width)
                {
                    take--;
                }
                lines.Add(rest[..take]);
                rest = rest[take..];
            }
            current.Append(rest);
        }

        if (current.Length > 0 || lines.Count == 0)
        {
            lines.Add(current.ToString());
        }
        return lines;
    }

    private static List<string> WrapAll(string text, bool bold, float size, PdfTextMeasure measure) =>
        ExportText.Lines(text).SelectMany(l => Wrap(l, bold, size, ContentWidth, measure)).ToList();

    private sealed class PageBuilder
    {
        private readonly List<PdfPagePlan> _pages = [];
        private List<PdfTextLine> _lines = [];

        public List<PdfImagePlacement> Images { get; private set; } = [];

        public float Y { get; set; } = Margin;

        private bool PageIsEmpty => _lines.Count == 0 && Images.Count == 0;

        /// <summary>Vai para a próxima página se o bloco não cabe no que sobrou mas cabe numa página nova.</summary>
        public void KeepTogether(float height)
        {
            if (!PageIsEmpty && Y + height > ContentBottom)
            {
                NewPage();
            }
        }

        public void AddLines(IReadOnlyList<string> lines, bool bold, float size)
        {
            float lineHeight = size * LineFactor;
            foreach (string line in lines)
            {
                if (!PageIsEmpty && Y + lineHeight > ContentBottom)
                {
                    NewPage();
                }
                _lines.Add(new PdfTextLine(line, bold, size, Margin, Y + size));
                Y += lineHeight;
            }
        }

        public IReadOnlyList<PdfPagePlan> Finish()
        {
            if (!PageIsEmpty || _pages.Count == 0)
            {
                NewPage();
            }
            return _pages;
        }

        private void NewPage()
        {
            _pages.Add(new PdfPagePlan(_lines, Images));
            _lines = [];
            Images = [];
            Y = Margin;
        }
    }
}
