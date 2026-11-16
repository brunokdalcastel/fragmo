namespace Fragmo.Ocr;

/// <summary>
/// Onde pôr as tarjas automáticas: converte os dados sensíveis achados no OCR de um recorte para coordenadas
/// de <paramref name="area"/>, com folga, sem passar da área. Lógica pura, usada pelo overlay e pelo modo tutorial.
/// </summary>
public static class SensitiveRedactor
{
    public static IReadOnlyList<Rectangle> Targets(OcrText text, Rectangle area, int padding) =>
        SensitiveDataDetector.Detect(text.Lines)
            .Select(r =>
            {
                r.Offset(area.Location);
                return Rectangle.Intersect(Rectangle.Inflate(r, padding, padding), area);
            })
            .Where(r => !r.IsEmpty)
            .ToList();
}
