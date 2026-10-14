using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage.Streams;

namespace Fragmo.Ocr;

/// <summary>
/// Reconhecimento de texto com o OCR do próprio Windows (<c>Windows.Media.Ocr</c>): roda no PC, sem rede.
/// Usa os idiomas do perfil do Windows.
/// </summary>
internal static class OcrService
{
    // Texto de tela é pequeno; ampliar antes do OCR melhora bastante o reconhecimento.
    private const double SmallImageScale = 2.0;
    private const long SmallImageMaxPixels = 2_000_000;

    /// <summary>
    /// Reconhece o texto da imagem. Devolve <c>null</c> quando não há idioma de OCR instalado no Windows.
    /// Os retângulos voltam nas coordenadas de <paramref name="image"/>.
    /// </summary>
    public static async Task<OcrText?> RecognizeAsync(Bitmap image)
    {
        OcrEngine? engine = OcrEngine.TryCreateFromUserProfileLanguages();
        if (engine is null)
        {
            return null;
        }

        double scale = ChooseScale(image.Size, (int)OcrEngine.MaxImageDimension);
        using Bitmap input = Scale(image, scale);
        using SoftwareBitmap software = await ToSoftwareBitmapAsync(input);
        OcrResult result = await engine.RecognizeAsync(software);

        var lines = new List<OcrLineBox>(result.Lines.Count);
        foreach (OcrLine line in result.Lines)
        {
            var words = line.Words
                .Select(w => new OcrWordBox(w.Text, ToRectangle(w.BoundingRect, scale)))
                .ToList();
            if (words.Count > 0)
            {
                lines.Add(new OcrLineBox(words));
            }
        }
        return new OcrText(lines);
    }

    /// <summary>
    /// Fator de escala da imagem antes do OCR: amplia imagens pequenas e reduz as que passam do
    /// tamanho máximo aceito pelo Windows.
    /// </summary>
    public static double ChooseScale(Size size, int maxDimension)
    {
        int largest = Math.Max(size.Width, size.Height);
        if (largest <= 0)
        {
            return 1.0;
        }
        if (largest > maxDimension)
        {
            return (double)maxDimension / largest;
        }
        bool small = (long)size.Width * size.Height <= SmallImageMaxPixels;
        if (small && largest * SmallImageScale <= maxDimension)
        {
            return SmallImageScale;
        }
        return 1.0;
    }

    private static Bitmap Scale(Bitmap image, double scale)
    {
        if (scale == 1.0)
        {
            return new Bitmap(image);
        }
        var size = new Size(Math.Max(1, (int)Math.Round(image.Width * scale)), Math.Max(1, (int)Math.Round(image.Height * scale)));
        var scaled = new Bitmap(size.Width, size.Height, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(scaled);
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.DrawImage(image, new Rectangle(Point.Empty, size));
        return scaled;
    }

    private static async Task<SoftwareBitmap> ToSoftwareBitmapAsync(Bitmap image)
    {
        byte[] png;
        using (var memory = new MemoryStream())
        {
            image.Save(memory, ImageFormat.Png);
            png = memory.ToArray();
        }

        using var stream = new InMemoryRandomAccessStream();
        await stream.WriteAsync(png.AsBuffer());
        stream.Seek(0);
        BitmapDecoder decoder = await BitmapDecoder.CreateAsync(stream);
        return await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
    }

    private static Rectangle ToRectangle(Windows.Foundation.Rect r, double scale)
    {
        int left = (int)Math.Floor(r.X / scale);
        int top = (int)Math.Floor(r.Y / scale);
        int right = (int)Math.Ceiling((r.X + r.Width) / scale);
        int bottom = (int)Math.Ceiling((r.Y + r.Height) / scale);
        return Rectangle.FromLTRB(left, top, right, bottom);
    }
}
