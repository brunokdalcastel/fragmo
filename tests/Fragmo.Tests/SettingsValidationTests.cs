using Fragmo.Settings;

namespace Fragmo.Tests;

[Collection("Strings")]
public sealed class SettingsValidationTests
{
    [Fact]
    public void Validate_Defaults_HasNoErrors()
    {
        Assert.Empty(SettingsValidator.Validate(new AppSettings()));
    }

    [Fact]
    public void Validate_DuplicateHotkeys_ReturnsError()
    {
        var s = new AppSettings();
        s.Hotkeys.CaptureWindow = "Ctrl+Alt+A";
        s.Hotkeys.RepeatLastArea = "ctrl+alt+a"; // mesmo atalho, escrito de outro jeito

        Assert.Single(SettingsValidator.Validate(s));
    }

    [Theory]
    [InlineData("Ctrl+Banana")]
    [InlineData("Ctrl+")]
    [InlineData("")]
    public void Validate_UnparseableHotkey_ReturnsError(string text)
    {
        var s = new AppSettings();
        s.Hotkeys.CaptureFullScreen = text;

        Assert.Single(SettingsValidator.Validate(s));
    }

    [Fact]
    public void Validate_EmptyFolder_ReturnsError()
    {
        var s = new AppSettings { SaveFolder = "  " };

        Assert.Single(SettingsValidator.Validate(s));
    }
}
