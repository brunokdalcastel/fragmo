using System.Runtime.InteropServices;

namespace Fragmo.Native;

internal static partial class User32
{
    public const int WM_HOTKEY = 0x0312;
    public const int WM_DPICHANGED = 0x02E0;

    public const uint MOD_ALT = 1;
    public const uint MOD_CONTROL = 2;
    public const uint MOD_SHIFT = 4;
    public const uint MOD_WIN = 8;
    public const uint MOD_NOREPEAT = 0x4000;

    public const uint SWP_NOZORDER = 0x4;
    public const uint SWP_NOACTIVATE = 0x10;

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int X;
        public int Y;
    }

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool DestroyIcon(IntPtr hIcon);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool UnregisterHotKey(IntPtr hWnd, int id);

    public const int WH_KEYBOARD_LL = 13;
    public const int WM_KEYUP = 0x0101;
    public const int WM_SYSKEYUP = 0x0105;
    public const int WM_APP = 0x8000;

    [StructLayout(LayoutKind.Sequential)]
    public struct KBDLLHOOKSTRUCT
    {
        public uint VkCode;
        public uint ScanCode;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }

    public delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    [LibraryImport("user32.dll", EntryPoint = "SetWindowsHookExW", SetLastError = true)]
    public static partial IntPtr SetWindowsHookEx(int idHook, IntPtr lpfn, IntPtr hmod, uint dwThreadId);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool UnhookWindowsHookEx(IntPtr hhk);

    [LibraryImport("user32.dll")]
    public static partial IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [LibraryImport("user32.dll")]
    public static partial short GetAsyncKeyState(int vKey);

    [LibraryImport("user32.dll", EntryPoint = "PostMessageW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool PostMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    [LibraryImport("kernel32.dll", EntryPoint = "GetModuleHandleW", StringMarshalling = StringMarshalling.Utf16)]
    public static partial IntPtr GetModuleHandle(string? lpModuleName);

    [LibraryImport("user32.dll")]
    public static partial IntPtr GetForegroundWindow();

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GetCursorPos(out POINT lpPoint);

    // ---- Modo tutorial: ganchos de mouse/teclado numa thread própria ----

    public const int WH_MOUSE_LL = 14;
    public const int WM_KEYDOWN = 0x0100;
    public const int WM_SYSKEYDOWN = 0x0104;
    public const int WM_LBUTTONDOWN = 0x0201;
    public const int WM_LBUTTONUP = 0x0202;
    public const int WM_RBUTTONDOWN = 0x0204;
    public const int WM_QUIT = 0x0012;
    public const int WM_TIMER = 0x0113;
    public const uint LLMHF_INJECTED = 0x1;
    public const uint LLKHF_INJECTED = 0x10;
    public const uint GA_ROOT = 2;
    public const int SM_CXDOUBLECLK = 36;
    public const int SM_CYDOUBLECLK = 37;
    public const uint WDA_NONE = 0x0;
    public const uint WDA_EXCLUDEFROMCAPTURE = 0x11;

    [StructLayout(LayoutKind.Sequential)]
    public struct MSLLHOOKSTRUCT
    {
        public POINT Pt;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public nuint ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MSG
    {
        public IntPtr Hwnd;
        public uint Message;
        public IntPtr WParam;
        public IntPtr LParam;
        public uint Time;
        public POINT Pt;
        public uint Private;
    }

    public delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

    [LibraryImport("user32.dll")]
    public static partial IntPtr WindowFromPoint(POINT point);

    [LibraryImport("user32.dll")]
    public static partial IntPtr GetAncestor(IntPtr hwnd, uint gaFlags);

    [LibraryImport("user32.dll", SetLastError = true)]
    public static partial uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [LibraryImport("user32.dll")]
    public static partial uint GetDoubleClickTime();

    [LibraryImport("user32.dll")]
    public static partial int GetSystemMetrics(int nIndex);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool SetWindowDisplayAffinity(IntPtr hWnd, uint dwAffinity);

    /// <summary>Devolve &gt; 0 para mensagem, 0 para WM_QUIT e -1 em erro.</summary>
    [LibraryImport("user32.dll", EntryPoint = "GetMessageW", SetLastError = true)]
    public static partial int GetMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool TranslateMessage(in MSG lpMsg);

    [LibraryImport("user32.dll", EntryPoint = "DispatchMessageW")]
    public static partial IntPtr DispatchMessage(in MSG lpMsg);

    [LibraryImport("user32.dll", EntryPoint = "PostThreadMessageW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool PostThreadMessage(uint idThread, int msg, IntPtr wParam, IntPtr lParam);

    [LibraryImport("user32.dll", SetLastError = true)]
    public static partial nuint SetTimer(IntPtr hWnd, nuint nIDEvent, uint uElapse, IntPtr lpTimerFunc);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool KillTimer(IntPtr hWnd, nuint uIDEvent);

    [LibraryImport("kernel32.dll")]
    public static partial uint GetCurrentThreadId();

    [LibraryImport("user32.dll", EntryPoint = "GetWindowTextW", SetLastError = true)]
    private static unsafe partial int GetWindowText(IntPtr hWnd, char* lpString, int nMaxCount);

    /// <summary>Título da janela (vazio se não houver), limitado a 512 caracteres.</summary>
    public static unsafe string GetWindowText(IntPtr hWnd)
    {
        const int MaxLength = 512;
        char* buffer = stackalloc char[MaxLength];
        int length = GetWindowText(hWnd, buffer, MaxLength);
        return length > 0 ? new string(buffer, 0, length) : string.Empty;
    }

    [LibraryImport("user32.dll", EntryPoint = "GetClassNameW", SetLastError = true)]
    private static unsafe partial int GetClassName(IntPtr hWnd, char* lpClassName, int nMaxCount);

    /// <summary>Nome da classe da janela (vazio se falhar).</summary>
    public static unsafe string GetClassName(IntPtr hWnd)
    {
        const int MaxLength = 256;
        char* buffer = stackalloc char[MaxLength];
        int length = GetClassName(hWnd, buffer, MaxLength);
        return length > 0 ? new string(buffer, 0, length) : string.Empty;
    }
}
