namespace PersistentCollections;

/// <summary>
/// Универсальный менеджер истории версий для персистентных структур данных.
///
/// <para>
/// Класс реализует линейную модель undo/redo:
/// <list type="bullet">
///   <item><description>Каждая новая версия фиксируется через <see cref="Commit"/>.</description></item>
///   <item><description><see cref="Undo"/> возвращает предыдущую версию.</description></item>
///   <item><description><see cref="Redo"/> возвращает отменённую версию.</description></item>
/// </list>
/// </para>
///
/// <para>
/// Все версии хранятся как объекты типа <typeparamref name="T"/>.
/// История не знает ничего о внутренней структуре версий —
/// она просто управляет ссылками на них.
/// </para>
///
/// <para>
/// При добавлении новой версии после <see cref="Undo"/> redo-история очищается.
/// </para>
/// </summary>
/// <typeparam name="T">
/// Тип версии. Обычно это сама персистентная структура данных
/// (например, <c>PersistentList&lt;T&gt;</c> или <c>PersistentLinkedList&lt;T&gt;</c>).
/// </typeparam>
public sealed class History<T>
{
    /// <summary>
    /// Стек undo-версий.
    /// Вершина стека — текущая версия.
    /// </summary>
    private readonly Stack<T> undoStack = new();

    /// <summary>
    /// Стек redo-версий.
    /// Содержит версии, отменённые вызовом <see cref="Undo"/>.
    /// </summary>
    private readonly Stack<T> redoStack = new();

    /// <summary>
    /// Фиксирует новую версию в истории.
    ///
    /// <para>
    /// Добавляет переданную версию в undo-стек и очищает redo-стек.
    /// </para>
    ///
    /// <para>
    /// Должен вызываться каждый раз, когда создаётся новая версия
    /// персистентной структуры данных.
    /// </para>
    /// </summary>
    /// <param name="version">
    /// Новая версия, которую нужно зафиксировать в истории.
    /// </param>
    /// <remarks>
    /// После вызова <see cref="Commit"/> операция <see cref="Redo"/> становится недоступной,
    /// так как история ветвится и прежние redo-версии больше не применимы.
    /// </remarks>
    public void Commit(T version)
    {
        undoStack.Push(version);
        redoStack.Clear();
    }

    /// <summary>
    /// Откатывает историю на одну версию назад.
    ///
    /// <para>
    /// Текущая версия перемещается в redo-стек,
    /// а предыдущая версия становится текущей.
    /// </para>
    /// </summary>
    /// <returns>
    /// Предыдущая версия, которая становится текущей после отката.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Выбрасывается, если в истории нет предыдущих версий
    /// (то есть undo-стек содержит только одну версию).
    /// </exception>
    /// <remarks>
    /// Минимально в undo-стеке всегда должна быть хотя бы одна версия —
    /// исходное состояние структуры данных.
    /// </remarks>
    public T Undo()
    {
        if (undoStack.Count <= 1)
            throw new InvalidOperationException("Nothing to undo.");

        var current = undoStack.Pop();
        redoStack.Push(current);

        return undoStack.Peek();
    }

    /// <summary>
    /// Повторно применяет ранее отменённую версию.
    ///
    /// <para>
    /// Перемещает верхнюю версию из redo-стека обратно в undo-стек
    /// и делает её текущей.
    /// </para>
    /// </summary>
    /// <returns>
    /// Версия, которая становится текущей после выполнения redo.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Выбрасывается, если redo-стек пуст
    /// (то есть нет версий для повторного применения).
    /// </exception>
    public T Redo()
    {
        if (redoStack.Count == 0)
            throw new InvalidOperationException("Nothing to redo.");

        var version = redoStack.Pop();
        undoStack.Push(version);

        return version;
    }
}
