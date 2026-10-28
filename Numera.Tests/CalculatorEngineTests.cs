using System.Globalization;

namespace Numera.Tests;

[TestClass]
public class CalculatorEngineTests
{
    [TestMethod]
    public void NullOptions_BehavesLikeDefault()
    {
        Assert.AreEqual(Calculator.Evaluate("2+3").IntVal, new CalculatorEngine().Evaluate("2+3").IntVal);
    }

    [TestMethod]
    public void NormalizationEnabledByDefault_AcceptsTypographicOperators()
    {
        Assert.AreEqual(6, (int)Calculator.Evaluate("2×3").IntVal);
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
        Assert.AreEqual(5, (int)TestEngines.RawEngine.Evaluate("2+3").IntVal);
    }

    [TestMethod]
    public void Culture_DecimalCommaIsAcceptedWhenConfigured()
    {
        var engine = new CalculatorEngine(CalculatorOptions.ForCulture(new CultureInfo("de-DE")));
        Assert.AreEqual(2.5m, engine.Evaluate("1,5+1").DecVal);
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
                variables: new Dictionary<string, MathValue>
                {
                    ["x"] = new MathValue((System.Numerics.BigInteger)i),
                },
                previousResult: null);
            if (engine.Evaluate("x + 0", context).IntVal != i ||
                engine.Evaluate("sin(90)").DblVal != 1.0)
            {
                Interlocked.Increment(ref failures);
            }
        });

        Assert.AreEqual(0, failures);
    }
}
