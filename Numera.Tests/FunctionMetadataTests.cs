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
        // Every function listed in Canonical must be executable in AstEvaluator
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

        // "euler beta" -> beta.
        var betaResults = Functions.Search("euler beta").ToList();
        Assert.Contains(r => r.Name == "beta", betaResults);

        // "riemann" -> zeta.
        var zetaResults = Functions.Search("riemann").ToList();
        Assert.Contains(r => r.Name == "zeta", zetaResults);

        // "hcf" or "greatest common divisor" -> gcd.
        var gcdResults = Functions.Search("hcf").ToList();
        Assert.Contains(r => r.Name == "gcd", gcdResults);

        // "lcm" -> lcm.
        var lcmResults = Functions.Search("lowest common multiple").ToList();
        Assert.Contains(r => r.Name == "lcm", lcmResults);

        // "pascal" or "combinations" -> comb.
        var combResults = Functions.Search("pascal").ToList();
        Assert.Contains(r => r.Name == "comb", combResults);

        // "arrangements" -> perm.
        var permResults = Functions.Search("arrangements").ToList();
        Assert.Contains(r => r.Name == "perm", permResults);

        // "primality" -> isprime.
        var primeResults = Functions.Search("primality").ToList();
        Assert.Contains(r => r.Name == "isprime", primeResults);

        // "divisor count" -> divcount.
        var divCountResults = Functions.Search("divisor count").ToList();
        Assert.Contains(r => r.Name == "divcount", divCountResults);

        // "divisor sum" -> sigma.
        var sigmaResults = Functions.Search("divisor sum").ToList();
        Assert.Contains(r => r.Name == "sigma", sigmaResults);

        // "euler totient" -> totient.
        var totientResults = Functions.Search("euler totient").ToList();
        Assert.Contains(r => r.Name == "totient", totientResults);
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
    [DataRow(false)]
    [DataRow(true)]
    public void EngineDiscoveryCombinesMetadataWithoutInvokingCallbacks(bool includeAliases)
    {
        var signature = new FunctionSignature(1, 2,
            [new ParameterInfo("value"), new ParameterInfo("offset", IsOptional: true)]);
        var described = new CustomFunction("zCustom", signature,
            _ => throw new InvalidOperationException("Discovery must not evaluate functions."), "Original description")
        {
            Description = "Initialized description"
        };
        var undescribed = CustomFunction.Create("AlphaCustom",
            () => throw new InvalidOperationException("Discovery must not evaluate functions."));
        var engine = new CalculatorEngine(options => options with
        {
            CustomFunctions = [described, undescribed]
        });

        var entries = engine.GetFunctions(includeAliases).ToList();
        var builtIns = includeAliases ? Functions.All : Functions.Canonical;
        Assert.HasCount(builtIns.Count + 2, entries);
        Assert.AreSequenceEqual(
            entries.Select(f => f.Name).OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList(),
            entries.Select(f => f.Name).ToList());
        Assert.AreEqual(entries.Count, entries.Select(f => f.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.AreSequenceEqual(builtIns.ToList(), entries.Where(f => f.Category != FunctionCategory.Custom).ToList());
        Assert.AreSequenceEqual(new[] { "AlphaCustom", "zCustom" },
            entries.Where(f => f.Category == FunctionCategory.Custom).Select(f => f.Name).ToArray());

        var describedInfo = entries.Single(f => f.Name == "zCustom");
        Assert.AreSame(signature, describedInfo.Signature);
        Assert.AreEqual("Initialized description", describedInfo.Description);
        var undescribedInfo = entries.Single(f => f.Name == "AlphaCustom");
        Assert.IsNull(undescribedInfo.Description);
        Assert.AreSame(undescribed.Signature, undescribedInfo.Signature);
        foreach (var custom in entries.Where(f => f.Category == FunctionCategory.Custom))
        {
            Assert.AreEqual(custom.Name, custom.CanonicalName);
            Assert.IsFalse(custom.IsAlias);
            Assert.IsEmpty(custom.Aliases);
            Assert.IsEmpty(custom.Keywords);
        }

        foreach (var query in new[] { "", "   ", "CUSTOM", "alph", "zCustom", "not_a_function" })
        {
            _ = engine.SearchFunctions(query, includeAliases).ToList();
        }
        Assert.AreSequenceEqual(entries.Select(f => f.Name).ToList(),
            engine.SearchFunctions("   ", includeAliases).Select(f => f.Name).ToList());
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void EngineWithoutCustomFunctionsListsTheBuiltInCatalog(bool includeAliases)
    {
        var expected = includeAliases ? Functions.All : Functions.Canonical;
        Assert.AreSequenceEqual(expected.ToList(), new CalculatorEngine().GetFunctions(includeAliases).ToList());
        Assert.IsTrue(expected.All(f => f.Category != FunctionCategory.Custom));
        Assert.IsTrue(expected.All(f => !string.IsNullOrWhiteSpace(f.Description)));
    }

    [TestMethod]
    [DataRow("", false)]
    [DataRow("   ", true)]
    [DataRow(" asin ", false)]
    [DataRow("asin", true)]
    [DataRow("gaussian", false)]
    [DataRow("TRIG", true)]
    [DataRow("arc", false)]
    [DataRow("mean", true)]
    [DataRow("not_a_function", false)]
    public void EngineWithoutCustomFunctionsPreservesBuiltInSearch(string query, bool includeAliases)
    {
        Assert.AreSequenceEqual(Functions.Search(query, includeAliases).ToList(),
            new CalculatorEngine().SearchFunctions(query, includeAliases).ToList());
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void EngineSearchRanksBuiltInAndCustomMatchesTogether(bool includeAliases)
    {
        var engine = new CalculatorEngine(options => options with
        {
            CustomFunctions =
            [
                CustomFunction.Create("zSin", x => x),
                CustomFunction.Create("sinExtra", x => x),
                CustomFunction.Create("mySin", x => x)
            ]
        });

        var names = engine.SearchFunctions(" SIN ", includeAliases).Select(f => f.Name).ToList();
        Assert.AreEqual("sin", names[0]);
        Assert.IsGreaterThan(0, names.IndexOf("sinExtra"));
        Assert.IsLessThan(names.IndexOf("mySin"), names.IndexOf("sinExtra"));
        Assert.IsLessThan(names.IndexOf("zSin"), names.IndexOf("mySin"));
        Assert.AreEqual("mySin", engine.SearchFunctions("MYSIN", includeAliases).First().Name);

        var categoryMatches = engine.SearchFunctions("CuStOm", includeAliases).ToList();
        Assert.AreSequenceEqual(new[] { "mySin", "sinExtra", "zSin" }, categoryMatches.Select(f => f.Name).ToArray());
        Assert.IsTrue(categoryMatches.All(f => f.Category == FunctionCategory.Custom));
        Assert.AreSequenceEqual(Functions.Search("sin", includeAliases).ToList(),
            engine.SearchFunctions("sin", includeAliases).Where(f => f.Category != FunctionCategory.Custom).ToList());
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void EngineSearchPreservesAliasRankingAlongsideCustomFunctions(bool includeAliases)
    {
        var engine = new CalculatorEngine(options => options with
        {
            CustomFunctions = [CustomFunction.Create("asinExtra", x => x)]
        });

        var results = engine.SearchFunctions("asin", includeAliases).ToList();
        Assert.AreEqual(includeAliases, results.Any(f => f.Name == "asin"));
        Assert.HasCount(1, results.Where(f => f.Name == "asinExtra").ToList());
        Assert.IsLessThan(results.FindIndex(f => f.Name == "asinExtra"), results.FindIndex(f => f.Name == "arcsin"));
        if (includeAliases)
        {
            Assert.AreEqual("asin", results[0].Name);
        }
        else
        {
            Assert.AreEqual("arcsin", results[0].Name);
        }
    }

    [TestMethod]
    public void DiscoveryRemainsScopedToTheEnginesImmutableConfiguration()
    {
        var originalFunction = CustomFunction.Create("localFunc", x => x, "Original");
        var original = new CalculatorEngine(options => options with { CustomFunctions = [originalFunction] });
        var replacement = CustomFunction.Create("localFunc", (x, y) => x + y, "Replacement");
        var derived = original.WithOptions(options => options with
        {
            CustomFunctions = options.CustomFunctions.SetItem(replacement)
                .SetItem("derivedOnly", () => new MathValue(1))
        });

        var originalInfo = original.SearchFunctions("localFunc").Single();
        var derivedInfo = derived.SearchFunctions("localFunc").Single();
        Assert.AreEqual("Original", originalInfo.Description);
        Assert.AreEqual(1, originalInfo.Signature.MinimumArguments);
        Assert.AreEqual("Replacement", derivedInfo.Description);
        Assert.AreEqual(2, derivedInfo.Signature.MinimumArguments);
        Assert.IsEmpty(original.SearchFunctions("derivedOnly").ToList());
        Assert.HasCount(1, derived.SearchFunctions("derivedOnly").ToList());
        Assert.IsFalse(Functions.TryGetInfo("localFunc", out _));
        Assert.IsFalse(Functions.TryGetSignature("localFunc", out _));
        Assert.IsEmpty(Functions.Search("localFunc").ToList());
        Assert.DoesNotContain("localFunc", Functions.AllNames);
        Assert.DoesNotContain(f => f.Category == FunctionCategory.Custom, new CalculatorEngine().GetFunctions());
    }

    [TestMethod]
    public void EngineSearchRejectsANullQueryBeforeEnumeration()
    {
        var engine = new CalculatorEngine();
        var exception = Assert.ThrowsExactly<ArgumentNullException>(() => engine.SearchFunctions(null!));
        Assert.AreEqual("query", exception.ParamName);
    }

    [TestMethod]
    public void FunctionsApiThrowsOnNullArguments()
    {
        Assert.Throws<ArgumentNullException>(() => Functions.TryGetInfo(null!, out _));
        Assert.Throws<ArgumentNullException>(() => Functions.TryGetSignature(null!, out _));
        Assert.Throws<ArgumentNullException>(() => Functions.Search(null!).ToList());
    }
}
