using System.Globalization;

namespace Numera.Tests;

[TestClass]
public class CalculatorEngineTests
{
    [TestMethod]
    public void ParameterlessConstructor_UsesDefaultSettings()
    {
        Assert.AreEqual(Calculator.Evaluate("2+3").IntegerValue, new CalculatorEngine().Evaluate("2+3").IntegerValue);
    }

    [TestMethod]
    public void RequiredConfigurationArgumentsRejectNull()
    {
        var engine = new CalculatorEngine();
        Assert.AreEqual("options", Assert.ThrowsExactly<ArgumentNullException>(() =>
            new CalculatorEngine((CalculatorOptions)null!)).ParamName);
        Assert.AreEqual("configure", Assert.ThrowsExactly<ArgumentNullException>(() =>
            new CalculatorEngine((Func<CalculatorOptions, CalculatorOptions>)null!)).ParamName);
        Assert.AreEqual("options", Assert.ThrowsExactly<ArgumentNullException>(() =>
            engine.WithOptions((CalculatorOptions)null!)).ParamName);
        Assert.AreEqual("configure", Assert.ThrowsExactly<ArgumentNullException>(() =>
            engine.WithOptions((Func<CalculatorOptions, CalculatorOptions>)null!)).ParamName);
        Assert.ThrowsExactly<ArgumentNullException>(() => CalculatorEngine.ForCulture(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => engine.WithCulture(null!));
    }

    [TestMethod]
    public void EveryConfigurationTransformationRejectsANullResult()
    {
        var options = new CalculatorOptions { MaxInputLength = 100 };
        var engine = new CalculatorEngine(options);
        Action[] createEngines =
        [
            () => new CalculatorEngine(_ => null!),
            () => engine.WithOptions(_ => null!),
            () => CalculatorEngine.ForCulture(CultureInfo.InvariantCulture, _ => null!),
            () => CalculatorEngine.ForCurrentCulture(_ => null!)
        ];

        foreach (var create in createEngines)
        {
            var exception = Assert.ThrowsExactly<ArgumentException>(create);
            Assert.AreEqual("configure", exception.ParamName);
        }

        Assert.AreSame(options, engine.Options);
    }

    [TestMethod]
    public void EveryConfigurationTransformationValidatesItsResult()
    {
        var engine = new CalculatorEngine();
        Func<CalculatorOptions, CalculatorOptions> invalidLimit = options => options with { MaxInputLength = 0 };
        Func<CalculatorOptions, CalculatorOptions> missingFormat = options => options with { ResultFormat = null! };

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new CalculatorEngine(invalidLimit));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => engine.WithOptions(invalidLimit));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => CalculatorEngine.ForCulture(CultureInfo.InvariantCulture, invalidLimit));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => CalculatorEngine.ForCurrentCulture(invalidLimit));
        Assert.ThrowsExactly<ArgumentNullException>(() => new CalculatorEngine(missingFormat));
        Assert.ThrowsExactly<ArgumentNullException>(() => engine.WithOptions(missingFormat));
        Assert.ThrowsExactly<ArgumentNullException>(() => CalculatorEngine.ForCulture(CultureInfo.InvariantCulture, missingFormat));
        Assert.ThrowsExactly<ArgumentNullException>(() => CalculatorEngine.ForCurrentCulture(missingFormat));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => engine.WithOptions(new CalculatorOptions { MaxInputLength = 0 }));
        Assert.ThrowsExactly<ArgumentNullException>(() => engine.WithOptions(new CalculatorOptions { ResultFormat = null! }));
    }

    [TestMethod]
    public void ConfigurationCallbackExceptionsAreNotHiddenOrReplacedWithDefaults()
    {
        var engine = new CalculatorEngine();
        var failure = new InvalidOperationException("Configuration failed.");
        Func<CalculatorOptions, CalculatorOptions> configure = _ => throw failure;
        Action[] createEngines =
        [
            () => new CalculatorEngine(configure),
            () => engine.WithOptions(configure),
            () => CalculatorEngine.ForCulture(CultureInfo.InvariantCulture, configure),
            () => CalculatorEngine.ForCurrentCulture(configure)
        ];

        foreach (var create in createEngines)
        {
            Assert.AreSame(failure, Assert.ThrowsExactly<InvalidOperationException>(create));
        }
    }

    [TestMethod]
    public void WithReplacementOptionsUsesTheCompleteConfigurationWithoutChangingTheOriginal()
    {
        var originalOptions = new CalculatorOptions
        {
            Culture = CultureInfo.GetCultureInfo("de-DE"),
            AngleUnit = AngleUnit.Degrees,
            MaxInputLength = 100,
            ResultFormat = new ResultFormatOptions { SignificantDigits = 6 }
        };
        var original = new CalculatorEngine(originalOptions);
        var replacement = new CalculatorOptions { Culture = CultureInfo.InvariantCulture };
        var derived = original.WithOptions(replacement);

        Assert.AreNotSame(original, derived);
        Assert.AreSame(originalOptions, original.Options);
        Assert.AreSame(replacement, derived.Options);
        Assert.AreEqual(AngleUnit.Radians, derived.Options.AngleUnit);
        Assert.AreEqual(CalculationLimits.MaxInputLength, derived.Options.MaxInputLength);
        Assert.AreEqual(24, derived.Options.ResultFormat.SignificantDigits);
        Assert.AreEqual("0.333333", original.Evaluate("1/3").FormatResult(culture: CultureInfo.InvariantCulture).PrimaryDisplay);
        Assert.AreEqual("0.333333333333333333333333", derived.Evaluate("1/3").PrimaryDisplay);
    }

    [TestMethod]
    public void FluentDerivationTransformsTheCurrentOptionsOnceAndPreservesOtherSettings()
    {
        var options = new CalculatorOptions
        {
            Culture = CultureInfo.InvariantCulture,
            MaxInputLength = 100,
            Normalization = null,
            ResultFormat = new ResultFormatOptions { SignificantDigits = 6 }
        };
        var original = new CalculatorEngine(options);
        int calls = 0;
        var derived = original.WithOptions(current =>
        {
            calls++;
            Assert.AreSame(options, current);
            return current with { ResultFormat = current.ResultFormat with { SignificantDigits = 12 } };
        }).WithAngleUnit(AngleUnit.Degrees).WithCulture(CultureInfo.GetCultureInfo("de-DE"));

        Assert.AreEqual(1, calls);
        Assert.AreSame(options, original.Options);
        Assert.AreEqual(6, original.Options.ResultFormat.SignificantDigits);
        Assert.AreEqual(12, derived.Options.ResultFormat.SignificantDigits);
        Assert.AreEqual(100, derived.Options.MaxInputLength);
        Assert.IsNull(derived.Options.Normalization);
        Assert.AreEqual("de-DE", derived.Options.Culture.Name);
        Assert.AreEqual(AngleUnit.Degrees, derived.Options.AngleUnit);
        Assert.AreEqual("0,333333333333", derived.Evaluate("1/3").PrimaryDisplay);
        Assert.AreEqual(1.0, derived.Evaluate("sin(90)").DoubleValue);
    }

    [TestMethod]
    public void ExplicitCultureFactorySuppliesItsCultureToTheTransformationOnce()
    {
        var culture = CultureInfo.GetCultureInfo("fr-FR");
        int calls = 0;
        var engine = CalculatorEngine.ForCulture(culture, options =>
        {
            calls++;
            Assert.AreSame(culture, options.Culture);
            return options with { AngleUnit = AngleUnit.Degrees };
        });

        Assert.AreEqual(1, calls);
        Assert.AreSame(culture, engine.Options.Culture);
        Assert.AreEqual(AngleUnit.Degrees, engine.Options.AngleUnit);
        Assert.AreEqual(2.5m, engine.Evaluate("1,5+1").DecimalValue);
        Assert.AreSame(culture, CalculatorEngine.ForCulture(culture, configure: null).Options.Culture);
    }

    [TestMethod]
    public void CurrentCultureFactoryCapturesBeforeRunningItsTransformation()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
            var plain = CalculatorEngine.ForCurrentCulture(configure: null);
            int calls = 0;
            var configured = CalculatorEngine.ForCurrentCulture(options =>
            {
                calls++;
                Assert.AreEqual("de-DE", options.Culture.Name);
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
                return options with { AngleUnit = AngleUnit.Degrees };
            });

            Assert.AreEqual(1, calls);
            Assert.AreEqual("de-DE", plain.Options.Culture.Name);
            Assert.AreEqual("de-DE", configured.Options.Culture.Name);
            Assert.AreEqual(AngleUnit.Degrees, configured.Options.AngleUnit);
            Assert.AreEqual("en-US", CalculatorEngine.ForCurrentCulture().Options.Culture.Name);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [TestMethod]
    public void NormalizationEnabledByDefault_AcceptsTypographicOperators()
    {
        Assert.AreEqual(6, (int)Calculator.Evaluate("2×3").IntegerValue);
    }

    [TestMethod]
    public void RawMode_DoesNotNormalizeTypographicOperators()
    {
        Assert.IsFalse(TestEngines.RawEngine.TryEvaluate("2×3", out _, out var diagnostic));
        Assert.IsNotNull(diagnostic);
    }

    [TestMethod]
    public void RawMode_StillEvaluatesPlainExpressions()
    {
        Assert.AreEqual(5, (int)TestEngines.RawEngine.Evaluate("2+3").IntegerValue);
    }

    [TestMethod]
    public void Culture_DecimalCommaIsAcceptedWhenConfigured()
    {
        var engine = new CalculatorEngine(CalculatorOptions.ForCulture(new CultureInfo("de-DE")));
        Assert.AreEqual(2.5m, engine.Evaluate("1,5+1").DecimalValue);
    }

    [TestMethod]
    public void Constructor_RejectsInvalidOptions()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new CalculatorEngine(new CalculatorOptions { MaxInputLength = 0 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => new CalculatorEngine(new CalculatorOptions { AngleUnit = (AngleUnit)999 }));
    }

    [TestMethod]
    public void FacadeReusesReadOnlyCultureConfigurationWithoutSharingSessionState()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        try
        {
            var culture = CultureInfo.ReadOnly(new CultureInfo("en-US"));
            CultureInfo.CurrentCulture = culture;
            var firstContext = new EvaluationContext([new Variable("x", 2)], previousResult: 3);
            var secondContext = new EvaluationContext([new Variable("x", 11)], previousResult: 7);
            var first = Calculator.Evaluate("ans+x", firstContext);
            var second = Calculator.Evaluate("ans+x", secondContext);

            Assert.AreSame(first.Options, second.Options);
            Assert.AreSame(culture, first.Culture);
            Assert.AreEqual(new MathValue(5), first.Value);
            Assert.AreEqual(new MathValue(18), second.Value);
            Assert.AreEqual(new MathValue(3), firstContext.PreviousResult);
            Assert.AreEqual(new MathValue(7), secondContext.PreviousResult);

            var node = Calculator.Parse("2+3");
            Assert.AreSame(first.Options, Calculator.Evaluate(node).Options);
            Assert.IsTrue(Calculator.TryEvaluate("2+3", out var attempted, out var diagnostic), diagnostic?.Message);
            Assert.AreSame(first.Options, attempted.Options);
            Assert.IsTrue(Calculator.TryEvaluate(node, out attempted, out diagnostic), diagnostic?.Message);
            Assert.AreSame(first.Options, attempted.Options);
            Assert.IsFalse(Calculator.TryEvaluate("ans", out _, out diagnostic));
            Assert.AreEqual(EvaluationError.UnknownConstant, diagnostic.Code);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [TestMethod]
    public void FacadeDistinguishesReadOnlyCultureReferencesWithTheSameName()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        try
        {
            var firstCulture = CultureInfo.ReadOnly(new CultureInfo("en-US"));
            var customized = new CultureInfo("en-US");
            customized.NumberFormat.NumberDecimalSeparator = ",";
            customized.NumberFormat.NumberGroupSeparator = ".";
            customized.TextInfo.ListSeparator = ";";
            var secondCulture = CultureInfo.ReadOnly(customized);
            Assert.AreEqual(firstCulture.Name, secondCulture.Name);
            Assert.IsTrue(secondCulture.NumberFormat.IsReadOnly);
            Assert.IsTrue(secondCulture.TextInfo.IsReadOnly);

            CultureInfo.CurrentCulture = firstCulture;
            var first = Calculator.Evaluate("1000+2.5");
            Assert.AreEqual("1,002.5", first.PrimaryDisplay);

            CultureInfo.CurrentCulture = secondCulture;
            var second = Calculator.Evaluate("1000+2,5");
            Assert.AreEqual("1.002,5", second.PrimaryDisplay);
            Assert.AreEqual(2.5m, Calculator.Evaluate("max(1,5; 2,5)").DecimalValue);
            Assert.AreSame(secondCulture, second.Culture);
            Assert.AreNotSame(first.Options, second.Options);
            Assert.AreEqual("1,002.5", first.PrimaryDisplay);

            CultureInfo.CurrentCulture = firstCulture;
            var returned = Calculator.Evaluate("1000+2.5");
            Assert.AreEqual("1,002.5", returned.PrimaryDisplay);
            Assert.AreSame(firstCulture, returned.Culture);
            Assert.AreNotSame(first.Options, returned.Options);
            Assert.AreSame(returned.Options, Calculator.Evaluate("42").Options);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [TestMethod]
    public void FacadeDoesNotCacheWritableCulturesAndObservesTheirMutations()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        try
        {
            var culture = new CultureInfo("en-US");
            CultureInfo.CurrentCulture = culture;
            var first = Calculator.Evaluate("max(1.5, 2.5)");
            var repeated = Calculator.Evaluate("max(1.5, 2.5)");
            Assert.IsFalse(culture.IsReadOnly);
            Assert.AreNotSame(first.Options, repeated.Options);
            Assert.AreEqual("2.5", first.PrimaryDisplay);

            culture.NumberFormat.NumberDecimalSeparator = ",";
            culture.NumberFormat.NumberGroupSeparator = ".";
            culture.TextInfo.ListSeparator = ";";
            var changed = Calculator.Evaluate("max(1,5; 2,5)");

            Assert.AreEqual(2.5m, changed.DecimalValue);
            Assert.AreEqual("2,5", changed.PrimaryDisplay);
            Assert.AreSame(culture, changed.Culture);
            Assert.AreNotSame(repeated.Options, changed.Options);
            Assert.AreEqual("1.002,5", Calculator.FormatResult(Calculator.Evaluate("1000+2,5")).PrimaryDisplay);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [TestMethod]
    public void PublicDefaultFactoriesContinueToCreateFreshInstances()
    {
        var first = CalculatorEngine.Default;
        var second = CalculatorEngine.Default;
        var firstOptions = CalculatorOptions.Default;
        var secondOptions = CalculatorOptions.Default;

        Assert.AreNotSame(first, second);
        Assert.AreNotSame(first.Options, second.Options);
        Assert.AreNotSame(firstOptions, secondOptions);
        Assert.AreSame(CultureInfo.CurrentCulture, first.Options.Culture);
    }

    [TestMethod]
    public async Task FacadeCacheIsThreadLocalWhileCurrentCultureFlows()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        try
        {
            var culture = CultureInfo.ReadOnly(new CultureInfo("de-DE"));
            CultureInfo.CurrentCulture = culture;
            var parent = Calculator.Evaluate("1,5+1");
            var worker = await Task.Factory.StartNew(() =>
            {
                Assert.AreSame(culture, CultureInfo.CurrentCulture);
                var first = Calculator.Evaluate("1,5+1");
                var second = Calculator.Evaluate("1,5+1");
                Assert.AreSame(first.Options, second.Options);
                Assert.AreEqual(2.5m, second.DecimalValue);
                return first;
            }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);

            Assert.AreNotSame(parent.Options, worker.Options);
            Assert.AreSame(culture, worker.Culture);
            Assert.AreSame(culture, CultureInfo.CurrentCulture);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [TestMethod]
    public async Task FacadeFollowsEachAsyncFlowsCulture()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        (CultureInfo Culture, string Input, string Expected)[] cases =
        [
            (CultureInfo.ReadOnly(new CultureInfo("en-US")), "1000+2.5", "1,002.5"),
            (CultureInfo.ReadOnly(new CultureInfo("de-DE")), "1000+2,5", "1.002,5")
        ];

        await Task.WhenAll(cases.Select(item => Task.Run(async () =>
        {
            CultureInfo.CurrentCulture = item.Culture;
            for (int i = 0; i < 16; i++)
            {
                await Task.Yield();
                var result = Calculator.Evaluate(item.Input);
                Assert.AreSame(item.Culture, result.Culture);
                Assert.AreEqual(item.Expected, result.PrimaryDisplay);
            }
        })));

        Assert.AreSame(originalCulture, CultureInfo.CurrentCulture);
    }

    [TestMethod]
    public void WarmedUpFacadeAllocationsMatchARetainedEngineForEveryRoute()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.ReadOnly(new CultureInfo("en-US"));
            var engine = CalculatorEngine.ForCurrentCulture();
            var node = engine.Parse("2+3");
            var result = engine.Evaluate(node);
            (string Name, Action Facade, Action Retained)[] operations =
            [
                ("Evaluate text", () => Calculator.Evaluate("2+3"), () => engine.Evaluate("2+3")),
                ("Evaluate AST", () => Calculator.Evaluate(node), () => engine.Evaluate(node)),
                ("TryEvaluate text", () => Calculator.TryEvaluate("2+3", out _, out _), () => engine.TryEvaluate("2+3", out _, out _)),
                ("TryEvaluate AST", () => Calculator.TryEvaluate(node, out _, out _), () => engine.TryEvaluate(node, out _, out _)),
                ("Parse", () => Calculator.Parse("2+3"), () => engine.Parse("2+3")),
                ("TryParse", () => Calculator.TryParse("2+3", out _, out _), () => engine.TryParse("2+3", out _, out _)),
                ("FormatExpression text", () => Calculator.FormatExpression("2+3"), () => engine.FormatExpression("2+3")),
                ("FormatExpression AST", () => Calculator.FormatExpression(node), () => engine.FormatExpression(node)),
                ("FormatResult result", () => Calculator.FormatResult(result), () => engine.FormatResult(result)),
                ("FormatResult value", () => Calculator.FormatResult(result.Value), () => engine.FormatResult(result.Value))
            ];
            Action fresh = () => CalculatorEngine.Default.TryEvaluate("2+3", out _, out _);

            for (int i = 0; i < 64; i++)
            {
                foreach (var operation in operations)
                {
                    operation.Facade();
                    operation.Retained();
                }
                fresh();
            }

            foreach (var operation in operations)
            {
                long retainedBytes = MeasureAllocations(operation.Retained);
                long facadeBytes = MeasureAllocations(operation.Facade);
                Assert.AreEqual(retainedBytes, facadeBytes, operation.Name);
            }

            long freshBytes = MeasureAllocations(fresh);
            long cachedBytes = MeasureAllocations(() => Calculator.TryEvaluate("2+3", out _, out _));
            Assert.IsTrue(cachedBytes < freshBytes, $"Cached: {cachedBytes} bytes; fresh: {freshBytes} bytes.");
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    private static long MeasureAllocations(Action operation)
    {
        // The lowest of repeated samples excludes incidental runtime initialization
        // without allowing any extra per-call allocations in the facade.
        long minimum = long.MaxValue;
        for (int sample = 0; sample < 5; sample++)
        {
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 128; i++)
            {
                operation();
            }
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            minimum = Math.Min(minimum, allocated);
        }
        return minimum;
    }

    [TestMethod]
    public void SharedEngine_IsSafeAcrossThreads()
    {
        var engine = TestEngines.DegreesEngine;
        var failures = 0;

        Parallel.For(0, 500, i =>
        {
            var context = new EvaluationContext(
                variables: new Variable[]
                {
                    new("x", new MathValue((System.Numerics.BigInteger)i)),
                },
                previousResult: null);
            if (engine.Evaluate("x + 0", context).IntegerValue != i ||
                engine.Evaluate("sin(90)").DoubleValue != 1.0)
            {
                Interlocked.Increment(ref failures);
            }
        });

        Assert.AreEqual(0, failures);
    }
}
