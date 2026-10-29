namespace Numera.Tests;

/// <summary>
/// Unit tests for <see cref="VariableCollection"/>, verifying ordering, MRU update
/// semantics, immutability, indexers, and case-insensitivity.
/// </summary>
[TestClass]
public sealed class VariableCollectionTests
{
    [TestMethod]
    public void Empty_HasZeroCountAndEmptyEnumerations()
    {
        var empty = VariableCollection.Empty;

        Assert.AreEqual(0, empty.Count);
        Assert.IsEmpty(empty.Keys);
        Assert.IsEmpty(empty.Values);
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
        Assert.AreSequenceEqual(["a", "b", "c"], vars.Keys.ToArray());
        Assert.AreSequenceEqual([1m, 2m, 3m], vars.Values.Select(v => v.DecVal).ToArray());

        Assert.AreEqual("a", vars[0].Name);
        Assert.AreEqual(1m, vars[0].Value.DecVal);
        Assert.AreEqual("b", vars[1].Name);
        Assert.AreEqual(2m, vars[1].Value.DecVal);
        Assert.AreEqual("c", vars[2].Name);
        Assert.AreEqual(3m, vars[2].Value.DecVal);
    }

    [TestMethod]
    public void Add_ExistingKeyMovesToEndWithUpdatedValue()
    {
        var vars = VariableCollection.Empty
            .Add("a", new MathValue(1m))
            .Add("b", new MathValue(2m))
            .Add("c", new MathValue(3m));

        // Re-adding "a" (case-insensitively) should update its value and move it to the
        // end (MRU order).
        var updated = vars.Add("A", new MathValue(10m));

        Assert.AreEqual(3, updated.Count);
        Assert.AreSequenceEqual(["b", "c", "A"], updated.Keys.ToArray());
        Assert.AreSequenceEqual([2m, 3m, 10m], updated.Values.Select(v => v.DecVal).ToArray());
        Assert.AreEqual(10m, updated["a"].DecVal);
        Assert.AreEqual(10m, updated["A"].DecVal);

        // Original remains unchanged.
        Assert.AreEqual(1m, vars["a"].DecVal);
        Assert.AreSequenceEqual(["a", "b", "c"], vars.Keys.ToArray());
    }

    [TestMethod]
    public void SetItems_MergesAndPreservesOrderWithReassignmentsAtEnd()
    {
        var initial = VariableCollection.Empty
            .Add("x", new MathValue(10m))
            .Add("y", new MathValue(20m))
            .Add("z", new MathValue(30m));

        var updates = new Variable[]
        {
            new("X", new MathValue(100m), "2 * 50"),
            new("w", new MathValue(40m)),
        };

        var next = initial.SetItems(updates);

        Assert.AreEqual(4, next.Count);
        // "x" was updated, so it moves to after "z"; then "w" is added.
        Assert.AreSequenceEqual(["y", "z", "X", "w"], next.Keys.ToArray());
        Assert.AreEqual(100m, next["x"].DecVal);
        Assert.AreEqual(20m, next["y"].DecVal);
        Assert.AreEqual(30m, next["z"].DecVal);
        Assert.AreEqual(40m, next["w"].DecVal);

        // Metadata preservation
        Assert.IsTrue(next.TryGetVariable("x", out var xVar));
        Assert.AreEqual("2 * 50", xVar.Definition);
    }

    [TestMethod]
    public void Constructor_WithDuplicates_RetainsLastOccurrenceInChronologicalOrder()
    {
        var vars = new Variable[]
        {
            new("alpha", new MathValue(1m), "init alpha"),
            new("beta", new MathValue(2m)),
            new("ALPHA", new MathValue(10m), "updated alpha"),
        };

        var collection = new VariableCollection(vars);

        Assert.AreEqual(2, collection.Count);
        Assert.AreSequenceEqual(["beta", "ALPHA"], collection.Keys.ToArray());
        Assert.AreEqual(10m, collection["alpha"].DecVal);
        Assert.AreEqual(2m, collection["beta"].DecVal);
        Assert.IsTrue(collection.TryGetVariable("alpha", out var alphaVar));
        Assert.AreEqual("updated alpha", alphaVar.Definition);
    }

    [TestMethod]
    public void Indexers_ThrowWhenOutOfRangeOrNotFound()
    {
        var vars = VariableCollection.Empty.Add("x", new MathValue(5m));

        Assert.Throws<IndexOutOfRangeException>(() => _ = vars[-1]);
        Assert.Throws<IndexOutOfRangeException>(() => _ = vars[1]);
        Assert.Throws<KeyNotFoundException>(() => _ = vars["nonexistent"]);
    }

    [TestMethod]
    public void Enumeration_YieldsFullVariableInstancesPreservingMetadata()
    {
        var vars = new VariableCollection(
        [
            new Variable("a", new MathValue(1m), "definition a"),
            new Variable("b", new MathValue(2m), "definition b")
        ]);

        var list = vars.ToList();
        Assert.HasCount(2, list);
        Assert.AreEqual("a", list[0].Name);
        Assert.AreEqual(1m, list[0].Value.DecVal);
        Assert.AreEqual("definition a", list[0].Definition);

        Assert.AreEqual("b", list[1].Name);
        Assert.AreEqual(2m, list[1].Value.DecVal);
        Assert.AreEqual("definition b", list[1].Definition);

        // Deconstruction test.
        var (name, val, def) = vars[0];
        Assert.AreEqual("a", name);
        Assert.AreEqual(1m, val.DecVal);
        Assert.AreEqual("definition a", def);
    }
}
