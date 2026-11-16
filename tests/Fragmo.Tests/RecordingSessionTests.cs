using Fragmo.Tutorial;
using Fragmo.Tutorial.Recording;

namespace Fragmo.Tests;

public sealed class RecordingSessionTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "fragmo-session-tests-" + Guid.NewGuid().ToString("N"));
    private static readonly DateTimeOffset Now = new(2026, 11, 16, 10, 30, 0, TimeSpan.FromHours(-3));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static TutorialItem Step(string text) => new()
    {
        Id = TutorialItem.NewId(),
        Kind = TutorialItemKind.Step,
        Action = StepAction.Click,
        Text = text,
    };

    private static Bitmap Image() => new(4, 3);

    private static TutorialDocument ReadJson(string folder) =>
        TutorialSerializer.FromJson(File.ReadAllText(Path.Combine(folder, "tutorial.json")));

    [Fact]
    public void Create_WritesUnfinishedJsonAndImagesFolder()
    {
        RecordingSession session = RecordingSession.Create(_root, "Meu tutorial", Now);

        Assert.True(Directory.Exists(Path.Combine(session.Folder, "images")));
        Assert.Matches(@"^\d{8}-\d{6}-[0-9a-f]{8}$", Path.GetFileName(session.Folder));
        TutorialDocument doc = ReadJson(session.Folder);
        Assert.False(doc.Finished);
        Assert.Equal("Meu tutorial", doc.Title);
        Assert.Equal(Now, doc.CreatedAt);
        Assert.Empty(doc.Items);
    }

    [Fact]
    public void AddStep_SavesNumberedPngAndRewritesJson()
    {
        RecordingSession session = RecordingSession.Create(_root, "T", Now);

        using (Bitmap a = Image())
        using (Bitmap b = Image())
        {
            TutorialItem first = session.AddStep(a, Step("um"));
            TutorialItem second = session.AddStep(b, Step("dois"));
            Assert.Equal("images/0001.png", first.ImageFile);
            Assert.Equal("images/0002.png", second.ImageFile);
        }

        Assert.True(File.Exists(Path.Combine(session.Folder, "images", "0001.png")));
        Assert.True(File.Exists(Path.Combine(session.Folder, "images", "0002.png")));
        Assert.Empty(Directory.GetFiles(session.Folder, "*.tmp", SearchOption.AllDirectories));
        TutorialDocument doc = ReadJson(session.Folder);
        Assert.Equal(["um", "dois"], doc.Items.Select(i => i.Text));
        Assert.Equal(2, session.StepCount);
        Assert.Equal("dois", session.LastStep?.Text);
    }

    [Fact]
    public void UpdateItem_ChangesOnlyThatItemAndKeepsId()
    {
        RecordingSession session = RecordingSession.Create(_root, "T", Now);
        using Bitmap img = Image();
        TutorialItem saved = session.AddStep(img, Step("antes"));

        bool ok = session.UpdateItem(saved.Id, i => i with
        {
            Id = "outro",
            Action = StepAction.DoubleClick,
            Redactions = [new RectDto(1, 2, 3, 4)],
        });

        Assert.True(ok);
        TutorialItem item = Assert.Single(ReadJson(session.Folder).Items);
        Assert.Equal(saved.Id, item.Id);
        Assert.Equal(StepAction.DoubleClick, item.Action);
        Assert.Equal(new RectDto(1, 2, 3, 4), Assert.Single(item.Redactions));
        Assert.False(session.UpdateItem("nao-existe", i => i));
    }

    [Fact]
    public void FindUnfinished_ListsOnlyUnfinishedReadableSessions()
    {
        RecordingSession open = RecordingSession.Create(_root, "aberta", Now);
        RecordingSession done = RecordingSession.Create(_root, "fechada", Now);
        done.MarkFinished();
        string broken = Path.Combine(_root, "quebrada");
        Directory.CreateDirectory(broken);
        File.WriteAllText(Path.Combine(broken, "tutorial.json"), "{ não é json");
        Directory.CreateDirectory(Path.Combine(_root, "vazia"));

        IReadOnlyList<string> found = RecordingSession.FindUnfinished(_root);

        Assert.Equal([open.Folder], found);
        Assert.True(ReadJson(done.Folder).Finished);
    }

    [Fact]
    public void FindUnfinished_MissingRoot_ReturnsEmpty() =>
        Assert.Empty(RecordingSession.FindUnfinished(Path.Combine(_root, "nada")));

    [Fact]
    public void Resume_ContinuesNumberingAfterExistingImages()
    {
        RecordingSession first = RecordingSession.Create(_root, "T", Now);
        using (Bitmap a = Image())
        using (Bitmap b = Image())
        {
            first.AddStep(a, Step("1"));
            first.AddStep(b, Step("2"));
        }

        RecordingSession resumed = RecordingSession.Resume(first.Folder);
        using Bitmap c = Image();
        TutorialItem third = resumed.AddStep(c, Step("3"));

        Assert.Equal("images/0003.png", third.ImageFile);
        Assert.Equal(3, ReadJson(first.Folder).Items.Count);
    }

    [Fact]
    public void Resume_InvalidFolder_ThrowsFormatException() =>
        Assert.Throws<TutorialFormatException>(() => RecordingSession.Resume(Path.Combine(_root, "nada")));

    [Fact]
    public void AddStep_WhenFolderVanished_ThrowsAndKeepsState()
    {
        RecordingSession session = RecordingSession.Create(_root, "T", Now);
        Directory.Delete(session.Folder, recursive: true);

        using Bitmap img = Image();
        Assert.Throws<DirectoryNotFoundException>(() => session.AddStep(img, Step("x")));
        Assert.Equal(0, session.StepCount);
    }

    [Fact]
    public void Delete_RemovesFolderAndIsIdempotent()
    {
        RecordingSession session = RecordingSession.Create(_root, "T", Now);

        session.Delete();
        session.Delete();

        Assert.False(Directory.Exists(session.Folder));
    }
}
