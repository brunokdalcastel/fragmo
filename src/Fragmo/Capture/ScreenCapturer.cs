using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Fragmo.Native;

namespace Fragmo.Capture;

/// <summary>Imagem da tela virtual inteira e a posição dela (a origem pode ser negativa).</summary>
internal sealed record CapturedScreen(Bitmap Image, Rectangle VirtualBounds) : IDisposable
{
    public void Dispose() => Image.Dispose();
}

internal static class ScreenCapturer
{
    // Área de trabalho e barra de tarefas não contam como "janela ativa".
    private static readonly string[] IgnoredWindowClasses = ["Progman", "WorkerW", "Shell_TrayWnd"];

    public static CapturedScreen CaptureVirtualScreen()
    {
        Rectangle vs = SystemInformation.VirtualScreen;
        var bmp = new Bitmap(vs.Width, vs.Height, PixelFormat.Format32bppArgb);
        try
        {
            using (var g = Graphics.FromImage(bmp))
            {
                g.CopyFromScreen(vs.Left, vs.Top, 0, 0, vs.Size, CopyPixelOperation.SourceCopy);
            }
            return new CapturedScreen(bmp, vs);
        }
        catch
        {
            bmp.Dispose();
            throw;
        }
    }

    public static Bitmap Crop(Bitmap source, Rectangle bitmapRect)
    {
        Rectangle rect = Rectangle.Intersect(bitmapRect, new Rectangle(Point.Empty, source.Size));
        if (rect.Width <= 0 || rect.Height <= 0)
        {
            throw new ArgumentException("A área selecionada está fora da imagem.", nameof(bitmapRect));
        }
        return source.Clone(rect, PixelFormat.Format32bppArgb);
    }

    /// <summary>Limites da janela ativa em coordenadas de tela, sem a sombra; <c>null</c> se não houver.</summary>
    public static Rectangle? GetForegroundWindowBounds()
    {
        IntPtr hwnd = User32.GetForegroundWindow();
        if (hwnd == IntPtr.Zero || IgnoredWindowClasses.Contains(User32.GetClassName(hwnd), StringComparer.Ordinal))
        {
            return null;
        }

        int hr = Dwmapi.DwmGetWindowAttribute(hwnd, Dwmapi.DWMWA_EXTENDED_FRAME_BOUNDS,
            out Dwmapi.RECT r, Marshal.SizeOf<Dwmapi.RECT>());
        if (hr != 0 || r.Right <= r.Left || r.Bottom <= r.Top)
        {
            return null;
        }
        return Rectangle.FromLTRB(r.Left, r.Top, r.Right, r.Bottom);
    }

    public static Rectangle GetPrimaryScreenBounds() => Screen.PrimaryScreen!.Bounds;
}
