namespace Fragmo.Settings;

public enum ImageFormatKind { Png, Jpeg, Bmp }
public enum ThemeMode { System, Light, Dark }
public enum UiLanguage { System, Portuguese, English }

public sealed class HotkeySettings
{
    public string CaptureArea { get; set; } = "PrintScreen";
    public string CaptureAreaFallback { get; set; } = "Ctrl+Shift+S";
    public string CaptureFullScreen { get; set; } = "Ctrl+Shift+F";
    public string CaptureWindow { get; set; } = "Ctrl+Shift+W";
    public string RepeatLastArea { get; set; } = "Ctrl+Shift+R";
}

public sealed record RegionDto(int X, int Y, int Width, int Height);

public sealed class AppSettings
{
    public int Version { get; set; } = 1;
    public HotkeySettings Hotkeys { get; set; } = new();
    public string SaveFolder { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Fragmo");
    public ImageFormatKind DefaultFormat { get; set; } = ImageFormatKind.Png;
    public int JpegQuality { get; set; } = 90;          // 1..100
    public string FileNamePrefix { get; set; } = "Fragmo";
    public bool AskWhereToSave { get; set; } = true;
    public bool ShowMagnifier { get; set; } = true;
    public bool StartWithWindows { get; set; } = false;
    public ThemeMode Theme { get; set; } = ThemeMode.Dark;
    public UiLanguage Language { get; set; } = UiLanguage.System;
    public string LastColor { get; set; } = "#FF3B30";
    public int LastThickness { get; set; } = 3;          // 1..20
    public RegionDto? LastRegion { get; set; }
}
