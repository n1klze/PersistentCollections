namespace PersistentCollections;

public class History<T>
{
    private Stack<T> undo = new();
    private Stack<T> redo = new();
    private T current;

    public History(T initial)
    {
        current = initial;
    }

    public T Current => current;

    public T Commit(T next)
    {
        undo.Push(current);
        redo.Clear();
        current = next;
        return current;
    }

    public T Undo()
    {
        if (undo.Count == 0)
            throw new InvalidOperationException();

        redo.Push(current);
        current = undo.Pop();
        return current;
    }

    public T Redo()
    {
        if (redo.Count == 0)
            throw new InvalidOperationException();

        undo.Push(current);
        current = redo.Pop();
        return current;
    }
}
