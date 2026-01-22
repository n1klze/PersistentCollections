using PersistentCollections;

namespace Test;

[TestClass]
public sealed class PersistentLinkedListTest
{
    [TestMethod]
    public void EmptyListHasCountZeroTest()
    {
        var list = PersistentLinkedList<int>.Empty();

        Assert.AreEqual(0, list.Count);
    }

    [TestMethod]
    public void AddFirstAddsElementToStartTest()
    {
        var list = PersistentLinkedList<int>.Empty();

        var v1 = list.AddFirst(10);
        var v2 = v1.AddFirst(20);

        Assert.AreEqual(2, v2.Count);
        Assert.AreEqual(20, v2[0]);
        Assert.AreEqual(10, v2[1]);
    }

    [TestMethod]
    public void AddLastAddsElementToEndTest()
    {
        var list = PersistentLinkedList<int>.Empty();

        var v1 = list.AddLast(10);
        var v2 = v1.AddLast(20);
        var v3 = v2.AddLast(30);

        Assert.AreEqual(3, v3.Count);
        Assert.AreEqual(10, v3[0]);
        Assert.AreEqual(20, v3[1]);
        Assert.AreEqual(30, v3[2]);
    }

    [TestMethod]
    public void OldVersionNotModifiedAfterAddLastTest()
    {
        var list = PersistentLinkedList<int>.Empty();

        var v1 = list.AddLast(1);
        var v2 = v1.AddLast(2);

        Assert.AreEqual(1, v1.Count);
        Assert.AreEqual(1, v1[0]);

        Assert.AreEqual(2, v2.Count);
        Assert.AreEqual(2, v2[1]);
    }

    [TestMethod]
    public void InsertInMiddleTest()
    {
        var list = PersistentLinkedList<int>.Empty().AddLast(10).AddLast(20).AddLast(30);

        var v2 = list.Insert(1, 999); // 10, 999, 20, 30

        Assert.AreEqual(4, v2.Count);
        Assert.AreEqual(10, v2[0]);
        Assert.AreEqual(999, v2[1]);
        Assert.AreEqual(20, v2[2]);
        Assert.AreEqual(30, v2[3]);
    }

    [TestMethod]
    public void RemoveAtRemovesElementInMiddleTest()
    {
        var list = PersistentLinkedList<int>.Empty().AddLast(10).AddLast(20).AddLast(30);

        var v2 = list.RemoveAt(1); // 10, 30

        Assert.AreEqual(2, v2.Count);
        Assert.AreEqual(10, v2[0]);
        Assert.AreEqual(30, v2[1]);
    }

    [TestMethod]
    public void RemoveFirstRemovesHeadTest()
    {
        var list = PersistentLinkedList<int>.Empty().AddLast(10).AddLast(20);

        var v2 = list.RemoveFirst();

        Assert.AreEqual(1, v2.Count);
        Assert.AreEqual(20, v2[0]);
    }

    [TestMethod]
    public void RemoveLastRemovesTailTest()
    {
        var list = PersistentLinkedList<int>.Empty().AddLast(10).AddLast(20);

        var v2 = list.RemoveLast();

        Assert.AreEqual(1, v2.Count);
        Assert.AreEqual(10, v2[0]);
    }

    [TestMethod]
    public void UndoRevertsToPreviousVersionTest()
    {
        var list = PersistentLinkedList<int>.Empty();

        var v1 = list.AddLast(10);
        var v2 = v1.AddLast(20);
        var v3 = v2.AddLast(30);

        var undo1 = v3.Undo(); // v2
        var undo2 = undo1.Undo(); // v1

        Assert.AreEqual(2, undo1.Count);
        Assert.AreEqual(20, undo1[1]);

        Assert.AreEqual(1, undo2.Count);
        Assert.AreEqual(10, undo2[0]);
    }

    [TestMethod]
    public void RedoReappliesUndoneVersionTest()
    {
        var list = PersistentLinkedList<int>.Empty();

        var v1 = list.AddLast(10);
        var v2 = v1.AddLast(20);
        var v3 = v2.AddLast(30);

        var undo = v3.Undo(); // v2
        var redo = undo.Redo(); // v3

        Assert.AreEqual(3, redo.Count);
        Assert.AreEqual(30, redo[2]);
    }

    [TestMethod]
    public void BranchingVersionsAreIndependentTest()
    {
        var baseList = PersistentLinkedList<string>.Empty();

        var ver1 = baseList.AddLast("A");
        var ver2 = ver1.AddLast("B");

        var branch1 = ver2.AddLast("C"); // [A, B, C]
        var branch2 = ver2.AddLast("X"); // [A, B, X]

        Assert.AreEqual("C", branch1[2]);
        Assert.AreEqual("X", branch2[2]);
        Assert.AreEqual(2, ver2.Count);
    }

    [TestMethod]
    public void NestedPersistentLinkedListUndoIsCascadingTest()
    {
        var inner = PersistentLinkedList<int>.Empty().AddLast(1).AddLast(2);

        var outer = PersistentLinkedList<PersistentLinkedList<int>>.Empty().AddLast(inner);

        var inner2 = inner.AddLast(3);
        var outer2 = outer.Insert(1, inner2);

        Assert.AreEqual(3, outer2[1].Count);

        var outerUndo = outer2.Undo();

        Assert.AreEqual(2, outerUndo[0].Count);
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentOutOfRangeException))]
    public void GetOutOfRangeThrowsTest()
    {
        var list = PersistentLinkedList<int>.Empty().AddLast(1);

        var _ = list[1];
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentOutOfRangeException))]
    public void InsertOutOfRangeThrowsTest()
    {
        var list = PersistentLinkedList<int>.Empty().AddLast(1);

        list.Insert(2, 999);
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentOutOfRangeException))]
    public void RemoveAtOutOfRangeThrowsTest()
    {
        var list = PersistentLinkedList<int>.Empty().AddLast(1);

        list.RemoveAt(1);
    }

    [TestMethod]
    [ExpectedException(typeof(InvalidOperationException))]
    public void RemoveFirstOnEmptyThrowsTest()
    {
        var list = PersistentLinkedList<int>.Empty();

        list.RemoveFirst();
    }

    [TestMethod]
    [ExpectedException(typeof(InvalidOperationException))]
    public void RemoveLastOnEmptyThrowsTest()
    {
        var list = PersistentLinkedList<int>.Empty();

        list.RemoveLast();
    }

    [TestMethod]
    public void AddManyElementsTest()
    {
        var list = PersistentLinkedList<int>.Empty();
        var current = list;

        for (int i = 0; i < 1000; i++)
        {
            current = current.AddLast(i);
        }

        Assert.AreEqual(1000, current.Count);
        Assert.AreEqual(0, current[0]);
        Assert.AreEqual(500, current[500]);
        Assert.AreEqual(999, current[999]);
    }

    [TestMethod]
    [ExpectedException(typeof(InvalidOperationException))]
    public void RedoIsClearedAfterNewOperationTest()
    {
        var list = PersistentLinkedList<int>.Empty();

        var v1 = list.AddLast(1);
        var v2 = v1.AddLast(2);

        var undo = v2.Undo(); // v1

        var v3 = undo.AddLast(3);

        undo.Redo();
    }
}
