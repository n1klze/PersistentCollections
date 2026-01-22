namespace PersistentCollections;

public sealed class History<T>
{
    private readonly Stack<T> undoStack = new();
    private readonly Stack<T> redoStack = new();

    public void Push(T version)
    {
        undoStack.Push(version);
        redoStack.Clear();
    }

    public T Undo()
    {
        if (undoStack.Count <= 1)
            throw new InvalidOperationException("Nothing to undo.");

        var current = undoStack.Pop();
        redoStack.Push(current);

        return undoStack.Peek();
    }

    public T Redo()
    {
        if (redoStack.Count == 0)
            throw new InvalidOperationException("Nothing to redo.");

        var version = redoStack.Pop();
        undoStack.Push(version);

        return version;
    }
}


// public class History<T>
// {
//     private Stack<T> undo = new();
//     private Stack<T> redo = new();
//     private T current;

//     public History(T initial)
//     {
//         current = initial;
//     }

//     public T Current => current;

//     public T Commit(T next)
//     {
//         undo.Push(current);
//         redo.Clear();
//         current = next;
//         return current;
//     }

//     public T Undo()
//     {
//         if (undo.Count == 0)
//             throw new InvalidOperationException();

//         redo.Push(current);
//         current = undo.Pop();
//         return current;
//     }

//     public T Redo()
//     {
//         if (redo.Count == 0)
//             throw new InvalidOperationException();

//         undo.Push(current);
//         current = redo.Pop();
//         return current;
//     }
// }
