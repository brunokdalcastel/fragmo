using Fragmo.Tutorial;

namespace Fragmo.Tests;

public sealed class TutorialEditHistoryTests
{
    private static TutorialDocument Doc(params string[] ids) => new()
    {
        Title = "T",
        Items = ids.Select(id => new TutorialItem { Id = id, Kind = TutorialItemKind.Step, Text = id }).ToList(),
    };

    private static string[] Ids(TutorialDocument doc) => doc.Items.Select(i => i.Id).ToArray();

    [Fact]
    public void MoveUp_And_MoveDown()
    {
        TutorialDocument doc = Doc("a", "b", "c");
        Assert.Equal(["b", "a", "c"], Ids(TutorialEditHistory.MoveUp(doc, "b")));
        Assert.Equal(["a", "c", "b"], Ids(TutorialEditHistory.MoveDown(doc, "b")));
        Assert.Equal(["a", "b", "c"], Ids(doc)); // original intacto
    }

    [Fact]
    public void MoveUp_First_And_MoveDown_Last_AreNoOps()
    {
        TutorialDocument doc = Doc("a", "b");
        Assert.Same(doc, TutorialEditHistory.MoveUp(doc, "a"));
        Assert.Same(doc, TutorialEditHistory.MoveDown(doc, "b"));
        Assert.Same(doc, TutorialEditHistory.MoveUp(doc, "zz"));
    }

    [Fact]
    public void Remove()
    {
        TutorialDocument doc = Doc("a", "b", "c");
        Assert.Equal(["a", "c"], Ids(TutorialEditHistory.Remove(doc, "b")));
        Assert.Same(doc, TutorialEditHistory.Remove(doc, "zz"));
    }

    [Fact]
    public void InsertNoteAfter()
    {
        TutorialDocument doc = Doc("a", "b");

        TutorialDocument after = TutorialEditHistory.InsertNoteAfter(doc, "a", "Atenção");
        Assert.Equal(3, after.Items.Count);
        TutorialItem note = after.Items[1];
        Assert.Equal(TutorialItemKind.Note, note.Kind);
        Assert.Equal("Atenção", note.Text);
        Assert.False(string.IsNullOrEmpty(note.Id));

        TutorialDocument atStart = TutorialEditHistory.InsertNoteAfter(doc, null, "Início");
        Assert.Equal(TutorialItemKind.Note, atStart.Items[0].Kind);

        TutorialDocument atEnd = TutorialEditHistory.InsertNoteAfter(doc, "b", "Fim");
        Assert.Equal(TutorialItemKind.Note, atEnd.Items[^1].Kind);

        Assert.Same(doc, TutorialEditHistory.InsertNoteAfter(doc, "zz", "x"));
    }

    [Fact]
    public void SetText_SetShowHighlight_SetTitle()
    {
        TutorialDocument doc = Doc("a", "b");

        Assert.Equal("novo", TutorialEditHistory.SetText(doc, "b", "novo").Items[1].Text);
        Assert.Same(doc, TutorialEditHistory.SetText(doc, "b", "b"));

        Assert.False(TutorialEditHistory.SetShowHighlight(doc, "a", false).Items[0].ShowHighlight);
        Assert.Same(doc, TutorialEditHistory.SetShowHighlight(doc, "a", true));

        Assert.Equal("Outro", TutorialEditHistory.SetTitle(doc, "Outro").Title);
        Assert.Same(doc, TutorialEditHistory.SetTitle(doc, "T"));
    }

    [Fact]
    public void Update_EditImage()
    {
        TutorialDocument doc = Doc("a");
        TutorialDocument edited = TutorialEditHistory.Update(doc, "a",
            i => i with { Edited = true, OriginalImageFile = "images/0001.png", ImageFile = "images/0001-e1.png" });
        Assert.True(edited.Items[0].Edited);
        Assert.Same(doc, TutorialEditHistory.Update(doc, "a", i => i));
    }

    [Fact]
    public void Undo_ReturnsPreviousStates_InReverseOrder()
    {
        var history = new TutorialEditHistory();
        Assert.False(history.CanUndo);
        Assert.False(history.TryUndo(out _));

        TutorialDocument d0 = Doc("a", "b");
        history.Push(d0);
        TutorialDocument d1 = TutorialEditHistory.MoveDown(d0, "a");
        history.Push(d1);

        Assert.True(history.TryUndo(out TutorialDocument u1));
        Assert.Same(d1, u1);
        Assert.True(history.TryUndo(out TutorialDocument u0));
        Assert.Same(d0, u0);
        Assert.False(history.CanUndo);
    }

    [Fact]
    public void Push_KeepsOnlyLast50()
    {
        var history = new TutorialEditHistory();
        var docs = Enumerable.Range(0, 60).Select(i => Doc($"x{i}")).ToList();
        docs.ForEach(history.Push);

        Assert.Equal(TutorialEditHistory.Limit, history.Count);
        TutorialDocument last = docs[^1];
        for (int i = 0; i < TutorialEditHistory.Limit; i++)
        {
            Assert.True(history.TryUndo(out last));
        }
        Assert.Same(docs[10], last);
        Assert.False(history.CanUndo);
    }
}
