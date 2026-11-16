using System.Drawing.Imaging;
using System.Globalization;

namespace Fragmo.Tutorial.Recording;

/// <summary>
/// Uma gravação em andamento, sempre em disco: <c>tutorial.json</c> (com <see cref="TutorialDocument.Finished"/>
/// = <c>false</c>) e <c>images/NNNN.png</c>. Cada mudança reescreve o JSON de forma atômica, então um processo
/// morto no meio deixa uma sessão recuperável. Seguro para chamadas de threads diferentes.
/// </summary>
internal sealed class RecordingSession
{
    private const string TempSuffix = ".tmp";

    private readonly object _lock = new();
    private TutorialDocument _document;
    private int _nextImage;

    private RecordingSession(string folder, TutorialDocument document)
    {
        Folder = folder;
        _document = document;
        _nextImage = document.Items
            .Select(i => ImageNumber(i.ImageFile))
            .DefaultIfEmpty(0)
            .Max() + 1;
    }

    /// <summary><c>%LOCALAPPDATA%\Fragmo\Tutorials</c>.</summary>
    public static string DefaultRoot => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Fragmo", "Tutorials");

    public string Folder { get; }

    public TutorialDocument Document
    {
        get
        {
            lock (_lock)
            {
                return _document;
            }
        }
    }

    public int StepCount => Document.Items.Count(i => i.Kind == TutorialItemKind.Step);

    private string JsonPath => Path.Combine(Folder, TutorialSerializer.JsonEntryName);

    /// <summary>Cria a pasta <c>&lt;yyyyMMdd-HHmmss&gt;-&lt;guid8&gt;</c> em <paramref name="root"/> com o JSON vazio.</summary>
    public static RecordingSession Create(string root, string title, DateTimeOffset now)
    {
        string folder = NewFolderPath(root, now);
        Directory.CreateDirectory(Path.Combine(folder, TutorialSerializer.ImagesFolder));

        var session = new RecordingSession(folder, new TutorialDocument
        {
            FormatVersion = TutorialSerializer.CurrentFormatVersion,
            Title = title,
            CreatedAt = now,
            Finished = false,
        });
        session.WriteJson(session._document);
        return session;
    }

    /// <summary>
    /// Caminho (ainda não criado) de uma pasta nova em <paramref name="root"/>:
    /// <c>&lt;yyyyMMdd-HHmmss&gt;-&lt;guid8&gt;</c>. Também usado pelo editor ao abrir um <c>.fragmotut</c>.
    /// </summary>
    public static string NewFolderPath(string root, DateTimeOffset now) => Path.Combine(root,
        now.ToLocalTime().ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + "-" + Guid.NewGuid().ToString("N")[..8]);

    /// <summary>Pastas de sessão em <paramref name="root"/> com <c>tutorial.json</c> legível e não finalizado.</summary>
    public static IReadOnlyList<string> FindUnfinished(string root)
    {
        if (!Directory.Exists(root))
        {
            return [];
        }

        List<string> result = [];
        foreach (string folder in Directory.EnumerateDirectories(root).Order(StringComparer.OrdinalIgnoreCase))
        {
            if (TryRead(folder) is { Finished: false })
            {
                result.Add(folder);
            }
        }
        return result;
    }

    /// <summary>Reabre uma sessão gravada antes (ex.: depois de o processo ter sido encerrado).</summary>
    /// <exception cref="TutorialFormatException">JSON ausente ou inválido.</exception>
    public static RecordingSession Resume(string folder)
    {
        string json;
        try
        {
            json = File.ReadAllText(Path.Combine(folder, TutorialSerializer.JsonEntryName));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new TutorialFormatException($"Não foi possível ler a sessão: {ex.Message}", ex);
        }
        return new RecordingSession(folder, TutorialSerializer.FromJson(json));
    }

    /// <summary>
    /// Grava <paramref name="image"/> como o próximo <c>images/NNNN.png</c>, acrescenta o passo com esse caminho
    /// e reescreve o JSON. Devolve o item como ficou salvo.
    /// </summary>
    /// <exception cref="DirectoryNotFoundException">A pasta da sessão sumiu.</exception>
    /// <exception cref="IOException">Disco cheio ou outro erro de gravação.</exception>
    public TutorialItem AddStep(Bitmap image, TutorialItem item)
    {
        lock (_lock)
        {
            if (!Directory.Exists(Folder))
            {
                throw new DirectoryNotFoundException(Folder);
            }

            string relative = $"{TutorialSerializer.ImagesFolder}/{_nextImage:0000}.png";
            string full = Path.Combine(Folder, TutorialSerializer.ImagesFolder, $"{_nextImage:0000}.png");
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            SaveImageAtomically(image, full);
            _nextImage++;

            TutorialItem saved = item with { ImageFile = relative };
            TutorialDocument next = _document with { Items = [.. _document.Items, saved] };
            WriteJson(next);
            _document = next;
            return saved;
        }
    }

    /// <summary>Troca o item <paramref name="id"/> pelo resultado de <paramref name="update"/>. Devolve <c>false</c> se não existe.</summary>
    public bool UpdateItem(string id, Func<TutorialItem, TutorialItem> update)
    {
        lock (_lock)
        {
            int index = _document.Items.ToList().FindIndex(i => i.Id == id);
            if (index < 0)
            {
                return false;
            }

            var items = _document.Items.ToArray();
            TutorialItem updated = update(items[index]) with { Id = id };
            if (updated == items[index])
            {
                return true;
            }
            items[index] = updated;
            TutorialDocument next = _document with { Items = items };
            WriteJson(next);
            _document = next;
            return true;
        }
    }

    /// <summary>O último passo, se houver (para o duplo clique).</summary>
    public TutorialItem? LastStep
    {
        get
        {
            lock (_lock)
            {
                return _document.Items.LastOrDefault(i => i.Kind == TutorialItemKind.Step);
            }
        }
    }

    public void MarkFinished()
    {
        lock (_lock)
        {
            if (_document.Finished)
            {
                return;
            }
            TutorialDocument next = _document with { Finished = true };
            WriteJson(next);
            _document = next;
        }
    }

    /// <summary>Apaga a pasta da sessão inteira. Não falha se ela já não existe.</summary>
    public void Delete()
    {
        lock (_lock)
        {
            if (Directory.Exists(Folder))
            {
                Directory.Delete(Folder, recursive: true);
            }
        }
    }

    private void WriteJson(TutorialDocument doc)
    {
        if (!Directory.Exists(Folder))
        {
            throw new DirectoryNotFoundException(Folder);
        }
        string tmp = JsonPath + TempSuffix;
        File.WriteAllText(tmp, TutorialSerializer.ToJson(doc));
        File.Move(tmp, JsonPath, overwrite: true);
    }

    private static void SaveImageAtomically(Bitmap image, string path)
    {
        string tmp = path + TempSuffix;
        try
        {
            image.Save(tmp, ImageFormat.Png);
        }
        catch (System.Runtime.InteropServices.ExternalException ex)
        {
            // O GDI+ devolve um "erro genérico" quando o disco enche ou a pasta some.
            TryDelete(tmp);
            throw new IOException(ex.Message, ex);
        }
        File.Move(tmp, path, overwrite: true);
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    private static TutorialDocument? TryRead(string folder)
    {
        try
        {
            string path = Path.Combine(folder, TutorialSerializer.JsonEntryName);
            return File.Exists(path) ? TutorialSerializer.FromJson(File.ReadAllText(path)) : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or TutorialFormatException)
        {
            return null;
        }
    }

    private static int ImageNumber(string? imageFile)
    {
        if (imageFile is null)
        {
            return 0;
        }
        string name = Path.GetFileNameWithoutExtension(imageFile);
        return int.TryParse(name, NumberStyles.None, CultureInfo.InvariantCulture, out int n) ? n : 0;
    }
}
