namespace Fragmo.Capture;

/// <summary>Converte entre coordenadas de tela (virtual) e coordenadas do bitmap capturado.</summary>
public static class CoordinateMapper
{
    public static Point ScreenToBitmap(Point screen, Rectangle virtualBounds) =>
        new(screen.X - virtualBounds.X, screen.Y - virtualBounds.Y);

    public static Rectangle BitmapToScreen(Rectangle r, Rectangle virtualBounds) =>
        new(r.X + virtualBounds.X, r.Y + virtualBounds.Y, r.Width, r.Height);
}
