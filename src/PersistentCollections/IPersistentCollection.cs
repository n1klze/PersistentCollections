public interface IPersistentCollection<TSelf>
    where TSelf : IPersistentCollection<TSelf>
{
    TSelf Snapshot();
    TSelf Undo();
    TSelf Redo();
}
