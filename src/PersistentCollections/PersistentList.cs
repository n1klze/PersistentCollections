using System.Collections;

namespace PersistentCollections;

/// <summary>
/// Персистентный индексируемый список, реализованный как
/// широкое дерево (vector trie) с фактором ветвления 32.
///
/// <para>
/// Структура является <b>иммутабельной</b>:
/// каждая модификация (Append, Set) возвращает новую версию списка,
/// при этом старая версия остаётся доступной.
/// </para>
///
/// <para>
/// Используется техника <b>path copying</b>:
/// при изменении копируются только узлы на пути от корня
/// к изменяемому элементу (O(log₃₂ n)),
/// остальные узлы переиспользуются.
/// </para>
/// </summary>
/// <typeparam name="T">Тип элементов списка.</typeparam>
public class PersistentList<T> : IPersistentCollection<PersistentList<T>>
{
    /// <summary>
    /// Фактор ветвления дерева (32).
    /// </summary>
    private const int Branching = 32;

    /// <summary>
    /// Количество бит, используемых на уровень (log₂(32) = 5).
    /// </summary>
    private const int Bits = 5;

    /// <summary>
    /// Маска для выделения индекса внутри узла.
    /// </summary>
    private const int Mask = Branching - 1;

    private readonly ArrayNode<T> root;
    private readonly int count;
    private readonly History<PersistentList<T>> history;

    /// <summary>
    /// Вычисляет минимальную глубину дерева,
    /// необходимую для хранения указанного количества элементов.
    /// </summary>
    private static int ComputeDepth(int count)
    {
        int depth = 1;
        int capacity = Branching;

        while (count >= capacity)
        {
            capacity <<= Bits; // *32
            depth++;
        }

        return depth;
    }

    /// <summary>
    /// Создаёт новый экземпляр персистентного списка.
    /// Используется внутренне при создании новых версий.
    /// </summary>
    private PersistentList(ArrayNode<T> root, int count, History<PersistentList<T>> history)
    {
        this.root = root;
        this.count = count;
        this.history = history;
    }

    /// <summary>
    /// Создаёт пустой персистентный список.
    /// </summary>
    public static PersistentList<T> Empty()
    {
        var history = new History<PersistentList<T>>();
        var root = new LeafNode<T>(new T[Branching]);
        var list = new PersistentList<T>(root, 0, history);
        history.Push(list);
        return list;
    }

    /// <summary>
    /// Количество элементов в списке.
    /// </summary>
    public int Count => count;

    /// <summary>
    /// Возвращает элемент по индексу.
    /// </summary>
    /// <param name="index">Индекс элемента.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Если индекс выходит за границы списка.
    /// </exception>
    public T this[int index]
    {
        get
        {
            if (index < 0 || index >= count)
                throw new ArgumentOutOfRangeException();

            var depth = ComputeDepth(count);
            return GetAt(root, index, 0, depth);
        }
    }

    /// <summary>
    /// Рекурсивно получает элемент по индексу,
    /// спускаясь по дереву.
    /// </summary>
    private T GetAt(ArrayNode<T> node, int index, int level, int depth)
    {
        if (level == depth - 1)
        {
            var leaf = (LeafNode<T>)node;
            return leaf.Values[index & Mask];
        }

        var branch = (BranchNode<T>)node;
        var shift = Bits * (depth - level - 1);
        var childIndex = (index >> shift) & Mask;

        return GetAt(branch.Children[childIndex], index, level + 1, depth);
    }

    /// <summary>
    /// Возвращает новую версию списка с изменённым элементом по индексу.
    /// </summary>
    /// <param name="index">Индекс изменяемого элемента.</param>
    /// <param name="value">Новое значение.</param>
    /// <returns>Новая версия списка.</returns>
    public PersistentList<T> Set(int index, T value)
    {
        if (index < 0 || index >= count)
            throw new ArgumentOutOfRangeException();

        var depth = ComputeDepth(count);
        var newRoot = SetAt(root, index, value, 0, depth);

        var newList = new PersistentList<T>(newRoot, count, history);
        history.Push(newList);
        return newList;
    }

    /// <summary>
    /// Реализует path copying при изменении элемента:
    /// копирует только узлы на пути от корня к листу.
    /// </summary>
    private ArrayNode<T> SetAt(ArrayNode<T> node, int index, T value, int level, int depth)
    {
        if (level == depth - 1)
        {
            var leaf = (LeafNode<T>)node;
            return leaf.WithValue(index & Mask, value);
        }

        var branch = (BranchNode<T>)node;
        var shift = Bits * (depth - level - 1);
        var childIndex = (index >> shift) & Mask;

        var oldChild = branch.Children[childIndex];
        var newChild = SetAt(oldChild, index, value, level + 1, depth);

        return branch.WithChild(childIndex, newChild);
    }

    /// <summary>
    /// Добавляет элемент в конец списка.
    /// </summary>
    /// <param name="value">Добавляемое значение.</param>
    /// <returns>Новая версия списка.</returns>
    public PersistentList<T> Append(T value)
    {
        var newCount = count + 1;
        var depth = ComputeDepth(newCount);

        var newRoot = AppendAt(root, count, value, 0, depth);

        var newList = new PersistentList<T>(newRoot, newCount, history);
        history.Push(newList);
        return newList;
    }

    /// <summary>
    /// Рекурсивно добавляет элемент,
    /// создавая новые узлы только на пути вставки.
    /// </summary>
    private ArrayNode<T> AppendAt(ArrayNode<T>? node, int index, T value, int level, int depth)
    {
        if (level == depth - 1)
        {
            if (node is LeafNode<T> leaf)
            {
                var newValues = (T[])leaf.Values.Clone();
                newValues[index & Mask] = value;
                return new LeafNode<T>(newValues);
            }
            else
            {
                var values = new T[Branching];
                values[index & Mask] = value;
                return new LeafNode<T>(values);
            }
        }

        BranchNode<T> branch = node is BranchNode<T> existing
            ? existing
            : new BranchNode<T>(new ArrayNode<T>[Branching]);

        var shift = Bits * (depth - level - 1);
        var childIndex = (index >> shift) & Mask;

        var newChild = AppendAt(branch.Children[childIndex], index, value, level + 1, depth);

        return branch.WithChild(childIndex, newChild);
    }

    /// <summary>
    /// Возвращает следующую версию из истории изменений.
    /// </summary>
    public PersistentList<T> Redo() => history.Redo();

    /// <summary>
    /// Возвращает текущую версию как снимок состояния.
    /// </summary>
    public PersistentList<T> Snapshot() => this;

    /// <summary>
    /// Возвращает предыдущую версию из истории изменений.
    /// </summary>
    public PersistentList<T> Undo() => history.Undo();
}

internal abstract class ArrayNode<T> { }

internal sealed class BranchNode<T> : ArrayNode<T>
{
    public readonly ArrayNode<T>[] Children;

    public BranchNode(ArrayNode<T>[] children)
    {
        Children = children;
    }

    public BranchNode<T> WithChild(int index, ArrayNode<T> child)
    {
        var newChildren = (ArrayNode<T>[])Children.Clone();
        newChildren[index] = child;
        return new BranchNode<T>(newChildren);
    }
}

internal sealed class LeafNode<T> : ArrayNode<T>
{
    public readonly T[] Values;

    public LeafNode(T[] values)
    {
        Values = values;
    }

    public LeafNode<T> WithValue(int index, T value)
    {
        var newValues = (T[])Values.Clone();
        newValues[index] = value;
        return new LeafNode<T>(newValues);
    }
}
