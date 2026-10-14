using Fragmo.Hotkeys;

namespace Fragmo.Settings;

/// <summary>Confere as configurações antes de salvar: atalhos válidos e sem repetição, pasta preenchida.</summary>
public static class SettingsValidator
{
    public static IReadOnlyList<string> Validate(AppSettings s)
    {
        List<string> errors = [];
        Dictionary<HotkeyBinding, HotkeyAction> seen = [];

        foreach ((HotkeyAction action, string text) in HotkeyEntries(s.Hotkeys))
        {
            if (!HotkeyBinding.TryParse(text, out HotkeyBinding binding))
            {
                errors.Add(string.Format(Strings.HotkeyInvalid, Strings.HotkeyLabel(action)));
                continue;
            }

            if (seen.TryGetValue(binding, out HotkeyAction other))
            {
                errors.Add(string.Format(Strings.HotkeyDuplicate, binding, Strings.HotkeyLabel(other), Strings.HotkeyLabel(action)));
                continue;
            }

            seen.Add(binding, action);
        }

        if (string.IsNullOrWhiteSpace(s.SaveFolder))
        {
            errors.Add(Strings.FolderEmpty);
        }

        return errors;
    }

    /// <summary>Os atalhos que aparecem nas configurações (o alternativo da área fica de fora).</summary>
    internal static (HotkeyAction Action, string Text)[] HotkeyEntries(HotkeySettings h) =>
    [
        (HotkeyAction.CaptureArea, h.CaptureArea),
        (HotkeyAction.CaptureFullScreen, h.CaptureFullScreen),
        (HotkeyAction.CaptureWindow, h.CaptureWindow),
        (HotkeyAction.RepeatLastArea, h.RepeatLastArea),
    ];
}
