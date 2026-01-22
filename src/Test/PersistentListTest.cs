using PersistentCollections;

namespace Test;

[TestClass]
public sealed class PersistentListTest
{
    [TestMethod]
    public void EmptyListHasCountZeroTest()
    {
        var list = PersistentList<int>.Empty();

        Assert.AreEqual(0, list.Count);
    }

    [TestMethod]
    public void AppendTest()
    {
        var list = PersistentList<int>.Empty();

        var v1 = list.Append(10);
        var v2 = v1.Append(20);
        var v3 = v2.Append(30);

        Assert.AreEqual(3, v3.Count);
        Assert.AreEqual(10, v3[0]);
        Assert.AreEqual(20, v3[1]);
        Assert.AreEqual(30, v3[2]);
    }

    [TestMethod]
    public void OldVersionNotModifiedAfterAppendTest()
    {
        var list = PersistentList<int>.Empty();

        var v1 = list.Append(1);
        var v2 = v1.Append(2);

        Assert.AreEqual(1, v1.Count);
        Assert.AreEqual(1, v1[0]);

        Assert.AreEqual(2, v2.Count);
        Assert.AreEqual(2, v2[1]);
    }

    [TestMethod]
    public void SetChangesElementInNewVersionOnlyTest()
    {
        var list = PersistentList<int>.Empty();

        var v1 = list.Append(10).Append(20).Append(30);
        var v2 = v1.Set(1, 999);

        Assert.AreEqual(20, v1[1]); // старая версия
        Assert.AreEqual(999, v2[1]); // новая версия
    }

    [TestMethod]
    public void UndoRevertsToPreviousVersionTest()
    {
        var list = PersistentList<int>.Empty();

        var v1 = list.Append(10);
        var v2 = v1.Append(20);
        var v3 = v2.Append(30);

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
        var list = PersistentList<int>.Empty();

        var v1 = list.Append(10);
        var v2 = v1.Append(20);
        var v3 = v2.Append(30);

        var undo = v3.Undo(); // v2
        var redo = undo.Redo(); // v3

        Assert.AreEqual(3, redo.Count);
        Assert.AreEqual(30, redo[2]);
    }

    [TestMethod]
    public void BranchingVersionsAreIndependentTest()
    {
        var baseList = PersistentList<string>.Empty();

        var ver1 = baseList.Append("A");
        var ver2 = ver1.Append("B");

        var branch1 = ver2.Append("C"); // [A, B, C]
        var branch2 = ver2.Append("X"); // [A, B, X]

        Assert.AreEqual("C", branch1[2]);
        Assert.AreEqual("X", branch2[2]);
        Assert.AreEqual(2, ver2.Count);
    }

    [TestMethod]
    public void NestedPersistentListUndoIsCascadingTest()
    {
        var inner = PersistentList<int>.Empty().Append(1).Append(2);

        var outer = PersistentList<PersistentList<int>>.Empty().Append(inner);

        var inner2 = inner.Append(3);
        var outer2 = outer.Set(0, inner2);

        Assert.AreEqual(3, outer2[0].Count);

        var outerUndo = outer2.Undo();

        Assert.AreEqual(2, outerUndo[0].Count);
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentOutOfRangeException))]
    public void GetOutOfRangeThrowsTest()
    {
        var list = PersistentList<int>.Empty().Append(1);

        var _ = list[1];
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentOutOfRangeException))]
    public void SetOutOfRangeThrowsTest()
    {
        var list = PersistentList<int>.Empty().Append(1);

        list.Set(1, 999);
    }

    [TestMethod]
    public void AppendManyElementsTest()
    {
        var list = PersistentList<int>.Empty();

        var current = list;

        for (int i = 0; i < 1000; i++)
        {
            current = current.Append(i);
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
        var list = PersistentList<int>.Empty();

        var v1 = list.Append(1);
        var v2 = v1.Append(2);

        var undo = v2.Undo(); // v1

        var v3 = undo.Append(3); // новая ветка

        // redo больше не должно работать
        undo.Redo();
    }
}
