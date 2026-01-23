using System.Collections;

namespace PersistentCollections;

/// <summary>
/// Персистентный (immutable) двусвязный список.
/// </summary>
/// <remarks>
/// Каждая модификация возвращает новую версию списка; старая версия остаётся
/// доступной (structural sharing не выполняется для узлов — реализуется full chain copy).
/// Операции модификации делают полную перестройку цепочки узлов (O(n)).
///
/// Для Undo/Redo используется внешний <see cref="History{T}"/>, в который
/// автоматически пушится каждая новая версия при вызове методов модификации.
/// </remarks>
/// <typeparam name="T">Тип элементов списка.</typeparam>
public class PersistentLinkedList<T> : IPersistentCollection<PersistentLinkedList<T>>
{
    private readonly ListNode<T>? head;
    private readonly ListNode<T>? tail;
    private readonly int count;
    private readonly History<PersistentLinkedList<T>> history;

    private PersistentLinkedList(
        ListNode<T>? head,
        ListNode<T>? tail,
        int count,
        History<PersistentLinkedList<T>> history
    )
    {
        this.head = head;
        this.tail = tail;
        this.count = count;
        this.history = history;
    }

    /// <summary>
    /// Создаёт пустой персистентный список и инициализирует историю состояний.
    /// </summary>
    /// <returns>Новый пустой <see cref="PersistentLinkedList{T}"/>.</returns>
    public static PersistentLinkedList<T> Empty()
    {
        var history = new History<PersistentLinkedList<T>>();
        var list = new PersistentLinkedList<T>(null, null, 0, history);
        history.Commit(list);
        return list;
    }

    /// <summary>
    /// Количество элементов в списке.
    /// </summary>
    public int Count => count;

    /// <summary>
    /// Получает значение по индексу.
    /// </summary>
    /// <param name="index">Нулевой индекс элемента (0 .. Count-1).</param>
    /// <returns>Значение элемента с указанным индексом.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Если индекс вне диапазона.</exception>
    /// <remarks>
    /// Операция выполняет последовательный проход от <see cref="head"/>, O(n).
    /// </remarks>
    public T this[int index]
    {
        get
        {
            if (index < 0 || index >= count)
                throw new ArgumentOutOfRangeException();

            var current = head;
            for (int i = 0; i < index; i++)
                current = current!.Next;

            return current!.Value;
        }
    }

    // ---------- public operations (now use chain-copy) ----------

    /// <summary>
    /// Возвращает новый список с добавленным элементом в начало.
    /// </summary>
    /// <param name="value">Добавляемое значение.</param>
    /// <returns>Новая версия списка с элементом в начале.</returns>
    /// <remarks>
    /// Реализовано через сбор всех значений в массив и полную перестройку цепочки.
    /// Временная сложность O(n), дополнительная память O(n).
    /// Также новая версия автоматически добавляется в историю (Undo/Redo поддерживается).
    /// </remarks>
    public PersistentLinkedList<T> AddFirst(T value)
    {
        var values = CollectValues();
        var newValues = new List<T>(values.Count + 1) { value };
        newValues.AddRange(values);

        var (newHead, newTail) = BuildFromValues(newValues);
        var newList = new PersistentLinkedList<T>(newHead, newTail, newValues.Count, history);
        history.Commit(newList);
        return newList;
    }

    /// <summary>
    /// Возвращает новый список с добавленным элементом в конец.
    /// </summary>
    /// <param name="value">Добавляемое значение.</param>
    /// <returns>Новая версия списка с элементом в конце.</returns>
    /// <remarks>O(n) по времени и памяти.</remarks>
    public PersistentLinkedList<T> AddLast(T value)
    {
        var values = CollectValues();
        var newValues = new List<T>(values.Count + 1);
        newValues.AddRange(values);
        newValues.Add(value);

        var (newHead, newTail) = BuildFromValues(newValues);
        var newList = new PersistentLinkedList<T>(newHead, newTail, newValues.Count, history);
        history.Commit(newList);
        return newList;
    }

    /// <summary>
    /// Вставляет значение в позицию с указанным индексом и возвращает новую версию списка.
    /// </summary>
    /// <param name="index">Позиция для вставки (0..Count).</param>
    /// <param name="value">Вставляемое значение.</param>
    /// <returns>Новый список с вставленным значением.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Если индекс вне диапазона [0, Count].</exception>
    /// <remarks>Вставка выполняется через сбор значений и перестройку цепочки (O(n)).</remarks>
    public PersistentLinkedList<T> Insert(int index, T value)
    {
        if (index == 0)
            return AddFirst(value);
        if (index == count)
            return AddLast(value);
        if (index < 0 || index > count)
            throw new ArgumentOutOfRangeException();

        var values = CollectValues();
        var newValues = new List<T>(values.Count + 1);
        for (int i = 0; i < index; i++)
            newValues.Add(values[i]);
        newValues.Add(value);
        for (int i = index; i < values.Count; i++)
            newValues.Add(values[i]);

        var (newHead, newTail) = BuildFromValues(newValues);
        var newList = new PersistentLinkedList<T>(newHead, newTail, newValues.Count, history);
        history.Commit(newList);
        return newList;
    }

    /// <summary>
    /// Удаляет элемент по индексу и возвращает новую версию списка.
    /// </summary>
    /// <param name="index">Индекс удаляемого элемента (0..Count-1).</param>
    /// <returns>Новая версия списка без удалённого элемента.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Если индекс вне диапазона.</exception>
    /// <remarks>O(n).</remarks>
    public PersistentLinkedList<T> RemoveAt(int index)
    {
        if (index < 0 || index >= count)
            throw new ArgumentOutOfRangeException();

        if (index == 0)
            return RemoveFirst();
        if (index == count - 1)
            return RemoveLast();

        var values = CollectValues();
        var newValues = new List<T>(values.Count - 1);
        for (int i = 0; i < values.Count; i++)
        {
            if (i == index)
                continue;
            newValues.Add(values[i]);
        }

        var (newHead, newTail) = BuildFromValues(newValues);
        var newList = new PersistentLinkedList<T>(newHead, newTail, newValues.Count, history);
        history.Commit(newList);
        return newList;
    }

    /// <summary>
    /// Удаляет первый элемент и возвращает новую версию списка.
    /// </summary>
    /// <returns>Новая версия списка без первого элемента.</returns>
    /// <exception cref="InvalidOperationException">Если список пуст.</exception>
    /// <remarks>O(n).</remarks>
    public PersistentLinkedList<T> RemoveFirst()
    {
        if (head == null)
            throw new InvalidOperationException();

        if (head == tail)
        {
            var list = new PersistentLinkedList<T>(null, null, 0, history);
            history.Commit(list);
            return list;
        }

        var values = CollectValues();
        var newValues = new List<T>(values.Count - 1);
        for (int i = 1; i < values.Count; i++)
            newValues.Add(values[i]);

        var (newHead, newTail) = BuildFromValues(newValues);
        var newList = new PersistentLinkedList<T>(newHead, newTail, newValues.Count, history);
        history.Commit(newList);
        return newList;
    }

    /// <summary>
    /// Удаляет последний элемент и возвращает новую версию списка.
    /// </summary>
    /// <returns>Новая версия списка без последнего элемента.</returns>
    /// <exception cref="InvalidOperationException">Если список пуст.</exception>
    /// <remarks>O(n).</remarks>
    public PersistentLinkedList<T> RemoveLast()
    {
        if (tail == null)
            throw new InvalidOperationException();

        if (head == tail)
        {
            var list = new PersistentLinkedList<T>(null, null, 0, history);
            history.Commit(list);
            return list;
        }

        var values = CollectValues();
        var newValues = new List<T>(values.Count - 1);
        for (int i = 0; i < values.Count - 1; i++)
            newValues.Add(values[i]);

        var (newHead, newTail) = BuildFromValues(newValues);
        var newList = new PersistentLinkedList<T>(newHead, newTail, newValues.Count, history);
        history.Commit(newList);
        return newList;
    }

    /// <summary>
    /// Повторяет последнюю отменённую операцию (Redo).
    /// </summary>
    /// <returns>Следующая версия списка из истории.</returns>
    public PersistentLinkedList<T> Redo() => history.Redo();

    /// <summary>
    /// Отменяет последнюю операцию (Undo).
    /// </summary>
    /// <returns>Предыдущая версия списка из истории.</returns>
    public PersistentLinkedList<T> Undo() => history.Undo();

    // ---------- private helpers ----------

    /// <summary>
    /// Собирает все значения в <see cref="List{T}"/>.
    /// </summary>
    /// <returns>Новый список значений (копия).</returns>
    /// <remarks>O(n) по времени.</remarks>
    private List<T> CollectValues()
    {
        var values = new List<T>(count);
        var cur = head;
        while (cur != null)
        {
            values.Add(cur.Value);
            cur = cur.Next;
        }
        return values;
    }

    /// <summary>
    /// Полностью строит новую согласованную цепочку узлов по массиву значений.
    /// </summary>
    /// <param name="values">Последовательность значений для построения.</param>
    /// <returns>Пару (head, tail). Если values пуст — (null, null).</returns>
    /// <remarks>Метод используется для реализации chain-copy; создаёт новые узлы и корректно
    /// устанавливает Prev/Next между ними.</remarks>
    private (ListNode<T>? head, ListNode<T>? tail) BuildFromValues(IList<T> values)
    {
        if (values == null || values.Count == 0)
            return (null, null);

        var n = values.Count;
        var nodes = new ListNode<T>[n];

        // Создаем узлы с пустыми ссылками
        for (int i = 0; i < n; i++)
        {
            nodes[i] = new ListNode<T>(values[i], null, null);
        }

        // Устанавливаем Prev/Next (internal set)
        for (int i = 0; i < n; i++)
        {
            if (i > 0)
                nodes[i].Prev = nodes[i - 1];
            if (i + 1 < n)
                nodes[i].Next = nodes[i + 1];
        }

        return (nodes[0], nodes[n - 1]);
    }
}

internal sealed class ListNode<T>
{
    /// <summary>
    /// Значение узла.
    /// </summary>
    public T Value { get; }

    /// <summary>
    /// Ссылка на предыдущий узел в цепочке. Устанавливается внутренне при построении цепочки.
    /// </summary>
    public ListNode<T>? Prev { get; internal set; }

    /// <summary>
    /// Ссылка на следующий узел в цепочке. Устанавливается внутренне при построении цепочки.
    /// </summary>
    public ListNode<T>? Next { get; internal set; }

    /// <summary>
    /// Создаёт новый узел.
    /// </summary>
    /// <param name="value">Значение узла.</param>
    /// <param name="prev">Предыдущий узел (может быть null).</param>
    /// <param name="next">Следующий узел (может быть null).</param>
    public ListNode(T value, ListNode<T>? prev, ListNode<T>? next)
    {
        Value = value;
        Prev = prev;
        Next = next;
    }

    /// <summary>
    /// Создаёт новый экземпляр узла с указанным Prev (не изменяя текущий).
    /// </summary>
    /// <param name="newPrev">Новый Prev.</param>
    /// <returns>Клонированный узел с другим Prev.</returns>
    public ListNode<T> WithPrev(ListNode<T>? newPrev) => new ListNode<T>(Value, newPrev, Next);

    /// <summary>
    /// Создаёт новый экземпляр узла с указанным Next (не изменяя текущий).
    /// </summary>
    /// <param name="newNext">Новый Next.</param>
    /// <returns>Клонированный узел с другим Next.</returns>
    public ListNode<T> WithNext(ListNode<T>? newNext) => new ListNode<T>(Value, Prev, newNext);
}
