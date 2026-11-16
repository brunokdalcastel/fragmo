using Fragmo.Ocr;

namespace Fragmo.Tests;

public sealed class SensitiveRedactorTests
{
    // "CPF 529.982.247-25": a palavra do CPF começa em x=40 e tem 140 px.
    private static OcrText CpfAt(int y) => new([new OcrLineBox(
    [
        new OcrWordBox("CPF", new Rectangle(0, y, 30, 20)),
        new OcrWordBox("529.982.247-25", new Rectangle(40, y, 140, 20)),
    ])]);

    [Fact]
    public void Targets_OffsetByAreaAndPadded()
    {
        Rectangle r = Assert.Single(SensitiveRedactor.Targets(CpfAt(10), new Rectangle(100, 200, 500, 300), 3));
        Assert.Equal(new Rectangle(137, 207, 146, 26), r);
    }

    [Fact]
    public void Targets_ClippedToArea()
    {
        // Área com 180 px de largura: a folga da direita passa da borda e é cortada; a de cima também (y=0).
        Rectangle r = Assert.Single(SensitiveRedactor.Targets(CpfAt(0), new Rectangle(0, 0, 180, 100), 3));
        Assert.Equal(Rectangle.FromLTRB(37, 0, 180, 23), r);
    }

    [Fact]
    public void Targets_OutsideArea_Removed()
    {
        // OCR fora da área (inconsistente) não vira tarja vazia.
        Assert.Empty(SensitiveRedactor.Targets(CpfAt(500), new Rectangle(0, 0, 400, 100), 3));
    }

    [Fact]
    public void Targets_CleanText_Empty()
    {
        var text = new OcrText([new OcrLineBox([new OcrWordBox("Olá", new Rectangle(0, 0, 30, 20))])]);
        Assert.Empty(SensitiveRedactor.Targets(text, new Rectangle(0, 0, 100, 100), 3));
    }
}
