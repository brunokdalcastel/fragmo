using Fragmo.Hotkeys;

namespace Fragmo.Tests;

public sealed class HotkeyBindingTests
{
    [Theory]
    [InlineData("Ctrl+Shift+S", HotkeyModifiers.Control | HotkeyModifiers.Shift, Keys.S)]
    [InlineData("ctrl + shift + s", HotkeyModifiers.Control | HotkeyModifiers.Shift, Keys.S)]
    [InlineData("PrintScreen", HotkeyModifiers.None, Keys.PrintScreen)]
    [InlineData("Alt+F1", HotkeyModifiers.Alt, Keys.F1)]
    [InlineData("Win+Shift+X", HotkeyModifiers.Win | HotkeyModifiers.Shift, Keys.X)]
    [InlineData("Control+Alt+Delete", HotkeyModifiers.Control | HotkeyModifiers.Alt, Keys.Delete)]
    public void TryParse_ValidInput_ReturnsBinding(string text, HotkeyModifiers mods, Keys key)
    {
        Assert.True(HotkeyBinding.TryParse(text, out HotkeyBinding binding));
        Assert.Equal(new HotkeyBinding(mods, key), binding);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Ctrl+")]
    [InlineData("Ctrl+Shift")]
    [InlineData("Ctrl+Ctrl+A")]
    [InlineData("Foo+A")]
    [InlineData("Ctrl+ShiftKey")]
    [InlineData("Ctrl+65")]
    public void TryParse_InvalidInput_ReturnsFalse(string text)
    {
        Assert.False(HotkeyBinding.TryParse(text, out _));
    }

    [Theory]
    [InlineData("Ctrl+Shift+S", "Ctrl+Shift+S")]
    [InlineData("shift+alt+ctrl+win+f5", "Ctrl+Alt+Shift+Win+F5")]
    [InlineData("PrintScreen", "PrintScreen")]
    public void ToString_UsesFixedOrder(string text, string expected)
    {
        Assert.True(HotkeyBinding.TryParse(text, out HotkeyBinding binding));
        Assert.Equal(expected, binding.ToString());
    }

    [Theory]
    [InlineData("Ctrl+Shift+S")]
    [InlineData("PrintScreen")]
    [InlineData("Alt+F1")]
    [InlineData("Win+Shift+X")]
    [InlineData("Ctrl+Alt+Shift+Win+PageDown")]
    public void TryParse_ToString_RoundTrips(string text)
    {
        Assert.True(HotkeyBinding.TryParse(text, out HotkeyBinding original));
        Assert.True(HotkeyBinding.TryParse(original.ToString(), out HotkeyBinding again));
        Assert.Equal(original, again);
    }
}
