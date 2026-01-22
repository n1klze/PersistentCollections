using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;

namespace PersistentCollections;

/// <summary>
/// Персистентный словарь на основе HAMT (Hash Array Mapped Trie).
/// </summary>
/// <remarks>
/// <para>
/// Структура данных является полностью иммутабельной:
/// каждая операция вставки или удаления возвращает новую версию словаря,
/// при этом предыдущие версии остаются доступными.
/// </para>
/// <para>
/// Реализация использует HAMT с bitmap-indexed узлами, что обеспечивает:
/// </para>
/// <list type="bullet">
/// <item><description>Амортизированную сложность O(1) для Put / Remove / Lookup</description></item>
/// <item><description>Структурное разделение (path copying)</description></item>
/// <item><description>Эффективную работу с памятью</description></item>
/// </list>
/// <para>
/// Поддержка Undo/Redo реализована через <see cref="History{T}"/>.
/// </para>
/// </remarks>
/// <typeparam name="TKey">Тип ключа.</typeparam>
/// <typeparam name="TValue">Тип значения.</typeparam>
public class PersistentDictionary<TKey, TValue>
    : IPersistentCollection<PersistentDictionary<TKey, TValue>>
{
    private readonly HamtNode<TKey, TValue>? root;
    private readonly int count;
    private readonly History<PersistentDictionary<TKey, TValue>> history;

    /// <summary>
    /// Количество бит, используемых на каждом уровне trie.
    /// </summary>
    /// <remarks>
    /// 5 бит ⇒ 2⁵ = 32 ветви.
    /// </remarks>
    private const int Bits = 5;

    /// <summary>
    /// Максимальное количество дочерних узлов на уровне (32).
    /// </summary>
    private const int Branching = 32;

    /// <summary>
    /// Маска для извлечения Bits из хеша.
    /// </summary>
    private const int Mask = Branching - 1;

    /// <summary>
    /// Извлекает фрагмент хеша для текущего уровня trie.
    /// </summary>
    private static int MaskHash(int hash, int shift) => (hash >> shift) & Mask;

    /// <summary>
    /// Преобразует индекс (0..31) в битовую позицию.
    /// </summary>
    private static int Bitpos(int index) => 1 << index;

    private PersistentDictionary(
        HamtNode<TKey, TValue>? root,
        int count,
        History<PersistentDictionary<TKey, TValue>> history
    )
    {
        this.root = root;
        this.count = count;
        this.history = history;
    }

    /// <summary>
    /// Получает значение по ключу.
    /// </summary>
    /// <param name="key">Ключ.</param>
    /// <returns>Значение, соответствующее ключу.</returns>
    /// <exception cref="KeyNotFoundException">
    /// Если ключ отсутствует в словаре.
    /// </exception>
    public TValue this[TKey key]
    {
        get
        {
            if (TryGetValue(key, out var value))
                return value;

            throw new KeyNotFoundException(
                $"The given key '{key}' was not present in the dictionary."
            );
        }
    }

    /// <summary>
    /// Создаёт пустой персистентный словарь.
    /// </summary>
    /// <returns>Пустой <see cref="PersistentDictionary{TKey, TValue}"/>.</returns>
    public static PersistentDictionary<TKey, TValue> Empty()
    {
        var history = new History<PersistentDictionary<TKey, TValue>>();
        var dict = new PersistentDictionary<TKey, TValue>(null, 0, history);
        history.Push(dict);
        return dict;
    }

    /// <summary>
    /// Количество элементов в словаре.
    /// </summary>
    public int Count => count;

    /// <summary>
    /// Пытается получить значение по ключу.
    /// </summary>
    /// <param name="key">Ключ.</param>
    /// <param name="value">
    /// Выходной параметр — значение, если ключ найден.
    /// </param>
    /// <returns>
    /// <c>true</c>, если ключ присутствует; иначе <c>false</c>.
    /// </returns>
    /// <remarks>
    /// Поиск выполняется по HAMT, сложность ~O(1).
    /// </remarks>
    public bool TryGetValue(TKey key, out TValue value)
    {
        var hash = key!.GetHashCode();
        return TryGetAt(root, key, hash, 0, out value);
    }

    /// <summary>
    /// Рекурсивный поиск значения в HAMT.
    /// </summary>
    private bool TryGetAt(
        HamtNode<TKey, TValue>? node,
        TKey key,
        int hash,
        int shift,
        out TValue value
    )
    {
        if (node == null)
        {
            value = default!;
            return false;
        }

        if (node is LeafNode<TKey, TValue> leaf)
        {
            foreach (var (k, v) in leaf.Entries)
            {
                if (EqualityComparer<TKey>.Default.Equals(k, key))
                {
                    value = v;
                    return true;
                }
            }

            value = default!;
            return false;
        }

        var branch = (BitmapIndexedNode<TKey, TValue>)node;
        var index = MaskHash(hash, shift);
        var bit = Bitpos(index);

        if (!branch.HasBit(bit))
        {
            value = default!;
            return false;
        }

        var childIndex = branch.Index(bit);
        return TryGetAt(branch.Children[childIndex], key, hash, shift + Bits, out value);
    }

    /// <summary>
    /// Добавляет или обновляет значение по ключу.
    /// </summary>
    /// <param name="key">Ключ.</param>
    /// <param name="value">Значение.</param>
    /// <returns>Новая версия словаря.</returns>
    /// <remarks>
    /// Если ключ уже существует, значение заменяется.
    /// Если нет — добавляется новая пара ключ–значение.
    /// </remarks>
    public PersistentDictionary<TKey, TValue> Put(TKey key, TValue value)
    {
        var hash = key!.GetHashCode();
        bool added;

        var newRoot = PutAt(root, key, value, hash, 0, out added);
        var newCount = added ? count + 1 : count;

        var newDict = new PersistentDictionary<TKey, TValue>(newRoot, newCount, history);
        history.Push(newDict);
        return newDict;
    }

    /// <summary>
    /// Рекурсивная вставка в HAMT с path copying.
    /// </summary>
    private HamtNode<TKey, TValue> PutAt(
        HamtNode<TKey, TValue>? node,
        TKey key,
        TValue value,
        int hash,
        int shift,
        out bool added
    )
    {
        if (node == null)
        {
            added = true;
            return new LeafNode<TKey, TValue>(hash, new[] { (key, value) });
        }

        if (node is LeafNode<TKey, TValue> leaf)
        {
            if (leaf.Hash == hash)
            {
                var entries = leaf.Entries.ToArray();
                for (int i = 0; i < entries.Length; i++)
                {
                    if (EqualityComparer<TKey>.Default.Equals(entries[i].Key, key))
                    {
                        entries[i] = (key, value);
                        added = false;
                        return new LeafNode<TKey, TValue>(hash, entries);
                    }
                }

                added = true;
                return new LeafNode<TKey, TValue>(hash, entries.Append((key, value)).ToArray());
            }

            // Разные хеши → необходимо создать ветку
            return MergeLeaves(leaf, key, value, hash, shift, out added);
        }

        var branch = (BitmapIndexedNode<TKey, TValue>)node;
        var index = MaskHash(hash, shift);
        var bit = Bitpos(index);
        var childIndex = branch.Index(bit);

        if (branch.HasBit(bit))
        {
            var oldChild = branch.Children[childIndex];
            var newChild = PutAt(oldChild, key, value, hash, shift + Bits, out added);

            var newChildren = (HamtNode<TKey, TValue>[])branch.Children.Clone();
            newChildren[childIndex] = newChild;

            return new BitmapIndexedNode<TKey, TValue>(branch.Bitmap, newChildren);
        }

        added = true;

        var children = new List<HamtNode<TKey, TValue>>(branch.Children);
        children.Insert(childIndex, new LeafNode<TKey, TValue>(hash, new[] { (key, value) }));

        return new BitmapIndexedNode<TKey, TValue>(branch.Bitmap | bit, children.ToArray());
    }

    /// <summary>
    /// Сливает два leaf-узла с разными хешами в общую ветку.
    /// </summary>
    private HamtNode<TKey, TValue> MergeLeaves(
        LeafNode<TKey, TValue> leaf,
        TKey key,
        TValue value,
        int hash,
        int shift,
        out bool added
    )
    {
        if (shift >= 32)
        {
            added = true;
            return new LeafNode<TKey, TValue>(hash, leaf.Entries.Append((key, value)).ToArray());
        }

        var index1 = MaskHash(leaf.Hash, shift);
        var index2 = MaskHash(hash, shift);

        if (index1 == index2)
        {
            var child = MergeLeaves(leaf, key, value, hash, shift + Bits, out added);
            return new BitmapIndexedNode<TKey, TValue>(Bitpos(index1), new[] { child });
        }

        var bit1 = Bitpos(index1);
        var bit2 = Bitpos(index2);

        added = true;
        return new BitmapIndexedNode<TKey, TValue>(
            bit1 | bit2,
            index1 < index2
                ? new HamtNode<TKey, TValue>[]
                {
                    leaf,
                    new LeafNode<TKey, TValue>(hash, new[] { (key, value) }),
                }
                : new HamtNode<TKey, TValue>[]
                {
                    new LeafNode<TKey, TValue>(hash, new[] { (key, value) }),
                    leaf,
                }
        );
    }

    /// <summary>
    /// Удаляет ключ из словаря.
    /// </summary>
    /// <param name="key">Удаляемый ключ.</param>
    /// <returns>
    /// Новая версия словаря, либо текущая, если ключ отсутствует.
    /// </returns>
    public PersistentDictionary<TKey, TValue> Remove(TKey key)
    {
        var hash = key!.GetHashCode();
        bool removed;

        var newRoot = RemoveAt(root, key, hash, 0, out removed);
        if (!removed)
            return this;

        var newDict = new PersistentDictionary<TKey, TValue>(newRoot, count - 1, history);
        history.Push(newDict);
        return newDict;
    }

    /// <summary>
    /// Рекурсивное удаление узла с path copying.
    /// </summary>
    private HamtNode<TKey, TValue>? RemoveAt(
        HamtNode<TKey, TValue>? node,
        TKey key,
        int hash,
        int shift,
        out bool removed
    )
    {
        if (node == null)
        {
            removed = false;
            return null;
        }

        if (node is LeafNode<TKey, TValue> leaf)
        {
            var entries = leaf
                .Entries.Where(e => !EqualityComparer<TKey>.Default.Equals(e.Key, key))
                .ToArray();

            if (entries.Length == leaf.Entries.Length)
            {
                removed = false;
                return leaf;
            }

            removed = true;
            return entries.Length == 0 ? null : new LeafNode<TKey, TValue>(hash, entries);
        }

        var branch = (BitmapIndexedNode<TKey, TValue>)node;
        var index = MaskHash(hash, shift);
        var bit = Bitpos(index);

        if (!branch.HasBit(bit))
        {
            removed = false;
            return branch;
        }

        var childIndex = branch.Index(bit);
        var newChild = RemoveAt(branch.Children[childIndex], key, hash, shift + Bits, out removed);

        if (!removed)
            return branch;

        if (newChild == null)
        {
            var newBitmap = branch.Bitmap & ~bit;
            if (newBitmap == 0)
                return null;

            var children = new List<HamtNode<TKey, TValue>>(branch.Children);
            children.RemoveAt(childIndex);

            return new BitmapIndexedNode<TKey, TValue>(newBitmap, children.ToArray());
        }

        var cloned = (HamtNode<TKey, TValue>[])branch.Children.Clone();
        cloned[childIndex] = newChild;
        return new BitmapIndexedNode<TKey, TValue>(branch.Bitmap, cloned);
    }

    /// <summary>
    /// Повторяет последнюю отменённую операцию.
    /// </summary>
    public PersistentDictionary<TKey, TValue> Redo() => history.Redo();

    /// <summary>
    /// Возвращает текущую версию словаря.
    /// </summary>
    public PersistentDictionary<TKey, TValue> Snapshot() => this;

    /// <summary>
    /// Отменяет последнюю операцию.
    /// </summary>
    public PersistentDictionary<TKey, TValue> Undo() => history.Undo();
}

/// <summary>
/// Абстрактный узел HAMT.
/// </summary>
internal abstract class HamtNode<TKey, TValue> { }

/// <summary>
/// Ветвящийся узел HAMT с bitmap-индексацией.
/// </summary>
/// <remarks>
/// Bitmap определяет, какие из 32 возможных дочерних узлов присутствуют.
/// Children содержит только реально существующие узлы.
/// </remarks>
internal sealed class BitmapIndexedNode<TKey, TValue> : HamtNode<TKey, TValue>
{
    public readonly int Bitmap;
    public readonly HamtNode<TKey, TValue>[] Children;

    public BitmapIndexedNode(int bitmap, HamtNode<TKey, TValue>[] children)
    {
        Bitmap = bitmap;
        Children = children;
    }

    /// <summary>
    /// Вычисляет индекс дочернего узла в массиве Children.
    /// </summary>
    public int Index(int bit) => BitOperations.PopCount((uint)(Bitmap & (bit - 1)));

    /// <summary>
    /// Проверяет, установлен ли бит.
    /// </summary>
    public bool HasBit(int bit) => (Bitmap & bit) != 0;
}

/// <summary>
/// Листовой узел HAMT.
/// </summary>
/// <remarks>
/// Может содержать несколько пар ключ–значение
/// в случае хеш-коллизий.
/// </remarks>
internal sealed class LeafNode<TKey, TValue> : HamtNode<TKey, TValue>
{
    public readonly int Hash;
    public readonly (TKey Key, TValue Value)[] Entries;

    public LeafNode(int hash, (TKey, TValue)[] entries)
    {
        Hash = hash;
        Entries = entries;
    }
}
