namespace Fragmo.Ocr;

/// <summary>Uma palavra reconhecida e onde ela está na imagem (em pixels).</summary>
public sealed record OcrWordBox(string Text, Rectangle Bounds);

/// <summary>Uma linha de texto reconhecida, com as palavras na ordem de leitura.</summary>
public sealed record OcrLineBox(IReadOnlyList<OcrWordBox> Words)
{
    public string Text => string.Join(" ", Words.Select(w => w.Text));
}

/// <summary>Resultado do OCR de uma imagem.</summary>
public sealed record OcrText(IReadOnlyList<OcrLineBox> Lines)
{
    public string FullText => string.Join(Environment.NewLine, Lines.Select(l => l.Text));
}
