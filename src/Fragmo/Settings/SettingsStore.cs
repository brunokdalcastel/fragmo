using System.Text.Json;
using System.Text.Json.Serialization;

namespace Fragmo.Settings;

/// <summary>
/// Lê e grava <see cref="AppSettings"/> em <c>settings.json</c>, de forma atômica e tolerante a arquivo corrompido.
/// </summary>
public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    private readonly string _directory;

    public SettingsStore(string directory)
    {
        _directory = directory;
    }

    public static SettingsStore CreateDefault() => new(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Fragmo"));

    public string Directory => _directory;

    public string FilePath => Path.Combine(_directory, "settings.json");

    private string BackupPath => Path.Combine(_directory, "settings.bad.json");

    public AppSettings Load()
    {
        if (!File.Exists(FilePath))
        {
            return new AppSettings();
        }

        AppSettings? loaded;
        try
        {
            loaded = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath), JsonOptions);
        }
        catch (JsonException)
        {
            File.Move(FilePath, BackupPath, overwrite: true);
            return new AppSettings();
        }

        return Normalize(loaded ?? new AppSettings());
    }

    public void Save(AppSettings s)
    {
        System.IO.Directory.CreateDirectory(_directory);
        string tmp = FilePath + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(s, JsonOptions));
        File.Move(tmp, FilePath, overwrite: true);
    }

    private static AppSettings Normalize(AppSettings s)
    {
        var defaults = new AppSettings();
        var defaultKeys = defaults.Hotkeys;

        s.Hotkeys ??= new HotkeySettings();
        s.Hotkeys.CaptureArea = OrDefault(s.Hotkeys.CaptureArea, defaultKeys.CaptureArea);
        s.Hotkeys.CaptureAreaFallback = OrDefault(s.Hotkeys.CaptureAreaFallback, defaultKeys.CaptureAreaFallback);
        s.Hotkeys.CaptureFullScreen = OrDefault(s.Hotkeys.CaptureFullScreen, defaultKeys.CaptureFullScreen);
        s.Hotkeys.CaptureWindow = OrDefault(s.Hotkeys.CaptureWindow, defaultKeys.CaptureWindow);
        s.Hotkeys.RepeatLastArea = OrDefault(s.Hotkeys.RepeatLastArea, defaultKeys.RepeatLastArea);

        s.SaveFolder = OrDefault(s.SaveFolder, defaults.SaveFolder);
        s.FileNamePrefix = OrDefault(s.FileNamePrefix, defaults.FileNamePrefix);
        s.LastColor = OrDefault(s.LastColor, defaults.LastColor);

        s.JpegQuality = Math.Clamp(s.JpegQuality, 1, 100);
        s.LastThickness = Math.Clamp(s.LastThickness, 1, 20);

        if (!Enum.IsDefined(s.DefaultFormat)) s.DefaultFormat = defaults.DefaultFormat;
        if (!Enum.IsDefined(s.Theme)) s.Theme = defaults.Theme;
        if (!Enum.IsDefined(s.Language)) s.Language = defaults.Language;

        return s;
    }

    private static string OrDefault(string? value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value;
}
