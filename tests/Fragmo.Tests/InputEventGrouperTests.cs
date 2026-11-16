using Fragmo.Hotkeys;
using Fragmo.Tutorial.Input;

namespace Fragmo.Tests;

public sealed class InputEventGrouperTests
{
    private const uint DoubleClickMs = 500;
    private static readonly Size DoubleClickArea = new(4, 4);
    private static readonly HotkeyBinding FragmoToggle = new(HotkeyModifiers.Control | HotkeyModifiers.Shift, Keys.G);

    private static InputEventGrouper NewGrouper() =>
        new(DoubleClickMs, DoubleClickArea, new HashSet<HotkeyBinding> { FragmoToggle });

    private static RawInputEvent Left(int x, int y, uint t) => new(RawInputKind.LeftDown, new Point(x, y), 0, HotkeyModifiers.None, t);
    private static RawInputEvent Right(int x, int y, uint t) => new(RawInputKind.RightDown, new Point(x, y), 0, HotkeyModifiers.None, t);
    private static RawInputEvent Key(Keys key, uint t, HotkeyModifiers mods = HotkeyModifiers.None) =>
        new(RawInputKind.KeyDown, Point.Empty, (int)key, mods, t);

    private static GroupedKind[] Kinds(IEnumerable<GroupedAction> actions) => actions.Select(a => a.Kind).ToArray();

    [Fact]
    public void SingleClick_IsNewClick()
    {
        Assert.Equal([GroupedKind.NewClick], Kinds(NewGrouper().Push(Left(10, 10, 1000))));
    }

    [Fact]
    public void RightClick_IsNewRightClick()
    {
        Assert.Equal([GroupedKind.NewRightClick], Kinds(NewGrouper().Push(Right(10, 10, 1000))));
    }

    [Fact]
    public void SecondClick_InsideTimeAndArea_PromotesToDoubleClick()
    {
        var g = NewGrouper();
        g.Push(Left(10, 10, 1000));
        Assert.Equal([GroupedKind.PromoteLastToDoubleClick], Kinds(g.Push(Left(12, 11, 1400))));
    }

    [Fact]
    public void SecondClick_AfterDoubleClickTime_IsNewClick()
    {
        var g = NewGrouper();
        g.Push(Left(10, 10, 1000));
        Assert.Equal([GroupedKind.NewClick], Kinds(g.Push(Left(10, 10, 1501))));
    }

    [Fact]
    public void SecondClick_OutsideArea_IsNewClick()
    {
        var g = NewGrouper();
        g.Push(Left(10, 10, 1000));
        Assert.Equal([GroupedKind.NewClick], Kinds(g.Push(Left(13, 10, 1100))));
    }

    [Fact]
    public void ThirdClick_StartsNewCycle()
    {
        var g = NewGrouper();
        g.Push(Left(10, 10, 1000));
        g.Push(Left(10, 10, 1100));
        Assert.Equal([GroupedKind.NewClick], Kinds(g.Push(Left(10, 10, 1200))));
        Assert.Equal([GroupedKind.PromoteLastToDoubleClick], Kinds(g.Push(Left(10, 10, 1300))));
    }

    [Fact]
    public void DoubleClick_SurvivesTickCounterWrap()
    {
        var g = NewGrouper();
        g.Push(Left(10, 10, uint.MaxValue - 100));
        Assert.Equal([GroupedKind.PromoteLastToDoubleClick], Kinds(g.Push(Left(10, 10, 100))));
    }

    [Fact]
    public void Typing_IsGrouped_AndClosedByClick()
    {
        var g = NewGrouper();
        Assert.Equal([GroupedKind.TypingStarted], Kinds(g.Push(Key(Keys.O, 1000))));
        Assert.Empty(g.Push(Key(Keys.L, 1100)));
        Assert.Empty(g.Push(Key(Keys.A, 1200)));
        Assert.Equal([GroupedKind.TypingFinished, GroupedKind.NewClick], Kinds(g.Push(Left(5, 5, 1300))));
        Assert.False(g.IsTyping);
    }

    [Fact]
    public void Typing_ClosedByShortcut()
    {
        var g = NewGrouper();
        g.Push(Key(Keys.A, 1000));
        IReadOnlyList<GroupedAction> actions = g.Push(Key(Keys.S, 1100, HotkeyModifiers.Control));
        Assert.Equal([GroupedKind.TypingFinished, GroupedKind.Shortcut], Kinds(actions));
        Assert.Equal("Ctrl+S", actions[1].Shortcut.ToString());
    }

    [Fact]
    public void Typing_ClosedByIdleTick()
    {
        var g = NewGrouper();
        g.Push(Key(Keys.A, 1000));
        g.Push(Key(Keys.B, 1500));
        Assert.Empty(g.Tick(2600));
        Assert.Equal([GroupedKind.TypingFinished], Kinds(g.Tick(2700)));
        Assert.Empty(g.Tick(5000));
    }

    [Fact]
    public void Tick_WithClockBeforeLastKey_DoesNotClose()
    {
        var g = NewGrouper();
        g.Push(Key(Keys.A, 5000));
        Assert.Empty(g.Tick(4000));
    }

    [Fact]
    public void Typing_ClosedByFlush()
    {
        var g = NewGrouper();
        g.Push(Key(Keys.A, 1000));
        Assert.Equal([GroupedKind.TypingFinished], Kinds(g.Flush()));
        Assert.Empty(g.Flush());
    }

    [Fact]
    public void ShiftLetter_CountsAsTyping()
    {
        Assert.Equal([GroupedKind.TypingStarted], Kinds(NewGrouper().Push(Key(Keys.A, 1000, HotkeyModifiers.Shift))));
    }

    [Theory]
    [InlineData(Keys.D5)]
    [InlineData(Keys.NumPad3)]
    [InlineData(Keys.Space)]
    [InlineData(Keys.OemPeriod)]
    [InlineData((Keys)0xC1)] // ABNT2 "/?"
    public void PrintableKeys_StartTyping(Keys key)
    {
        Assert.Equal([GroupedKind.TypingStarted], Kinds(NewGrouper().Push(Key(key, 1000))));
    }

    [Fact]
    public void CtrlC_IsShortcut()
    {
        IReadOnlyList<GroupedAction> actions = NewGrouper().Push(Key(Keys.C, 1000, HotkeyModifiers.Control));
        GroupedAction a = Assert.Single(actions);
        Assert.Equal(GroupedKind.Shortcut, a.Kind);
        Assert.Equal(new HotkeyBinding(HotkeyModifiers.Control, Keys.C), a.Shortcut);
    }

    [Theory]
    [InlineData(Keys.Enter)]
    [InlineData(Keys.Tab)]
    [InlineData(Keys.Escape)]
    [InlineData(Keys.Delete)]
    [InlineData(Keys.F5)]
    public void StandaloneKeys_OutsideTyping_AreShortcuts(Keys key)
    {
        Assert.Equal([GroupedKind.Shortcut], Kinds(NewGrouper().Push(Key(key, 1000))));
    }

    [Fact]
    public void Enter_InsideTyping_ClosesTypingAndIsShortcut()
    {
        var g = NewGrouper();
        g.Push(Key(Keys.A, 1000));
        Assert.Equal([GroupedKind.TypingFinished, GroupedKind.Shortcut], Kinds(g.Push(Key(Keys.Enter, 1100))));
    }

    [Theory]
    [InlineData(Keys.Left)]
    [InlineData(Keys.Back)]
    [InlineData(Keys.Home)]
    public void EditingKeys_OutsideTyping_AreIgnored(Keys key)
    {
        Assert.Empty(NewGrouper().Push(Key(key, 1000)));
    }

    [Theory]
    [InlineData(Keys.Back)]
    [InlineData(Keys.Delete)]
    [InlineData(Keys.Left)]
    public void EditingKeys_InsideTyping_StayInGroup(Keys key)
    {
        var g = NewGrouper();
        g.Push(Key(Keys.A, 1000));
        Assert.Empty(g.Push(Key(key, 1100)));
        Assert.True(g.IsTyping);
        // A tecla de edição também conta para a inatividade.
        Assert.Empty(g.Tick(2250));
    }

    [Fact]
    public void FragmoHotkey_IsIgnored_ButClosesTyping()
    {
        var g = NewGrouper();
        Assert.Empty(g.Push(Key(Keys.G, 1000, HotkeyModifiers.Control | HotkeyModifiers.Shift)));

        g.Push(Key(Keys.A, 2000));
        Assert.Equal([GroupedKind.TypingFinished],
            Kinds(g.Push(Key(Keys.G, 2100, HotkeyModifiers.Control | HotkeyModifiers.Shift))));
    }

    [Fact]
    public void KeyBetweenClicks_BreaksDoubleClick()
    {
        var g = NewGrouper();
        g.Push(Left(10, 10, 1000));
        g.Push(Key(Keys.F5, 1050));
        Assert.Equal([GroupedKind.NewClick], Kinds(g.Push(Left(10, 10, 1100))));
    }
}
