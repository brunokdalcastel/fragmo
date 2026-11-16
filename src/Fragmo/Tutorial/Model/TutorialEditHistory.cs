namespace Fragmo.Tutorial;

/// <summary>
/// Desfazer do editor de tutorial: guarda documentos anteriores (imutáveis; as imagens ficam no disco).
/// As funções estáticas são as edições puras; quando não há o que mudar, devolvem o mesmo objeto,
/// para quem chama saber que não precisa empilhar nada (<see cref="object.ReferenceEquals"/>).
/// </summary>
public sealed class TutorialEditHistory
{
    public const int Limit = 50;

    private readonly LinkedList<TutorialDocument> _undo = new();

    public bool CanUndo => _undo.Count > 0;

    public int Count => _undo.Count;

    /// <summary>Guarda o estado de antes de uma edição. Passando do limite, o mais antigo sai.</summary>
    public void Push(TutorialDocument before)
    {
        _undo.AddLast(before);
        if (_undo.Count > Limit)
        {
            _undo.RemoveFirst();
        }
    }

    public bool TryUndo(out TutorialDocument doc)
    {
        if (_undo.Last is not { } last)
        {
            doc = null!;
            return false;
        }
        _undo.RemoveLast();
        doc = last.Value;
        return true;
    }

    public void Clear() => _undo.Clear();

    // ----- Edições puras -----

    public static TutorialDocument MoveUp(TutorialDocument doc, string id) => Swap(doc, id, -1);

    public static TutorialDocument MoveDown(TutorialDocument doc, string id) => Swap(doc, id, +1);

    public static TutorialDocument Remove(TutorialDocument doc, string id)
    {
        int index = IndexOf(doc, id);
        if (index < 0)
        {
            return doc;
        }
        var items = doc.Items.ToList();
        items.RemoveAt(index);
        return doc with { Items = items };
    }

    /// <summary>Insere uma nota depois do item <paramref name="afterId"/>; null põe a nota no começo.</summary>
    public static TutorialDocument InsertNoteAfter(TutorialDocument doc, string? afterId, string text)
    {
        int index = 0;
        if (afterId is not null)
        {
            index = IndexOf(doc, afterId);
            if (index < 0)
            {
                return doc;
            }
            index++;
        }
        var items = doc.Items.ToList();
        items.Insert(index, new TutorialItem { Id = TutorialItem.NewId(), Kind = TutorialItemKind.Note, Text = text });
        return doc with { Items = items };
    }

    public static TutorialDocument SetText(TutorialDocument doc, string id, string text) =>
        Update(doc, id, i => i.Text == text ? i : i with { Text = text });

    public static TutorialDocument SetShowHighlight(TutorialDocument doc, string id, bool show) =>
        Update(doc, id, i => i.ShowHighlight == show ? i : i with { ShowHighlight = show });

    public static TutorialDocument SetTitle(TutorialDocument doc, string title) =>
        doc.Title == title ? doc : doc with { Title = title };

    /// <summary>Troca um item por outro (editar/restaurar imagem). Mesmo item → mesmo documento.</summary>
    public static TutorialDocument Update(TutorialDocument doc, string id, Func<TutorialItem, TutorialItem> change)
    {
        int index = IndexOf(doc, id);
        if (index < 0)
        {
            return doc;
        }
        TutorialItem changed = change(doc.Items[index]);
        if (ReferenceEquals(changed, doc.Items[index]))
        {
            return doc;
        }
        var items = doc.Items.ToList();
        items[index] = changed;
        return doc with { Items = items };
    }

    private static TutorialDocument Swap(TutorialDocument doc, string id, int delta)
    {
        int index = IndexOf(doc, id);
        int other = index + delta;
        if (index < 0 || other < 0 || other >= doc.Items.Count)
        {
            return doc;
        }
        var items = doc.Items.ToList();
        (items[index], items[other]) = (items[other], items[index]);
        return doc with { Items = items };
    }

    private static int IndexOf(TutorialDocument doc, string id)
    {
        for (int i = 0; i < doc.Items.Count; i++)
        {
            if (doc.Items[i].Id == id)
            {
                return i;
            }
        }
        return -1;
    }
}
