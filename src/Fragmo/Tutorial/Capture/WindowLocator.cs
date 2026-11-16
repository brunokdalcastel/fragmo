using Fragmo.Capture;
using Fragmo.Native;

namespace Fragmo.Tutorial.Capture;

/// <summary>Decide qual área da tela vira o print de um passo.</summary>
internal static class WindowLocator
{
    // Área de trabalho, barras de tarefas e menus soltos: o print é o monitor inteiro.
    private static readonly string[] MonitorClasses = ["Progman", "WorkerW", "Shell_TrayWnd", "Shell_SecondaryTrayWnd", "#32768"];
    private const int MinWidth = 200, MinHeight = 100;

    /// <summary>Área do print para um clique no ponto (pixels físicos).</summary>
    public static Rectangle GetCaptureBounds(Point screenPoint, out string windowTitle, out bool isOwnProcess)
    {
        IntPtr hwnd = User32.WindowFromPoint(new User32.POINT { X = screenPoint.X, Y = screenPoint.Y });
        IntPtr root = hwnd == IntPtr.Zero ? IntPtr.Zero : User32.GetAncestor(hwnd, User32.GA_ROOT);
        return BoundsFor(root, screenPoint, out windowTitle, out isOwnProcess);
    }

    /// <summary>Área do print para digitação, atalhos e passo manual (janela em primeiro plano).</summary>
    public static Rectangle GetForegroundCaptureBounds(out string windowTitle, out bool isOwnProcess)
    {
        IntPtr hwnd = User32.GetForegroundWindow();
        Point reference = Cursor.Position;
        if (hwnd != IntPtr.Zero && ScreenCapturer.GetWindowFrameBounds(hwnd) is Rectangle frame)
        {
            reference = new Point(frame.Left + frame.Width / 2, frame.Top + frame.Height / 2);
        }
        return BoundsFor(hwnd, reference, out windowTitle, out isOwnProcess);
    }

    /// <summary><c>true</c> se a janela pertence a este processo.</summary>
    public static bool IsOwnWindow(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
        {
            return false;
        }
        User32.GetWindowThreadProcessId(hwnd, out uint pid);
        return pid == (uint)Environment.ProcessId;
    }

    private static Rectangle BoundsFor(IntPtr root, Point referencePoint, out string windowTitle, out bool isOwnProcess)
    {
        isOwnProcess = IsOwnWindow(root);
        windowTitle = string.Empty;
        Rectangle monitor = Screen.FromPoint(referencePoint).Bounds;

        if (root == IntPtr.Zero || MonitorClasses.Contains(User32.GetClassName(root), StringComparer.Ordinal))
        {
            return monitor;
        }

        if (ScreenCapturer.GetWindowFrameBounds(root) is not Rectangle frame || frame.Width < MinWidth || frame.Height < MinHeight)
        {
            return monitor;
        }

        Rectangle clipped = Rectangle.Intersect(frame, SystemInformation.VirtualScreen);
        if (clipped.Width <= 0 || clipped.Height <= 0)
        {
            return monitor;
        }
        windowTitle = User32.GetWindowText(root);
        return clipped;
    }
}
