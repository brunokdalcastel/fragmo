using Fragmo.Settings;

namespace Fragmo.Tests;

public sealed class SettingsStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
    private readonly SettingsStore _store;

    public SettingsStoreTests()
    {
        _store = new SettingsStore(_dir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    [Fact]
    public void Load_WhenMissing_ReturnsDefaults()
    {
        var s = _store.Load();

        Assert.Equal(1, s.Version);
        Assert.Equal("PrintScreen", s.Hotkeys.CaptureArea);
        Assert.Equal(ImageFormatKind.Png, s.DefaultFormat);
        Assert.Equal(90, s.JpegQuality);
        Assert.Null(s.LastRegion);
        Assert.False(File.Exists(_store.FilePath));
    }

    [Fact]
    public void SaveThenLoad_RoundTripsAllValues()
    {
        var original = new AppSettings
        {
            Hotkeys = new HotkeySettings
            {
                CaptureArea = "Ctrl+Alt+A",
                CaptureAreaFallback = "Ctrl+Shift+Q",
                CaptureFullScreen = "Alt+F1",
                CaptureWindow = "Win+Shift+W",
                RepeatLastArea = "Ctrl+Alt+R",
            },
            SaveFolder = @"D:\Shots",
            DefaultFormat = ImageFormatKind.Jpeg,
            JpegQuality = 70,
            FileNamePrefix = "Shot",
            AskWhereToSave = false,
            ShowMagnifier = false,
            StartWithWindows = true,
            Theme = ThemeMode.Light,
            Language = UiLanguage.English,
            LastColor = "#00FF00",
            LastThickness = 8,
            LastRegion = new RegionDto(-1920, -200, 640, 480),
        };

        _store.Save(original);
        var loaded = _store.Load();

        Assert.Equal(original.Hotkeys.CaptureArea, loaded.Hotkeys.CaptureArea);
        Assert.Equal(original.Hotkeys.CaptureAreaFallback, loaded.Hotkeys.CaptureAreaFallback);
        Assert.Equal(original.Hotkeys.CaptureFullScreen, loaded.Hotkeys.CaptureFullScreen);
        Assert.Equal(original.Hotkeys.CaptureWindow, loaded.Hotkeys.CaptureWindow);
        Assert.Equal(original.Hotkeys.RepeatLastArea, loaded.Hotkeys.RepeatLastArea);
        Assert.Equal(original.SaveFolder, loaded.SaveFolder);
        Assert.Equal(ImageFormatKind.Jpeg, loaded.DefaultFormat);
        Assert.Equal(70, loaded.JpegQuality);
        Assert.Equal("Shot", loaded.FileNamePrefix);
        Assert.False(loaded.AskWhereToSave);
        Assert.False(loaded.ShowMagnifier);
        Assert.True(loaded.StartWithWindows);
        Assert.Equal(ThemeMode.Light, loaded.Theme);
        Assert.Equal(UiLanguage.English, loaded.Language);
        Assert.Equal("#00FF00", loaded.LastColor);
        Assert.Equal(8, loaded.LastThickness);
        Assert.Equal(new RegionDto(-1920, -200, 640, 480), loaded.LastRegion);
        Assert.False(File.Exists(_store.FilePath + ".tmp"));
    }

    [Fact]
    public void Load_WhenCorrupt_ReturnsDefaultsAndKeepsBackup()
    {
        Directory.CreateDirectory(_dir);
        const string garbage = "{ this is not json";
        File.WriteAllText(_store.FilePath, garbage);

        var s = _store.Load();

        Assert.Equal(ThemeMode.Dark, s.Theme);
        string backup = Path.Combine(_dir, "settings.bad.json");
        Assert.True(File.Exists(backup));
        Assert.Equal(garbage, File.ReadAllText(backup));
        Assert.False(File.Exists(_store.FilePath));
    }

    [Fact]
    public void Load_ClampsOutOfRangeValues()
    {
        _store.Save(new AppSettings { JpegQuality = 500, LastThickness = 0, FileNamePrefix = "", LastColor = " " });

        var s = _store.Load();

        Assert.Equal(100, s.JpegQuality);
        Assert.Equal(1, s.LastThickness);
        Assert.Equal("Fragmo", s.FileNamePrefix);
        Assert.Equal("#FF3B30", s.LastColor);
    }

    [Fact]
    public void Load_ReplacesNullHotkeysWithDefaults()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(_store.FilePath, """{ "hotkeys": null, "language": "english" }""");

        var s = _store.Load();

        Assert.Equal("PrintScreen", s.Hotkeys.CaptureArea);
        Assert.Equal(UiLanguage.English, s.Language);
    }

    [Fact]
    public void Defaults_AreDarkThemeAndSystemLanguage()
    {
        var s = new AppSettings();

        Assert.Equal(ThemeMode.Dark, s.Theme);
        Assert.Equal(UiLanguage.System, s.Language);
    }
}
