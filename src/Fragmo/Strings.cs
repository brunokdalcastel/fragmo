using System.Globalization;
using Fragmo.Hotkeys;
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
    public static string MenuFullScreen => T("Tela cheia (salvar)", "Full screen (save)");
    public static string MenuWindow => T("Janela ativa", "Active window");
    public static string MenuRepeatLast => T("Repetir última área", "Repeat last area");
    public static string MenuClosePins => T("Fechar imagens fixadas", "Close pinned images");
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

    public static string HotkeysInUse => T(
        "Atalhos em uso por outro programa: {0}",
        "Hotkeys in use by another program: {0}");

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
    public static string ToolMarker => T("Marcador (M)", "Highlighter (M)");
    public static string ToolBlur => T("Desfoque (B)", "Blur (B)");
    public static string ToolStep => T("Passos numerados (N)", "Numbered steps (N)");
    public static string ToolColor => T("Cor", "Color");
    public static string ToolThickness => T("Espessura", "Thickness");
    public static string MoreColors => T("Mais cores…", "More colors…");
    public static string ActionUndo => T("Desfazer (Ctrl+Z)", "Undo (Ctrl+Z)");
    public static string ActionRedo => T("Refazer (Ctrl+Y)", "Redo (Ctrl+Y)");
    public static string ActionCopy => T("Copiar (Ctrl+C)", "Copy (Ctrl+C)");
    public static string ActionSave => T("Salvar (Ctrl+S)", "Save (Ctrl+S)");
    public static string ActionClose => T("Fechar (Esc)", "Close (Esc)");
    public static string ActionPin => T("Fixar na tela (Ctrl+T)", "Pin to screen (Ctrl+T)");
    public static string ActionPrint => T("Imprimir (Ctrl+P)", "Print (Ctrl+P)");

    // Janela fixada: menu de contexto.
    public static string PinCopy => T("Copiar", "Copy");
    public static string PinSave => T("Salvar…", "Save…");
    public static string PinClose => T("Fechar", "Close");
    public const string PinZoomReset = "Zoom 100%";

    // Configurações.
    public static string SettingsTitle => T("Configurações do Fragmo", "Fragmo settings");
    public static string SectionHotkeys => T("Atalhos", "Hotkeys");
    public static string SectionSave => T("Salvar", "Saving");
    public static string SectionGeneral => T("Geral", "General");
    public static string HotkeyHint => T("Clique na caixa e pressione o atalho. Backspace limpa.",
                                         "Click a box and press the hotkey. Backspace clears it.");
    public static string HotkeyTaken => T("em uso por outro programa", "in use by another program");
    public static string LabelFolder => T("Pasta", "Folder");
    public static string BrowseFolder => T("Escolher…", "Browse…");
    public static string FolderDialogDescription => T("Pasta onde as capturas são salvas", "Folder where captures are saved");
    public static string LabelFormat => T("Formato padrão", "Default format");
    public static string LabelJpegQuality => T("Qualidade JPEG", "JPEG quality");
    public static string LabelPrefix => T("Prefixo do nome", "File name prefix");
    public static string FileNamePreview => T("Exemplo: {0}", "Example: {0}");
    public static string AskWhereToSave => T("Perguntar onde salvar", "Ask where to save");
    public static string StartWithWindows => T("Iniciar com o Windows", "Start with Windows");
    public static string ShowMagnifier => T("Mostrar lupa", "Show magnifier");
    public static string LabelTheme => T("Tema", "Theme");
    public static string ThemeDark => T("Escuro", "Dark");
    public static string ThemeLight => T("Claro", "Light");
    public static string FollowWindows => T("Seguir o Windows", "Follow Windows");
    public static string LabelLanguage => T("Idioma", "Language");
    public static string ButtonCancel => T("Cancelar", "Cancel");
    public static string ButtonDefaults => T("Restaurar padrões", "Restore defaults");
    public const string ButtonOk = "OK";
    public static string SettingsErrors => T("Corrija antes de salvar:\n\n{0}", "Please fix before saving:\n\n{0}");
    public static string StartupError => T("Não foi possível alterar o início com o Windows: {0}",
                                           "Could not change start with Windows: {0}");
    public static string HotkeyInvalid => T("\"{0}\": atalho vazio ou inválido.", "\"{0}\": empty or invalid hotkey.");
    public static string HotkeyDuplicate => T("O atalho {0} está repetido em \"{1}\" e \"{2}\".",
                                              "The hotkey {0} is used by both \"{1}\" and \"{2}\".");
    public static string FolderEmpty => T("Escolha uma pasta para salvar as capturas.", "Choose a folder to save captures in.");

    public static string HotkeyLabel(HotkeyAction action) => action switch
    {
        HotkeyAction.CaptureFullScreen => MenuFullScreen,
        HotkeyAction.CaptureWindow => MenuWindow,
        HotkeyAction.RepeatLastArea => MenuRepeatLast,
        _ => MenuCaptureArea,
    };

    public static string UnexpectedError => T(
        "Ocorreu um erro inesperado. Detalhes gravados em {0}.\n\n{1}",
        "An unexpected error occurred. Details were written to {0}.\n\n{1}");
}
