using System.Drawing.Imaging;

namespace Fragmo.Tutorial.Export;

/// <summary>Um item do documento exportado: nota (só texto) ou passo numerado com a imagem composta.</summary>
public abstract record ExportEntry;

public sealed record ExportNote(string Text) : ExportEntry;

/// <summary>Passo com a imagem já composta em PNG. <see cref="Png"/> vazio = passo sem imagem.</summary>
public sealed record ExportStep(int Number, string Text, byte[] Png, Size PixelSize) : ExportEntry
{
    public bool HasImage => Png.Length > 0 && PixelSize.Width > 0 && PixelSize.Height > 0;
}

/// <summary>
/// O tutorial pronto para os exportadores: textos finais e imagens compostas (tarjas e destaque aplicados).
/// Todos os formatos saem daqui, então as tarjas valem para todos.
/// </summary>
public sealed record ExportDocument(string Title, IReadOnlyList<ExportEntry> Entries)
{
    public IEnumerable<ExportStep> Steps => Entries.OfType<ExportStep>();

    /// <summary>
    /// Compõe as imagens um passo por vez (só o PNG fica em memória, não o bitmap).
    /// A numeração conta só os passos; notas não recebem número.
    /// </summary>
    public static ExportDocument Build(TutorialDocument doc, string folder)
    {
        var entries = new List<ExportEntry>(doc.Items.Count);
        int number = 0;
        foreach (TutorialItem item in doc.Items)
        {
            if (item.Kind == TutorialItemKind.Note)
            {
                entries.Add(new ExportNote(item.Text));
                continue;
            }

            number++;
            if (item.ImageFile is null)
            {
                entries.Add(new ExportStep(number, item.Text, [], Size.Empty));
                continue;
            }

            using Bitmap image = StepImageRenderer.Render(item, number, folder);
            entries.Add(new ExportStep(number, item.Text, ToPng(image), image.Size));
        }

        return new ExportDocument(doc.Title, entries);
    }

    internal static byte[] ToPng(Bitmap image)
    {
        using var stream = new MemoryStream();
        image.Save(stream, ImageFormat.Png);
        return stream.ToArray();
    }
}

/// <summary>Regras de texto comuns a todos os exportadores.</summary>
internal static class ExportText
{
    /// <summary>Quebra o texto nas linhas que o usuário digitou (aceita \r\n, \n e \r).</summary>
    public static string[] Lines(string text) =>
        text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');

    /// <summary>Uma linha só (para títulos): quebras viram espaço.</summary>
    public static string SingleLine(string text) => string.Join(' ', Lines(text).Select(l => l.Trim())).Trim();

    /// <summary>Grava em <c>.tmp</c> e troca no fim, para um erro no meio não deixar meio arquivo no lugar do antigo.</summary>
    public static void WriteAtomic(string path, Action<Stream> write)
    {
        string tmp = path + ".tmp";
        try
        {
            using (var stream = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                write(stream);
            }
            File.Move(tmp, path, overwrite: true);
        }
        catch
        {
            try { File.Delete(tmp); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            throw;
        }
    }
}
