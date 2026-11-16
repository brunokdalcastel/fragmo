using System.Diagnostics;
using System.Runtime.InteropServices;
using Fragmo.Hotkeys;
using Fragmo.Native;

namespace Fragmo.Tutorial.Input;

/// <summary>
/// Ganchos globais de mouse e teclado numa thread dedicada com o seu próprio laço de mensagens.
/// Os callbacks só montam um <see cref="RawInputEvent"/> e o entregam ao <c>sink</c>, que deve só enfileirar.
/// </summary>
internal sealed class InputHookThread : IDisposable
{
    private const int VK_SHIFT = 0x10, VK_CONTROL = 0x11, VK_MENU = 0x12, VK_LWIN = 0x5B, VK_RWIN = 0x5C;
    private const int VK_LSHIFT = 0xA0, VK_RMENU = 0xA5;
    private const uint RefreshTimerMs = 60_000;

    private readonly Action<RawInputEvent> _sink;
    // Delegates em campos: o GC não pode coletá-los enquanto os ganchos existem.
    private readonly User32.LowLevelMouseProc _mouseProc;
    private readonly User32.LowLevelKeyboardProc _keyboardProc;
    private readonly ManualResetEventSlim _started = new(false);
    private Thread? _thread;
    private uint _threadId;
    private IntPtr _mouseHook;
    private IntPtr _keyboardHook;
    private bool _disposed;

    public InputHookThread(Action<RawInputEvent> sink)
    {
        _sink = sink;
        _mouseProc = OnMouse;
        _keyboardProc = OnKeyboard;
    }

    /// <summary><c>true</c> se os dois ganchos foram instalados na última tentativa.</summary>
    public bool HooksInstalled { get; private set; }

    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_thread is not null)
        {
            return;
        }
        _thread = new Thread(Run) { IsBackground = true, Name = nameof(InputHookThread) };
        _thread.Start();
        _started.Wait(TimeSpan.FromSeconds(5));
    }

    private void Run()
    {
        _threadId = User32.GetCurrentThreadId();
        InstallHooks();
        nuint timer = User32.SetTimer(IntPtr.Zero, 0, RefreshTimerMs, IntPtr.Zero);
        _started.Set();

        try
        {
            while (User32.GetMessage(out User32.MSG msg, IntPtr.Zero, 0, 0) > 0)
            {
                if (msg.Message == User32.WM_TIMER && msg.Hwnd == IntPtr.Zero && (nuint)msg.WParam == timer)
                {
                    // O Windows desliga em silêncio um gancho lento; reinstalar de tempos em tempos o traz de volta.
                    InstallHooks();
                    continue;
                }
                User32.TranslateMessage(in msg);
                User32.DispatchMessage(in msg);
            }
        }
        finally
        {
            if (timer != 0)
            {
                User32.KillTimer(IntPtr.Zero, timer);
            }
            RemoveHooks();
        }
    }

    private void InstallHooks()
    {
        RemoveHooks();
        IntPtr module = User32.GetModuleHandle(null);
        _mouseHook = User32.SetWindowsHookEx(User32.WH_MOUSE_LL, Marshal.GetFunctionPointerForDelegate(_mouseProc), module, 0);
        _keyboardHook = User32.SetWindowsHookEx(User32.WH_KEYBOARD_LL, Marshal.GetFunctionPointerForDelegate(_keyboardProc), module, 0);
        HooksInstalled = _mouseHook != IntPtr.Zero && _keyboardHook != IntPtr.Zero;
        if (!HooksInstalled)
        {
            Debug.WriteLine($"InputHookThread: falha ao instalar ganchos ({Marshal.GetLastPInvokeError()}).");
        }
    }

    private void RemoveHooks()
    {
        if (_mouseHook != IntPtr.Zero)
        {
            User32.UnhookWindowsHookEx(_mouseHook);
            _mouseHook = IntPtr.Zero;
        }
        if (_keyboardHook != IntPtr.Zero)
        {
            User32.UnhookWindowsHookEx(_keyboardHook);
            _keyboardHook = IntPtr.Zero;
        }
    }

    private IntPtr OnMouse(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            int msg = (int)wParam;
            if (msg is User32.WM_LBUTTONDOWN or User32.WM_RBUTTONDOWN)
            {
                var data = Marshal.PtrToStructure<User32.MSLLHOOKSTRUCT>(lParam);
                if ((data.Flags & User32.LLMHF_INJECTED) == 0)
                {
                    var kind = msg == User32.WM_LBUTTONDOWN ? RawInputKind.LeftDown : RawInputKind.RightDown;
                    _sink(new RawInputEvent(kind, new Point(data.Pt.X, data.Pt.Y), 0, CurrentModifiers(), data.Time));
                }
            }
        }
        return User32.CallNextHookEx(_mouseHook, nCode, wParam, lParam);
    }

    private IntPtr OnKeyboard(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            int msg = (int)wParam;
            if (msg is User32.WM_KEYDOWN or User32.WM_SYSKEYDOWN)
            {
                var data = Marshal.PtrToStructure<User32.KBDLLHOOKSTRUCT>(lParam);
                int vk = (int)data.VkCode;
                if ((data.Flags & User32.LLKHF_INJECTED) == 0 && !IsModifierKey(vk))
                {
                    _sink(new RawInputEvent(RawInputKind.KeyDown, Point.Empty, vk, CurrentModifiers(), data.Time));
                }
            }
        }
        return User32.CallNextHookEx(_keyboardHook, nCode, wParam, lParam);
    }

    internal static bool IsModifierKey(int vk) =>
        vk is VK_SHIFT or VK_CONTROL or VK_MENU or VK_LWIN or VK_RWIN or (>= VK_LSHIFT and <= VK_RMENU);

    private static HotkeyModifiers CurrentModifiers()
    {
        var mods = HotkeyModifiers.None;
        if (IsDown(VK_CONTROL))
        {
            mods |= HotkeyModifiers.Control;
        }
        if (IsDown(VK_SHIFT))
        {
            mods |= HotkeyModifiers.Shift;
        }
        if (IsDown(VK_MENU))
        {
            mods |= HotkeyModifiers.Alt;
        }
        if (IsDown(VK_LWIN) || IsDown(VK_RWIN))
        {
            mods |= HotkeyModifiers.Win;
        }
        return mods;
    }

    private static bool IsDown(int vk) => (User32.GetAsyncKeyState(vk) & 0x8000) != 0;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        if (_thread is not null)
        {
            // Os ganchos saem na própria thread (finally de Run) antes de ela encerrar.
            User32.PostThreadMessage(_threadId, User32.WM_QUIT, IntPtr.Zero, IntPtr.Zero);
            _thread.Join(TimeSpan.FromSeconds(2));
        }
        _started.Dispose();
    }
}
