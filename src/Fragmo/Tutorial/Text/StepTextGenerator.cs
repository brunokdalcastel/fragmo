using System.Text;
using Fragmo.Settings;

namespace Fragmo.Tutorial.Text;

/// <summary>Tudo o que o gerador precisa para escrever o texto de um passo.</summary>
public sealed record StepTextInput(
    StepAction Action,
    ElementKind Kind,
    string? ElementName,
    string? WindowTitle,
    bool? IsToggledOn,
    string? Shortcut,
    bool MentionWindow,
    UiLanguage Language);

/// <summary>
/// Escreve o texto automático de um passo ("Clique no botão “Salvar”"). Lógica pura: o nome e o título
/// já chegam mascarados (RF9); aqui só se normaliza, corta e escolhe o modelo.
/// </summary>
public static class StepTextGenerator
{
    public const int MaxNameLength = 60;

    public static string Generate(StepTextInput input)
    {
        string? name = Normalize(input.ElementName);
        string? title = Normalize(input.WindowTitle);
        UiLanguage lang = input.Language;

        (StepTemplate template, string? arg, bool generic) = Choose(input, name, title);
        string text = Format(template, arg, lang);

        // "na janela “T”" só quando a janela mudou, há título e o modelo já não fala da janela/tela.
        if (input.MentionWindow && title is not null && !generic && input.Action != StepAction.Manual)
        {
            text += Format(StepTemplate.InWindowSuffix, title, lang);
        }
        return text;
    }

    /// <summary>Colapsa espaços e quebras de linha, corta em 60 caracteres com "…"; vazio vira null.</summary>
    public static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var sb = new StringBuilder(value.Length);
        bool pendingSpace = false;
        foreach (char c in value)
        {
            if (char.IsWhiteSpace(c) || char.IsControl(c))
            {
                pendingSpace = sb.Length > 0;
                continue;
            }
            if (pendingSpace)
            {
                sb.Append(' ');
                pendingSpace = false;
            }
            sb.Append(c);
        }

        if (sb.Length <= MaxNameLength)
        {
            return sb.ToString();
        }

        int cut = MaxNameLength - 1;
        if (char.IsHighSurrogate(sb[cut - 1]))
        {
            cut--; // não partir um emoji ao meio
        }
        return sb.ToString(0, cut).TrimEnd() + "…";
    }

    private static (StepTemplate Template, string? Arg, bool Generic) Choose(StepTextInput input, string? name, string? title)
    {
        switch (input.Action)
        {
            case StepAction.Click:
                return name is null
                    ? Unnamed(StepTemplate.ClickWindow, StepTemplate.ClickScreen, title)
                    : (ClickTemplate(input.Kind, input.IsToggledOn), name, false);

            case StepAction.RightClick:
                return name is null
                    ? Unnamed(StepTemplate.RightClickWindow, StepTemplate.RightClickScreen, title)
                    : (StepTemplate.RightClick, name, false);

            case StepAction.DoubleClick:
                return name is null
                    ? Unnamed(StepTemplate.DoubleClickWindow, StepTemplate.DoubleClickScreen, title)
                    : (StepTemplate.DoubleClick, name, false);

            case StepAction.Type:
                return name is null ? (StepTemplate.TypeText, null, false) : (StepTemplate.TypeIn, name, false);

            case StepAction.TypePassword:
                return name is null ? (StepTemplate.TypePassword, null, false) : (StepTemplate.TypePasswordIn, name, false);

            case StepAction.Shortcut:
                string? keys = Normalize(input.Shortcut);
                return keys is null ? (StepTemplate.Manual, null, false) : (StepTemplate.Shortcut, keys, false);

            default:
                return (StepTemplate.Manual, null, false);
        }
    }

    private static (StepTemplate, string?, bool) Unnamed(StepTemplate inWindow, StepTemplate onScreen, string? title) =>
        title is null ? (onScreen, null, true) : (inWindow, title, true);

    private static StepTemplate ClickTemplate(ElementKind kind, bool? toggledOn) => kind switch
    {
        ElementKind.Button => StepTemplate.ClickButton,
        ElementKind.MenuItem => StepTemplate.ClickMenuItem,
        ElementKind.Tab => StepTemplate.ClickTab,
        ElementKind.Link => StepTemplate.ClickLink,
        // O estado é o de antes do clique: marcada → o passo desmarca.
        ElementKind.CheckBox => toggledOn switch
        {
            true => StepTemplate.CheckBoxUncheck,
            false => StepTemplate.CheckBoxCheck,
            null => StepTemplate.CheckBoxClick,
        },
        ElementKind.RadioButton => StepTemplate.ClickRadioButton,
        ElementKind.Edit => StepTemplate.ClickEdit,
        ElementKind.ListItem or ElementKind.TreeItem => StepTemplate.ClickSelect,
        ElementKind.ComboBox => StepTemplate.ClickComboBox,
        _ => StepTemplate.ClickOther,
    };

    private static string Format(StepTemplate template, string? arg, UiLanguage lang) =>
        string.Format(Strings.StepTemplateText(template, lang), arg ?? "");
}
