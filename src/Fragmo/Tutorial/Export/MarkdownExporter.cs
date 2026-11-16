using System.Text;
using System.Text.RegularExpressions;

namespace Fragmo.Tutorial.Export;

/// <summary>
/// Markdown com as imagens numa pasta <c>&lt;nome&gt;_imagens</c> ao lado do <c>.md</c> (links relativos),
/// para funcionar no GitHub, no VS Code e no Obsidian.
/// </summary>
public static partial class MarkdownExporter
{
    public const string ImagesSuffix = "_imagens";

    public static void Write(ExportDocument doc, string mdPath)
    {
        string fullPath = Path.GetFullPath(mdPath);
        string folderName = Path.GetFileNameWithoutExtension(fullPath) + ImagesSuffix;
        string imagesFolder = Path.Combine(Path.GetDirectoryName(fullPath)!, folderName);

        var steps = doc.Steps.Where(s => s.HasImage).ToList();
        if (steps.Count > 0)
        {
            Directory.CreateDirectory(imagesFolder);
            RemoveOldImages(imagesFolder);
        }

        int digits = steps.Count == 0 ? 2 : Math.Max(2, steps.Max(s => s.Number).ToString(System.Globalization.CultureInfo.InvariantCulture).Length);
        var md = new StringBuilder();
        string title = ExportText.SingleLine(doc.Title);
        if (title.Length > 0)
        {
            md.Append("# ").Append(Escape(title)).Append("\n\n");
        }

        foreach (ExportEntry entry in doc.Entries)
        {
            switch (entry)
            {
                case ExportNote note:
                    md.Append("## ").Append(Escape(ExportText.SingleLine(note.Text))).Append("\n\n");
                    break;

                case ExportStep step:
                    md.Append("**").Append(step.Number).Append(".** ").Append(MultiLine(step.Text)).Append("\n\n");
                    if (step.HasImage)
                    {
                        string fileName = ImageFileName(step.Number, digits);
                        File.WriteAllBytes(Path.Combine(imagesFolder, fileName), step.Png);
                        md.Append("![").Append(Escape(ExportText.SingleLine(step.Text))).Append("](")
                            .Append(EncodePathSegment(folderName)).Append('/').Append(fileName).Append(")\n\n");
                    }
                    break;
            }
        }

        ExportText.WriteAtomic(fullPath, stream =>
        {
            byte[] bytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(md.ToString());
            stream.Write(bytes);
        });
    }

    /// <summary><c>passo-01.png</c>; 3 dígitos quando há mais de 99 passos.</summary>
    public static string ImageFileName(int number, int digits) =>
        "passo-" + number.ToString(System.Globalization.CultureInfo.InvariantCulture).PadLeft(digits, '0') + ".png";

    /// <summary>Escapa o que o Markdown interpretaria como formatação, link, título ou HTML.</summary>
    public static string Escape(string text)
    {
        var sb = new StringBuilder(text.Length);
        foreach (char c in text)
        {
            if (c is '\\' or '*' or '_' or '[' or ']' or '#' or '<' or '>' or '`')
            {
                sb.Append('\\');
            }
            sb.Append(c);
        }
        return sb.ToString();
    }

    /// <summary>
    /// Codifica só o que quebra um link Markdown (espaço, parênteses, <c>%</c>, <c>#</c>, <c>?</c>...).
    /// Acentos ficam como estão, que é o que o Obsidian e o VS Code esperam.
    /// </summary>
    public static string EncodePathSegment(string segment)
    {
        var sb = new StringBuilder(segment.Length);
        foreach (char c in segment)
        {
            if (c is ' ' or '(' or ')' or '%' or '#' or '?' or '<' or '>' or '[' or ']' or '\\' or '"' || char.IsControl(c))
            {
                foreach (byte b in Encoding.UTF8.GetBytes(c.ToString()))
                {
                    sb.Append('%').Append(b.ToString("X2", System.Globalization.CultureInfo.InvariantCulture));
                }
            }
            else
            {
                sb.Append(c);
            }
        }
        return sb.ToString();
    }

    /// <summary>Linha de passo com várias linhas: quebra forçada do Markdown (dois espaços).</summary>
    private static string MultiLine(string text) => string.Join("  \n", ExportText.Lines(text).Select(Escape));

    /// <summary>Uma exportação anterior com mais passos deixaria imagens sobrando; só apaga o que é nosso.</summary>
    private static void RemoveOldImages(string folder)
    {
        foreach (string file in Directory.EnumerateFiles(folder, "passo-*.png"))
        {
            if (OwnImagePattern().IsMatch(Path.GetFileName(file)))
            {
                File.Delete(file);
            }
        }
    }

    [GeneratedRegex(@"^passo-\d{2,}\.png$")]
    private static partial Regex OwnImagePattern();
}
