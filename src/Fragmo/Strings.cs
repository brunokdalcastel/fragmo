using System.Globalization;
using Fragmo.Settings;

namespace Fragmo;

/// <summary>
/// Todos os textos visíveis do app, em português e inglês lado a lado.
/// </summary>
internal static class Strings
{
    public static UiLanguage Current { get; private set; } = UiLanguage.Portuguese;

    public static void Apply(UiLanguage setting) => Current = setting switch
    {
        UiLanguage.System => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "pt"
            ? UiLanguage.Portuguese : UiLanguage.English,
        _ => setting,
    };

    private static string T(string pt, string en) => Current == UiLanguage.Portuguese ? pt : en;

    private static string VersionText =>
        typeof(Strings).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";

    public const string AppName = "Fragmo";

    public static string TrayTooltip => T("Fragmo — captura de tela", "Fragmo — screen capture");
    public static string MenuCaptureArea => T("Capturar área", "Capture area");
    public static string MenuSettings => T("Configurações…", "Settings…");
    public static string MenuOpenFolder => T("Abrir pasta de capturas", "Open captures folder");
    public static string MenuAbout => T("Sobre", "About");
    public static string MenuExit => T("Sair", "Exit");

    public static string AlreadyRunning => T("O Fragmo já está aberto na bandeja.",
                                             "Fragmo is already running in the tray.");

    public static string AboutText => T(
        $"{AppName} {VersionText}\n\nCaptura de tela 100% local, sem telemetria.",
        $"{AppName} {VersionText}\n\nScreen capture, 100% local, no telemetry.");

    public static string PrintScreenTaken => T(
        "A tecla Print Screen está em uso pelo Windows. Usando Ctrl+Shift+S. Para liberar: Configurações › Acessibilidade › Teclado › desative \"Usar a tecla Print Screen para abrir a captura de tela\".",
        "The Print Screen key is taken by Windows. Using Ctrl+Shift+S. To free it: Settings › Accessibility › Keyboard › turn off \"Use the Print screen key to open screen capture\".");

    public static string HotkeyFailed => T(
        "Não foi possível registrar o atalho de captura. Use \"Capturar área\" no menu da bandeja.",
        "Could not register the capture hotkey. Use \"Capture area\" in the tray menu.");

    public static string Copied => T("Imagem copiada", "Image copied");
    public static string SavedTo => T("Salvo em {0}", "Saved to {0}");
    public static string CaptureError => T("Falha na captura: {0}", "Capture failed: {0}");

    // Igual nos dois idiomas, por isso é constante e não passa por T().
    public const string SaveFilter = "PNG|*.png|JPEG|*.jpg|Bitmap|*.bmp";

    // Editor: dicas das ferramentas e ações (com o atalho).
    public static string ToolPen => T("Caneta (P)", "Pen (P)");
    public static string ToolLine => T("Linha (L)", "Line (L)");
    public static string ToolArrow => T("Seta (A)", "Arrow (A)");
    public static string ToolRectangle => T("Retângulo (R)", "Rectangle (R)");
    public static string ToolText => T("Texto (T)", "Text (T)");
    public static string ToolColor => T("Cor", "Color");
    public static string ToolThickness => T("Espessura", "Thickness");
    public static string MoreColors => T("Mais cores…", "More colors…");
    public static string ActionUndo => T("Desfazer (Ctrl+Z)", "Undo (Ctrl+Z)");
    public static string ActionRedo => T("Refazer (Ctrl+Y)", "Redo (Ctrl+Y)");
    public static string ActionCopy => T("Copiar (Ctrl+C)", "Copy (Ctrl+C)");
    public static string ActionSave => T("Salvar (Ctrl+S)", "Save (Ctrl+S)");
    public static string ActionClose => T("Fechar (Esc)", "Close (Esc)");

    public static string UnexpectedError => T(
        "Ocorreu um erro inesperado. Detalhes gravados em {0}.\n\n{1}",
        "An unexpected error occurred. Details were written to {0}.\n\n{1}");
}
