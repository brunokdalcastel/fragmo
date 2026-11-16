using System.Drawing.Drawing2D;
using Fragmo.Editor;
using Fragmo.Ui;

namespace Fragmo.Tutorial.Export;

/// <summary>
/// Monta a imagem final de um passo: print cru + tarjas + destaque do elemento + marcador numerado (D7).
/// Passo editado (D8) já traz tudo achatado na imagem, então nada é desenhado por cima.
/// </summary>
internal static class StepImageRenderer
{
    public const int HighlightThickness = 3;
    public const int MarkerRadius = 14;
    public const int ThumbnailWidth = 160;

    /// <summary>Carrega a imagem do passo de <paramref name="folder"/> e devolve a imagem composta.</summary>
    public static Bitmap Render(TutorialItem step, int stepNumber, string folder)
    {
        using Bitmap source = LoadImage(step, folder);
        return Compose(source, step, stepNumber);
    }

    /// <summary>Imagem composta reduzida para a lista do editor (largura fixa, proporção mantida).</summary>
    public static Bitmap RenderThumbnail(TutorialItem step, int stepNumber, string folder)
    {
        using Bitmap full = Render(step, stepNumber, folder);
        int height = Math.Max(1, (int)Math.Round(full.Height * (double)ThumbnailWidth / full.Width));
        var thumb = new Bitmap(ThumbnailWidth, height);
        using Graphics g = Graphics.FromImage(thumb);
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.DrawImage(full, new Rectangle(0, 0, ThumbnailWidth, height));
        return thumb;
    }

    /// <summary>Composição sem disco (usada pelos testes e pelo editor, que já tem o bitmap).</summary>
    public static Bitmap Compose(Bitmap source, TutorialItem step, int stepNumber) =>
        ImageComposer.Compose(source, Annotations(step, stepNumber), new Rectangle(Point.Empty, source.Size));

    /// <summary>Ordem: tarjas → retângulo do elemento → marcador no ponto do clique.</summary>
    public static IReadOnlyList<IAnnotation> Annotations(TutorialItem step, int stepNumber)
    {
        if (step.Edited)
        {
            return [];
        }

        var items = new List<IAnnotation>();
        foreach (RectDto r in step.Redactions)
        {
            items.Add(new RedactAnnotation(new Rectangle(r.X, r.Y, r.Width, r.Height)));
        }

        if (step.ShowHighlight && step.Highlight is { } h)
        {
            items.Add(new RectangleAnnotation(
                new StrokeStyle(Palette.Accent, HighlightThickness),
                new Rectangle(h.X, h.Y, h.Width, h.Height)));
        }

        if (step.ShowHighlight && step.ClickPoint is { } p)
        {
            items.Add(new StepAnnotation(Palette.Accent, stepNumber, new Point(p.X, p.Y), MarkerRadius));
        }

        return items;
    }

    /// <summary>
    /// Lê o PNG sem manter o arquivo aberto (o GDI+ trava o arquivo enquanto o <see cref="Bitmap"/> vive).
    /// O caminho passa pela mesma regra do pacote, para um <c>tutorial.json</c> adulterado não ler fora da pasta.
    /// </summary>
    private static Bitmap LoadImage(TutorialItem step, string folder)
    {
        if (step.ImageFile is not { } relative || !TutorialSerializer.IsValidImagePath(relative))
        {
            throw new TutorialFormatException($"Passo sem imagem válida: {step.ImageFile ?? "(nenhuma)"}");
        }

        byte[] bytes = File.ReadAllBytes(Path.Combine(folder, relative.Replace('/', Path.DirectorySeparatorChar)));
        using var stream = new MemoryStream(bytes);
        using var image = Image.FromStream(stream);
        return new Bitmap(image);
    }
}
