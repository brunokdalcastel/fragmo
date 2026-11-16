using System.Globalization;
using System.Text;

namespace Fragmo.Tutorial.Export;

/// <summary>
/// Larguras das fontes padrão Helvetica e Helvetica-Bold (AFM da Adobe, em 1/1000 do tamanho) e a codificação
/// WinAnsi (cp1252) usada no PDF (D4). Acentuadas têm a largura da letra base, como no AFM.
/// </summary>
internal static class HelveticaMetrics
{
    /// <summary>cp1252 com <c>?</c> para tudo que não existe nela (emoji, CJK...), sem aproximações.</summary>
    public static readonly Encoding WinAnsi = CreateWinAnsi();

    // ASCII 32..126.
    private static readonly short[] RegularAscii =
    [
        278, 278, 355, 556, 556, 889, 667, 191, 333, 333, 389, 584, 278, 333, 278, 278,
        556, 556, 556, 556, 556, 556, 556, 556, 556, 556, 278, 278, 584, 584, 584, 556,
        1015, 667, 667, 722, 722, 667, 611, 778, 722, 278, 500, 667, 556, 833, 722, 778,
        667, 778, 722, 667, 611, 722, 667, 944, 667, 667, 611, 278, 278, 278, 469, 556,
        333, 556, 556, 500, 556, 556, 278, 556, 556, 222, 222, 500, 222, 833, 556, 556,
        556, 556, 333, 500, 278, 556, 500, 722, 500, 500, 500, 334, 260, 334, 584,
    ];

    private static readonly short[] BoldAscii =
    [
        278, 333, 474, 556, 556, 889, 722, 238, 333, 333, 389, 584, 278, 333, 278, 278,
        556, 556, 556, 556, 556, 556, 556, 556, 556, 556, 333, 333, 584, 584, 584, 611,
        975, 722, 722, 722, 722, 667, 611, 778, 722, 278, 556, 722, 611, 833, 722, 778,
        667, 778, 722, 667, 611, 722, 667, 944, 667, 667, 611, 333, 278, 333, 584, 556,
        333, 556, 611, 556, 611, 556, 333, 611, 611, 278, 278, 556, 278, 889, 611, 611,
        611, 611, 389, 556, 333, 611, 556, 778, 556, 556, 500, 389, 280, 389, 584,
    ];

    private static readonly short[] Regular = BuildTable(RegularAscii, bold: false);
    private static readonly short[] Bold = BuildTable(BoldAscii, bold: true);

    /// <summary>Largura em pontos do texto já convertido para WinAnsi.</summary>
    public static float Measure(string text, bool bold, float size)
    {
        short[] table = bold ? Bold : Regular;
        int units = 0;
        foreach (byte b in ToWinAnsi(text))
        {
            units += table[b];
        }
        return units * size / 1000f;
    }

    /// <summary>Bytes cp1252; cada caractere fora dela vira um <c>?</c> (um só por emoji, não um por metade do par UTF-16).</summary>
    public static byte[] ToWinAnsi(string text)
    {
        var sb = new StringBuilder(text.Length);
        foreach (Rune rune in text.EnumerateRunes())
        {
            if (rune.IsBmp)
            {
                sb.Append((char)rune.Value);
            }
            else
            {
                sb.Append('?');
            }
        }
        return WinAnsi.GetBytes(sb.ToString());
    }

    private static Encoding CreateWinAnsi()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return Encoding.GetEncoding(1252, new EncoderReplacementFallback("?"), new DecoderReplacementFallback("?"));
    }

    private static short[] BuildTable(short[] ascii, bool bold)
    {
        var table = new short[256];
        Array.Fill(table, (short)556);
        for (int i = 0; i < 32; i++)
        {
            table[i] = 278;
        }
        Array.Copy(ascii, 0, table, 32, ascii.Length);

        for (int b = 128; b < 256; b++)
        {
            string ch = WinAnsi.GetString([(byte)b]);
            table[b] = Special(ch[0], bold) ?? BaseLetterWidth(ch, table) ?? 556;
        }
        return table;
    }

    private static short? Special(char c, bool bold) => c switch
    {
        '\u00A0' => 278,                       // espaço não separável
        '\u2013' => 556,                       // –
        '\u2014' or '\u2026' or '\u2122' or '\u2030' => 1000, // — … ™ ‰
        '\u2018' or '\u2019' or '\u201A' => (short)(bold ? 278 : 222),
        '\u201C' or '\u201D' or '\u201E' => (short)(bold ? 500 : 333),
        '\u2022' => 350,                       // •
        '\u00B0' => 400,                       // °
        '\u00A9' or '\u00AE' => 737,           // © ®
        '\u00AA' => 370, // ª
        '\u00BA' => 365, // º
        '\u00DF' => 611,                       // ß
        '\u00E6' => 889, // æ
        '\u00C6' => 1000,                      // Æ
        '\u0153' => 944, // œ
        '\u0152' => 1000,                      // Œ
        '\u00F8' => 611,                       // ø
        '\u00D8' => 778,                       // Ø
        _ => null,
    };

    /// <summary>Letra acentuada: mesma largura da letra sem acento.</summary>
    private static short? BaseLetterWidth(string ch, short[] table)
    {
        string decomposed = ch.Normalize(NormalizationForm.FormD);
        char first = decomposed[0];
        if (first is >= ' ' and <= '~' && decomposed.Length > 1 && CharUnicodeInfo.GetUnicodeCategory(decomposed[1]) == UnicodeCategory.NonSpacingMark)
        {
            return table[first];
        }
        return null;
    }
}
