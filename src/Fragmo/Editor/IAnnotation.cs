namespace Fragmo.Editor;

public interface IAnnotation
{
    Rectangle Bounds { get; }            // usado para invalidar só a área necessária
    void Draw(Graphics g, Bitmap source); // source: bitmap base (usado pelo desfoque na Fase 4)
}

public sealed record StrokeStyle(Color Color, int Thickness);
