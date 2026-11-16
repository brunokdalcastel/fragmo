using Fragmo.Settings;
using Fragmo.Tutorial;
using Fragmo.Tutorial.Text;

namespace Fragmo.Tests;

public sealed class StepTextGeneratorTests
{
    private static string Gen(StepAction action, ElementKind kind, string? name, UiLanguage lang,
        bool? toggled = null, string? title = null, bool mention = false, string? shortcut = null) =>
        StepTextGenerator.Generate(new StepTextInput(action, kind, name, title, toggled, shortcut, mention, lang));

    [Theory]
    [InlineData(ElementKind.Button, "Clique no botão “Salvar”", "Click the “Salvar” button")]
    [InlineData(ElementKind.MenuItem, "Clique no menu “Salvar”", "Click the “Salvar” menu")]
    [InlineData(ElementKind.Tab, "Clique na aba “Salvar”", "Click the “Salvar” tab")]
    [InlineData(ElementKind.Link, "Clique no link “Salvar”", "Click the “Salvar” link")]
    [InlineData(ElementKind.RadioButton, "Selecione a opção “Salvar”", "Select the “Salvar” option")]
    [InlineData(ElementKind.Edit, "Clique no campo “Salvar”", "Click the “Salvar” field")]
    [InlineData(ElementKind.ListItem, "Selecione “Salvar”", "Select “Salvar”")]
    [InlineData(ElementKind.TreeItem, "Selecione “Salvar”", "Select “Salvar”")]
    [InlineData(ElementKind.ComboBox, "Abra a lista “Salvar”", "Open the “Salvar” list")]
    [InlineData(ElementKind.Other, "Clique em “Salvar”", "Click “Salvar”")]
    public void Click_ByKind(ElementKind kind, string pt, string en)
    {
        Assert.Equal(pt, Gen(StepAction.Click, kind, "Salvar", UiLanguage.Portuguese));
        Assert.Equal(en, Gen(StepAction.Click, kind, "Salvar", UiLanguage.English));
    }

    [Theory]
    [InlineData(false, "Marque a caixa “Lembrar”", "Check the “Lembrar” box")]
    [InlineData(true, "Desmarque a caixa “Lembrar”", "Uncheck the “Lembrar” box")]
    [InlineData(null, "Clique na caixa “Lembrar”", "Click the “Lembrar” box")]
    public void Click_CheckBox_UsesStateBeforeClick(bool? toggled, string pt, string en)
    {
        Assert.Equal(pt, Gen(StepAction.Click, ElementKind.CheckBox, "Lembrar", UiLanguage.Portuguese, toggled));
        Assert.Equal(en, Gen(StepAction.Click, ElementKind.CheckBox, "Lembrar", UiLanguage.English, toggled));
    }

    [Fact]
    public void Click_Unnamed_UsesWindowOrScreen()
    {
        Assert.Equal("Clique na janela “Bloco de Notas”",
            Gen(StepAction.Click, ElementKind.Other, null, UiLanguage.Portuguese, title: "Bloco de Notas"));
        Assert.Equal("Click in the “Notepad” window",
            Gen(StepAction.Click, ElementKind.Other, "  ", UiLanguage.English, title: "Notepad"));
        Assert.Equal("Clique na tela", Gen(StepAction.Click, ElementKind.Button, null, UiLanguage.Portuguese));
        Assert.Equal("Click the screen", Gen(StepAction.Click, ElementKind.Button, null, UiLanguage.English));
    }

    [Fact]
    public void Unnamed_DoesNotRepeatWindow()
    {
        Assert.Equal("Clique na janela “Bloco de Notas”",
            Gen(StepAction.Click, ElementKind.Other, null, UiLanguage.Portuguese, title: "Bloco de Notas", mention: true));
    }

    [Fact]
    public void RightAndDoubleClick()
    {
        Assert.Equal("Clique com o botão direito em “Arquivo”", Gen(StepAction.RightClick, ElementKind.ListItem, "Arquivo", UiLanguage.Portuguese));
        Assert.Equal("Right-click “Arquivo”", Gen(StepAction.RightClick, ElementKind.ListItem, "Arquivo", UiLanguage.English));
        Assert.Equal("Clique duas vezes em “Fotos”", Gen(StepAction.DoubleClick, ElementKind.ListItem, "Fotos", UiLanguage.Portuguese));
        Assert.Equal("Double-click “Fotos”", Gen(StepAction.DoubleClick, ElementKind.ListItem, "Fotos", UiLanguage.English));
        Assert.Equal("Clique duas vezes na tela", Gen(StepAction.DoubleClick, ElementKind.Other, null, UiLanguage.Portuguese));
        Assert.Equal("Right-click in the “Explorer” window",
            Gen(StepAction.RightClick, ElementKind.Other, null, UiLanguage.English, title: "Explorer"));
    }

    [Fact]
    public void Typing()
    {
        Assert.Equal("Digite no campo “Nome”", Gen(StepAction.Type, ElementKind.Edit, "Nome", UiLanguage.Portuguese));
        Assert.Equal("Type in the “Nome” field", Gen(StepAction.Type, ElementKind.Edit, "Nome", UiLanguage.English));
        Assert.Equal("Digite o texto", Gen(StepAction.Type, ElementKind.Edit, null, UiLanguage.Portuguese));
        Assert.Equal("Type the text", Gen(StepAction.Type, ElementKind.Edit, null, UiLanguage.English));
        Assert.Equal("Digite a senha no campo “Senha”", Gen(StepAction.TypePassword, ElementKind.Edit, "Senha", UiLanguage.Portuguese));
        Assert.Equal("Type the password in the “Senha” field", Gen(StepAction.TypePassword, ElementKind.Edit, "Senha", UiLanguage.English));
        Assert.Equal("Digite a senha", Gen(StepAction.TypePassword, ElementKind.Edit, null, UiLanguage.Portuguese));
        Assert.Equal("Type the password", Gen(StepAction.TypePassword, ElementKind.Edit, null, UiLanguage.English));
    }

    [Fact]
    public void ShortcutAndManual()
    {
        Assert.Equal("Pressione Ctrl+C", Gen(StepAction.Shortcut, ElementKind.Other, null, UiLanguage.Portuguese, shortcut: "Ctrl+C"));
        Assert.Equal("Press Ctrl+C", Gen(StepAction.Shortcut, ElementKind.Other, null, UiLanguage.English, shortcut: "Ctrl+C"));
        Assert.Equal("Observe a tela", Gen(StepAction.Manual, ElementKind.Other, null, UiLanguage.Portuguese));
        Assert.Equal("Look at the screen", Gen(StepAction.Manual, ElementKind.Other, null, UiLanguage.English));
    }

    [Fact]
    public void Manual_NeverMentionsWindow()
    {
        Assert.Equal("Observe a tela",
            Gen(StepAction.Manual, ElementKind.Other, null, UiLanguage.Portuguese, title: "Word", mention: true));
    }

    [Fact]
    public void MentionWindow_AppendsTitle()
    {
        Assert.Equal("Clique no botão “OK” na janela “Propriedades”",
            Gen(StepAction.Click, ElementKind.Button, "OK", UiLanguage.Portuguese, title: "Propriedades", mention: true));
        Assert.Equal("Press Ctrl+S in the “Notepad” window",
            Gen(StepAction.Shortcut, ElementKind.Other, null, UiLanguage.English, title: "Notepad", mention: true, shortcut: "Ctrl+S"));
        Assert.Equal("Clique no botão “OK”",
            Gen(StepAction.Click, ElementKind.Button, "OK", UiLanguage.Portuguese, title: "Propriedades", mention: false));
        Assert.Equal("Clique no botão “OK”",
            Gen(StepAction.Click, ElementKind.Button, "OK", UiLanguage.Portuguese, title: null, mention: true));
    }

    [Fact]
    public void Normalize_CollapsesWhitespace()
    {
        Assert.Equal("Salvar como", StepTextGenerator.Normalize("  Salvar\r\n\t como  "));
        Assert.Null(StepTextGenerator.Normalize(" \n "));
        Assert.Null(StepTextGenerator.Normalize(null));
    }

    [Fact]
    public void Normalize_CutsAt60WithEllipsis()
    {
        string longName = new('a', 100);
        string result = StepTextGenerator.Normalize(longName)!;
        Assert.Equal(StepTextGenerator.MaxNameLength, result.Length);
        Assert.EndsWith("…", result);

        string exact = new('b', 60);
        Assert.Equal(exact, StepTextGenerator.Normalize(exact));
    }

    [Fact]
    public void Normalize_DoesNotSplitSurrogatePair()
    {
        string name = new string('a', 57) + "😀" + new string('b', 10);
        string result = StepTextGenerator.Normalize(name)!;
        Assert.EndsWith("…", result);
        Assert.False(char.IsHighSurrogate(result[^2]));
    }

    [Fact]
    public void AllStepTemplates_HavePortugueseAndEnglish()
    {
        foreach (StepTemplate t in Enum.GetValues<StepTemplate>())
        {
            string pt = Strings.StepTemplateText(t, UiLanguage.Portuguese);
            string en = Strings.StepTemplateText(t, UiLanguage.English);
            Assert.False(string.IsNullOrWhiteSpace(pt), $"{t} vazio em pt");
            Assert.False(string.IsNullOrWhiteSpace(en), $"{t} empty in en");
            Assert.NotEqual(pt, en);
        }
    }
}
