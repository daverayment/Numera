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
    public void Default_IsInvariantWithNormalizationEnabled()
    {
        Assert.AreSame(CultureInfo.InvariantCulture, CalculatorOptions.Default.Culture);
        Assert.IsNotNull(CalculatorOptions.Default.Normalization);
        Assert.AreEqual(AngleUnit.Radians, CalculatorOptions.Default.AngleUnit);
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
    public void DefaultConstructor_Matches_ForCultureInvariantCulture()
    {
        var engine1 = new CalculatorEngine();
        var engine2 = new CalculatorEngine(CalculatorOptions.ForCulture(CultureInfo.InvariantCulture));

        Assert.AreEqual(engine1.Options.Culture, engine2.Options.Culture);
        Assert.AreEqual(engine1.Options.AngleUnit, engine2.Options.AngleUnit);
        Assert.AreEqual(engine1.Options.Normalization, engine2.Options.Normalization);
        Assert.AreEqual(engine1.Evaluate("2×3! + 10%").DecimalValue,
            engine2.Evaluate("2×3! + 10%").DecimalValue);
    }
}
