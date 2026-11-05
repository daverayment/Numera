using System.Collections;
using System.Globalization;
using Numera.Ast;

namespace Numera.Tests;

/// <summary>
/// Unit tests for <see cref="VariableCollection"/>, verifying ordering, MRU update
/// semantics, immutability, indexers, and case-insensitivity.
/// </summary>
[TestClass]
public sealed class VariableCollectionTests
{
    private static Variable V(string name, decimal value, string? definition = null) =>
        new(name, new MathValue(value), expression: null, definition);

    private static string[] Names(VariableCollection c) => c.Keys.ToArray();

    private static decimal[] Decimals(VariableCollection c) =>
        c.Values.Select(v => v.DecimalValue).ToArray();

    /// A sequence that is neither an ICollection nor counted, and records how often it is
    /// walked.
    private sealed class CountingSequence(IReadOnlyList<Variable> items) :
        IEnumerable<Variable>
    {
        public int EnumerationCount { get; private set; }

        public IEnumerator<Variable> GetEnumerator()
        {
            EnumerationCount++;
            return items.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    // Creation.

    [TestMethod]
    public void EmptyCollectionExpression_ReturnsSharedEmptyInstance()
    {
        VariableCollection vars = [];
        Assert.AreSame(VariableCollection.Empty, vars);
    }

    [TestMethod]
    public void Create_EmptySpan_ReturnsSharedEmptyInstance()
    {
        Assert.AreSame(VariableCollection.Empty, VariableCollection.Create( [] ));
    }

    [TestMethod]
    public void CollectionExpression_SupportsTupleAndSpread()
    {
        VariableCollection a = [("x", 1m), ("y", 2m)];
        VariableCollection b = [.. a, ("z", 3m)];

        Assert.AreSequenceEqual(["x", "y"], Names(a));
        Assert.AreSequenceEqual([1m, 2m], Decimals(a));
        Assert.AreSequenceEqual(["x", "y", "z"], Names(b));
    }

    [TestMethod]
    public void Create_DefaultVariable_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(
            () => VariableCollection.Create(new[] { V("a", 1m), default }));
        Assert.AreEqual("items", ex.ParamName);
    }

    [TestMethod]
    public void CreateRange_NullReceiver_ThrowsArgumentNullException()
    {
        IEnumerable<Variable> source = null!;
        var ex = Assert.Throws<ArgumentNullException>(() => VariableCollection.CreateRange(source));
        Assert.AreEqual("source", ex.ParamName);
    }

    [TestMethod]
    public void CreateRange_EmptySequences_ReturnSharedEmptyInstance()
    {
        Assert.AreSame(VariableCollection.Empty, VariableCollection.CreateRange(new List<Variable>()));
        Assert.AreSame(VariableCollection.Empty, VariableCollection.CreateRange(Array.Empty<Variable>()));
        Assert.AreSame(VariableCollection.Empty, VariableCollection.CreateRange(Enumerable.Empty<Variable>()));
        Assert.AreSame(VariableCollection.Empty, VariableCollection.CreateRange(new CountingSequence([])));
    }

    [TestMethod]
    public void CreateRange_UncountedSequence_IsEnumeratedOnceWithLastDuplicateWinning()
    {
        var source = new CountingSequence([V("a", 1m), V("b", 2m), V("A", 10m)]);

        var vars = VariableCollection.CreateRange(source);

        Assert.AreEqual(1, source.EnumerationCount);
        Assert.AreSequenceEqual(["b", "A"], Names(vars));
        Assert.AreSequenceEqual([2m, 10m], Decimals(vars));
    }

    [TestMethod]
    public void CreateRange_DefaultVariable_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            VariableCollection.CreateRange(new List<Variable> { default }));
        Assert.AreEqual("source", ex.ParamName);
    }

    // Lookup.

    [TestMethod]
    public void Lookup_IsCaseInsensitiveAcrossAllEntryPoints()
    {
        VariableCollection vars = [V("Total", 5m)];

        Assert.AreEqual(5m, vars["TOTAL"].DecimalValue);
        Assert.IsTrue(vars.ContainsKey("total"));
        Assert.IsTrue(vars.TryGetVariable("ToTaL", out var v));
        Assert.AreEqual(5m, v.DecimalValue);
        Assert.IsTrue(vars.TryGetVariable("TOTAL", out var variable));
        Assert.AreEqual("Total", variable.Name); // stored casing is preserved
    }

    [TestMethod]
    public void Lookup_IsCultureInvariant_UnderTurkishCulture()
    {
        var originalCulture = Thread.CurrentThread.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("tr-TR");
            VariableCollection vars = [V("title", 1m)];

            // Culture-sensitive comparison under tr-TR would NOT match "TITLE" to "title"
            // because of the dotted/dotless I, but our collection is culture-invariant.
            Assert.IsTrue(vars.ContainsKey("TITLE"));
            Assert.IsFalse(vars.ContainsKey("T\u0130TLE")); // dotted I is different
            Assert.IsFalse(vars.ContainsKey("t\u0131tle")); // dotless i is different
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [TestMethod]
    public void TryGetValueAndTryGetVariable_WhenMissing_ReturnFalseWithDefaults()
    {
        VariableCollection vars = [V("x", 1m)];

        Assert.IsFalse(vars.TryGetVariable("missing", out _));
        Assert.IsFalse(vars.TryGetVariable("missing", out var variable));
        Assert.IsNull(variable.Name);
        Assert.AreEqual(default, variable);

        Assert.IsFalse(VariableCollection.Empty.TryGetVariable("x", out _));
    }

    [TestMethod]
    public void TryGetVariable_PreservesDefinitionAndExpression()
    {
        var node = new NumberNode(1);
        var vars = VariableCollection.Empty.SetItem(
            new Variable("x", new MathValue(1m), node, "1"));

        Assert.IsTrue(vars.TryGetVariable("x", out var variable));
        Assert.AreEqual("1", variable.Definition);
        Assert.AreSame(node, variable.Expression);
    }

    [TestMethod]
    public void IntIndexer_SupportsFromEndIndexForMostRecent()
    {
        VariableCollection vars = [V("a", 1m), V("b", 2m), V("c", 3m)];
        var updated = vars.SetItem("a", new MathValue(10m));

        Assert.AreEqual("c", vars[^1].Name);
        Assert.AreEqual("a", updated[^1].Name);
        Assert.AreEqual(10m, updated[^1].Value.DecimalValue);
    }

    [TestMethod]
    public void KeysAndValues_StayAlignedWithEnumerationOrderAfterUpdates()
    {
        var vars = VariableCollection.Empty
            .SetItem("a", new MathValue(1m))
            .SetItem("b", new MathValue(2m))
            .SetItem("A", new MathValue(3m));

        Assert.AreSequenceEqual(["b", "A"], Names(vars));
        Assert.AreSequenceEqual([2m, 3m], vars.Select(v => v.DecimalValue).ToArray());
        Assert.AreSequenceEqual(["b", "A"], vars.Select(v => v.Name).ToArray());
    }

    [TestMethod]
    public void Collection_SupportsReadOnlyListAndNonGenericEnumeration()
    {
        VariableCollection vars = [V("a", 1m), ("b", 2m)];

        IReadOnlyList<Variable> list = vars;
        Assert.AreEqual(2, list.Count);
        Assert.AreEqual("b", list[1].Name);

        var names = new List<string>();
        foreach (object item in (IEnumerable)vars)
        {
            names.Add(((Variable)item).Name);
        }

        Assert.AreSequenceEqual(["a", "b"], names.ToArray());
    }

    [TestMethod]
    public void ToString_EmptyAndPopulated()
    {
        Assert.AreEqual("(empty)", VariableCollection.Empty.ToString());

        VariableCollection vars = [V("a", 1m, "one"), V("b", 2m)];
        Assert.AreEqual($"{vars[0]}, {vars[1]}", vars.ToString());
    }

    // SetItem.

    [TestMethod]
    public void SetItem_OnEmpty_NeverMutatesSharedEmptyInstance()
    {
        var result = VariableCollection.Empty.SetItem("x", new MathValue(5m));

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual(0, VariableCollection.Empty.Count);
        Assert.AreNotSame(VariableCollection.Empty, result);
    }

    [TestMethod]
    public void SetItem_Replacement_IsWholesaleAndDropsOldMetadata()
    {
        var node = new NumberNode(1);
        var vars = VariableCollection.Empty.SetItem(new Variable("x", new MathValue(1m), node, "old"));

        var updated = vars.SetItem("X", new MathValue(2m));

        Assert.IsTrue(updated.TryGetVariable("x", out var variable));
        Assert.AreEqual("X", variable.Name);
        Assert.IsNull(variable.Definition);
        Assert.IsNull(variable.Expression);
    }

    [TestMethod]
    public void SetItem_StringOverload_MatchesVariableOverload()
    {
        var a = VariableCollection.Empty.SetItem("x", new MathValue(5m));
        var b = VariableCollection.Empty.SetItem(V("x", 5m));

        Assert.AreSequenceEqual(Names(a), Names(b));
        Assert.AreSequenceEqual(Decimals(a), Decimals(b));
    }

    [TestMethod]
    public void SetItem_DefaultVariable_ThrowsAndLeavesOriginalUnchanged()
    {
        VariableCollection vars = [V("a", 1m)];

        var ex = Assert.Throws<ArgumentException>(() => vars.SetItem(default(Variable)));

        Assert.AreEqual("variable", ex.ParamName);
        Assert.AreSequenceEqual(["a"], Names(vars));
    }

    // SetItems
    [TestMethod]
    public void SetItems_EmptyInput_ReturnsSameInstance()
    {
        VariableCollection vars = [V("a", 1m)];
        IEnumerable<Variable> sequence = new List<Variable>();
        ReadOnlySpan<Variable> span = [];

        Assert.AreSame(vars, vars.SetItems(sequence));
        Assert.AreSame(vars, vars.SetItems(span));
    }

    [TestMethod]
    public void SetItems_NullSequence_Throws()
    {
        VariableCollection vars = [V("a", 1m)];
        Assert.Throws<ArgumentNullException>(() =>
            vars.SetItems((IEnumerable<Variable>)null!));
    }

    [TestMethod]
    public void SetItems_DuplicatesWithinInput_OrderByLastOccurrence()
    {
        IEnumerable<Variable> input = new List<Variable> { V("a", 1m), V("b", 2m), V("A", 10m) };

        var result = VariableCollection.Empty.SetItems(input);

        Assert.AreSequenceEqual(["b", "A"], Names(result));
        Assert.AreSequenceEqual([2m, 10m], Decimals(result));
    }

    [TestMethod]
    public void SetItems_UncountedSequence_IsEnumeratedOnce()
    {
        var source = new CountingSequence([V("a", 1m), V("b", 2m)]);

        var result = VariableCollection.Empty.SetItems(source);

        Assert.AreEqual(1, source.EnumerationCount);
        Assert.AreEqual(2, result.Count);
    }


    [TestMethod]
    public void SetItems_SpanAndSequenceOverloads_ProduceIdenticalResults()
    {
        VariableCollection initial = [V("x", 1m), V("y", 2m), ("z", 3m)];
        Variable[] updates = [V("Y", 20m), V("w", 4m), V("X", 10m)];

        IEnumerable<Variable> sequence = updates;
        ReadOnlySpan<Variable> span = updates;

        var fromSequence = initial.SetItems(sequence);
        var fromSpan = initial.SetItems(span);

        Assert.AreSequenceEqual(Names(fromSequence), Names(fromSpan));
        Assert.AreSequenceEqual(Decimals(fromSequence), Decimals(fromSpan));
        Assert.AreSequenceEqual(["z", "Y", "w", "X"], Names(fromSequence));
    }

    [TestMethod]
    public void SetItems_CollectionAsEnumerable_MergesAndPreservesMetadata()
    {
        var node = new NumberNode(10);
        VariableCollection initial = [V("a", 1m, "initial"), V("b", 2m), V("c", 3m)];
        VariableCollection updates = [new Variable("A", new MathValue(10m), node, "updated"), V("d", 4m)];
        IEnumerable<Variable> sequence = updates;

        var result = initial.SetItems(sequence);

        Assert.AreSequenceEqual(["b", "c", "A", "d"], Names(result));
        Assert.AreSequenceEqual([2m, 3m, 10m, 4m], Decimals(result));
        Assert.AreEqual("updated", result["a"].Definition);
        Assert.AreSame(node, result["a"].Expression);
        Assert.AreSequenceEqual(["a", "b", "c"], Names(initial));
        Assert.AreEqual("initial", initial["a"].Definition);
        Assert.AreSequenceEqual(["A", "d"], Names(updates));
    }

    [TestMethod]
    public void SetItems_EmptyCollectionAsEnumerable_ReturnsReceiver()
    {
        VariableCollection initial = [V("a", 1m)];
        IEnumerable<Variable> empty = VariableCollection.Empty;

        Assert.AreSame(initial, initial.SetItems(empty));
        Assert.AreSequenceEqual(["a"], Names(initial));
    }

    [TestMethod]
    public void SetItems_WithItself_IsSafeAndPreservesOrder()
    {
        VariableCollection vars = [V("a", 1m), V("b", 2m), V("c", 3m)];

        var result = vars.SetItems(vars);

        Assert.AreSequenceEqual(["a", "b", "c"], Names(result));
        Assert.AreSequenceEqual([1m, 2m, 3m], Decimals(result));
        Assert.AreSequenceEqual(["a", "b", "c"], Names(vars)); // original unchanged
    }

    [TestMethod]
    public void SetItems_DefaultVariable_ThrowsAndLeavesOriginalUnchanged()
    {
        VariableCollection vars = [V("a", 1m)];
        IEnumerable<Variable> sequence = new List<Variable> { V("b", 2m), default };

        var ex = Assert.Throws<ArgumentException>(() => vars.SetItems(sequence));
        Assert.AreEqual("items", ex.ParamName);

        var spanEx = Assert.Throws<ArgumentException>(
            () => vars.SetItems(new Variable[] { V("b", 2m), default }.AsSpan()));
        Assert.AreEqual("items", spanEx.ParamName);

        Assert.AreSequenceEqual(["a"], Names(vars));
    }

    // Without.

    [TestMethod]
    public void Without_RemovesCaseInsensitivelyaAndPreservesRemainingOrder()
    {
        VariableCollection vars = [V("a", 1m), V("b", 2m), V("c", 3m), V("d", 4m)];

        var result = vars.Without("B", "d");

        Assert.AreSequenceEqual(["a", "c"], Names(result));
        Assert.AreSequenceEqual([1m, 3m], Decimals(result));
        Assert.AreSequenceEqual(["a", "b", "c", "d"], Names(vars)); // original intact
    }

    [TestMethod]
    public void Without_NothingToRemove_ReturnsSameInstance()
    {
        VariableCollection vars = [V("x", 1m), V("y", 2m)];

        Assert.AreSame(vars, vars.Without("missing"));
        Assert.AreSame(vars, vars.Without());
        Assert.AreSame(VariableCollection.Empty, VariableCollection.Empty.Without("x"));
    }

    [TestMethod]
    public void Without_RepeatedAndMixedPresentAbsentNames_RemovesOnce()
    {
        VariableCollection vars = [V("a", 1m), V("b", 2m), V("c", 3m)];

        var result = vars.Without("a", "A", "zzz", "a");

        Assert.AreSequenceEqual(["b", "c"], Names(result));
        Assert.AreSequenceEqual([2m, 3m], Decimals(result));
    }

    [TestMethod]
    public void Without_AllNames_ReturnsSharedEmptyInstance()
    {
        VariableCollection vars = [V("a", 1m), V("b", 2m)];

        Assert.AreSame(VariableCollection.Empty, vars.Without("a", "B"));
    }

    [TestMethod]
    public void Without_NullOrEmptyName_ThrowsArgumentException()
    {
        VariableCollection vars = [V("x", 1m)];

        Assert.Throws<ArgumentNullException>(() => vars.Without("x", (string)null!));
        Assert.AreSequenceEqual(["x"], Names(vars)); // original intact
    }

    // Fuzz.

    [TestMethod]
    public void RandomOperations_MatchReferenceModel_AndNeverMutateEarlierVersions()
    {
        var rng = new Random(20261104);
        string[] pool = ["a", "b", "c", "d", "e"];
        decimal nextValue = 1m;

        string RandomName()
        {
            string n = pool[rng.Next(pool.Length)];
            return rng.Next(3) == 0 ? n.ToUpperInvariant() : n; // random casing
        }

        Variable RandomVariable() => V(RandomName(), nextValue++);

        for (int session = 0; session < 200; session++)
        {
            var model = new List<(string Name, decimal Value)>();
            var current = VariableCollection.Empty;
            var versions = new List<(VariableCollection Collection, (string Name, decimal Value)[] Expected)>()
            {
                (current, [])
            };

            void ModelSet(Variable v)
            {
                model.RemoveAll(m => VariableCollection.NameComparer.Equals(m.Name, v.Name));
                model.Add((v.Name, v.Value.DecimalValue));
            }

            for (int op = 0; op < 30; op++)
            {
                string what;
                switch (rng.Next(3))
                {
                    case 0:
                        {
                            var v = RandomVariable();
                            what = $"SetItem({v})";
                            current = current.SetItem(v);
                            ModelSet(v);
                            break;
                        }

                    case 1:
                        {
                            var items = Enumerable.Range(0, rng.Next(0, 5)).Select(_ => RandomVariable()).ToArray();
                            what = $"SetItems([{string.Join(", ", items)}])";
                            if (rng.Next(2) == 0)
                            {
                                current = current.SetItems((IEnumerable<Variable>)items);
                            }
                            else
                            {
                                ReadOnlySpan<Variable> span = items;
                                current = current.SetItems(span);
                            }

                            foreach (var v in items)
                            {
                                ModelSet(v);
                            }

                            break;
                        }

                    default:
                        {
                            var names = Enumerable.Range(0, rng.Next(0, 4)).Select(_ => RandomName()).ToArray();
                            what = $"Without({string.Join(", ", names)})";
                            ReadOnlySpan<string> span = names;
                            current = current.Without(span);

                            foreach (var n in names)
                            {
                                model.RemoveAll(m => VariableCollection.NameComparer.Equals(m.Name, n));
                            }

                            break;
                        }
                }

                versions.Add((current, model.ToArray()));
                string context = $"Session {session}, operation {op}: {what}";

                // Every version ever produced should match the model and remain immutable.
                foreach (var (collection, expected) in versions)
                {
                    Assert.AreEqual(expected.Length, collection.Count, context);

                    for (int i = 0; i < expected.Length; i++)
                    {
                        Assert.AreEqual(expected[i].Name, collection[i].Name, context);
                        Assert.AreEqual(
                            expected[i].Value, collection[i].Value.DecimalValue, context);

                        Assert.IsTrue(collection.TryGetVariable(
                            expected[i].Name.ToUpperInvariant(), out var found), context);
                        Assert.AreEqual(expected[i].Value, found.DecimalValue, context);
                    }
                }
            }
        }
    }

    // General.

    [TestMethod]
    public void Empty_HasZeroCountAndEmptyEnumerations()
    {
        var empty = VariableCollection.Empty;

        Assert.AreEqual(0, empty.Count);
        Assert.IsEmpty(empty.Keys);
        Assert.IsEmpty(empty.Values);
        Assert.IsFalse(empty.ContainsKey("x"));
        Assert.IsFalse(empty.TryGetVariable("x", out _));
        Assert.Throws<KeyNotFoundException>(() => _ = empty["x"]);
        Assert.Throws<IndexOutOfRangeException>(() => _ = empty[0]);
    }

    [TestMethod]
    public void Add_AppendsInChronologicalOrder()
    {
        var vars = VariableCollection.Empty
            .SetItem("a", new MathValue(1m))
            .SetItem("b", new MathValue(2m))
            .SetItem("c", new MathValue(3m));

        Assert.AreEqual(3, vars.Count);
        Assert.AreSequenceEqual(["a", "b", "c"], vars.Keys.ToArray());
        Assert.AreSequenceEqual([1m, 2m, 3m], vars.Select(v => v.DecimalValue).ToArray());

        Assert.AreEqual("a", vars[0].Name);
        Assert.AreEqual(1m, vars[0].Value.DecimalValue);
        Assert.AreEqual("b", vars[1].Name);
        Assert.AreEqual(2m, vars[1].Value.DecimalValue);
        Assert.AreEqual("c", vars[2].Name);
        Assert.AreEqual(3m, vars[2].Value.DecimalValue);
    }

    [TestMethod]
    public void Add_ExistingKeyMovesToEndWithUpdatedValue()
    {
        var vars = VariableCollection.Empty
            .SetItem("a", new MathValue(1m))
            .SetItem("b", new MathValue(2m))
            .SetItem("c", new MathValue(3m));

        // Re-adding "a" (case-insensitively) should update its value and move it to the
        // end (MRU order).
        var updated = vars.SetItem("A", new MathValue(10m));

        Assert.AreEqual(3, updated.Count);
        Assert.AreSequenceEqual(["b", "c", "A"], updated.Keys.ToArray());
        Assert.AreSequenceEqual([2m, 3m, 10m], updated.Select(v => v.DecimalValue).ToArray());
        Assert.AreEqual(10m, updated["a"].DecimalValue);
        Assert.AreEqual(10m, updated["A"].DecimalValue);

        // Original remains unchanged.
        Assert.AreEqual(1m, vars["a"].DecimalValue);
        Assert.AreSequenceEqual(["a", "b", "c"], vars.Keys.ToArray());
    }

    [TestMethod]
    public void SetItems_MergesAndPreservesOrderWithReassignmentsAtEnd()
    {
        var initial = VariableCollection.Empty
            .SetItem("x", new MathValue(10m))
            .SetItem("y", new MathValue(20m))
            .SetItem("z", new MathValue(30m));

        var updates = new Variable[]
        {
            new("X", new MathValue(100m), "2 * 50"),
            new("w", new MathValue(40m)),
        };

        var next = initial.SetItems(updates);

        Assert.AreEqual(4, next.Count);
        // "x" was updated, so it moves to after "z"; then "w" is added.
        Assert.AreSequenceEqual(["y", "z", "X", "w"], next.Keys.ToArray());
        Assert.AreEqual(100m, next["x"].DecimalValue);
        Assert.AreEqual(20m, next["y"].DecimalValue);
        Assert.AreEqual(30m, next["z"].DecimalValue);
        Assert.AreEqual(40m, next["w"].DecimalValue);

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

        var collection = VariableCollection.Create(vars);

        Assert.AreEqual(2, collection.Count);
        Assert.AreSequenceEqual(["beta", "ALPHA"], collection.Keys.ToArray());
        Assert.AreEqual(10m, collection["alpha"].DecimalValue);
        Assert.AreEqual(2m, collection["beta"].DecimalValue);
        Assert.IsTrue(collection.TryGetVariable("alpha", out var alphaVar));
        Assert.AreEqual("updated alpha", alphaVar.Definition);
    }

    [TestMethod]
    public void Indexers_ThrowWhenOutOfRangeOrNotFound()
    {
        VariableCollection vars = [("x", new MathValue(5m))];

        Assert.Throws<IndexOutOfRangeException>(() => _ = vars[-1]);
        Assert.Throws<IndexOutOfRangeException>(() => _ = vars[1]);
        Assert.Throws<KeyNotFoundException>(() => _ = vars["nonexistent"]);
    }

    [TestMethod]
    public void Enumeration_YieldsFullVariableInstancesPreservingMetadata()
    {
        VariableCollection vars =
        [
            new("a", new MathValue(1m), "definition a"),
            new("b", new MathValue(2m), "definition b")
        ];

        var list = vars.ToList();
        Assert.HasCount(2, list);
        Assert.AreEqual("a", list[0].Name);
        Assert.AreEqual(1m, list[0].Value.DecimalValue);
        Assert.AreEqual("definition a", list[0].Definition);

        Assert.AreEqual("b", list[1].Name);
        Assert.AreEqual(2m, list[1].Value.DecimalValue);
        Assert.AreEqual("definition b", list[1].Definition);

        // Deconstruction test.
        var (name, val, def) = vars[0];
        Assert.AreEqual("a", name);
        Assert.AreEqual(1m, val.DecimalValue);
        Assert.AreEqual("definition a", def);
    }
}
