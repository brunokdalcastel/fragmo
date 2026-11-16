using Fragmo.Settings;

namespace Fragmo.Output;

/// <summary>Monta nomes de arquivo de captura e evita sobrescrever arquivos existentes.</summary>
public static class FileNameBuilder
{
    private const string DefaultPrefix = "Fragmo";

    private const int MaxBaseNameLength = 80;

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

    /// <summary>
    /// Texto livre (ex.: título do tutorial) como nome de arquivo sem extensão: tira caracteres inválidos,
    /// junta espaços, corta em 80 caracteres e tira pontos/espaços do fim (o Windows não aceita).
    /// Vazio ou nome reservado (CON, NUL…) → <paramref name="fallback"/>.
    /// </summary>
    public static string Sanitize(string? text, string fallback)
    {
        char[] invalid = Path.GetInvalidFileNameChars();
        string clean = new string((text ?? string.Empty).Select(c => char.IsWhiteSpace(c) ? ' ' : c)
            .Where(c => !invalid.Contains(c)).ToArray());
        clean = string.Join(' ', clean.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        if (clean.Length > MaxBaseNameLength)
        {
            clean = clean[..MaxBaseNameLength];
        }
        clean = clean.TrimEnd(' ', '.');
        return clean.Length == 0 || IsReserved(clean) ? fallback : clean;
    }

    private static bool IsReserved(string name)
    {
        string stem = name.Split('.')[0].Trim().ToUpperInvariant();
        return stem is "CON" or "PRN" or "AUX" or "NUL"
            || (stem.Length == 4 && (stem.StartsWith("COM", StringComparison.Ordinal) || stem.StartsWith("LPT", StringComparison.Ordinal))
                && stem[3] is >= '1' and <= '9');
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
