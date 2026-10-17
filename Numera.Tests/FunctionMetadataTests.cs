namespace Numera.Tests;

[TestClass]
public sealed class FunctionMetadataTests
{
    [TestMethod]
    public void CatalogContainsAllExpectedFunctionsAndAliases()
    {
        Assert.IsGreaterThanOrEqualTo(50, Functions.AllNames.Count);
        Assert.IsGreaterThanOrEqualTo(40, Functions.CanonicalNames.Count);
        Assert.IsGreaterThanOrEqualTo(50, Functions.All.Count);
        Assert.IsGreaterThanOrEqualTo(40, Functions.Canonical.Count);

        // All entries in AllNames should match All.
        Assert.AreSequenceEqual(
            Functions.AllNames.ToList(),
            Functions.All.Select(f => f.Name).ToList(), SequenceOrder.InAnyOrder);

        // All entries in CanonicalNames should match Canonical.
        Assert.AreSequenceEqual(
            Functions.CanonicalNames.ToList(),
            Functions.Canonical.Select(f => f.Name).ToList(), SequenceOrder.InAnyOrder);

        // Names should be sorted alphabetically.
        var sortedAllNames = Functions.AllNames.OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList();
        Assert.AreSequenceEqual(sortedAllNames, Functions.AllNames.ToList());

        var sortedCanonicalNames = Functions.CanonicalNames.OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList();
        Assert.AreSequenceEqual(sortedCanonicalNames, Functions.CanonicalNames.ToList());
    }

    [TestMethod]
    public void TryGetInfoRetrievesCanonicalAndAliasEntries()
    {
        // Canonical lookup.
        Assert.IsTrue(Functions.TryGetInfo("arcsin", out var arcsinInfo));
        Assert.IsNotNull(arcsinInfo);
        Assert.AreEqual("arcsin", arcsinInfo.Name);
        Assert.AreEqual("arcsin", arcsinInfo.CanonicalName);
        Assert.IsFalse(arcsinInfo.IsAlias);
        Assert.AreEqual(FunctionCategory.Trigonometry, arcsinInfo.Category);
        Assert.Contains("asin", arcsinInfo.Aliases.ToList());

        // Alias lookup.
        Assert.IsTrue(Functions.TryGetInfo("asin", out var asinInfo));
        Assert.IsNotNull(asinInfo);
        Assert.AreEqual("asin", asinInfo.Name);
        Assert.AreEqual("arcsin", asinInfo.CanonicalName);
        Assert.IsTrue(asinInfo.IsAlias);
        Assert.AreEqual(FunctionCategory.Trigonometry, asinInfo.Category);

        // Case insensitivity.
        Assert.IsTrue(Functions.TryGetInfo("ARCSIN", out var upperInfo));
        Assert.AreEqual("arcsin", upperInfo?.Name);

        // Unknown function.
        Assert.IsFalse(Functions.TryGetInfo("unknown_func", out var unknownInfo));
        Assert.IsNull(unknownInfo);
    }

    [TestMethod]
    public void TryGetSignatureReturnsBoundsAndParameters()
    {
        // Single argument fixed arity.
        Assert.IsTrue(Functions.TryGetSignature("sqrt", out var sqrtSig));
        Assert.IsNotNull(sqrtSig);
        Assert.AreEqual(1, sqrtSig.MinimumArguments);
        Assert.AreEqual(1, sqrtSig.MaximumArguments);
        Assert.IsFalse(sqrtSig.AllowsMultipleArguments);
        Assert.HasCount(1, sqrtSig.Parameters);
        Assert.AreEqual("x", sqrtSig.Parameters[0].Name);
        Assert.IsFalse(sqrtSig.Parameters[0].IsOptional);
        Assert.IsFalse(sqrtSig.Parameters[0].IsVariadic);

        // Optional argument.
        Assert.IsTrue(Functions.TryGetSignature("round", out var roundSig));
        Assert.IsNotNull(roundSig);
        Assert.AreEqual(1, roundSig.MinimumArguments);
        Assert.AreEqual(2, roundSig.MaximumArguments);
        Assert.IsTrue(roundSig.AllowsMultipleArguments);
        Assert.HasCount(2, roundSig.Parameters);
        Assert.AreEqual("x", roundSig.Parameters[0].Name);
        Assert.AreEqual("places", roundSig.Parameters[1].Name);
        Assert.IsTrue(roundSig.Parameters[1].IsOptional);

        // Variadic.
        Assert.IsTrue(Functions.TryGetSignature("max", out var maxSig));
        Assert.IsNotNull(maxSig);
        Assert.AreEqual(1, maxSig.MinimumArguments);
        Assert.IsNull(maxSig.MaximumArguments);
        Assert.IsTrue(maxSig.AllowsMultipleArguments);
        Assert.HasCount(1, maxSig.Parameters);
        Assert.IsTrue(maxSig.Parameters[0].IsVariadic);

        // 0-argument.
        Assert.IsTrue(Functions.TryGetSignature("rand", out var randSig));
        Assert.IsNotNull(randSig);
        Assert.AreEqual(0, randSig.MinimumArguments);
        Assert.AreEqual(0, randSig.MaximumArguments);
        Assert.IsFalse(randSig.AllowsMultipleArguments);
        Assert.IsEmpty(randSig.Parameters);
    }

    [TestMethod]
    public void FunctionSignaturesMatchEveryRegisteredExecutionFunction()
    {
        // Every function listed in Canonical must be executable in ExpressionParser
        // without UnknownFunction error.
        foreach (var func in Functions.Canonical)
        {
            var testExpr = func.Signature.MinimumArguments switch
            {
                0 => $"{func.Name}()",
                1 => $"{func.Name}(1)",
                2 => $"{func.Name}(1, 2)",
                3 => $"{func.Name}(1, 2, 0.5)",
                _ => $"{func.Name}(1)",
            };

            // Evaluating may succeed or throw a math domain error, but must never throw
            // UnknownFunction.
            if (!Calculator.TryEvaluate(testExpr, out _, out var diag))
            {
                Assert.AreNotEqual(
                    EvaluationError.UnknownFunction,
                    diag?.Code,
                    $"Function {func.Name} returned UnknownFunction on expression {testExpr}");
            }
        }
    }

    [TestMethod]
    public void SearchTier1ExactMatchRanksFirst()
    {
        var results = Functions.Search("sin").ToList();
        Assert.IsNotEmpty(results);
        Assert.AreEqual("sin", results[0].Name);

        var aliasResults = Functions.Search("asin", includeAliases: true).ToList();
        Assert.IsNotEmpty(aliasResults);
        Assert.AreEqual("asin", aliasResults[0].Name);
    }

    [TestMethod]
    public void SearchTier2PrefixMatchRanksBeforeSubstringsAndKeywords()
    {
        // "arc" should rank arcsin, arccos, arctan, arccot, arcsec, arccsc before substrings
        var results = Functions.Search("arc").ToList();
        Assert.IsGreaterThanOrEqualTo(6, results.Count);
        Assert.IsTrue(results.Take(6).All(r => r.Name.StartsWith("arc", StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    public void SearchTier3SubstringMatchFindsInternalSubstrings()
    {
        // "atan" matches arctan (as alias atan), artanh (as alias atanh), and atan2.
        var results = Functions.Search("atan2").ToList();
        Assert.Contains(r => r.Name == "atan2", results);
    }

    [TestMethod]
    public void SearchTier4CategoryMatchReturnsWholeCategory()
    {
        var trigResults = Functions.Search("trig").ToList();
        Assert.IsGreaterThanOrEqualTo(10, trigResults.Count);
        Assert.IsTrue(trigResults.All(r => r.Category == FunctionCategory.Trigonometry));

        var statsResults = Functions.Search("stats").ToList();
        Assert.IsGreaterThanOrEqualTo(5, statsResults.Count);
        Assert.IsTrue(statsResults.All(r => r.Category == FunctionCategory.StatisticalAndAggregation));

        var bitResults = Functions.Search("bitwise").ToList();
        Assert.IsGreaterThanOrEqualTo(6, bitResults.Count);
        Assert.IsTrue(bitResults.All(r => r.Category == FunctionCategory.Bitwise));
    }

    [TestMethod]
    public void SearchTier5KeywordsFindsConcepts()
    {
        // "pythagoras" -> hypot.
        var hypotResults = Functions.Search("pythagoras").ToList();
        Assert.Contains(r => r.Name == "hypot", hypotResults);

        // "gaussian" or "bell" -> ncdf.
        var ncdfResults = Functions.Search("gaussian").ToList();
        Assert.Contains(r => r.Name == "ncdf", ncdfResults);

        // "mean" -> avg.
        var avgResults = Functions.Search("mean").ToList();
        Assert.Contains(r => r.Name == "avg", avgResults);

        // "modulo" -> mod.
        var modResults = Functions.Search("modulo").ToList();
        Assert.Contains(r => r.Name == "mod", modResults);
    }

    [TestMethod]
    public void SearchIncludeAliasesControlsWhetherAliasesAreReturned()
    {
        // Canonical only (default).
        var canonicalSearch = Functions.Search("asin", includeAliases: false).ToList();
        Assert.Contains(r => r.Name == "arcsin", canonicalSearch);
        Assert.DoesNotContain(r => r.Name == "asin", canonicalSearch);

        // With aliases.
        var aliasSearch = Functions.Search("asin", includeAliases: true).ToList();
        Assert.Contains(r => r.Name == "asin", aliasSearch);
    }

    [TestMethod]
    public void SearchWithEmptyOrWhitespaceReturnsAllItems()
    {
        var canonicalAll = Functions.Search("").ToList();
        Assert.HasCount(Functions.Canonical.Count, canonicalAll);

        var allItems = Functions.Search("   ", includeAliases: true).ToList();
        Assert.HasCount(Functions.All.Count, allItems);
    }

    [TestMethod]
    public void FunctionsApiThrowsOnNullArguments()
    {
        Assert.Throws<ArgumentNullException>(() => Functions.TryGetInfo(null!, out _));
        Assert.Throws<ArgumentNullException>(() => Functions.TryGetSignature(null!, out _));
        Assert.Throws<ArgumentNullException>(() => Functions.Search(null!).ToList());
    }
}
