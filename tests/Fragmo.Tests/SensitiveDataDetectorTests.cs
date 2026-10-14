using Fragmo.Ocr;

namespace Fragmo.Tests;

public sealed class SensitiveDataDetectorTests
{
    private const int CharWidth = 10;
    private const int LineHeight = 20;

    /// <summary>Monta uma linha de OCR com uma palavra por espaço; cada caractere tem 10 px de largura.</summary>
    private static OcrLineBox Line(string text, int y = 0)
    {
        var words = new List<OcrWordBox>();
        int x = 0;
        foreach (string w in text.Split(' '))
        {
            words.Add(new OcrWordBox(w, new Rectangle(x, y, w.Length * CharWidth, LineHeight)));
            x += (w.Length + 1) * CharWidth;
        }
        return new OcrLineBox(words);
    }

    private static IReadOnlyList<SensitiveMatch> Find(params string[] lines) =>
        SensitiveDataDetector.Find(lines.Select((l, i) => Line(l, i * 30)));

    // ----- CPF -----

    [Theory]
    [InlineData("529.982.247-25")]
    [InlineData("52998224725")]
    [InlineData("123.456.789-09")]
    public void Cpf_Valid_IsFound(string cpf)
    {
        SensitiveMatch m = Assert.Single(Find($"CPF {cpf}"));
        Assert.Equal(SensitiveKind.Cpf, m.Kind);
    }

    [Theory]
    [InlineData("529.982.247-24")] // último dígito errado
    [InlineData("123.456.789-00")]
    [InlineData("111.111.111-11")] // todos iguais
    public void Cpf_InvalidCheckDigits_IsNotBlurred(string cpf)
    {
        Assert.Empty(Find($"CPF {cpf}"));
    }

    [Fact]
    public void Cpf_Validation_Directly()
    {
        Assert.True(SensitiveDataDetector.IsValidCpf("52998224725"));
        Assert.False(SensitiveDataDetector.IsValidCpf("52998224724"));
        Assert.False(SensitiveDataDetector.IsValidCpf("5299822472"));
        Assert.False(SensitiveDataDetector.IsValidCpf("00000000000"));
    }

    // ----- CNPJ -----

    [Theory]
    [InlineData("11.222.333/0001-81")]
    [InlineData("11222333000181")]
    public void Cnpj_Valid_IsFound(string cnpj)
    {
        SensitiveMatch m = Assert.Single(Find($"CNPJ {cnpj}"));
        Assert.Equal(SensitiveKind.Cnpj, m.Kind);
    }

    [Theory]
    [InlineData("11.222.333/0001-82")]
    [InlineData("00.000.000/0000-00")]
    public void Cnpj_InvalidCheckDigits_IsNotBlurred(string cnpj)
    {
        Assert.Empty(Find($"CNPJ {cnpj}"));
    }

    // ----- E-mail -----

    [Theory]
    [InlineData("ana@exemplo.com")]
    [InlineData("maria.souza+fatura@exemplo.com.br")]
    public void Email_IsFound(string email)
    {
        SensitiveMatch m = Assert.Single(Find($"Contato: {email}"));
        Assert.Equal(SensitiveKind.Email, m.Kind);
        Assert.Equal(email, m.Text);
    }

    [Theory]
    [InlineData("ana@exemplo")]
    [InlineData("@exemplo.com")]
    public void Email_Incomplete_IsNotFound(string text)
    {
        Assert.Empty(Find(text));
    }

    // ----- IPv4 -----

    [Theory]
    [InlineData("192.168.0.10")]
    [InlineData("10.0.0.1")]
    [InlineData("255.255.255.0")]
    public void IPv4_Valid_IsFound(string ip)
    {
        SensitiveMatch m = Assert.Single(Find($"Servidor {ip}"));
        Assert.Equal(SensitiveKind.IPv4, m.Kind);
        Assert.Equal(ip, m.Text);
    }

    [Theory]
    [InlineData("999.1.1.1")]
    [InlineData("1.2.3")]
    [InlineData("1.2.3.4.5")]
    public void IPv4_Invalid_IsNotFound(string text)
    {
        Assert.DoesNotContain(Find($"versão {text}"), m => m.Kind == SensitiveKind.IPv4);
    }

    // ----- Telefone -----

    [Theory]
    [InlineData("(11) 98765-4321")]
    [InlineData("11 98765-4321")]
    [InlineData("11987654321")]
    [InlineData("+55 11 98765-4321")]
    [InlineData("98765-4321")]
    [InlineData("987654321")]
    [InlineData("(21) 3234-5678")]
    [InlineData("3234-5678")]
    [InlineData("32345678")]
    public void Phone_IsFound(string phone)
    {
        SensitiveMatch m = Assert.Single(Find($"Tel. {phone}"));
        Assert.Equal(SensitiveKind.Phone, m.Kind);
        Assert.Equal(phone, m.Text);
    }

    [Theory]
    [InlineData("Ano 2024")]
    [InlineData("Total R$ 1.234,56")]
    [InlineData("Pedido 1234")]
    [InlineData("Código 12345")]
    public void Phone_ShortNumbers_AreNotFound(string text)
    {
        Assert.Empty(Find(text));
    }

    // ----- Texto misturado e retângulos -----

    [Fact]
    public void MixedText_FindsEachItemOnce()
    {
        IReadOnlyList<SensitiveMatch> found = Find(
            "Cliente: Ana Souza CPF 529.982.247-25",
            "E-mail ana@exemplo.com Tel (11) 98765-4321",
            "Servidor 192.168.0.10 CNPJ 11.222.333/0001-81",
            "Pedido 1234 total R$ 98,50");

        Assert.Equal(
            [SensitiveKind.Cpf, SensitiveKind.Email, SensitiveKind.Phone, SensitiveKind.Cnpj, SensitiveKind.IPv4],
            found.Select(m => m.Kind).ToArray());
    }

    [Fact]
    public void Bounds_CoverOnlyTheData_NotTheLabel()
    {
        // "CPF" ocupa x 0..30; o número começa em x 40.
        SensitiveMatch m = Assert.Single(Find("CPF 529.982.247-25"));

        Assert.Equal(new Rectangle(40, 0, 14 * CharWidth, LineHeight), m.Bounds);
    }

    [Fact]
    public void Bounds_SpanSeveralWords()
    {
        // "Tel" (0..30) + "(11)" (40..80) + "98765-4321" (90..190)
        SensitiveMatch m = Assert.Single(Find("Tel (11) 98765-4321"));

        Assert.Equal(Rectangle.FromLTRB(40, 0, 190, LineHeight), m.Bounds);
    }

    [Fact]
    public void Bounds_PartOfAWord_UsesProportionalWidth()
    {
        // Uma palavra só, de 18 caracteres: "CPF:" + 14 do número.
        SensitiveMatch m = Assert.Single(Find("CPF:529.982.247-25"));

        Assert.Equal(Rectangle.FromLTRB(40, 0, 180, LineHeight), m.Bounds);
    }

    [Fact]
    public void Lines_AreIndependent()
    {
        IReadOnlyList<Rectangle> rects = SensitiveDataDetector.Detect([Line("ana@exemplo.com", 0), Line("bia@exemplo.com", 30)]);

        Assert.Equal(2, rects.Count);
        Assert.Equal(0, rects[0].Top);
        Assert.Equal(30, rects[1].Top);
    }

    [Fact]
    public void EmptyWordsFromOcr_AreIgnored()
    {
        // O OCR às vezes devolve "palavras" vazias (ex.: um ponto do meio).
        var line = new OcrLineBox(
        [
            new OcrWordBox("ana@exemplo.com", new Rectangle(0, 0, 150, 20)),
            new OcrWordBox(" ", new Rectangle(160, 0, 4, 20)),
            new OcrWordBox("3234-5678", new Rectangle(170, 0, 90, 20)),
        ]);

        IReadOnlyList<SensitiveMatch> found = SensitiveDataDetector.Find([line]);

        Assert.Equal([SensitiveKind.Email, SensitiveKind.Phone], found.Select(m => m.Kind).ToArray());
    }

    [Fact]
    public void NoSensitiveData_ReturnsEmpty()
    {
        Assert.Empty(Find("Relatório de vendas do terceiro trimestre", "Valores em milhares de reais"));
    }
}
