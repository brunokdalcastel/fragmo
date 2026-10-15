using System.Runtime.InteropServices;
using Fragmo.Native;

namespace Fragmo.Hotkeys;

// CaptureAreaAlt: o atalho alternativo da área (Ctrl+Shift+S), registrado junto com o principal.
internal enum HotkeyAction { CaptureArea, CaptureFullScreen, CaptureWindow, RepeatLastArea, CaptureAreaAlt }

/// <summary>
/// Registra atalhos globais numa janela só de mensagens e avisa quando são pressionados.
/// </summary>
/// <remarks>
/// A Print Screen sozinha também é escutada por um gancho de teclado: em alguns Windows o registro dela é aceito,
/// mas o aviso do atalho nunca chega. O gancho segura a tecla, então o aviso do registro não chega em dobro.
/// </remarks>
internal sealed class HotkeyManager : NativeWindow, IDisposable
{
    private static readonly IntPtr HwndMessage = new(-3);
    private const int WM_PRINTSCREEN = User32.WM_APP + 1;
    private const int VK_SHIFT = 0x10, VK_CONTROL = 0x11, VK_MENU = 0x12, VK_LWIN = 0x5B, VK_RWIN = 0x5C;

    private readonly HashSet<int> _registered = [];
    private readonly PrintScreenKeyFilter _filter = new();
    private readonly User32.LowLevelKeyboardProc _hookProc;
    // O Windows desliga em silêncio um gancho que demorou a responder; reinstalar de tempos em tempos o traz de volta.
    private readonly System.Windows.Forms.Timer _hookRefresh = new() { Interval = 60_000 };
    private IntPtr _hook;
    private HotkeyAction? _printScreenAction;

    public event Action<HotkeyAction>? Pressed;

    public HotkeyManager()
    {
        CreateHandle(new CreateParams { Parent = HwndMessage });
        _hookProc = OnKeyboardHook;
        _hookRefresh.Tick += (_, _) => InstallHook();
    }

    public bool TryRegister(HotkeyAction action, HotkeyBinding binding)
    {
        int id = (int)action + 1;
        if (_registered.Remove(id))
        {
            User32.UnregisterHotKey(Handle, id);
        }
        if (_printScreenAction == action)
        {
            StopPrintScreenHook();
        }

        if (!User32.RegisterHotKey(Handle, id, (uint)binding.Modifiers | User32.MOD_NOREPEAT, (uint)binding.Key))
        {
            return false;
        }

        _registered.Add(id);
        if (binding == new HotkeyBinding(HotkeyModifiers.None, Keys.PrintScreen))
        {
            _printScreenAction = action;
            InstallHook();
            _hookRefresh.Start();
        }
        return true;
    }

    /// <summary>Libera todos os atalhos, para registrar de novo (ex.: depois de trocar nas configurações).</summary>
    public void UnregisterAll()
    {
        StopPrintScreenHook();
        foreach (int id in _registered)
        {
            User32.UnregisterHotKey(Handle, id);
        }
        _registered.Clear();
    }

    private void InstallHook()
    {
        RemoveHook();
        _hook = User32.SetWindowsHookEx(User32.WH_KEYBOARD_LL, Marshal.GetFunctionPointerForDelegate(_hookProc),
            User32.GetModuleHandle(null), 0);
    }

    private void RemoveHook()
    {
        if (_hook != IntPtr.Zero)
        {
            User32.UnhookWindowsHookEx(_hook);
            _hook = IntPtr.Zero;
        }
    }

    private void StopPrintScreenHook()
    {
        _hookRefresh.Stop();
        RemoveHook();
        _printScreenAction = null;
    }

    private IntPtr OnKeyboardHook(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            var key = Marshal.PtrToStructure<User32.KBDLLHOOKSTRUCT>(lParam);
            int msg = (int)wParam;
            bool keyUp = msg is User32.WM_KEYUP or User32.WM_SYSKEYUP;
            PrintScreenKeyFilter.Decision decision = _filter.Handle((int)key.VkCode, keyUp, AnyModifierDown());
            if (decision.Fire)
            {
                // O gancho precisa responder rápido: a captura roda depois, pela fila de mensagens.
                User32.PostMessage(Handle, WM_PRINTSCREEN, IntPtr.Zero, IntPtr.Zero);
            }
            if (decision.Swallow)
            {
                return 1;
            }
        }
        return User32.CallNextHookEx(_hook, nCode, wParam, lParam);
    }

    private static bool AnyModifierDown() =>
        IsDown(VK_SHIFT) || IsDown(VK_CONTROL) || IsDown(VK_MENU) || IsDown(VK_LWIN) || IsDown(VK_RWIN);

    private static bool IsDown(int vk) => (User32.GetAsyncKeyState(vk) & 0x8000) != 0;

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == User32.WM_HOTKEY)
        {
            var action = (HotkeyAction)((int)m.WParam - 1);
            if (Enum.IsDefined(action))
            {
                Pressed?.Invoke(action);
            }
            return;
        }
        if (m.Msg == WM_PRINTSCREEN)
        {
            if (_printScreenAction is HotkeyAction action)
            {
                Pressed?.Invoke(action);
            }
            return;
        }
        base.WndProc(ref m);
    }

    public void Dispose()
    {
        UnregisterAll();
        _hookRefresh.Dispose();
        DestroyHandle();
    }
}
