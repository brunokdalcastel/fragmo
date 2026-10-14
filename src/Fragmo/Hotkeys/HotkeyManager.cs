using Fragmo.Native;

namespace Fragmo.Hotkeys;

internal enum HotkeyAction { CaptureArea, CaptureFullScreen, CaptureWindow, RepeatLastArea }

/// <summary>
/// Registra atalhos globais numa janela só de mensagens e avisa quando são pressionados.
/// </summary>
internal sealed class HotkeyManager : NativeWindow, IDisposable
{
    private static readonly IntPtr HwndMessage = new(-3);

    private readonly HashSet<int> _registered = [];

    public event Action<HotkeyAction>? Pressed;

    public HotkeyManager()
    {
        CreateHandle(new CreateParams { Parent = HwndMessage });
    }

    public bool TryRegister(HotkeyAction action, HotkeyBinding binding)
    {
        int id = (int)action + 1;
        if (_registered.Remove(id))
        {
            User32.UnregisterHotKey(Handle, id);
        }

        if (!User32.RegisterHotKey(Handle, id, (uint)binding.Modifiers | User32.MOD_NOREPEAT, (uint)binding.Key))
        {
            return false;
        }

        _registered.Add(id);
        return true;
    }

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
        base.WndProc(ref m);
    }

    public void Dispose()
    {
        foreach (int id in _registered)
        {
            User32.UnregisterHotKey(Handle, id);
        }
        _registered.Clear();
        DestroyHandle();
    }
}
