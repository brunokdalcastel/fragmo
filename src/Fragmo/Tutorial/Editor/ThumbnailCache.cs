namespace Fragmo.Tutorial.Editor;

/// <summary>
/// Miniaturas da lista do editor, geradas sob demanda e guardadas num LRU (RNF4: um tutorial de 200 passos
/// não segura 200 bitmaps). A chave é o item (record imutável: qualquer edição gera outro) + o número do passo,
/// que aparece no marcador. Falha ao gerar fica guardada como <c>null</c>, para não tentar de novo a cada pintura.
/// </summary>
internal sealed class ThumbnailCache(int capacity, Func<TutorialItem, int, Bitmap?> render) : IDisposable
{
    private readonly record struct Key(TutorialItem Item, int Number);

    private readonly Dictionary<Key, LinkedListNode<(Key Key, Bitmap? Image)>> _map = [];
    private readonly LinkedList<(Key Key, Bitmap? Image)> _order = new(); // primeiro = mais recente

    public int Count => _map.Count;

    public Bitmap? Get(TutorialItem item, int number)
    {
        var key = new Key(item, number);
        if (_map.TryGetValue(key, out var node))
        {
            _order.Remove(node);
            _order.AddFirst(node);
            return node.Value.Image;
        }

        Bitmap? image = render(item, number);
        _map[key] = _order.AddFirst((key, image));
        while (_map.Count > capacity && _order.Last is { } oldest)
        {
            _order.RemoveLast();
            _map.Remove(oldest.Value.Key);
            oldest.Value.Image?.Dispose();
        }
        return image;
    }

    public void Clear()
    {
        foreach ((Key _, Bitmap? image) in _order)
        {
            image?.Dispose();
        }
        _order.Clear();
        _map.Clear();
    }

    public void Dispose() => Clear();
}
