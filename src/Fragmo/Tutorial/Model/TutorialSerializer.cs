using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Fragmo.Tutorial;

/// <summary>Tutorial com formato inválido, de versão futura ou com caminhos de imagem perigosos.</summary>
public sealed class TutorialFormatException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>
/// Lê e grava o <c>tutorial.json</c> e o pacote <c>.fragmotut</c> (ZIP com <c>tutorial.json</c> + <c>images/</c>).
/// Tudo que vem de arquivo é tratado como não confiável: versão, caminhos e tamanhos são conferidos.
/// </summary>
public static partial class TutorialSerializer
{
    public const int CurrentFormatVersion = 1;
    public const string JsonEntryName = "tutorial.json";
    public const string ImagesFolder = "images";

    private const long MaxJsonBytes = 16L * 1024 * 1024;
    private const long MaxImageBytes = 200L * 1024 * 1024;

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false) },
    };

    // Só "images/<nome>.png", sem subpastas, "..", "\" nem caminho absoluto.
    [GeneratedRegex(@"^images/[A-Za-z0-9][A-Za-z0-9_\-]*\.png$")]
    private static partial Regex ImagePathPattern();

    public static bool IsValidImagePath(string path) => ImagePathPattern().IsMatch(path);

    public static string ToJson(TutorialDocument doc) => JsonSerializer.Serialize(doc, Options);

    public static TutorialDocument FromJson(string json)
    {
        TutorialDocument? doc;
        try
        {
            doc = JsonSerializer.Deserialize<TutorialDocument>(json, Options);
        }
        catch (JsonException ex)
        {
            throw new TutorialFormatException($"tutorial.json inválido: {ex.Message}", ex);
        }

        if (doc is null)
        {
            throw new TutorialFormatException("tutorial.json vazio.");
        }
        if (doc.FormatVersion is < 1 or > CurrentFormatVersion)
        {
            throw new TutorialFormatException(
                $"Versão de formato {doc.FormatVersion} não suportada (esta versão do Fragmo lê até a {CurrentFormatVersion}).");
        }

        // O JSON pode trazer null explícito em campos que o modelo considera não nulos.
        var items = new List<TutorialItem>((doc.Items ?? []).Count);
        foreach (TutorialItem? item in doc.Items ?? [])
        {
            if (item is null || string.IsNullOrWhiteSpace(item.Id))
            {
                throw new TutorialFormatException("Item sem identificador.");
            }
            CheckImagePath(item.ImageFile);
            CheckImagePath(item.OriginalImageFile);
            items.Add(item with { Text = item.Text ?? "", Redactions = item.Redactions ?? [] });
        }

        if (items.Select(i => i.Id).Distinct(StringComparer.Ordinal).Count() != items.Count)
        {
            throw new TutorialFormatException("Itens com identificador repetido.");
        }

        return doc with { Title = doc.Title ?? "", Items = items };
    }

    /// <summary>
    /// Grava o pacote: ZIP com <c>tutorial.json</c> e todas as imagens referenciadas (atuais e originais),
    /// primeiro em <c>.tmp</c> e depois movido por cima do destino.
    /// </summary>
    public static void SavePackage(TutorialDocument doc, string sourceFolder, string targetFile)
    {
        string tmp = targetFile + ".tmp";
        try
        {
            using (var zip = ZipFile.Open(tmp, ZipArchiveMode.Create))
            {
                ZipArchiveEntry jsonEntry = zip.CreateEntry(JsonEntryName, CompressionLevel.Optimal);
                using (var writer = new StreamWriter(jsonEntry.Open(), new UTF8Encoding(false)))
                {
                    writer.Write(ToJson(doc));
                }

                foreach (string image in ReferencedImages(doc))
                {
                    CheckImagePath(image);
                    string source = Path.Combine(sourceFolder, image.Replace('/', Path.DirectorySeparatorChar));
                    if (!File.Exists(source))
                    {
                        throw new FileNotFoundException($"Imagem do tutorial não encontrada: {image}", source);
                    }
                    // PNG já é comprimido: não vale gastar CPU de novo.
                    zip.CreateEntryFromFile(source, image, CompressionLevel.NoCompression);
                }
            }
            File.Move(tmp, targetFile, overwrite: true);
        }
        catch
        {
            TryDelete(tmp);
            throw;
        }
    }

    /// <summary>
    /// Abre um pacote em <paramref name="targetFolder"/>: extrai só o <c>tutorial.json</c> e as entradas
    /// <c>images/*.png</c> que ficam dentro da pasta (anti zip-slip) e confere que toda imagem citada existe.
    /// </summary>
    public static TutorialDocument OpenPackage(string file, string targetFolder)
    {
        string root = Path.GetFullPath(targetFolder);
        string rootWithSeparator = Path.EndsInDirectorySeparator(root) ? root : root + Path.DirectorySeparatorChar;
        Directory.CreateDirectory(Path.Combine(root, ImagesFolder));

        string? json = null;
        try
        {
            using ZipArchive zip = ZipFile.OpenRead(file);
            foreach (ZipArchiveEntry entry in zip.Entries)
            {
                if (entry.FullName == JsonEntryName)
                {
                    if (entry.Length > MaxJsonBytes)
                    {
                        throw new TutorialFormatException("tutorial.json grande demais.");
                    }
                    using var reader = new StreamReader(entry.Open(), Encoding.UTF8);
                    json = reader.ReadToEnd();
                    continue;
                }

                if (!IsValidImagePath(entry.FullName))
                {
                    continue; // qualquer outra coisa no ZIP é ignorada
                }
                string destination = Path.GetFullPath(Path.Combine(root, entry.FullName));
                if (!destination.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                if (entry.Length > MaxImageBytes)
                {
                    throw new TutorialFormatException($"Imagem grande demais: {entry.FullName}");
                }
                entry.ExtractToFile(destination, overwrite: true);
            }
        }
        catch (InvalidDataException ex)
        {
            throw new TutorialFormatException($"Arquivo de tutorial corrompido: {ex.Message}", ex);
        }

        if (json is null)
        {
            throw new TutorialFormatException("O arquivo não tem tutorial.json.");
        }

        TutorialDocument doc = FromJson(json);
        foreach (string image in ReferencedImages(doc))
        {
            if (!File.Exists(Path.Combine(root, image.Replace('/', Path.DirectorySeparatorChar))))
            {
                throw new TutorialFormatException($"Imagem citada no tutorial e ausente do arquivo: {image}");
            }
        }
        return doc;
    }

    /// <summary>Imagens citadas pelo documento (atuais e originais), sem repetição.</summary>
    public static IReadOnlyList<string> ReferencedImages(TutorialDocument doc) =>
        doc.Items
            .SelectMany(i => new[] { i.ImageFile, i.OriginalImageFile })
            .OfType<string>()
            .Distinct(StringComparer.Ordinal)
            .ToList();

    private static void CheckImagePath(string? path)
    {
        if (path is not null && !IsValidImagePath(path))
        {
            throw new TutorialFormatException($"Caminho de imagem inválido: {path}");
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
