using Fragmo.Output;
using Fragmo.Settings;

namespace Fragmo.Tests;

public sealed class FileNameBuilderTests
{
    private static readonly DateTime Now = new(2026, 10, 14, 15, 30, 12);

    [Theory]
    [InlineData(ImageFormatKind.Png, "Fragmo_2026-10-14_153012.png")]
    [InlineData(ImageFormatKind.Jpeg, "Fragmo_2026-10-14_153012.jpg")]
    [InlineData(ImageFormatKind.Bmp, "Fragmo_2026-10-14_153012.bmp")]
    public void Build_EachFormat(ImageFormatKind fmt, string expected)
    {
        Assert.Equal(expected, FileNameBuilder.Build("Fragmo", Now, fmt));
    }

    [Fact]
    public void Build_InvalidCharsInPrefix_AreRemoved()
    {
        Assert.Equal("Tela1_2026-10-14_153012.png", FileNameBuilder.Build("Te:la?1", Now, ImageFormatKind.Png));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(":?*")]
    public void Build_EmptyPrefix_UsesDefault(string prefix)
    {
        Assert.Equal("Fragmo_2026-10-14_153012.png", FileNameBuilder.Build(prefix, Now, ImageFormatKind.Png));
    }

    [Fact]
    public void MakeUnique_NotExisting_KeepsName()
    {
        Assert.Equal("a.png", FileNameBuilder.MakeUnique(@"C:\x", "a.png", _ => false));
    }

    [Fact]
    public void MakeUnique_Existing_AppendsCounter()
    {
        var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            Path.Combine(@"C:\x", "a.png"),
        };
        Assert.Equal("a_2.png", FileNameBuilder.MakeUnique(@"C:\x", "a.png", existing.Contains));

        existing.Add(Path.Combine(@"C:\x", "a_2.png"));
        Assert.Equal("a_3.png", FileNameBuilder.MakeUnique(@"C:\x", "a.png", existing.Contains));
    }
}
