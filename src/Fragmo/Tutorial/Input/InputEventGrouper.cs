using Fragmo.Hotkeys;

namespace Fragmo.Tutorial.Input;

internal enum GroupedKind { NewClick, NewRightClick, PromoteLastToDoubleClick, TypingStarted, TypingFinished, Shortcut }

/// <summary>Uma ação já agrupada. <see cref="Shortcut"/> só vem preenchido em <see cref="GroupedKind.Shortcut"/>.</summary>
internal sealed record GroupedAction(GroupedKind Kind, RawInputEvent Source, HotkeyBinding? Shortcut = null);

/// <summary>
/// Transforma os eventos crus dos ganchos em ações de passo: junta dois cliques próximos num duplo clique,
/// junta teclas imprimíveis num grupo de digitação e separa os atalhos. Lógica pura, sem relógio próprio:
/// o tempo vem dos eventos e do <see cref="Tick"/>. Só vê códigos de tecla, nunca caracteres.
/// </summary>
internal sealed class InputEventGrouper
{
    private readonly uint _doubleClickMs;
    private readonly Size _doubleClickArea;
    private readonly IReadOnlySet<HotkeyBinding> _ignored;
    private readonly uint _typingIdleMs;

    private RawInputEvent? _lastClick;      // 1º clique de um possível duplo clique
    private RawInputEvent? _lastTypingKey;  // != null enquanto há um grupo de digitação aberto

    public InputEventGrouper(uint doubleClickMs, Size doubleClickArea, IReadOnlySet<HotkeyBinding> ignored, int typingIdleMs = 1200)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(typingIdleMs);
        _doubleClickMs = doubleClickMs;
        _doubleClickArea = doubleClickArea;
        _ignored = ignored;
        _typingIdleMs = (uint)typingIdleMs;
    }

    public bool IsTyping => _lastTypingKey is not null;

    public IReadOnlyList<GroupedAction> Push(RawInputEvent e)
    {
        var result = new List<GroupedAction>(2);
        switch (e.Kind)
        {
            case RawInputKind.LeftDown:
                CloseTyping(result);
                if (_lastClick is RawInputEvent first && IsDoubleClick(first, e))
                {
                    result.Add(new GroupedAction(GroupedKind.PromoteLastToDoubleClick, e));
                    _lastClick = null; // o 3º clique começa um ciclo novo
                }
                else
                {
                    result.Add(new GroupedAction(GroupedKind.NewClick, e));
                    _lastClick = e;
                }
                break;

            case RawInputKind.RightDown:
                CloseTyping(result);
                result.Add(new GroupedAction(GroupedKind.NewRightClick, e));
                _lastClick = null;
                break;

            case RawInputKind.KeyDown:
                _lastClick = null;
                PushKey(e, result);
                break;
        }
        return result;
    }

    /// <summary>Fecha o grupo de digitação se passou <c>typingIdleMs</c> desde a última tecla dele.</summary>
    public IReadOnlyList<GroupedAction> Tick(uint nowMs)
    {
        if (_lastTypingKey is RawInputEvent last && unchecked(nowMs - last.TimeMs) >= _typingIdleMs
            && unchecked(nowMs - last.TimeMs) < int.MaxValue) // relógio anterior à tecla (fora de ordem) não fecha
        {
            var result = new List<GroupedAction>(1);
            CloseTyping(result);
            return result;
        }
        return [];
    }

    /// <summary>Ao parar a gravação: fecha o que estiver aberto.</summary>
    public IReadOnlyList<GroupedAction> Flush()
    {
        var result = new List<GroupedAction>(1);
        CloseTyping(result);
        _lastClick = null;
        return result;
    }

    private void PushKey(RawInputEvent e, List<GroupedAction> result)
    {
        var key = (Keys)e.VirtualKey;
        bool commandModifier = (e.Modifiers & (HotkeyModifiers.Control | HotkeyModifiers.Alt | HotkeyModifiers.Win)) != 0;

        if (!commandModifier)
        {
            if (IsTyping && (IsPrintable(key) || IsEditingKey(key)))
            {
                _lastTypingKey = e;
                return;
            }
            if (IsPrintable(key))
            {
                _lastTypingKey = e;
                result.Add(new GroupedAction(GroupedKind.TypingStarted, e));
                return;
            }
            if (!IsStandaloneShortcutKey(key))
            {
                return; // seta, Backspace, Home etc. fora de digitação não viram passo
            }
        }

        var binding = new HotkeyBinding(e.Modifiers, key);
        CloseTyping(result);
        if (_ignored.Contains(binding))
        {
            return; // atalho do próprio Fragmo
        }
        result.Add(new GroupedAction(GroupedKind.Shortcut, e, binding));
    }

    private void CloseTyping(List<GroupedAction> result)
    {
        if (_lastTypingKey is RawInputEvent last)
        {
            result.Add(new GroupedAction(GroupedKind.TypingFinished, last));
            _lastTypingKey = null;
        }
    }

    private bool IsDoubleClick(RawInputEvent first, RawInputEvent second)
    {
        uint elapsed = unchecked(second.TimeMs - first.TimeMs);
        return elapsed <= _doubleClickMs
            && Math.Abs(second.ScreenPoint.X - first.ScreenPoint.X) * 2 <= _doubleClickArea.Width
            && Math.Abs(second.ScreenPoint.Y - first.ScreenPoint.Y) * 2 <= _doubleClickArea.Height;
    }

    // ABNT2: a tecla "/?" (0xC1) e o "." do teclado numérico (0xC2) não têm nome no enum Keys.
    private const Keys AbntC1 = (Keys)0xC1;
    private const Keys AbntC2 = (Keys)0xC2;

    /// <summary>Teclas que produzem texto: letras, dígitos, teclado numérico, espaço e pontuação (OEM).</summary>
    internal static bool IsPrintable(Keys key) => key switch
    {
        >= Keys.A and <= Keys.Z => true,
        >= Keys.D0 and <= Keys.D9 => true,
        >= Keys.NumPad0 and <= Keys.NumPad9 => true,
        Keys.Space or Keys.Decimal or Keys.Add or Keys.Subtract or Keys.Multiply or Keys.Divide => true,
        >= Keys.Oem1 and <= Keys.Oemtilde => true,          // 0xBA–0xC0
        AbntC1 or AbntC2 => true,
        >= Keys.OemOpenBrackets and <= Keys.Oem8 => true,   // 0xDB–0xDF
        Keys.OemBackslash => true,                          // 0xE2
        _ => false,
    };

    /// <summary>Teclas de edição que continuam um grupo de digitação aberto.</summary>
    private static bool IsEditingKey(Keys key) =>
        key is Keys.Back or Keys.Delete or Keys.Left or Keys.Right or Keys.Up or Keys.Down;

    /// <summary>Teclas que viram passo mesmo sem Ctrl/Alt/Win (fora de um grupo de digitação).</summary>
    private static bool IsStandaloneShortcutKey(Keys key) =>
        key is Keys.Enter or Keys.Tab or Keys.Escape or Keys.Delete || key is >= Keys.F1 and <= Keys.F12;
}
