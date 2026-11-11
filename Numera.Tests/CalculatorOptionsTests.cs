using System.Globalization;

namespace Numera.Tests;

[TestClass]
public sealed class CalculatorOptionsTests
{
    [TestMethod]
    public void Validate_RejectsOutOfRangeLimits()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new CalculatorOptions { MaxInputLength = 0 }.Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new CalculatorOptions { MaxInputLength = -1 }.Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new CalculatorOptions { MaxInputLength = CalculationLimits.MaxInputLength + 1 }.Validate());

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new CalculatorOptions { MaxNestingDepth = 0 }.Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new CalculatorOptions { MaxNestingDepth = -1 }.Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new CalculatorOptions { MaxNestingDepth = CalculationLimits.MaxNestingDepth + 1 }.Validate());

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new CalculatorOptions { AngleUnit = (AngleUnit)(-1) }.Validate());
    }

    [TestMethod]
    public void ForCulture_RejectsNull() =>
        Assert.Throws<ArgumentNullException>(() => CalculatorOptions.ForCulture(null!));

    [TestMethod]
    public void RequiredOptionsRejectNullDuringValidationAndEngineConstruction()
    {
        (CalculatorOptions Options, string Parameter)[] cases =
        [
            (new CalculatorOptions { Culture = null! }, nameof(CalculatorOptions.Culture)),
            (new CalculatorOptions { CustomFunctions = null! }, nameof(CalculatorOptions.CustomFunctions)),
            (new CalculatorOptions { ResultFormat = null! }, nameof(CalculatorOptions.ResultFormat))
        ];

        foreach (var (options, parameter) in cases)
        {
            Assert.AreEqual(parameter, Assert.ThrowsExactly<ArgumentNullException>(options.Validate).ParamName);
            Assert.AreEqual(parameter, Assert.ThrowsExactly<ArgumentNullException>(() => new CalculatorEngine(options)).ParamName);
        }
    }

    [TestMethod]
    public void Defaults_CaptureCurrentCultureWithNormalizationEnabled()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
            var defaults = CalculatorOptions.Default;
            var options = new CalculatorOptions();
            var engine = new CalculatorEngine();
            var defaultEngine = CalculatorEngine.Default;
            var configuredEngine = new CalculatorEngine(settings => settings with { AngleUnit = AngleUnit.Degrees });

            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");

            Assert.AreEqual("de-DE", defaults.Culture.Name);
            Assert.AreEqual("de-DE", options.Culture.Name);
            Assert.AreEqual("de-DE", engine.Options.Culture.Name);
            Assert.AreEqual("de-DE", defaultEngine.Options.Culture.Name);
            Assert.AreEqual("de-DE", configuredEngine.Options.Culture.Name);
            Assert.AreEqual(AngleUnit.Degrees, configuredEngine.Options.AngleUnit);
            Assert.IsNotNull(defaults.Normalization);
            Assert.AreEqual(AngleUnit.Radians, defaults.AngleUnit);
            Assert.AreEqual("en-US", CalculatorOptions.Default.Culture.Name);
            Assert.AreEqual("en-US", CalculatorEngine.Default.Options.Culture.Name);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [TestMethod]
    public void ForCurrentCulture_CapturesCultureAtCallTime()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");
            var options = CalculatorOptions.ForCurrentCulture();

            // Later culture changes must not alter an already-created options instance.
            CultureInfo.CurrentCulture = new CultureInfo("en-US");

            Assert.AreEqual("de-DE", options.Culture.Name);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [TestMethod]
    public void ForCulture_And_ForCurrentCulture_HaveNormalizationEnabledByDefault()
    {
        var options = CalculatorOptions.ForCurrentCulture();
        Assert.IsNotNull(options.Normalization,
            "Normalization must be ON by default for culture options.");
        Assert.IsTrue(options.Normalization.TrimLauncherEquals);
        Assert.IsTrue(options.Normalization.ReplaceUnicodeOperators);
        Assert.IsTrue(options.Normalization.ReplaceDivisionColon);

        var deOptions = CalculatorOptions.ForCulture(new CultureInfo("de-DE"));
        Assert.IsNotNull(deOptions.Normalization);
    }

    [TestMethod]
    public void DefaultConstructor_Matches_ForCurrentCulture()
    {
        var engine1 = new CalculatorEngine();
        var engine2 = CalculatorEngine.ForCurrentCulture();

        Assert.AreEqual(engine1.Options.Culture, engine2.Options.Culture);
        Assert.AreEqual(engine1.Options.AngleUnit, engine2.Options.AngleUnit);
        Assert.AreEqual(engine1.Options.Normalization, engine2.Options.Normalization);
        var result1 = engine1.Evaluate("2×3! + 10%");
        var result2 = engine2.Evaluate("2×3! + 10%");
        Assert.AreEqual(new MathValue(13.2m), result1.Value);
        Assert.AreEqual(result1.Value, result2.Value);
    }

    [TestMethod]
    public void ExplicitInvariantCultureIsIndependentOfCurrentCulture()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
            var engine = new CalculatorEngine(new CalculatorOptions { Culture = CultureInfo.InvariantCulture });
            var result = engine.Evaluate("1000 + 2.5");

            Assert.AreSame(CultureInfo.InvariantCulture, engine.Options.Culture);
            Assert.AreEqual(1002.5m, result.DecimalValue);
            Assert.AreEqual("1,002.5", result.PrimaryDisplay);
            Assert.AreEqual(result.Value, engine.Evaluate(result.PrimaryDisplay).Value);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }
}
