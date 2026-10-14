namespace Fragmo.Overlay;

/// <summary>Cálculos da seleção retangular: normalizar, limitar, alças, redimensionar e mover.</summary>
public static class SelectionGeometry
{
    public enum Handle { None, Move, N, S, E, W, NE, NW, SE, SW }

    public static Rectangle FromPoints(Point a, Point b) => Normalize(a.X, a.Y, b.X, b.Y);

    public static Rectangle Clamp(Rectangle r, Size bounds)
    {
        Rectangle result = Rectangle.Intersect(r, new Rectangle(Point.Empty, bounds));
        return result.Width > 0 && result.Height > 0 ? result : Rectangle.Empty;
    }

    public static Handle HitTest(Rectangle sel, Point p, int grip = 6)
    {
        if (sel.Width <= 0 || sel.Height <= 0)
        {
            return Handle.None;
        }

        bool nearLeft = Math.Abs(p.X - sel.Left) <= grip;
        bool nearRight = Math.Abs(p.X - sel.Right) <= grip;
        bool nearTop = Math.Abs(p.Y - sel.Top) <= grip;
        bool nearBottom = Math.Abs(p.Y - sel.Bottom) <= grip;
        bool withinX = p.X >= sel.Left - grip && p.X <= sel.Right + grip;
        bool withinY = p.Y >= sel.Top - grip && p.Y <= sel.Bottom + grip;

        if (nearTop && nearLeft) return Handle.NW;
        if (nearTop && nearRight) return Handle.NE;
        if (nearBottom && nearLeft) return Handle.SW;
        if (nearBottom && nearRight) return Handle.SE;
        if (nearTop && withinX) return Handle.N;
        if (nearBottom && withinX) return Handle.S;
        if (nearLeft && withinY) return Handle.W;
        if (nearRight && withinY) return Handle.E;
        if (sel.Contains(p)) return Handle.Move;
        return Handle.None;
    }

    public static Rectangle Resize(Rectangle original, Handle h, Point delta)
    {
        int left = original.Left, top = original.Top, right = original.Right, bottom = original.Bottom;

        if (h is Handle.W or Handle.NW or Handle.SW) left += delta.X;
        if (h is Handle.E or Handle.NE or Handle.SE) right += delta.X;
        if (h is Handle.N or Handle.NW or Handle.NE) top += delta.Y;
        if (h is Handle.S or Handle.SW or Handle.SE) bottom += delta.Y;

        return Normalize(left, top, right, bottom);
    }

    public static Rectangle Move(Rectangle original, Point delta, Size bounds)
    {
        int x = Math.Clamp(original.X + delta.X, 0, Math.Max(0, bounds.Width - original.Width));
        int y = Math.Clamp(original.Y + delta.Y, 0, Math.Max(0, bounds.Height - original.Height));
        return new Rectangle(x, y, original.Width, original.Height);
    }

    public static Rectangle MagnifierSource(Point p, int radius) =>
        new(p.X - radius, p.Y - radius, 2 * radius + 1, 2 * radius + 1);

    private static Rectangle Normalize(int x1, int y1, int x2, int y2) =>
        new(Math.Min(x1, x2), Math.Min(y1, y2), Math.Abs(x2 - x1), Math.Abs(y2 - y1));
}
