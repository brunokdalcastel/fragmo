namespace Fragmo.Output;

/// <summary>Posição da imagem na página impressa.</summary>
public static class PrintLayout
{
    /// <summary>Encaixa a imagem em <paramref name="bounds"/> mantendo a proporção, centralizada e sem ampliar.</summary>
    public static Rectangle Fit(Size image, Rectangle bounds)
    {
        if (image.Width <= 0 || image.Height <= 0 || bounds.Width <= 0 || bounds.Height <= 0)
        {
            return Rectangle.Empty;
        }

        double scale = Math.Min(1.0, Math.Min((double)bounds.Width / image.Width, (double)bounds.Height / image.Height));
        int width = Math.Max(1, (int)Math.Round(image.Width * scale));
        int height = Math.Max(1, (int)Math.Round(image.Height * scale));
        return new Rectangle(
            bounds.X + (bounds.Width - width) / 2,
            bounds.Y + (bounds.Height - height) / 2,
            width,
            height);
    }
}
