using Fragmo.Editor;

namespace Fragmo.Tests;

public sealed class AnnotationHistoryTests
{
    private sealed class FakeAnnotation : IAnnotation
    {
        public Rectangle Bounds => new(0, 0, 1, 1);

        public void Draw(Graphics g, Bitmap source)
        {
        }
    }

    [Fact]
    public void Add_IncreasesItems()
    {
        var history = new AnnotationHistory();
        history.Add(new FakeAnnotation());
        history.Add(new FakeAnnotation());

        Assert.Equal(2, history.Items.Count);
        Assert.True(history.CanUndo);
        Assert.False(history.CanRedo);
    }

    [Fact]
    public void UndoRedo_InSequence_RestoreItemsInOrder()
    {
        var history = new AnnotationHistory();
        var a = new FakeAnnotation();
        var b = new FakeAnnotation();
        history.Add(a);
        history.Add(b);

        Assert.True(history.Undo());
        Assert.Equal([a], history.Items);
        Assert.True(history.Undo());
        Assert.Empty(history.Items);
        Assert.False(history.CanUndo);

        Assert.True(history.Redo());
        Assert.Equal([a], history.Items);
        Assert.True(history.Redo());
        Assert.Equal([a, b], history.Items);
        Assert.False(history.Redo());
    }

    [Fact]
    public void Add_AfterUndo_ClearsRedo()
    {
        var history = new AnnotationHistory();
        history.Add(new FakeAnnotation());
        history.Undo();
        Assert.True(history.CanRedo);

        history.Add(new FakeAnnotation());

        Assert.False(history.CanRedo);
        Assert.False(history.Redo());
        Assert.Single(history.Items);
    }

    [Fact]
    public void Undo_OnEmptyHistory_ReturnsFalse()
    {
        var history = new AnnotationHistory();
        int changes = 0;
        history.Changed += () => changes++;

        Assert.False(history.Undo());
        Assert.False(history.Redo());
        Assert.Equal(0, changes);
    }

    [Fact]
    public void Changed_FiresOncePerOperation()
    {
        var history = new AnnotationHistory();
        int changes = 0;
        history.Changed += () => changes++;

        history.Add(new FakeAnnotation());
        Assert.Equal(1, changes);
        history.Undo();
        Assert.Equal(2, changes);
        history.Redo();
        Assert.Equal(3, changes);
        history.Clear();
        Assert.Equal(4, changes);
        Assert.Empty(history.Items);
    }
}
