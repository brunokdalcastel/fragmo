using Fragmo.Capture;

namespace Fragmo.Editor;

/// <summary>Único caminho de exportação: bitmap base + anotações, recortado à seleção.</summary>
internal static class ImageComposer
{
    public static Bitmap Compose(Bitmap baseImage, IReadOnlyList<IAnnotation> items, Rectangle selection)
    {
        using var result = new Bitmap(baseImage);
        using (var g = Graphics.FromImage(result))
        {
            foreach (IAnnotation item in items)
            {
                item.Draw(g, baseImage);
            }
        }
        return ScreenCapturer.Crop(result, selection);
    }
}
