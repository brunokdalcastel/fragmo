using Fragmo.Hotkeys;

namespace Fragmo.Tests;

public sealed class PrintScreenKeyFilterTests
{
    private const int PrtSc = PrintScreenKeyFilter.VkSnapshot;

    [Fact]
    public void Press_FiresOnce_AndSwallowsDownAndUp()
    {
        var f = new PrintScreenKeyFilter();

        Assert.Equal(new PrintScreenKeyFilter.Decision(true, true), f.Handle(PrtSc, keyUp: false, modifierDown: false));
        Assert.Equal(new PrintScreenKeyFilter.Decision(true, false), f.Handle(PrtSc, keyUp: true, modifierDown: false));
    }

    [Fact]
    public void HeldKey_RepeatsAreSwallowed_WithoutFiringAgain()
    {
        var f = new PrintScreenKeyFilter();
        f.Handle(PrtSc, keyUp: false, modifierDown: false);

        PrintScreenKeyFilter.Decision repeat = f.Handle(PrtSc, keyUp: false, modifierDown: false);

        Assert.True(repeat.Swallow);
        Assert.False(repeat.Fire);
    }

    [Fact]
    public void SecondPress_AfterRelease_FiresAgain()
    {
        var f = new PrintScreenKeyFilter();
        f.Handle(PrtSc, keyUp: false, modifierDown: false);
        f.Handle(PrtSc, keyUp: true, modifierDown: false);

        Assert.True(f.Handle(PrtSc, keyUp: false, modifierDown: false).Fire);
    }

    [Fact]
    public void WithModifier_PassesThrough()
    {
        var f = new PrintScreenKeyFilter();

        Assert.Equal(default, f.Handle(PrtSc, keyUp: false, modifierDown: true));
        Assert.Equal(default, f.Handle(PrtSc, keyUp: true, modifierDown: true));
    }

    [Fact]
    public void ReleaseWithoutOurPress_PassesThrough()
    {
        var f = new PrintScreenKeyFilter();

        Assert.False(f.Handle(PrtSc, keyUp: true, modifierDown: false).Swallow);
    }

    [Theory]
    [InlineData(0x41)] // A
    [InlineData(0x53)] // S
    [InlineData(0x0D)] // Enter
    public void OtherKeys_PassThrough(int vk)
    {
        var f = new PrintScreenKeyFilter();

        Assert.Equal(default, f.Handle(vk, keyUp: false, modifierDown: false));
        Assert.Equal(default, f.Handle(vk, keyUp: true, modifierDown: false));
    }
}
