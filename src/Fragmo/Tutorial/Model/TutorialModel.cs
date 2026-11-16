namespace Fragmo.Tutorial;

public enum StepAction { Click, RightClick, DoubleClick, Type, TypePassword, Shortcut, Manual }

public enum ElementKind { Other, Button, MenuItem, Tab, Link, CheckBox, RadioButton, Edit, ListItem, TreeItem, ComboBox }

public enum TutorialItemKind { Step, Note }

/// <summary>Retângulo em coordenadas da imagem do passo.</summary>
public sealed record RectDto(int X, int Y, int Width, int Height);

/// <summary>Ponto em coordenadas da imagem do passo.</summary>
public sealed record PointDto(int X, int Y);

/// <summary>
/// Um passo (print + texto) ou uma nota (só texto). Imutável: o desfazer guarda listas antigas,
/// e as imagens ficam no disco.
/// </summary>
public sealed record TutorialItem
{
    public required string Id { get; init; }            // Guid "N"
    public TutorialItemKind Kind { get; init; }
    public string Text { get; init; } = "";             // texto do passo ou da nota
    public StepAction? Action { get; init; }
    public string? ImageFile { get; init; }             // "images/0001.png" (atual)
    public string? OriginalImageFile { get; init; }     // preenchido só quando Edited
    public bool Edited { get; init; }
    public RectDto? Highlight { get; init; }
    public PointDto? ClickPoint { get; init; }
    public bool ShowHighlight { get; init; } = true;
    public IReadOnlyList<RectDto> Redactions { get; init; } = [];

    public static string NewId() => Guid.NewGuid().ToString("N");
}

public sealed record TutorialDocument
{
    public int FormatVersion { get; init; } = 1;
    public string Title { get; init; } = "";
    public DateTimeOffset CreatedAt { get; init; }
    public bool Finished { get; init; }                 // false = sessão de gravação em andamento
    public IReadOnlyList<TutorialItem> Items { get; init; } = [];
}
