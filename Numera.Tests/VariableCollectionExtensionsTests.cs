using System.Collections;

namespace Numera.Tests;

[TestClass]
public sealed class VariableCollectionExtensionsTests
{
    private static Variable V(string name, decimal value, string? definition = null) =>
        new(name, new MathValue(value), expression: null, definition);

    private static string[] Names(VariableCollection c) => c.Keys.ToArray();

    private static decimal[] Decimals(VariableCollection c) =>
        c.Values.Select(v => v.DecimalValue).ToArray();

    // A sequence that is not an ICollection and records how many times it is enumerated,
    // to verify that the extension methods do not enumerate more than once.
    private sealed class CountingSequence(IReadOnlyList<Variable> items) : IEnumerable<Variable>
    {
        public int EnumerationCount { get; private set; }

        public IEnumerator<Variable> GetEnumerator()
        {
            EnumerationCount++;
            return items.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    [TestMethod]
    public void ToVariableCollection_NullReceiver_ThrowsArgumentNullException()
    {
        IEnumerable<Variable> source = null!;
        var ex = Assert.Throws<ArgumentNullException>(() => source.ToVariableCollection());
        Assert.AreEqual("source", ex.ParamName);
    }

    [TestMethod]
    public void ToVariableCollection_EmptySequences_ReturnSharedEmptyInstance()
    {
        Assert.AreSame(VariableCollection.Empty, new List<Variable>().ToVariableCollection());
        Assert.AreSame(VariableCollection.Empty, Array.Empty<Variable>().ToVariableCollection());
        Assert.AreSame(VariableCollection.Empty, Enumerable.Empty<Variable>().ToVariableCollection());
        Assert.AreSame(VariableCollection.Empty, new CountingSequence([]).ToVariableCollection());
    }

    [TestMethod]
    public void ToVariableCollection_PreservesSortOrderAndMetadata()
    {
        var source = new List<Variable>
        {
            V("a", 1m, "first"),
            V("b", 2m),
            V("c", 3m)
        };

        var vars = source.ToVariableCollection();

        Assert.AreSequenceEqual(["a", "b", "c"], Names(vars));
        Assert.AreSequenceEqual([1m, 2m, 3m], Decimals(vars));
        Assert.IsTrue(vars.TryGetVariable("A", out var first));
        Assert.AreEqual("first", first.Definition);
    }

    [TestMethod]
    public void ToVariableCollection_DuplicateNames_LastWinsAndMovesToEnd()
    {
        var vars = new[] { V("a", 1m), V("b", 2m), V("A", 10m) }
            .ToVariableCollection();

        Assert.AreSequenceEqual(["b", "A"], Names(vars));
        Assert.AreSequenceEqual([2m, 10m], Decimals(vars));
    }

    [TestMethod]
    public void ToVariableCollection_UncountedSequence_IsEnumeratedOnce()
    {
        var source = new CountingSequence([V("a", 1m), V("b", 2m)]);
        var vars = source.ToVariableCollection();

        Assert.AreEqual(1, source.EnumerationCount);
        Assert.HasCount(2, vars);
    }

    [TestMethod]
    public void ToVariableCollection_ChainsFromLinqProjection()
    {
        var constants = new Dictionary<string, decimal>
        {
            ["rate"] = 0.05m, ["amount"] = 1000m
        };

        var vars = constants
            .Select(kvp => new Variable(kvp.Key, kvp.Value))
            .ToVariableCollection();

        Assert.HasCount(2, vars);
        Assert.AreEqual(0.05m, vars["RATE"].DecimalValue);
        Assert.AreEqual(1000m, vars["amount"].DecimalValue);
    }

    [TestMethod]
    public void ToVariableCollection_DefaultVariable_ThrowsArgumentExceptionNamingSource()
    {
        var source = new[] { V("x", 1m), default };

        var ex = Assert.Throws<ArgumentException>(() => source.ToVariableCollection());

        Assert.AreEqual("source", ex.ParamName);
    }

    [TestMethod]
    public void ToVariableCollection_IsEquivalentToCreateRange()
    {
        var source = new List<Variable> { V("x", 1m), V("y", 2m), V("X", 3m) };

        var viaExtension = source.ToVariableCollection();
        var viaFactory = VariableCollection.CreateRange(source);

        Assert.AreSequenceEqual(Names(viaFactory), Names(viaExtension));
        Assert.AreSequenceEqual(Decimals(viaFactory), Decimals(viaExtension));
    }
}
