using System.Text.RegularExpressions;

namespace Fragmo.Ocr;

public enum SensitiveKind { Email, Cnpj, Cpf, IPv4, Phone }

/// <summary>Um dado sensível encontrado: o tipo, o texto e onde ele está na imagem.</summary>
public sealed record SensitiveMatch(SensitiveKind Kind, string Text, Rectangle Bounds);

/// <summary>
/// Acha dados sensíveis no texto reconhecido pelo OCR e devolve os retângulos a desfocar:
/// CPF e CNPJ (com dígitos verificadores válidos), e-mail, IPv4 e telefone brasileiro.
/// Lógica pura: recebe as linhas e palavras com seus retângulos, sem depender do OCR em si.
/// </summary>
public static partial class SensitiveDataDetector
{
    // Ordem importa: quem vem antes "reserva" o trecho (um CNPJ não vira telefone, um IP não vira telefone).
    private static readonly (SensitiveKind Kind, Regex Pattern, Func<string, bool> Accept)[] Rules =
    [
        (SensitiveKind.Email, EmailPattern(), _ => true),
        (SensitiveKind.Cnpj, CnpjPattern(), text => IsValidCnpj(Digits(text))),
        (SensitiveKind.Cpf, CpfPattern(), text => IsValidCpf(Digits(text))),
        (SensitiveKind.IPv4, IPv4Pattern(), _ => true),
        (SensitiveKind.Phone, PhonePattern(), _ => true),
    ];

    /// <summary>Os retângulos a desfocar, um por dado encontrado.</summary>
    public static IReadOnlyList<Rectangle> Detect(IEnumerable<OcrLineBox> lines) =>
        Find(lines).Select(m => m.Bounds).ToList();

    public static IReadOnlyList<SensitiveMatch> Find(IEnumerable<OcrLineBox> lines)
    {
        var found = new List<SensitiveMatch>();
        foreach (OcrLineBox line in lines)
        {
            FindInLine(line, found);
        }
        return found;
    }

    private sealed record Span(int Start, int End, OcrWordBox Word);

    private static void FindInLine(OcrLineBox line, List<SensitiveMatch> found)
    {
        // Junta as palavras com um espaço, guardando onde cada uma começa no texto da linha.
        var spans = new List<Span>();
        var text = new System.Text.StringBuilder();
        foreach (OcrWordBox word in line.Words)
        {
            string w = word.Text.Trim();
            if (w.Length == 0)
            {
                continue;
            }
            if (text.Length > 0)
            {
                text.Append(' ');
            }
            spans.Add(new Span(text.Length, text.Length + w.Length, word with { Text = w }));
            text.Append(w);
        }

        string lineText = text.ToString();
        var taken = new List<(int Start, int End)>();
        foreach ((SensitiveKind kind, Regex pattern, Func<string, bool> accept) in Rules)
        {
            foreach (Match m in pattern.Matches(lineText))
            {
                int start = m.Index, end = m.Index + m.Length;
                if (taken.Any(t => start < t.End && t.Start < end) || !accept(m.Value))
                {
                    continue;
                }
                Rectangle bounds = BoundsOf(spans, start, end);
                if (bounds.IsEmpty)
                {
                    continue;
                }
                taken.Add((start, end));
                found.Add(new SensitiveMatch(kind, m.Value, bounds));
            }
        }
    }

    /// <summary>
    /// Retângulo do trecho [start, end) do texto da linha. Quando o trecho pega só parte de uma palavra
    /// (ex.: "CPF:529.982.247-25"), usa a fração proporcional da largura dela.
    /// </summary>
    private static Rectangle BoundsOf(List<Span> spans, int start, int end)
    {
        Rectangle result = Rectangle.Empty;
        foreach (Span s in spans)
        {
            int from = Math.Max(start, s.Start), to = Math.Min(end, s.End);
            if (from >= to)
            {
                continue;
            }
            Rectangle b = s.Word.Bounds;
            int length = s.End - s.Start;
            int left = b.Left + (int)Math.Floor((double)b.Width * (from - s.Start) / length);
            int right = b.Left + (int)Math.Ceiling((double)b.Width * (to - s.Start) / length);
            var part = Rectangle.FromLTRB(left, b.Top, right, b.Bottom);
            result = result.IsEmpty ? part : Rectangle.Union(result, part);
        }
        return result;
    }

    private static string Digits(string text) => new(text.Where(char.IsAsciiDigit).ToArray());

    /// <summary>CPF com 11 dígitos e os dois verificadores certos (todos iguais não vale).</summary>
    public static bool IsValidCpf(string digits)
    {
        if (digits.Length != 11 || !digits.All(char.IsAsciiDigit) || digits.Distinct().Count() == 1)
        {
            return false;
        }
        return CheckDigit(digits, 9, 10) == digits[9] - '0' && CheckDigit(digits, 10, 11) == digits[10] - '0';

        static int CheckDigit(string d, int count, int firstWeight)
        {
            int sum = 0;
            for (int i = 0; i < count; i++)
            {
                sum += (d[i] - '0') * (firstWeight - i);
            }
            int rest = sum % 11;
            return rest < 2 ? 0 : 11 - rest;
        }
    }

    /// <summary>CNPJ com 14 dígitos e os dois verificadores certos (todos iguais não vale).</summary>
    public static bool IsValidCnpj(string digits)
    {
        if (digits.Length != 14 || !digits.All(char.IsAsciiDigit) || digits.Distinct().Count() == 1)
        {
            return false;
        }
        int[] first = [5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2];
        int[] second = [6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2];
        return CheckDigit(digits, first) == digits[12] - '0' && CheckDigit(digits, second) == digits[13] - '0';

        static int CheckDigit(string d, int[] weights)
        {
            int sum = 0;
            for (int i = 0; i < weights.Length; i++)
            {
                sum += (d[i] - '0') * weights[i];
            }
            int rest = sum % 11;
            return rest < 2 ? 0 : 11 - rest;
        }
    }

    [GeneratedRegex(@"[A-Za-z0-9._%+\-]+@[A-Za-z0-9\-]+(?:\.[A-Za-z0-9\-]+)*\.[A-Za-z]{2,}")]
    private static partial Regex EmailPattern();

    // 00.000.000/0000-00, com ou sem pontuação.
    [GeneratedRegex(@"(?<!\d)\d{2}\.?\d{3}\.?\d{3}/?\d{4}-?\d{2}(?!\d)")]
    private static partial Regex CnpjPattern();

    // 000.000.000-00, com ou sem pontuação.
    [GeneratedRegex(@"(?<!\d)\d{3}\.?\d{3}\.?\d{3}-?\d{2}(?!\d)")]
    private static partial Regex CpfPattern();

    [GeneratedRegex(@"(?<![\d.])(?:(?:25[0-5]|2[0-4]\d|1\d\d|[1-9]?\d)\.){3}(?:25[0-5]|2[0-4]\d|1\d\d|[1-9]?\d)(?![\d.]*\d)")]
    private static partial Regex IPv4Pattern();

    // Telefone brasileiro: +55 opcional, DDD opcional (com ou sem parênteses), celular 9XXXX-XXXX
    // ou fixo [2-5]XXX-XXXX, com ou sem hífen/espaço.
    [GeneratedRegex(@"(?<!\d)(?:\+?55[\s-]?)?(?:\(\s?[1-9]{2}\s?\)\s?|[1-9]{2}[\s-]?)?(?:9\s?\d{4}|[2-5]\d{3})[\s-]?\d{4}(?!\d)")]
    private static partial Regex PhonePattern();
}
