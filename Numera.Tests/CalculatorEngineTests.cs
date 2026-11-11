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
