using Microsoft.Win32;
using Fragmo.Settings;

namespace Fragmo.Ui;

/// <summary>Cor de destaque do app, única fonte do valor.</summary>
internal static class Palette
{
    /// <summary>Botões, ícone e destaques sobre fundo claro.</summary>
    public static readonly Color Accent = ColorTranslator.FromHtml("#A8204F");

    /// <summary>Textos e bordas sobre fundo escuro, onde o <see cref="Accent"/> fica apagado.</summary>
    public static readonly Color AccentLight = ColorTranslator.FromHtml("#D9467A");
}

/// <summary>
/// Cores da interface. O escuro é o padrão do app. <see cref="Accent"/> é o fundo de botões e itens ativos
/// (com <see cref="AccentForeground"/> por cima); <see cref="AccentText"/> é a cor de destaque para textos e
/// bordas desenhados direto sobre <see cref="Background"/>.
/// </summary>
internal sealed record Theme(Color Background, Color Surface, Color Foreground, Color Border, Color Accent, Color AccentText, Color AccentForeground)
{

    public static Theme Dark { get; } = new(
        Background: ColorTranslator.FromHtml("#1E1E22"),
        Surface: ColorTranslator.FromHtml("#2A2A30"),
        Foreground: ColorTranslator.FromHtml("#F2F2F2"),
        Border: ColorTranslator.FromHtml("#3A3A42"),
        Accent: Palette.Accent,
        AccentText: Palette.AccentLight,
        AccentForeground: Color.White);

    public static Theme Light { get; } = new(
        Background: ColorTranslator.FromHtml("#F7F7F8"),
        Surface: ColorTranslator.FromHtml("#E4E4E8"),
        Foreground: ColorTranslator.FromHtml("#1E1E22"),
        Border: ColorTranslator.FromHtml("#C8C8D0"),
        Accent: Palette.Accent,
        AccentText: Palette.Accent,
        AccentForeground: Color.White);

    public static Theme Resolve(ThemeMode mode) => mode switch
    {
        ThemeMode.Light => Light,
        ThemeMode.Dark => Dark,
        _ => WindowsUsesLightTheme() ? Light : Dark,
    };

    /// <summary>Lê a preferência de aplicativos do Windows (0 = escuro; ausente = claro).</summary>
    private static bool WindowsUsesLightTheme()
    {
        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is not int value || value != 0;
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or IOException or UnauthorizedAccessException)
        {
            return true;
        }
    }
}
