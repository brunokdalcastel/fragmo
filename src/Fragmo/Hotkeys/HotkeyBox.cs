using System.ComponentModel;

namespace Fragmo.Hotkeys;

/// <summary>
/// Caixa que grava um atalho: mostra a combinação pressionada; Backspace/Delete limpa.
/// </summary>
internal sealed class HotkeyBox : TextBox
{
    private HotkeyBinding? _binding;

    public HotkeyBox()
    {
        ReadOnly = true;
        ShortcutsEnabled = false;
        Cursor = Cursors.Arrow;
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public HotkeyBinding? Binding
    {
        get => _binding;
        set
        {
            _binding = value;
            Text = value?.ToString() ?? string.Empty;
        }
    }

    /// <summary>Pega a tecla antes do formulário (Enter, Alt+letra, setas…); Tab e Esc continuam navegando.</summary>
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        Keys key = keyData & Keys.KeyCode;
        Keys modifiers = keyData & Keys.Modifiers;

        if ((key == Keys.Tab && (modifiers & ~Keys.Shift) == 0) || (key == Keys.Escape && modifiers == Keys.None))
        {
            return base.ProcessCmdKey(ref msg, keyData);
        }

        if ((key == Keys.Back || key == Keys.Delete) && modifiers == Keys.None)
        {
            Binding = null;
            return true;
        }

        TryAccept(modifiers, key);
        return true;
    }

    // O Windows não envia KeyDown para a Print Screen, só KeyUp.
    protected override void OnKeyUp(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.PrintScreen)
        {
            TryAccept(e.Modifiers, e.KeyCode);
            e.Handled = true;
        }
        base.OnKeyUp(e);
    }

    private void TryAccept(Keys modifiers, Keys key)
    {
        var mods = HotkeyModifiers.None;
        if ((modifiers & Keys.Control) != 0) mods |= HotkeyModifiers.Control;
        if ((modifiers & Keys.Alt) != 0) mods |= HotkeyModifiers.Alt;
        if ((modifiers & Keys.Shift) != 0) mods |= HotkeyModifiers.Shift;

        // Só aceita o que o formato de texto consegue ler de volta (ignora teclas que são só modificador).
        var candidate = new HotkeyBinding(mods, key);
        if (HotkeyBinding.TryParse(candidate.ToString(), out HotkeyBinding parsed) && parsed == candidate)
        {
            Binding = candidate;
        }
    }
}
