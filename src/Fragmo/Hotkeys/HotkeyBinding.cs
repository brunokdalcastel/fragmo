namespace Fragmo.Hotkeys;

[Flags]
public enum HotkeyModifiers
{
    None = 0,
    Alt = 1,
    Control = 2,
    Shift = 4,
    Win = 8,
}

/// <summary>
/// Combinação de atalho global, lida e escrita no formato <c>Ctrl+Alt+Shift+Win+Tecla</c>.
/// </summary>
public readonly record struct HotkeyBinding(HotkeyModifiers Modifiers, Keys Key)
{
    // Teclas com mais de um nome no enum Keys: fixamos o nome mostrado.
    private static readonly Dictionary<Keys, string> FriendlyNames = new()
    {
        [Keys.PrintScreen] = "PrintScreen",
        [Keys.Enter] = "Enter",
        [Keys.PageUp] = "PageUp",
        [Keys.PageDown] = "PageDown",
        [Keys.CapsLock] = "CapsLock",
    };

    private static readonly HashSet<Keys> ModifierOnlyKeys =
    [
        Keys.ControlKey, Keys.LControlKey, Keys.RControlKey,
        Keys.ShiftKey, Keys.LShiftKey, Keys.RShiftKey,
        Keys.Menu, Keys.LMenu, Keys.RMenu,
        Keys.LWin, Keys.RWin,
    ];

    public static bool TryParse(string text, out HotkeyBinding binding)
    {
        binding = default;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        string[] parts = text.Split('+').Select(p => p.Trim()).ToArray();
        var modifiers = HotkeyModifiers.None;

        for (int i = 0; i < parts.Length - 1; i++)
        {
            HotkeyModifiers mod = ParseModifier(parts[i]);
            if (mod == HotkeyModifiers.None || modifiers.HasFlag(mod))
            {
                return false;
            }
            modifiers |= mod;
        }

        if (!TryParseKey(parts[^1], out Keys key))
        {
            return false;
        }

        binding = new HotkeyBinding(modifiers, key);
        return true;
    }

    public override string ToString()
    {
        var parts = new List<string>(5);
        if (Modifiers.HasFlag(HotkeyModifiers.Control)) parts.Add("Ctrl");
        if (Modifiers.HasFlag(HotkeyModifiers.Alt)) parts.Add("Alt");
        if (Modifiers.HasFlag(HotkeyModifiers.Shift)) parts.Add("Shift");
        if (Modifiers.HasFlag(HotkeyModifiers.Win)) parts.Add("Win");
        parts.Add(FriendlyNames.TryGetValue(Key, out string? name) ? name : Key.ToString());
        return string.Join("+", parts);
    }

    private static HotkeyModifiers ParseModifier(string part) => part.ToLowerInvariant() switch
    {
        "ctrl" or "control" => HotkeyModifiers.Control,
        "alt" => HotkeyModifiers.Alt,
        "shift" => HotkeyModifiers.Shift,
        "win" => HotkeyModifiers.Win,
        _ => HotkeyModifiers.None,
    };

    private static bool TryParseKey(string part, out Keys key)
    {
        key = Keys.None;
        // Só nomes: o Enum.TryParse também aceitaria números e listas separadas por vírgula.
        if (part.Length == 0 || !char.IsLetter(part[0]) || part.Contains(','))
        {
            return false;
        }
        if (ParseModifier(part) != HotkeyModifiers.None)
        {
            return false;
        }
        if (!Enum.TryParse(part, ignoreCase: true, out key) || !Enum.IsDefined(key))
        {
            return false;
        }
        return key != Keys.None && (key & Keys.Modifiers) == 0 && !ModifierOnlyKeys.Contains(key);
    }
}
