namespace Numera.Tests;

/// <summary>
/// Unit tests for <see cref="VariableCollection"/>, verifying ordering, MRU update semantics,
/// immutability, indexers, and case-insensitivity.
/// </summary>
[TestClass]
public sealed class VariableCollectionTests
{
    [TestMethod]
    public void Empty_HasZeroCountAndEmptyEnumerations()
    {
        var empty = VariableCollection.Empty;

        Assert.AreEqual(0, empty.Count);
        Assert.IsFalse(empty.Keys.Any());
        Assert.IsFalse(empty.Values.Any());
        Assert.IsFalse(empty.ContainsKey("x"));
        Assert.IsFalse(empty.TryGetValue("x", out _));
        Assert.Throws<KeyNotFoundException>(() => _ = empty["x"]);
        Assert.Throws<IndexOutOfRangeException>(() => _ = empty[0]);
    }

    [TestMethod]
    public void Add_AppendsInChronologicalOrder()
    {
        var vars = VariableCollection.Empty
            .Add("a", new MathValue(1m))
            .Add("b", new MathValue(2m))
            .Add("c", new MathValue(3m));

        Assert.AreEqual(3, vars.Count);
        CollectionAssert.AreEqual(new[] { "a", "b", "c" }, vars.Keys.ToArray());
        CollectionAssert.AreEqual(new[] { 1m, 2m, 3m }, vars.Values.Select(v => v.DecVal).ToArray());

        Assert.AreEqual("a", vars[0].Key);
        Assert.AreEqual(1m, vars[0].Value.DecVal);
        Assert.AreEqual("b", vars[1].Key);
        Assert.AreEqual(2m, vars[1].Value.DecVal);
        Assert.AreEqual("c", vars[2].Key);
        Assert.AreEqual(3m, vars[2].Value.DecVal);
    }

    [TestMethod]
    public void Add_ExistingKeyMovesToEndWithUpdatedValue()
    {
        var vars = VariableCollection.Empty
            .Add("a", new MathValue(1m))
            .Add("b", new MathValue(2m))
            .Add("c", new MathValue(3m));

        // Re-adding "a" (case-insensitively) should update its value and move it to the end (MRU order).
        var updated = vars.Add("A", new MathValue(10m));

        Assert.AreEqual(3, updated.Count);
        CollectionAssert.AreEqual(new[] { "b", "c", "A" }, updated.Keys.ToArray());
        CollectionAssert.AreEqual(new[] { 2m, 3m, 10m }, updated.Values.Select(v => v.DecVal).ToArray());
        Assert.AreEqual(10m, updated["a"].DecVal);
        Assert.AreEqual(10m, updated["A"].DecVal);

        // Original remains unchanged.
        Assert.AreEqual(1m, vars["a"].DecVal);
        CollectionAssert.AreEqual(new[] { "a", "b", "c" }, vars.Keys.ToArray());
    }

    [TestMethod]
    public void SetItems_MergesAndPreservesOrderWithReassignmentsAtEnd()
    {
        var initial = VariableCollection.Empty
            .Add("x", new MathValue(10m))
            .Add("y", new MathValue(20m))
            .Add("z", new MathValue(30m));

        var updates = new[]
        {
            new KeyValuePair<string, MathValue>("X", new MathValue(100m)),
            new KeyValuePair<string, MathValue>("w", new MathValue(40m)),
        };

        var next = initial.SetItems(updates);

        Assert.AreEqual(4, next.Count);
        // "x" was updated, so it moves to after "z"; then "w" is added.
        CollectionAssert.AreEqual(new[] { "y", "z", "X", "w" }, next.Keys.ToArray());
        Assert.AreEqual(100m, next["x"].DecVal);
        Assert.AreEqual(20m, next["y"].DecVal);
        Assert.AreEqual(30m, next["z"].DecVal);
        Assert.AreEqual(40m, next["w"].DecVal);
    }

    [TestMethod]
    public void Constructor_WithDuplicates_RetainsLastOccurrenceInChronologicalOrder()
    {
        var pairs = new[]
        {
            new KeyValuePair<string, MathValue>("alpha", new MathValue(1m)),
            new KeyValuePair<string, MathValue>("beta", new MathValue(2m)),
            new KeyValuePair<string, MathValue>("ALPHA", new MathValue(10m)),
        };

        var collection = new VariableCollection(pairs);

        Assert.AreEqual(2, collection.Count);
        CollectionAssert.AreEqual(new[] { "beta", "ALPHA" }, collection.Keys.ToArray());
        Assert.AreEqual(10m, collection["alpha"].DecVal);
        Assert.AreEqual(2m, collection["beta"].DecVal);
    }

    [TestMethod]
    public void Indexers_ThrowWhenOutOfRangeOrNotFound()
    {
        var vars = VariableCollection.Empty.Add("x", new MathValue(5m));

        Assert.Throws<IndexOutOfRangeException>(() => _ = vars[-1]);
        Assert.Throws<IndexOutOfRangeException>(() => _ = vars[1]);
        Assert.Throws<KeyNotFoundException>(() => _ = vars["nonexistent"]);
    }
}
