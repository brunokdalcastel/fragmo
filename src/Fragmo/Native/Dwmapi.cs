using System.Runtime.InteropServices;

namespace Fragmo.Native;

internal static partial class Dwmapi
{
    /// <summary>Limites visíveis da janela, sem a sombra invisível do Windows 10/11.</summary>
    public const int DWMWA_EXTENDED_FRAME_BOUNDS = 9;

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [LibraryImport("dwmapi.dll")]
    public static partial int DwmGetWindowAttribute(IntPtr hwnd, int attr, out RECT rect, int size);
}
