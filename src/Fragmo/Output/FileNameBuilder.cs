using Fragmo.Settings;

namespace Fragmo.Output;

/// <summary>Monta nomes de arquivo de captura e evita sobrescrever arquivos existentes.</summary>
public static class FileNameBuilder
{
    private const string DefaultPrefix = "Fragmo";

    public static string Build(string prefix, DateTime now, ImageFormatKind fmt)
    {
        char[] invalid = Path.GetInvalidFileNameChars();
        string clean = new string((prefix ?? string.Empty).Where(c => !invalid.Contains(c)).ToArray()).Trim();
        if (clean.Length == 0)
        {
            clean = DefaultPrefix;
        }
        return $"{clean}_{now:yyyy-MM-dd_HHmmss}.{Extension(fmt)}";
    }

    public static string MakeUnique(string folder, string fileName, Func<string, bool> exists)
    {
        string name = Path.GetFileNameWithoutExtension(fileName);
        string ext = Path.GetExtension(fileName);
        string candidate = fileName;
        for (int n = 2; exists(Path.Combine(folder, candidate)); n++)
        {
            candidate = $"{name}_{n}{ext}";
        }
        return candidate;
    }

    public static string Extension(ImageFormatKind fmt) => fmt switch
    {
        ImageFormatKind.Jpeg => "jpg",
        ImageFormatKind.Bmp => "bmp",
        _ => "png",
    };
}
