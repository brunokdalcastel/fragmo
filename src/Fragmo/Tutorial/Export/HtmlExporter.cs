using System.Net;
using System.Text;

namespace Fragmo.Tutorial.Export;

/// <summary>
/// HTML5 de arquivo único: CSS embutido e imagens em <c>data:</c>, para mandar por e-mail ou abrir offline.
/// Todo texto passa por <see cref="WebUtility.HtmlEncode(string)"/>; nenhum script.
/// </summary>
public static class HtmlExporter
{
    private const string Css = """
        body { margin: 0; background: #f4f4f6; color: #1f1f24; font: 16px/1.5 "Segoe UI", system-ui, sans-serif; }
        main { max-width: 960px; margin: 0 auto; padding: 32px 24px 64px; background: #fff; }
        h1 { margin: 0 0 24px; font-size: 28px; }
        h2 { margin: 32px 0 12px; font-size: 20px; }
        .step { margin: 0 0 32px; }
        .step p { margin: 0 0 8px; }
        .num { font-weight: 600; color: #A8204F; }
        img { display: block; max-width: 100%; height: auto; border: 1px solid #d8d8de; border-radius: 4px; }
        @media print {
          body { background: #fff; }
          main { padding: 0; }
          .step { break-inside: avoid; }
        }
        """;

    public static string Build(ExportDocument doc)
    {
        var html = new StringBuilder();
        string title = ExportText.SingleLine(doc.Title);

        html.Append("<!DOCTYPE html>\n<html>\n<head>\n<meta charset=\"utf-8\">\n")
            .Append("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">\n")
            .Append("<title>").Append(Encode(title)).Append("</title>\n")
            .Append("<style>\n").Append(Css).Append("\n</style>\n</head>\n<body>\n<main>\n");

        if (title.Length > 0)
        {
            html.Append("<h1>").Append(Encode(title)).Append("</h1>\n");
        }

        foreach (ExportEntry entry in doc.Entries)
        {
            switch (entry)
            {
                case ExportNote note:
                    html.Append("<h2>").Append(MultiLine(note.Text)).Append("</h2>\n");
                    break;

                case ExportStep step:
                    html.Append("<section class=\"step\">\n<p><span class=\"num\">")
                        .Append(step.Number).Append(".</span> ")
                        .Append(MultiLine(step.Text)).Append("</p>\n");
                    if (step.HasImage)
                    {
                        html.Append("<img src=\"data:image/png;base64,").Append(Convert.ToBase64String(step.Png))
                            .Append("\" width=\"").Append(step.PixelSize.Width)
                            .Append("\" height=\"").Append(step.PixelSize.Height)
                            .Append("\" alt=\"").Append(Encode(ExportText.SingleLine(step.Text))).Append("\">\n");
                    }
                    html.Append("</section>\n");
                    break;
            }
        }

        html.Append("</main>\n</body>\n</html>\n");
        return html.ToString();
    }

    public static void Write(ExportDocument doc, string path) =>
        ExportText.WriteAtomic(path, stream =>
        {
            byte[] bytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(Build(doc));
            stream.Write(bytes);
        });

    private static string Encode(string text) => WebUtility.HtmlEncode(text);

    private static string MultiLine(string text) => string.Join("<br>", ExportText.Lines(text).Select(Encode));
}
