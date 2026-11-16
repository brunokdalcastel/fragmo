using Fragmo.Hotkeys;
using Fragmo.Settings;
using Fragmo.Tutorial;
using Fragmo.Tutorial.Capture;
using Fragmo.Tutorial.Recording;

namespace Fragmo.Tests;

public sealed class TutorialRecordingTests
{
    private static ElementInfo Element(Rectangle screen) => new("OK", ElementKind.Button, screen, false, null);

    [Fact]
    public void ToImageRect_SubtractsCaptureOrigin()
    {
        var capture = new Rectangle(100, 50, 800, 600);

        RectDto? r = TutorialRecorder.ToImageRect(Element(new Rectangle(150, 80, 40, 20)), capture);

        Assert.Equal(new RectDto(50, 30, 40, 20), r);
    }

    [Fact]
    public void ToImageRect_ClipsToImage()
    {
        var capture = new Rectangle(-1920, 0, 1920, 1080); // monitor à esquerda do principal

        RectDto? r = TutorialRecorder.ToImageRect(Element(new Rectangle(-1950, 1070, 100, 40)), capture);

        Assert.Equal(new RectDto(0, 1070, 70, 10), r);
    }

    [Theory]
    [InlineData(0, 0, 0, 0)]          // sem retângulo
    [InlineData(2000, 2000, 50, 50)]  // fora da captura
    [InlineData(90, 40, 900, 700)]    // cobre a janela inteira: não serve de destaque
    public void ToImageRect_UselessRectangle_ReturnsNull(int x, int y, int w, int h)
    {
        var capture = new Rectangle(100, 50, 800, 600);

        Assert.Null(TutorialRecorder.ToImageRect(Element(new Rectangle(x, y, w, h)), capture));
    }

    [Fact]
    public void ToImageRect_NoElement_ReturnsNull() =>
        Assert.Null(TutorialRecorder.ToImageRect(null, new Rectangle(0, 0, 10, 10)));

    [Fact]
    public void IgnoredShortcuts_IncludesEveryFragmoHotkeyAndFallback()
    {
        var hotkeys = new HotkeySettings();

        IReadOnlySet<HotkeyBinding> set = TutorialWorkflow.IgnoredShortcuts(hotkeys);

        foreach (string text in new[] { "PrintScreen", "Ctrl+Shift+S", "Ctrl+Shift+F", "Ctrl+Shift+W", "Ctrl+Shift+R", "Ctrl+Shift+G", "Ctrl+Shift+Y" })
        {
            Assert.True(HotkeyBinding.TryParse(text, out HotkeyBinding b));
            Assert.Contains(b, set);
        }
        Assert.Equal(7, set.Count);
    }

    [Fact]
    public void Defaults_TutorialHotkeysAndRedaction()
    {
        var s = new AppSettings();

        Assert.Equal("Ctrl+Shift+G", s.Hotkeys.ToggleTutorial);
        Assert.Equal("Ctrl+Shift+Y", s.Hotkeys.TutorialManualStep);
        Assert.True(s.RedactTutorials);
        Assert.Empty(SettingsValidator.Validate(s));
    }

    [Fact]
    public void Validate_TutorialHotkeyClashingWithCapture_ReturnsError()
    {
        var s = new AppSettings();
        s.Hotkeys.TutorialManualStep = s.Hotkeys.CaptureWindow;

        Assert.Single(SettingsValidator.Validate(s));
    }

    [Fact]
    public void HotkeyEntries_IncludeTutorialActions()
    {
        var actions = SettingsValidator.HotkeyEntries(new HotkeySettings()).Select(e => e.Action).ToList();

        Assert.Contains(HotkeyAction.ToggleTutorial, actions);
        Assert.Contains(HotkeyAction.TutorialManualStep, actions);
    }
}
