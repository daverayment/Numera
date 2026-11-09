using System.Collections;
using System.Globalization;

namespace Numera.Tests;

/// <summary>
/// Tests registration order, replacement, lookup, and immutable snapshots of custom
/// functions.
/// </summary>
[TestClass]
public sealed class CustomFunctionCollectionTests
{
    private static CustomFunction F(string name, decimal value = 1m, string? description = null) =>
        CustomFunction.Create(name, _ => new MathValue(value), description);

    private sealed class CountingSequence(IReadOnlyList<CustomFunction> items) :
        IEnumerable<CustomFunction>
    {
        public int EnumerationCount { get; private set; }

        public IEnumerator<CustomFunction> GetEnumerator()
        {
            EnumerationCount++;
            return items.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    [TestMethod]
    public void EmptyCollectionExpressionAndCreate_ReturnSharedEmptyInstance()
    {
        CustomFunctionCollection functions = [];

        Assert.AreSame(CustomFunctionCollection.Empty, functions);
        Assert.AreSame(CustomFunctionCollection.Empty, CustomFunctionCollection.Create([]));
        Assert.AreEqual(0, functions.Count);
        Assert.IsEmpty(functions.Names);
        Assert.IsEmpty(functions);
        Assert.AreEqual("(empty)", functions.ToString());
    }

    [TestMethod]
    public void CollectionExpression_SupportsSpreadAndPreservesOrder()
    {
        var first = F("alpha");
        var second = F("betaCustom");
        var third = F("gammaCustom");
        CustomFunctionCollection initial = [first, second];
        CustomFunctionCollection expanded = [.. initial, third];

        Assert.AreSequenceEqual(["alpha", "betaCustom"], initial.Names);
        Assert.AreSequenceEqual(["alpha", "betaCustom", "gammaCustom"], expanded.Names);
        Assert.AreSame(first, expanded[0]);
        Assert.AreSame(second, expanded[1]);
        Assert.AreSame(third, expanded[^1]);
    }

    [TestMethod]
    public void CollectionExpression_FilteredSpreadOmitsFunctionWithoutChangingOriginal()
    {
        var first = F("alpha");
        var second = F("betaCustom");
        var third = F("gammaCustom");
        CustomFunctionCollection initial = [first, second, third];

        CustomFunctionCollection filtered = [.. initial.Where(f => f != second)];

        Assert.AreSequenceEqual(["alpha", "gammaCustom"], filtered.Names);
        Assert.AreSame(first, filtered[0]);
        Assert.AreSame(third, filtered[1]);
        Assert.IsFalse(filtered.ContainsName("betaCustom"));
        Assert.AreSequenceEqual(["alpha", "betaCustom", "gammaCustom"], initial.Names);
        Assert.AreSame(second, initial[1]);
    }

    [TestMethod]
    public void Create_DuplicateNames_ThrowsCaseInsensitively()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            CustomFunctionCollection.Create([F("alpha"), F("ALPHA")]));

        Assert.AreEqual("items", ex.ParamName);
    }

    [TestMethod]
    public void Create_NullFunction_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            CustomFunctionCollection.Create([F("alpha"), null!]));

        Assert.AreEqual("items", ex.ParamName);
    }

    [TestMethod]
    public void CreateRange_NullSource_ThrowsArgumentNullException()
    {
        var ex = Assert.Throws<ArgumentNullException>(() => CustomFunctionCollection.CreateRange(null!));

        Assert.AreEqual("source", ex.ParamName);
    }

    [TestMethod]
    public void CreateRange_EmptySequences_ReturnSharedEmptyInstance()
    {
        var uncounted = new CountingSequence([]);

        Assert.AreSame(CustomFunctionCollection.Empty, CustomFunctionCollection.CreateRange(new List<CustomFunction>()));
        Assert.AreSame(CustomFunctionCollection.Empty, CustomFunctionCollection.CreateRange(Array.Empty<CustomFunction>()));
        Assert.AreSame(CustomFunctionCollection.Empty, CustomFunctionCollection.CreateRange(Enumerable.Empty<CustomFunction>()));
        Assert.AreSame(CustomFunctionCollection.Empty, CustomFunctionCollection.CreateRange(uncounted));
        Assert.AreEqual(1, uncounted.EnumerationCount);
    }

    [TestMethod]
    public void CreateRange_ExistingCollection_ReturnsSameInstance()
    {
        CustomFunctionCollection functions = [F("alpha")];

        Assert.AreSame(functions, CustomFunctionCollection.CreateRange(functions));
        Assert.AreSame(CustomFunctionCollection.Empty, CustomFunctionCollection.CreateRange(CustomFunctionCollection.Empty));
    }

    [TestMethod]
    public void CreateRange_UncountedSequence_EnumeratesOnceAndPreservesInstances()
    {
        var first = F("alpha");
        var second = F("betaCustom");
        var source = new CountingSequence([first, second]);

        var functions = CustomFunctionCollection.CreateRange(source);

        Assert.AreEqual(1, source.EnumerationCount);
        Assert.AreSequenceEqual(["alpha", "betaCustom"], functions.Names);
        Assert.AreSame(first, functions[0]);
        Assert.AreSame(second, functions[1]);
    }

    [TestMethod]
    public void CreateRange_DuplicateNamesAndNullFunction_ThrowArgumentException()
    {
        var duplicate = Assert.Throws<ArgumentException>(() =>
            CustomFunctionCollection.CreateRange(new CountingSequence([F("alpha"), F("ALPHA")])));
        var nullFunction = Assert.Throws<ArgumentException>(() =>
            CustomFunctionCollection.CreateRange(new List<CustomFunction> { F("alpha"), null! }));

        Assert.AreEqual("source", duplicate.ParamName);
        Assert.AreEqual("source", nullFunction.ParamName);
    }

    [TestMethod]
    public void Lookup_IsCaseInsensitiveAndPreservesInstanceAndMetadata()
    {
        var function = F("CustomName", 7m, "description");
        CustomFunctionCollection functions = [function];

        Assert.IsTrue(functions.ContainsName("CUSTOMNAME"));
        Assert.AreSame(function, functions["customname"]);
        Assert.IsTrue(functions.TryGetFunction("CuStOmNaMe", out var found));
        Assert.AreSame(function, found);
        Assert.AreEqual("description", found.Description);
        Assert.IsTrue(functions.TryGetFunction("CUSTOMNAME".AsSpan(), out var spanFound));
        Assert.AreSame(function, spanFound);
    }

    [TestMethod]
    public void Lookup_IsCultureInvariantUnderTurkishCulture()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("tr-TR");
            CustomFunctionCollection functions = [F("title")];

            Assert.IsTrue(functions.ContainsName("TITLE"));
            Assert.IsTrue(functions.TryGetFunction("TITLE".AsSpan(), out _));
            Assert.IsFalse(functions.ContainsName("T\u0130TLE"));
            Assert.IsFalse(functions.ContainsName("t\u0131tle"));
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [TestMethod]
    public void Lookup_MissingNames_ReturnFalseOrThrow()
    {
        CustomFunctionCollection functions = [F("alpha")];

        Assert.IsFalse(functions.ContainsName("missing"));
        Assert.IsFalse(functions.TryGetFunction("missing", out var found));
        Assert.IsNull(found);
        Assert.IsFalse(functions.TryGetFunction("missing".AsSpan(), out var spanFound));
        Assert.IsNull(spanFound);
        Assert.Throws<KeyNotFoundException>(() => _ = functions["missing"]);
        Assert.IsFalse(CustomFunctionCollection.Empty.ContainsName("alpha"));
        Assert.IsFalse(CustomFunctionCollection.Empty.TryGetFunction("alpha", out _));
        Assert.IsFalse(CustomFunctionCollection.Empty.TryGetFunction("alpha".AsSpan(), out _));
        Assert.Throws<KeyNotFoundException>(() => _ = CustomFunctionCollection.Empty["alpha"]);
    }

    [TestMethod]
    public void Indexer_OutOfRange_Throws()
    {
        CustomFunctionCollection functions = [F("alpha"), F("betaCustom")];

        Assert.Throws<IndexOutOfRangeException>(() => _ = functions[-1]);
        Assert.Throws<IndexOutOfRangeException>(() => _ = functions[2]);
        Assert.Throws<IndexOutOfRangeException>(() => _ = CustomFunctionCollection.Empty[0]);
    }

    [TestMethod]
    public void Enumeration_SupportsReadOnlyListAndNonGenericEnumerable()
    {
        var first = F("alpha");
        var second = F("betaCustom");
        CustomFunctionCollection functions = [first, second];
        IReadOnlyList<CustomFunction> list = functions;

        Assert.AreEqual(2, list.Count);
        Assert.AreSame(second, list[1]);
        Assert.AreSequenceEqual([first, second], functions);
        var enumerated = ((IEnumerable)functions).Cast<CustomFunction>().ToArray();
        Assert.AreSequenceEqual([first, second], enumerated);
        Assert.AreEqual("alpha, betaCustom", functions.ToString());
    }

    [TestMethod]
    public void SetItem_AddsAtEndWithoutMutatingPriorSnapshots()
    {
        var first = F("alpha");
        var second = F("betaCustom");
        var initial = CustomFunctionCollection.Empty.SetItem(first);
        var expanded = initial.SetItem(second);

        Assert.AreEqual(0, CustomFunctionCollection.Empty.Count);
        Assert.AreSequenceEqual(["alpha"], initial.Names);
        Assert.AreSequenceEqual(["alpha", "betaCustom"], expanded.Names);
        Assert.AreSame(first, initial[0]);
        Assert.AreSame(second, expanded[1]);
    }

    [TestMethod]
    public void SetItem_ReplacementPreservesPositionAndReplacesWholeFunction()
    {
        var original = F("alpha", 1m, "old");
        var other = F("betaCustom");
        var replacement = F("ALPHA", 5m, "new");
        CustomFunctionCollection initial = [original, other];

        var updated = initial.SetItem(replacement);

        Assert.AreEqual(2, updated.Count);
        Assert.AreSequenceEqual(["ALPHA", "betaCustom"], updated.Names);
        Assert.AreSame(replacement, updated[0]);
        Assert.AreSame(replacement, updated["alpha"]);
        Assert.AreSame(other, updated[1]);
        Assert.AreEqual("new", updated[0].Description);
        Assert.AreEqual(5m, updated[0].Implementation([new MathValue(0m)]).DecimalValue);
        Assert.AreSame(original, initial[0]);
        Assert.AreSequenceEqual(["alpha", "betaCustom"], initial.Names);
    }

    [TestMethod]
    public void SetItem_NullFunction_ThrowsAndLeavesOriginalUnchanged()
    {
        CustomFunctionCollection functions = [F("alpha")];

        var ex = Assert.Throws<ArgumentNullException>(() => functions.SetItem((CustomFunction)null!));

        Assert.AreEqual("function", ex.ParamName);
        Assert.AreSequenceEqual(["alpha"], functions.Names);
    }

    [TestMethod]
    public void SetItem_FluentOverloadsPreserveDescriptionsAndHandlers()
    {
        var functions = CustomFunctionCollection.Empty
            .SetItem("unary", x => x, "one")
            .SetItem("binary", (x, y) => x + y, "two")
            .SetItem("ternary", (x, y, z) => x + y + z, "three")
            .SetItem("variadic", 1, args => args[0], "many");
        MathValue one = new(1m);
        MathValue two = new(2m);
        MathValue three = new(3m);

        Assert.AreSequenceEqual(["unary", "binary", "ternary", "variadic"], functions.Names);
        Assert.AreEqual("one", functions[0].Description);
        Assert.AreEqual("two", functions[1].Description);
        Assert.AreEqual("three", functions[2].Description);
        Assert.AreEqual("many", functions[3].Description);
        Assert.AreEqual(1m, functions[0].Implementation([one]).DecimalValue);
        Assert.AreEqual(new MathValue(3m), functions[1].Implementation([one, two]));
        Assert.AreEqual(new MathValue(6m), functions[2].Implementation([one, two, three]));
        Assert.AreEqual(1m, functions[3].Implementation([one, two]).DecimalValue);
        Assert.AreEqual(1, functions[3].Signature.MinimumArguments);
        Assert.IsNull(functions[3].Signature.MaximumArguments);
    }

    [TestMethod]
    public void SetItem_FluentOverloadsRejectNullHandlers()
    {
        var functions = CustomFunctionCollection.Empty;

        Assert.Throws<ArgumentNullException>(() => functions.SetItem("unary", (Func<MathValue, MathValue>)null!));
        Assert.Throws<ArgumentNullException>(() => functions.SetItem("binary", (Func<MathValue, MathValue, MathValue>)null!));
        Assert.Throws<ArgumentNullException>(() => functions.SetItem("ternary", (Func<MathValue, MathValue, MathValue, MathValue>)null!));
        Assert.Throws<ArgumentNullException>(() => functions.SetItem("variadic", 1, null!));
        Assert.AreEqual(0, functions.Count);
    }

    [TestMethod]
    public void SetItem_RepeatedUpdatesMatchOrderedModelAndPreserveSnapshots()
    {
        var random = new Random(12345);
        string[] names = ["alpha", "betaCustom", "gammaCustom", "deltaCustom"];
        var model = new List<CustomFunction>();
        var versions = new List<(CustomFunctionCollection Collection, CustomFunction[] Expected)>();
        var current = CustomFunctionCollection.Empty;

        for (int i = 0; i < 80; i++)
        {
            string name = names[random.Next(names.Length)];
            if (random.Next(2) == 0)
            {
                name = name.ToUpperInvariant();
            }

            var function = F(name, i);
            int index = model.FindIndex(f => CustomFunctionCollection.NameComparer.Equals(f.Name, name));
            if (index < 0)
            {
                model.Add(function);
            }
            else
            {
                model[index] = function;
            }

            current = current.SetItem(function);
            versions.Add((current, model.ToArray()));

            foreach (var (collection, expected) in versions)
            {
                Assert.AreEqual(expected.Length, collection.Count);
                for (int j = 0; j < expected.Length; j++)
                {
                    Assert.AreSame(expected[j], collection[j]);
                    Assert.AreSame(expected[j], collection[expected[j].Name.ToUpperInvariant()]);
                }
            }
        }
    }
}
