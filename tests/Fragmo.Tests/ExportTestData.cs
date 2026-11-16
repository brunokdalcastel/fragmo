using Fragmo.Tutorial;
using Fragmo.Tutorial.Export;

namespace Fragmo.Tests;

/// <summary>Documentos e imagens pequenos para os testes dos exportadores.</summary>
internal static class ExportTestData
{
    public static byte[] Png(int width, int height, Color color)
    {
        using var bmp = new Bitmap(width, height);
        using (Graphics g = Graphics.FromImage(bmp))
        {
            g.Clear(color);
        }
        return ExportDocument.ToPng(bmp);
    }

    public static ExportStep Step(int number, string text, int width = 40, int height = 30) =>
        new(number, text, Png(width, height, Color.SteelBlue), new Size(width, height));

    /// <summary>Título, uma nota e dois passos com imagem.</summary>
    public static ExportDocument Sample(string title = "Como instalar") => new(title,
    [
        new ExportNote("Antes de começar"),
        Step(1, "Clique no botão “Avançar”"),
        Step(2, "Digite no campo “Nome”"),
    ]);

    public static string TempFolder()
    {
        string folder = Path.Combine(Path.GetTempPath(), "fragmo-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        return folder;
    }

    public static TutorialItem StepItem(string image = "images/0001.png") => new()
    {
        Id = TutorialItem.NewId(),
        Kind = TutorialItemKind.Step,
        Text = "Clique",
        Action = StepAction.Click,
        ImageFile = image,
    };
}
