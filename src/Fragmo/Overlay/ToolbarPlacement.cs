namespace Fragmo.Overlay;

/// <summary>
/// Onde fica a barra do editor: centralizada abaixo da seleção; sem espaço, acima; se a seleção ocupa
/// a altura toda, dentro dela, junto à borda inferior. Sempre dentro do monitor da seleção.
/// Todas as coordenadas no mesmo sistema (o do bitmap capturado).
/// </summary>
public static class ToolbarPlacement
{
    public const int DefaultGap = 10;

    public static Point Place(Rectangle selection, Size bar, IReadOnlyList<Rectangle> monitors, int gap = DefaultGap)
    {
        Rectangle monitor = MonitorFor(selection, monitors);

        int x = selection.Left + (selection.Width - bar.Width) / 2;

        int y;
        if (selection.Bottom + gap + bar.Height <= monitor.Bottom)
        {
            y = selection.Bottom + gap;
        }
        else if (selection.Top - gap - bar.Height >= monitor.Top)
        {
            y = selection.Top - gap - bar.Height;
        }
        else
        {
            y = selection.Bottom - gap - bar.Height;
        }

        return new Point(Fit(x, monitor.Left, monitor.Right - bar.Width), Fit(y, monitor.Top, monitor.Bottom - bar.Height));
    }

    /// <summary>O monitor com a maior parte da seleção; se ela não toca nenhum, o mais próximo.</summary>
    public static Rectangle MonitorFor(Rectangle selection, IReadOnlyList<Rectangle> monitors)
    {
        if (monitors.Count == 0)
        {
            return selection;
        }

        Rectangle best = monitors[0];
        long bestArea = -1;
        foreach (Rectangle m in monitors)
        {
            Rectangle overlap = Rectangle.Intersect(m, selection);
            long area = (long)overlap.Width * overlap.Height;
            if (area > bestArea)
            {
                best = m;
                bestArea = area;
            }
        }
        if (bestArea > 0)
        {
            return best;
        }

        var center = new Point(selection.Left + selection.Width / 2, selection.Top + selection.Height / 2);
        return monitors.MinBy(m => DistanceSquared(m, center));
    }

    // Se a barra for maior que o monitor, fica encostada no começo dele.
    private static int Fit(int value, int min, int max) => Math.Max(min, Math.Min(value, max));

    private static long DistanceSquared(Rectangle r, Point p)
    {
        long dx = p.X < r.Left ? r.Left - p.X : p.X >= r.Right ? p.X - r.Right + 1 : 0;
        long dy = p.Y < r.Top ? r.Top - p.Y : p.Y >= r.Bottom ? p.Y - r.Bottom + 1 : 0;
        return dx * dx + dy * dy;
    }
}
