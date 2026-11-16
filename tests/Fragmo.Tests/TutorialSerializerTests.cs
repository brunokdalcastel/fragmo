using System.IO.Compression;
using Fragmo.Tutorial;

namespace Fragmo.Tests;

public sealed class TutorialSerializerTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "fragmo-tests", Guid.NewGuid().ToString("N"));

    public TutorialSerializerTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    private static TutorialDocument SampleDoc() => new()
    {
        Title = "Como exportar",
        CreatedAt = new DateTimeOffset(2026, 11, 16, 10, 0, 0, TimeSpan.FromHours(-3)),
        Finished = true,
        Items =
        [
            new TutorialItem
            {
                Id = "a1", Kind = TutorialItemKind.Step, Text = "Clique no botão “Salvar”", Action = StepAction.Click,
                ImageFile = "images/0001.png", Highlight = new RectDto(1, 2, 30, 40), ClickPoint = new PointDto(5, 6),
                Redactions = [new RectDto(10, 10, 20, 5)],
            },
            new TutorialItem { Id = "n1", Kind = TutorialItemKind.Note, Text = "Depois:" },
            new TutorialItem
            {
                Id = "a2", Kind = TutorialItemKind.Step, Text = "Pressione Ctrl+S", Action = StepAction.Shortcut,
                ImageFile = "images/0002-e1.png", OriginalImageFile = "images/0002.png", Edited = true, ShowHighlight = false,
            },
        ],
    };

    private static void AssertSameDoc(TutorialDocument expected, TutorialDocument actual)
    {
        Assert.Equal(expected.Title, actual.Title);
        Assert.Equal(expected.CreatedAt, actual.CreatedAt);
        Assert.Equal(expected.Finished, actual.Finished);
        Assert.Equal(expected.FormatVersion, actual.FormatVersion);
        Assert.Equal(expected.Items.Count, actual.Items.Count);
        for (int i = 0; i < expected.Items.Count; i++)
        {
            TutorialItem e = expected.Items[i], a = actual.Items[i];
            Assert.Equal(e with { Redactions = [] }, a with { Redactions = [] });
            Assert.Equal(e.Redactions, a.Redactions);
        }
    }

    [Fact]
    public void Json_RoundTrip()
    {
        TutorialDocument doc = SampleDoc();
        string json = TutorialSerializer.ToJson(doc);
        Assert.Contains("\"action\": \"click\"", json);
        Assert.Contains("\"formatVersion\": 1", json);
        AssertSameDoc(doc, TutorialSerializer.FromJson(json));
    }

    [Fact]
    public void FromJson_FutureVersion_Rejected()
    {
        string json = TutorialSerializer.ToJson(SampleDoc() with { FormatVersion = 2 });
        Assert.Throws<TutorialFormatException>(() => TutorialSerializer.FromJson(json));
    }

    [Theory]
    [InlineData("images/../../evil.png")]
    [InlineData("../evil.png")]
    [InlineData("C:/Windows/evil.png")]
    [InlineData("images\\0001.png")]
    [InlineData("/images/0001.png")]
    [InlineData("images/sub/0001.png")]
    [InlineData("images/0001.exe")]
    public void FromJson_BadImagePath_Rejected(string path)
    {
        TutorialDocument doc = SampleDoc() with
        {
            Items = [new TutorialItem { Id = "x", ImageFile = path }],
        };
        Assert.Throws<TutorialFormatException>(() => TutorialSerializer.FromJson(TutorialSerializer.ToJson(doc)));
    }

    [Theory]
    [InlineData("não é json")]
    [InlineData("null")]
    [InlineData("{\"formatVersion\":1,\"items\":[{\"kind\":\"step\"}]}")]          // sem id
    [InlineData("{\"formatVersion\":1,\"items\":[{\"id\":\"a\",\"kind\":5}]}")]    // enum numérico
    [InlineData("{\"formatVersion\":1,\"items\":[{\"id\":\"a\"},{\"id\":\"a\"}]}")] // id repetido
    public void FromJson_Invalid_Rejected(string json)
    {
        Assert.Throws<TutorialFormatException>(() => TutorialSerializer.FromJson(json));
    }

    [Fact]
    public void FromJson_NullFields_BecomeDefaults()
    {
        TutorialDocument doc = TutorialSerializer.FromJson(
            "{\"formatVersion\":1,\"title\":null,\"items\":[{\"id\":\"a\",\"text\":null,\"redactions\":null}]}");
        Assert.Equal("", doc.Title);
        Assert.Equal("", doc.Items[0].Text);
        Assert.Empty(doc.Items[0].Redactions);
    }

    private string MakeSourceFolder()
    {
        string source = Path.Combine(_root, "source");
        Directory.CreateDirectory(Path.Combine(source, "images"));
        foreach (string name in new[] { "0001.png", "0002.png", "0002-e1.png" })
        {
            using var bmp = new Bitmap(4, 3);
            bmp.SetPixel(1, 1, Color.Red);
            bmp.Save(Path.Combine(source, "images", name), System.Drawing.Imaging.ImageFormat.Png);
        }
        return source;
    }

    [Fact]
    public void Package_RoundTrip()
    {
        string source = MakeSourceFolder();
        string file = Path.Combine(_root, "tutorial.fragmotut");
        TutorialDocument doc = SampleDoc();

        TutorialSerializer.SavePackage(doc, source, file);
        Assert.False(File.Exists(file + ".tmp"));

        string target = Path.Combine(_root, "opened");
        TutorialDocument opened = TutorialSerializer.OpenPackage(file, target);

        AssertSameDoc(doc, opened);
        foreach (string image in TutorialSerializer.ReferencedImages(doc))
        {
            string rel = image.Replace('/', Path.DirectorySeparatorChar);
            Assert.Equal(File.ReadAllBytes(Path.Combine(source, rel)), File.ReadAllBytes(Path.Combine(target, rel)));
        }
    }

    [Fact]
    public void SavePackage_Overwrites_AndMissingImage_KeepsOldFile()
    {
        string source = MakeSourceFolder();
        string file = Path.Combine(_root, "t.fragmotut");
        TutorialSerializer.SavePackage(SampleDoc(), source, file);
        byte[] before = File.ReadAllBytes(file);

        TutorialDocument broken = SampleDoc() with
        {
            Items = [new TutorialItem { Id = "z", ImageFile = "images/9999.png" }],
        };
        Assert.Throws<FileNotFoundException>(() => TutorialSerializer.SavePackage(broken, source, file));
        Assert.Equal(before, File.ReadAllBytes(file));
        Assert.False(File.Exists(file + ".tmp"));
    }

    [Fact]
    public void OpenPackage_ZipSlip_NotExtractedOutside()
    {
        string file = Path.Combine(_root, "evil.fragmotut");
        TutorialDocument doc = new() { Items = [new TutorialItem { Id = "a", ImageFile = "images/0001.png" }] };
        using (ZipArchive zip = ZipFile.Open(file, ZipArchiveMode.Create))
        {
            using (var w = new StreamWriter(zip.CreateEntry("tutorial.json").Open()))
            {
                w.Write(TutorialSerializer.ToJson(doc));
            }
            using (var w = new StreamWriter(zip.CreateEntry("images/0001.png").Open()))
            {
                w.Write("png");
            }
            foreach (string evil in new[] { "../evil.png", "images/../../evil2.png", "images/evil.exe" })
            {
                using var w = new StreamWriter(zip.CreateEntry(evil).Open());
                w.Write("x");
            }
        }

        string target = Path.Combine(_root, "out", "work");
        TutorialSerializer.OpenPackage(file, target);

        Assert.False(File.Exists(Path.Combine(_root, "out", "evil.png")));
        Assert.False(File.Exists(Path.Combine(_root, "evil2.png")));
        Assert.False(File.Exists(Path.Combine(target, "images", "evil.exe")));
        Assert.True(File.Exists(Path.Combine(target, "images", "0001.png")));
    }

    [Fact]
    public void OpenPackage_MissingImage_Rejected()
    {
        string file = Path.Combine(_root, "missing.fragmotut");
        TutorialDocument doc = new() { Items = [new TutorialItem { Id = "a", ImageFile = "images/0001.png" }] };
        using (ZipArchive zip = ZipFile.Open(file, ZipArchiveMode.Create))
        {
            using var w = new StreamWriter(zip.CreateEntry("tutorial.json").Open());
            w.Write(TutorialSerializer.ToJson(doc));
        }
        Assert.Throws<TutorialFormatException>(() => TutorialSerializer.OpenPackage(file, Path.Combine(_root, "o")));
    }

    [Fact]
    public void OpenPackage_NotAZip_Rejected()
    {
        string file = Path.Combine(_root, "bad.fragmotut");
        File.WriteAllText(file, "isto não é um zip");
        Assert.Throws<TutorialFormatException>(() => TutorialSerializer.OpenPackage(file, Path.Combine(_root, "o")));
    }
}
