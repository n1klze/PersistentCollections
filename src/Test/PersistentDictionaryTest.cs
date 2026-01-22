using PersistentCollections;

namespace Test;

[TestClass]
public sealed class PersistentDictionaryTest
{
    [TestMethod]
    public void EmptyDictionaryHasCountZeroTest()
    {
        var dict = PersistentDictionary<string, int>.Empty();

        Assert.AreEqual(0, dict.Count);
    }

    [TestMethod]
    public void PutAddsNewKeyTest()
    {
        var dict = PersistentDictionary<string, int>.Empty();

        var v1 = dict.Put("a", 1);

        Assert.AreEqual(1, v1.Count);
        Assert.IsTrue(v1.TryGetValue("a", out var value));
        Assert.AreEqual(1, value);
    }

    [TestMethod]
    public void PutUpdatesExistingKeyTest()
    {
        var dict = PersistentDictionary<string, int>.Empty();

        var v1 = dict.Put("a", 1);
        var v2 = v1.Put("a", 999);

        Assert.AreEqual(1, v2.Count);
        Assert.IsTrue(v2.TryGetValue("a", out var value));
        Assert.AreEqual(999, value);

        // старая версия не изменилась
        Assert.IsTrue(v1.TryGetValue("a", out var oldValue));
        Assert.AreEqual(1, oldValue);
    }

    [TestMethod]
    public void OldVersionNotModifiedAfterPutTest()
    {
        var dict = PersistentDictionary<string, int>.Empty();

        var v1 = dict.Put("a", 1);
        var v2 = v1.Put("b", 2);

        Assert.AreEqual(1, v1.Count);
        Assert.IsFalse(v1.TryGetValue("b", out _));

        Assert.AreEqual(2, v2.Count);
        Assert.IsTrue(v2.TryGetValue("b", out var value));
        Assert.AreEqual(2, value);
    }

    [TestMethod]
    public void RemoveDeletesKeyTest()
    {
        var dict = PersistentDictionary<string, int>.Empty();

        var v1 = dict.Put("a", 1).Put("b", 2);
        var v2 = v1.Remove("a");

        Assert.AreEqual(1, v2.Count);
        Assert.IsFalse(v2.TryGetValue("a", out _));
        Assert.IsTrue(v2.TryGetValue("b", out var value));
        Assert.AreEqual(2, value);

        // старая версия не изменилась
        Assert.IsTrue(v1.TryGetValue("a", out var oldValue));
        Assert.AreEqual(1, oldValue);
    }

    [TestMethod]
    public void RemoveNonexistentKeyReturnsSameVersionTest()
    {
        var dict = PersistentDictionary<string, int>.Empty();

        var v1 = dict.Put("a", 1);
        var v2 = v1.Remove("x");

        Assert.AreSame(v1, v2);
    }

    [TestMethod]
    public void UndoRevertsToPreviousVersionTest()
    {
        var dict = PersistentDictionary<string, int>.Empty();

        var v1 = dict.Put("a", 1);
        var v2 = v1.Put("b", 2);
        var v3 = v2.Put("c", 3);

        var undo1 = v3.Undo(); // v2
        var undo2 = undo1.Undo(); // v1

        Assert.AreEqual(2, undo1.Count);
        Assert.IsTrue(undo1.TryGetValue("b", out var v));
        Assert.AreEqual(2, v);

        Assert.AreEqual(1, undo2.Count);
        Assert.IsTrue(undo2.TryGetValue("a", out var a));
        Assert.AreEqual(1, a);
    }

    [TestMethod]
    public void RedoReappliesUndoneVersionTest()
    {
        var dict = PersistentDictionary<string, int>.Empty();

        var v1 = dict.Put("a", 1);
        var v2 = v1.Put("b", 2);
        var v3 = v2.Put("c", 3);

        var undo = v3.Undo(); // v2
        var redo = undo.Redo(); // v3

        Assert.AreEqual(3, redo.Count);
        Assert.IsTrue(redo.TryGetValue("c", out var value));
        Assert.AreEqual(3, value);
    }

    [TestMethod]
    public void BranchingVersionsAreIndependentTest()
    {
        var baseDict = PersistentDictionary<string, int>.Empty();

        var v1 = baseDict.Put("a", 1);
        var v2 = v1.Put("b", 2);

        var branch1 = v2.Put("c", 3);
        var branch2 = v2.Put("c", 999);

        Assert.AreEqual(3, branch1.Count);
        Assert.AreEqual(3, branch2.Count);

        Assert.IsTrue(branch1.TryGetValue("c", out var v1c));
        Assert.IsTrue(branch2.TryGetValue("c", out var v2c));

        Assert.AreEqual(3, v1c);
        Assert.AreEqual(999, v2c);

        Assert.IsFalse(v2.TryGetValue("c", out _));
    }

    private sealed class BadHash
    {
        public string Value { get; }

        public BadHash(string value)
        {
            Value = value;
        }

        public override int GetHashCode() => 42;

        public override bool Equals(object? obj) => obj is BadHash other && Value == other.Value;
    }

    [TestMethod]
    public void HandlesHashCollisionsTest()
    {
        var dict = PersistentDictionary<BadHash, int>.Empty();

        var k1 = new BadHash("a");
        var k2 = new BadHash("b");
        var k3 = new BadHash("c");

        var v1 = dict.Put(k1, 1);
        var v2 = v1.Put(k2, 2);
        var v3 = v2.Put(k3, 3);

        Assert.AreEqual(3, v3.Count);

        Assert.IsTrue(v3.TryGetValue(k1, out var v1v));
        Assert.IsTrue(v3.TryGetValue(k2, out var v2v));
        Assert.IsTrue(v3.TryGetValue(k3, out var v3v));

        Assert.AreEqual(1, v1v);
        Assert.AreEqual(2, v2v);
        Assert.AreEqual(3, v3v);
    }

    [TestMethod]
    public void NestedPersistentDictionaryUndoIsCascadingTest()
    {
        var inner = PersistentDictionary<string, int>.Empty().Put("x", 1).Put("y", 2);

        var outer = PersistentDictionary<string, PersistentDictionary<string, int>>
            .Empty()
            .Put("inner", inner);

        var inner2 = inner.Put("z", 3);
        var outer2 = outer.Put("inner", inner2);

        Assert.AreEqual(3, outer2["inner"].Count);

        var outerUndo = outer2.Undo();

        Assert.AreEqual(2, outerUndo["inner"].Count);
    }

    [TestMethod]
    public void PutManyElementsTest()
    {
        var dict = PersistentDictionary<int, int>.Empty();
        var current = dict;

        for (int i = 0; i < 2000; i++)
        {
            current = current.Put(i, i * 10);
        }

        Assert.AreEqual(2000, current.Count);
        Assert.IsTrue(current.TryGetValue(0, out var v0));
        Assert.IsTrue(current.TryGetValue(1500, out var v1500));
        Assert.AreEqual(0, v0);
        Assert.AreEqual(15000, v1500);
    }

    [TestMethod]
    [ExpectedException(typeof(InvalidOperationException))]
    public void RedoIsClearedAfterNewOperationTest()
    {
        var dict = PersistentDictionary<string, int>.Empty();

        var v1 = dict.Put("a", 1);
        var v2 = v1.Put("b", 2);

        var undo = v2.Undo(); // v1

        var v3 = undo.Put("c", 3);

        undo.Redo();
    }
}
