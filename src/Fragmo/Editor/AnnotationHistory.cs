namespace Fragmo.Editor;

/// <summary>Lista de anotações com desfazer/refazer.</summary>
public sealed class AnnotationHistory
{
    private readonly List<IAnnotation> _items = [];
    private readonly Stack<IAnnotation> _redo = new();

    public event Action? Changed;

    public IReadOnlyList<IAnnotation> Items => _items;

    public bool CanUndo => _items.Count > 0;

    public bool CanRedo => _redo.Count > 0;

    public void Add(IAnnotation annotation)
    {
        ArgumentNullException.ThrowIfNull(annotation);
        _items.Add(annotation);
        _redo.Clear();
        Changed?.Invoke();
    }

    public bool Undo()
    {
        if (!CanUndo)
        {
            return false;
        }
        IAnnotation last = _items[^1];
        _items.RemoveAt(_items.Count - 1);
        _redo.Push(last);
        Changed?.Invoke();
        return true;
    }

    public bool Redo()
    {
        if (!CanRedo)
        {
            return false;
        }
        _items.Add(_redo.Pop());
        Changed?.Invoke();
        return true;
    }

    public void Clear()
    {
        _items.Clear();
        _redo.Clear();
        Changed?.Invoke();
    }
}
