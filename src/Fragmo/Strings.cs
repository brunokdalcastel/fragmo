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
    public static string ToolRedact => T("Tarja opaca (H)", "Redact (H)");
    public static string ToolAutoRedact => T("Ocultar dados sensíveis (X)", "Hide sensitive data (X)");
    public static string ToolColor => T("Cor", "Color");
    public static string ToolThickness => T("Espessura", "Thickness");
    public static string MoreColors => T("Mais cores…", "More colors…");
    public static string ActionUndo => T("Desfazer (Ctrl+Z)", "Undo (Ctrl+Z)");
    public static string ActionRedo => T("Refazer (Ctrl+Y)", "Redo (Ctrl+Y)");
    public static string ActionCopy => T("Copiar (Ctrl+C)", "Copy (Ctrl+C)");
    public static string ActionSave => T("Salvar (Ctrl+S)", "Save (Ctrl+S)");
    public static string ActionClose => T("Fechar (Esc)", "Close (Esc)");
    public static string ActionDone => T("Concluir (Enter)", "Done (Enter)");
    public static string ActionPin => T("Fixar na tela (Ctrl+T)", "Pin to screen (Ctrl+T)");
    public static string ActionPrint => T("Imprimir (Ctrl+P)", "Print (Ctrl+P)");
    public static string ActionCopyText => T("Copiar texto (O)", "Copy text (O)");

    // Copiar texto (OCR).
    public static string OcrOneLineCopied => T("1 linha copiada", "1 line copied");
    public static string OcrLinesCopied => T("{0} linhas copiadas", "{0} lines copied");
    public static string OcrNoText => T("Nenhum texto encontrado na seleção", "No text found in the selection");
    public static string OcrFailed => T("Falha ao reconhecer o texto: {0}", "Text recognition failed: {0}");
    public static string OcrNoLanguage => T(
        "O Windows não tem nenhum idioma de reconhecimento de texto (OCR) instalado.\n\n" +
        "Para instalar: Configurações › Hora e idioma › Idioma e região › clique em \"…\" ao lado do idioma › " +
        "Opções de idioma › em \"Reconhecimento óptico de caracteres\", clique em Baixar.\n\n" +
        "O reconhecimento roda no próprio computador; nada é enviado para a internet.",
        "Windows has no text recognition (OCR) language installed.\n\n" +
        "To install one: Settings › Time & language › Language & region › click \"…\" next to the language › " +
        "Language options › under \"Optical character recognition\", click Download.\n\n" +
        "Recognition runs on your computer; nothing is sent to the internet.");

    // Ocultar dados sensíveis (tarja automática).
    public static string AutoRedactNone => T("Nenhum dado sensível encontrado", "No sensitive data found");
    public static string AutoRedactOne => T("1 item ocultado com tarja", "1 item redacted");
    public static string AutoRedactMany => T("{0} itens ocultados com tarja", "{0} items redacted");
    public static string AutoRedactOnCapture => T("Ocultar dados sensíveis automaticamente ao capturar",
                                                  "Automatically hide sensitive data when capturing");

    public static string ItemsRedacted(int count) =>
        count == 1 ? AutoRedactOne : string.Format(AutoRedactMany, count);

    public static string LinesCopied(int count) =>
        count == 1 ? OcrOneLineCopied : string.Format(OcrLinesCopied, count);

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
        HotkeyAction.ToggleTutorial => HotkeyToggleTutorial,
        HotkeyAction.TutorialManualStep => HotkeyTutorialManualStep,
        _ => MenuCaptureArea,
    };

    public static string UnexpectedError => T(
        "Ocorreu um erro inesperado. Detalhes gravados em {0}.\n\n{1}",
        "An unexpected error occurred. Details were written to {0}.\n\n{1}");

    // ---- Modo tutorial ----
    public static string RecordingStatus => T("● Gravando · {0} passos", "● Recording · {0} steps");
    public static string RecordingPausedStatus => T("❚❚ Pausado · {0} passos", "❚❚ Paused · {0} steps");
    public static string RecordingProcessing => T("Processando…", "Processing…");
    public static string RecordingStop => T("Parar", "Stop");
    public static string RecordingPause => T("Pausar", "Pause");
    public static string RecordingResume => T("Continuar", "Resume");
    public static string RecordingDiscard => T("Descartar", "Discard");
    public static string RecordingDiscardConfirm => T(
        "Descartar a gravação e apagar os {0} passos gravados?",
        "Discard the recording and delete the {0} recorded steps?");
    public static string RecordingDragHint => T("Arraste para mover", "Drag to move");
    public static string MenuRecordTutorial => T("Gravar tutorial", "Record tutorial");
    public static string MenuStopRecording => T("Parar gravação", "Stop recording");
    public static string HotkeyToggleTutorial => T("Gravar/parar tutorial", "Record/stop tutorial");
    public static string HotkeyTutorialManualStep => T("Passo manual (gravando)", "Manual step (while recording)");
    public static string RedactTutorials => T("Ocultar dados sensíveis nos tutoriais", "Hide sensitive data in tutorials");
    public static string StopRecordingFirst => T("Pare a gravação do tutorial primeiro.", "Stop the tutorial recording first.");
    public static string TutorialDefaultTitle => T("Tutorial de {0}", "Tutorial from {0}");
    public static string TutorialEmpty => T("Nenhum passo gravado; a gravação foi descartada.", "No steps recorded; the recording was discarded.");
    public static string TutorialDiscarded => T("Gravação descartada.", "Recording discarded.");
    public static string TutorialHooksFailed => T(
        "Não foi possível começar a gravar: o Windows recusou a escuta de mouse e teclado.",
        "Could not start recording: Windows refused the mouse and keyboard hooks.");
    public static string TutorialStartFailed => T("Não foi possível começar a gravar: {0}", "Could not start recording: {0}");
    public static string TutorialRecordingFailed => T(
        "A gravação parou: não foi possível salvar o passo ({0}).",
        "Recording stopped: the step could not be saved ({0}).");
    public static string TutorialUnfinished => T(
        "Há uma gravação de tutorial que não foi finalizada ({0}, {1} passos).\n\n" +
        "Sim: recuperar a gravação\nNão: descartar\nCancelar: perguntar na próxima vez",
        "There is a tutorial recording that was not finished ({0}, {1} steps).\n\n" +
        "Yes: recover the recording\nNo: discard it\nCancel: ask me next time");
    public static string TutorialUnfinishedTray => T(
        "Há gravações de tutorial não finalizadas. Abra o Fragmo pelo menu Iniciar para recuperá-las.",
        "There are unfinished tutorial recordings. Open Fragmo from the Start menu to recover them.");

    // ----- Editor de tutorial -----
    public static string MenuOpenTutorial => T("Abrir tutorial…", "Open tutorial…");
    public static string TutorialEditorTitle => T("{0} — Editor de tutorial", "{0} — Tutorial editor");
    public static string TutorialTitleLabel => T("Título:", "Title:");
    public static string TutorialSave => T("Salvar", "Save");
    public static string TutorialExport => T("Exportar ▾", "Export ▾");
    public static string TutorialUndo => T("Desfazer", "Undo");
    public static string TutorialExportHtml => T("Página HTML…", "HTML page…");
    public static string TutorialExportMarkdown => T("Markdown (com imagens)…", "Markdown (with images)…");
    public static string TutorialExportPdf => T("Documento PDF…", "PDF document…");
    public static string TutorialExportWord => T("Documento do Word…", "Word document…");
    public static string TutorialFilter => T("Tutorial do Fragmo (*.fragmotut)|*.fragmotut", "Fragmo tutorial (*.fragmotut)|*.fragmotut");
    public static string TutorialFilterHtml => T("Página HTML (*.html)|*.html", "HTML page (*.html)|*.html");
    public static string TutorialFilterMarkdown => T("Markdown (*.md)|*.md", "Markdown file (*.md)|*.md");
    public static string TutorialFilterPdf => T("Documento PDF (*.pdf)|*.pdf", "PDF document (*.pdf)|*.pdf");
    public static string TutorialFilterWord => T("Documento do Word (*.docx)|*.docx", "Word document (*.docx)|*.docx");
    public static string TutorialHighlight => T("Destacar elemento", "Highlight element");
    public static string TutorialMoveUp => T("Subir", "Move up");
    public static string TutorialMoveDown => T("Descer", "Move down");
    public static string TutorialDelete => T("Excluir", "Delete");
    public static string TutorialInsertNote => T("Inserir nota", "Insert note");
    public static string TutorialEditImage => T("Editar imagem", "Edit image");
    public static string TutorialRestoreImage => T("Restaurar original", "Restore original");
    public static string TutorialNewNote => T("Nova nota", "New note");
    public static string TutorialNoteLabel => T("Nota", "Note");
    public static string TutorialImageMissing => T("Imagem indisponível", "Image unavailable");
    public static string TutorialUnsaved => T(
        "O tutorial \"{0}\" tem alterações que não foram salvas.\n\nSim: salvar\nNão: descartar\nCancelar: continuar editando",
        "The tutorial \"{0}\" has unsaved changes.\n\nYes: save\nNo: discard\nCancel: keep editing");
    public static string TutorialSaved => T("Tutorial salvo em {0}", "Tutorial saved to {0}");
    public static string TutorialExported => T("Tutorial exportado para {0}", "Tutorial exported to {0}");
    public static string TutorialSaveFailed => T("Não foi possível salvar o tutorial: {0}", "Could not save the tutorial: {0}");
    public static string TutorialExportFailed => T("Não foi possível exportar o tutorial: {0}", "Could not export the tutorial: {0}");
    public static string TutorialEditFailed => T("Não foi possível guardar a imagem editada: {0}", "Could not store the edited image: {0}");
    public static string TutorialOpenFailed => T("Não foi possível abrir o tutorial:\n{0}", "Could not open the tutorial:\n{0}");

    /// <summary>
    /// Modelo do texto de um passo (formato do <see cref="string.Format(string, object?)"/>, {0} = nome ou título).
    /// Recebe o idioma em vez de usar <see cref="Current"/> para o gerador ser testável sem estado global.
    /// </summary>
    public static string StepTemplateText(StepTemplate template, UiLanguage language)
    {
        (string pt, string en) = StepTemplates[template];
        return language == UiLanguage.English ? en : pt;
    }

    internal static IReadOnlyCollection<StepTemplate> AllStepTemplates => StepTemplates.Keys;

    private static readonly Dictionary<StepTemplate, (string Pt, string En)> StepTemplates = new()
    {
        [StepTemplate.ClickButton] = ("Clique no botão “{0}”", "Click the “{0}” button"),
        [StepTemplate.ClickMenuItem] = ("Clique no menu “{0}”", "Click the “{0}” menu"),
        [StepTemplate.ClickTab] = ("Clique na aba “{0}”", "Click the “{0}” tab"),
        [StepTemplate.ClickLink] = ("Clique no link “{0}”", "Click the “{0}” link"),
        [StepTemplate.CheckBoxCheck] = ("Marque a caixa “{0}”", "Check the “{0}” box"),
        [StepTemplate.CheckBoxUncheck] = ("Desmarque a caixa “{0}”", "Uncheck the “{0}” box"),
        [StepTemplate.CheckBoxClick] = ("Clique na caixa “{0}”", "Click the “{0}” box"),
        [StepTemplate.ClickRadioButton] = ("Selecione a opção “{0}”", "Select the “{0}” option"),
        [StepTemplate.ClickEdit] = ("Clique no campo “{0}”", "Click the “{0}” field"),
        [StepTemplate.ClickSelect] = ("Selecione “{0}”", "Select “{0}”"),
        [StepTemplate.ClickComboBox] = ("Abra a lista “{0}”", "Open the “{0}” list"),
        [StepTemplate.ClickOther] = ("Clique em “{0}”", "Click “{0}”"),
        [StepTemplate.ClickWindow] = ("Clique na janela “{0}”", "Click in the “{0}” window"),
        [StepTemplate.ClickScreen] = ("Clique na tela", "Click the screen"),
        [StepTemplate.RightClick] = ("Clique com o botão direito em “{0}”", "Right-click “{0}”"),
        [StepTemplate.RightClickWindow] = ("Clique com o botão direito na janela “{0}”", "Right-click in the “{0}” window"),
        [StepTemplate.RightClickScreen] = ("Clique com o botão direito na tela", "Right-click the screen"),
        [StepTemplate.DoubleClick] = ("Clique duas vezes em “{0}”", "Double-click “{0}”"),
        [StepTemplate.DoubleClickWindow] = ("Clique duas vezes na janela “{0}”", "Double-click in the “{0}” window"),
        [StepTemplate.DoubleClickScreen] = ("Clique duas vezes na tela", "Double-click the screen"),
        [StepTemplate.TypeIn] = ("Digite no campo “{0}”", "Type in the “{0}” field"),
        [StepTemplate.TypeText] = ("Digite o texto", "Type the text"),
        [StepTemplate.TypePasswordIn] = ("Digite a senha no campo “{0}”", "Type the password in the “{0}” field"),
        [StepTemplate.TypePassword] = ("Digite a senha", "Type the password"),
        [StepTemplate.Shortcut] = ("Pressione {0}", "Press {0}"),
        [StepTemplate.Manual] = ("Observe a tela", "Look at the screen"),
        [StepTemplate.InWindowSuffix] = (" na janela “{0}”", " in the “{0}” window"),
    };
}

/// <summary>Modelos de texto de passo do modo tutorial (ver <see cref="Strings.StepTemplateText"/>).</summary>
internal enum StepTemplate
{
    ClickButton, ClickMenuItem, ClickTab, ClickLink, CheckBoxCheck, CheckBoxUncheck, CheckBoxClick,
    ClickRadioButton, ClickEdit, ClickSelect, ClickComboBox, ClickOther, ClickWindow, ClickScreen,
    RightClick, RightClickWindow, RightClickScreen, DoubleClick, DoubleClickWindow, DoubleClickScreen,
    TypeIn, TypeText, TypePasswordIn, TypePassword, Shortcut, Manual, InWindowSuffix,
}
