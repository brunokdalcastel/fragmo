using Fragmo.Editor;

namespace Fragmo.Tests;

public sealed class StepCounterTests
{
    private static StepAnnotation Step(int number) => new(Color.Red, number, new Point(10, 10), 12);

    [Fact]
    public void Next_EmptyList_IsOne()
    {
        Assert.Equal(1, StepCounter.Next([]));
    }

    [Fact]
    public void Next_AfterSteps_IsOnePlusMax()
    {
        Assert.Equal(6, StepCounter.Next([Step(1), Step(2), Step(5)]));
    }

    [Fact]
    public void Next_ListWithoutSteps_IsOne()
    {
        var style = new StrokeStyle(Color.Red, 3);
        IAnnotation[] items =
        [
            new RectangleAnnotation(style, new Rectangle(0, 0, 10, 10)),
            new LineAnnotation(style, Point.Empty, new Point(5, 5)),
        ];

        Assert.Equal(1, StepCounter.Next(items));
    }

    [Fact]
    public void Next_AfterUndo_ReusesNumber()
    {
        var history = new AnnotationHistory();
        history.Add(Step(StepCounter.Next(history.Items)));
        history.Add(Step(StepCounter.Next(history.Items)));
        history.Undo();

        Assert.Equal(2, StepCounter.Next(history.Items));
    }
}
