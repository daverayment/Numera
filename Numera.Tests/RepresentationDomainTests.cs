using System.Globalization;
using System.Numerics;

namespace Numera.Tests;

[TestClass]
public sealed class RepresentationDomainTests
{
    [TestMethod]
    [DataRow("42", MathValue.DataType.Integer)]
    [DataRow("0", MathValue.DataType.Integer)]
    [DataRow("42.0", MathValue.DataType.Decimal)]
    [DataRow("0.0", MathValue.DataType.Decimal)]
    [DataRow("1/2", MathValue.DataType.Rational)]
    [DataRow("hypot(0,42)", MathValue.DataType.Double)]
    [DataRow("ln(1)", MathValue.DataType.Double)]
    public void MismatchedStorageAccessorsThrowAcrossPublicSurfaces(string expression, MathValue.DataType kind)
    {
        var result = Calculator.Evaluate(expression);
        var value = result.Value;
        var variable = new Variable("x", value);
        Assert.AreEqual(kind, value.Kind);

        if (kind != MathValue.DataType.Integer)
        {
            Assert.ThrowsExactly<InvalidOperationException>(() => _ = value.IntegerValue);
            Assert.ThrowsExactly<InvalidOperationException>(() => _ = result.IntegerValue);
            Assert.ThrowsExactly<InvalidOperationException>(() => _ = variable.IntegerValue);
        }

        if (kind != MathValue.DataType.Decimal)
        {
            Assert.ThrowsExactly<InvalidOperationException>(() => _ = value.DecimalValue);
            Assert.ThrowsExactly<InvalidOperationException>(() => _ = result.DecimalValue);
            Assert.ThrowsExactly<InvalidOperationException>(() => _ = variable.DecimalValue);
        }

        if (kind != MathValue.DataType.Double)
        {
            Assert.ThrowsExactly<InvalidOperationException>(() => _ = value.DoubleValue);
            Assert.ThrowsExactly<InvalidOperationException>(() => _ = result.DoubleValue);
            Assert.ThrowsExactly<InvalidOperationException>(() => _ = variable.DoubleValue);
        }

        if (kind != MathValue.DataType.Rational)
        {
            Assert.ThrowsExactly<InvalidOperationException>(() => _ = value.Numerator);
            Assert.ThrowsExactly<InvalidOperationException>(() => _ = value.Denominator);
        }
    }

    [TestMethod]
    public void MatchingStorageAccessorsReturnStoredValuesAcrossPublicSurfaces()
    {
        var integer = Calculator.Evaluate("42");
        Assert.AreEqual(new BigInteger(42), integer.Value.IntegerValue);
        Assert.AreEqual(new BigInteger(42), integer.IntegerValue);
        Assert.AreEqual(new BigInteger(42), new Variable("x", integer.Value).IntegerValue);

        var dec = Calculator.Evaluate("42.00");
        Assert.AreEqual(42m, dec.Value.DecimalValue);
        Assert.AreEqual(42m, dec.DecimalValue);
        Assert.AreEqual(42m, new Variable("x", dec.Value).DecimalValue);

        var floating = Calculator.Evaluate("hypot(0,42)");
        Assert.AreEqual(42.0, floating.Value.DoubleValue);
        Assert.AreEqual(42.0, floating.DoubleValue);
        Assert.AreEqual(42.0, new Variable("x", floating.Value).DoubleValue);

        var half = Calculator.Evaluate("1/2").Value;
        Assert.AreEqual(BigInteger.One, half.Numerator);
        Assert.AreEqual(new BigInteger(2), half.Denominator);
    }

    [TestMethod]
    public void DefaultIntegerZeroAndDoubleSpecialValuesKeepTheirStoredValues()
    {
        var zero = default(MathValue);
        Assert.AreEqual(BigInteger.Zero, zero.IntegerValue);
        Assert.AreEqual(BigInteger.Zero, new EvaluationResult(zero, EvaluationContext.Empty).IntegerValue);
        Assert.AreEqual(BigInteger.Zero, new Variable("x", zero).IntegerValue);
        Assert.ThrowsExactly<InvalidOperationException>(() => _ = zero.DecimalValue);
        Assert.ThrowsExactly<InvalidOperationException>(() => _ = zero.DoubleValue);

        foreach (double input in new[] { 0.0, -0.0, double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        {
            var value = new MathValue(input);
            var result = new EvaluationResult(value, EvaluationContext.Empty);
            var variable = new Variable("x", value);
            long expectedBits = BitConverter.DoubleToInt64Bits(input);
            Assert.AreEqual(expectedBits, BitConverter.DoubleToInt64Bits(value.DoubleValue));
            Assert.AreEqual(expectedBits, BitConverter.DoubleToInt64Bits(result.DoubleValue));
            Assert.AreEqual(expectedBits, BitConverter.DoubleToInt64Bits(variable.DoubleValue));
            Assert.ThrowsExactly<InvalidOperationException>(() => _ = value.IntegerValue);
            Assert.ThrowsExactly<InvalidOperationException>(() => _ = value.DecimalValue);
        }
    }

    [TestMethod]
    public void ExplicitConversionsRemainIndependentOfStorageAccessors()
    {
        foreach (var value in new[] { new MathValue(42), new MathValue(42m), new MathValue(42.0) })
        {
            var result = new EvaluationResult(value, EvaluationContext.Empty);
            Assert.AreEqual(42.0, value.AsDouble());
            Assert.AreEqual(42m, value.ToDecimal());
            Assert.IsTrue(value.TryGetInteger(out var integer));
            Assert.AreEqual(new BigInteger(42), integer);
            Assert.AreEqual(42, (int)result);
            Assert.AreEqual(42m, (decimal)result);
            Assert.AreEqual(42.0, (double)result);
        }

        var half = Calculator.Evaluate("1/2");
        Assert.AreEqual(0.5, half.AsDouble());
        Assert.AreEqual(0.5m, (decimal)half);
        Assert.ThrowsExactly<InvalidCastException>(() => _ = (int)half);
        Assert.AreEqual(MathValue.DataType.Rational, half.Type);
    }

    [TestMethod]
    [DataRow("1/2", MathValue.DataType.Rational, 1, 2)]
    [DataRow("1/3", MathValue.DataType.Rational, 1, 3)]
    [DataRow("71/9", MathValue.DataType.Rational, 71, 9)]
    [DataRow("10/2", MathValue.DataType.Integer, 5, 1)]
    [DataRow("1/3+2/3", MathValue.DataType.Integer, 1, 1)]
    [DataRow("1/2+1/2", MathValue.DataType.Integer, 1, 1)]
    [DataRow("0.5", MathValue.DataType.Decimal, 1, 2)]
    [DataRow("194.40", MathValue.DataType.Decimal, 972, 5)]
    [DataRow("1.00", MathValue.DataType.Decimal, 1, 1)]
    [DataRow("0.00", MathValue.DataType.Decimal, 0, 1)]
    [DataRow("1e-2", MathValue.DataType.Decimal, 1, 100)]
    [DataRow("100e-2", MathValue.DataType.Decimal, 1, 1)]
    [DataRow("1e2", MathValue.DataType.Integer, 100, 1)]
    [DataRow("1.0e2", MathValue.DataType.Decimal, 100, 1)]
    [DataRow("0.1+0.2", MathValue.DataType.Decimal, 3, 10)]
    [DataRow("0.5+0.5", MathValue.DataType.Decimal, 1, 1)]
    [DataRow("0.5*2", MathValue.DataType.Decimal, 1, 1)]
    [DataRow("1+0.0", MathValue.DataType.Decimal, 1, 1)]
    [DataRow("2.0/2", MathValue.DataType.Decimal, 1, 1)]
    [DataRow("2/2.0", MathValue.DataType.Decimal, 1, 1)]
    [DataRow("1.0/3", MathValue.DataType.Rational, 1, 3)]
    [DataRow("1/3+0.5", MathValue.DataType.Rational, 5, 6)]
    [DataRow("1/4+0.5", MathValue.DataType.Rational, 3, 4)]
    [DataRow("1/2+0.5", MathValue.DataType.Integer, 1, 1)]
    [DataRow("1/2*0.5", MathValue.DataType.Rational, 1, 4)]
    [DataRow("0.5-1/2", MathValue.DataType.Integer, 0, 1)]
    [DataRow("1.5%1", MathValue.DataType.Decimal, 1, 2)]
    [DataRow("1.0%1", MathValue.DataType.Decimal, 0, 1)]
    [DataRow("2^-1", MathValue.DataType.Rational, 1, 2)]
    [DataRow("2.0^-1", MathValue.DataType.Decimal, 1, 2)]
    [DataRow("0.5^0", MathValue.DataType.Decimal, 1, 1)]
    [DataRow("0.5^-1", MathValue.DataType.Decimal, 2, 1)]
    [DataRow("0.5^2", MathValue.DataType.Decimal, 1, 4)]
    [DataRow("abs(-1/2)", MathValue.DataType.Rational, 1, 2)]
    [DataRow("abs(-0.50)", MathValue.DataType.Decimal, 1, 2)]
    [DataRow("floor(1.5)", MathValue.DataType.Decimal, 1, 1)]
    [DataRow("ceil(0.5)", MathValue.DataType.Decimal, 1, 1)]
    [DataRow("trunc(1.5)", MathValue.DataType.Decimal, 1, 1)]
    [DataRow("frac(1.0)", MathValue.DataType.Decimal, 0, 1)]
    [DataRow("round(0.5)", MathValue.DataType.Decimal, 1, 1)]
    [DataRow("round(0.5, 1)", MathValue.DataType.Decimal, 1, 2)]
    [DataRow("round(1/2)", MathValue.DataType.Integer, 1, 1)]
    [DataRow("round(1/3, 2)", MathValue.DataType.Rational, 33, 100)]
    [DataRow("sqrt(16.0)", MathValue.DataType.Decimal, 4, 1)]
    [DataRow("cbrt(125.0)", MathValue.DataType.Decimal, 5, 1)]
    [DataRow("root(81.0, 4)", MathValue.DataType.Decimal, 3, 1)]
    [DataRow("avg(1, 2)", MathValue.DataType.Rational, 3, 2)]
    [DataRow("avg(1.0, 2.0)", MathValue.DataType.Decimal, 3, 2)]
    [DataRow("median(1, 2)", MathValue.DataType.Rational, 3, 2)]
    [DataRow("sum(0.5, 0.5)", MathValue.DataType.Decimal, 1, 1)]
    [DataRow("mul(0.5, 2)", MathValue.DataType.Decimal, 1, 1)]
    [DataRow("variance(1, 2, 3)", MathValue.DataType.Integer, 1, 1)]
    [DataRow("variance(1.0, 2.0, 3.0)", MathValue.DataType.Decimal, 1, 1)]
    [DataRow("0.5==0.5", MathValue.DataType.Integer, 1, 1)]
    public void RepresentationFollowsTheExactArithmeticDomain(
        string expression, MathValue.DataType kind, int numerator, int denominator)
    {
        var result = Calculator.Evaluate(expression);
        Assert.AreEqual(kind, result.Type, expression);

        var expected = (new BigInteger(numerator), new BigInteger(denominator));
        Assert.AreEqual(expected, result.GetExactFraction(), expression);
        Assert.AreEqual(expected, result.Value.GetExactFraction(), expression);
        Assert.AreEqual(CalculationExactness.Exact, result.Exactness);
        Assert.IsTrue(result.HasExactFraction);
        Assert.IsTrue(result.ExactFraction is (var _, var _));
    }

    [TestMethod]
    public void FractionFactoriesNeverChooseDecimalForTerminatingFractions()
    {
        foreach (var value in new[]
        {
            MathValue.FromFraction(1, 2), MathValue.FromRational(2, 4), MathValue.FromRational(-2, -4)
        })
        {
            Assert.AreEqual(MathValue.DataType.Rational, value.Kind);
            Assert.AreEqual(BigInteger.One, value.Numerator);
            Assert.AreEqual(new BigInteger(2), value.Denominator);
            Assert.AreEqual(0.5m, value.ToDecimal());
            Assert.AreEqual(value, new MathValue(0.5m));
            Assert.AreEqual(value.GetHashCode(), new MathValue(0.5m).GetHashCode());
        }

        var one = MathValue.FromRational(3, 3);
        Assert.AreEqual(MathValue.DataType.Integer, one.Kind);
        Assert.AreEqual((BigInteger.One, BigInteger.One), one.GetExactFraction());
        Assert.ThrowsExactly<InvalidOperationException>(() => _ = one.Numerator);
        Assert.ThrowsExactly<InvalidOperationException>(() => _ = one.Denominator);

        var decimalOne = new MathValue(1.00m);
        Assert.ThrowsExactly<InvalidOperationException>(() => _ = decimalOne.Numerator);
        Assert.ThrowsExactly<InvalidOperationException>(() => _ = decimalOne.Denominator);
    }

    [TestMethod]
    public void FractionAvailabilityDependsOnExactnessNotStorageOrDisplay()
    {
        foreach (var value in new[]
        {
            default(MathValue), new MathValue(1).WithExactness(CalculationExactness.Unverified),
            new MathValue(0.5m).WithExactness(CalculationExactness.Unverified),
            MathValue.FromFraction(1, 3).WithExactness(CalculationExactness.Unverified),
            new MathValue(0.5), new MathValue(double.NaN),
            new MathValue(double.PositiveInfinity), new MathValue(double.NegativeInfinity),
            Calculator.Evaluate("sqrt(2)").Value
        })
        {
            var result = new EvaluationResult(value, EvaluationContext.Empty);
            Assert.IsNull(value.ExactFraction);
            Assert.IsNull(result.ExactFraction);
            Assert.IsFalse(result.HasExactFraction);
            Assert.ThrowsExactly<InvalidOperationException>(() => value.GetExactFraction());
            Assert.ThrowsExactly<InvalidOperationException>(() => result.GetExactFraction());
        }

        var trusted = new MathValue(0.1).WithExactness(CalculationExactness.Exact);
        Assert.AreEqual(
            (BigInteger.Parse("3602879701896397"), BigInteger.Parse("36028797018963968")),
            trusted.GetExactFraction());
        Assert.AreEqual(
            (BigInteger.Zero, BigInteger.One),
            new MathValue(0).GetExactFraction());
    }

    [TestMethod]
    public void DecimalCapacityNeverRoundsOrSilentlyUsesDouble()
    {
        var max = new MathValue(decimal.MaxValue);
        var beyond = max + new MathValue(1);
        Assert.AreEqual(MathValue.DataType.Integer, beyond.Kind);
        Assert.AreEqual(new BigInteger(decimal.MaxValue) + 1, beyond.IntegerValue);

        var fractional = max + new MathValue(0.1m);
        Assert.AreEqual(MathValue.DataType.Rational, fractional.Kind);
        Assert.AreEqual((new BigInteger(decimal.MaxValue) * 10 + 1, new BigInteger(10)), fractional.GetExactFraction());
        Assert.AreEqual(CalculationExactness.Exact, fractional.Exactness);

        var tiny = Calculator.Evaluate("1e-400");
        Assert.AreEqual(MathValue.DataType.Rational, tiny.Type);
        Assert.AreEqual((BigInteger.One, BigInteger.Pow(10, 400)), tiny.GetExactFraction());

        var longLiteral = Calculator.Evaluate("0.123456789012345678901234567890123456789");
        Assert.AreEqual(MathValue.DataType.Rational, longLiteral.Type);
        Assert.AreEqual(BigInteger.Parse("123456789012345678901234567890123456789"), longLiteral.GetExactFraction().Numerator);
        Assert.AreEqual(BigInteger.Pow(10, 39), longLiteral.GetExactFraction().Denominator);
    }

    [TestMethod]
    public void CapturedCompositionAndSerializationPreserveDomainsAndScale()
    {
        var engine = new CalculatorEngine();
        var captured = engine.Evaluate("amount=194.40");
        Assert.AreEqual(MathValue.DataType.Decimal, engine.Evaluate("amount-100", captured.Context).Type);
        Assert.AreEqual(MathValue.DataType.Decimal, (captured - 100).Type);
        Assert.AreEqual("94.40", (captured - 100).ToString());
        Assert.AreEqual("94.40", (captured - 100).MachineReadable);

        var half = engine.Evaluate("1/2");
        Assert.AreEqual(MathValue.DataType.Rational, (half + new MathValue(0.25m)).Type);
        Assert.AreEqual(MathValue.DataType.Integer, (half + new MathValue(0.5m)).Type);
        foreach (var value in new[] { captured.Value, half.Value, engine.Evaluate("0.5+0.5").Value })
        {
            var restored = MathValue.Deserialize(value.Serialize());
            Assert.AreEqual(value.Kind, restored.Kind);
            Assert.AreEqual(value.GetExactFraction(), restored.GetExactFraction());
            Assert.AreEqual(value.DecimalDisplayScale, restored.DecimalDisplayScale);
        }
        Assert.AreEqual("1/2", half.MachineReadable);
        Assert.AreEqual("0.5", half.ToString(CultureInfo.InvariantCulture));
        Assert.AreEqual("1/2", half.FormatResult().FractionRepresentation);
    }

    [TestMethod]
    public void DoubleMixingRemainsUnverifiedAndConversionsAreExplicit()
    {
        var exact = MathValue.FromFraction(1, 2);
        var mixed = exact + new MathValue(0.5);
        Assert.AreEqual(MathValue.DataType.Double, mixed.Kind);
        Assert.AreEqual(CalculationExactness.Unverified, mixed.Exactness);
        Assert.IsNull(mixed.ExactFraction);
        Assert.AreEqual(0.5m, exact.ToDecimal());
        Assert.AreEqual(MathValue.DataType.Rational, exact.Kind);
        Assert.AreEqual(0.3333333333333333333333333333m, MathValue.FromRational(1, 3).ToDecimal());
    }

    [TestMethod]
    public void DecimalRepresentationWorkHonorsLimitsWithoutChangingDomainToAvoidThem()
    {
        var rational = MathValue.Pow(MathValue.FromFraction(1, 2), new MathValue(14));
        Assert.AreEqual((BigInteger.One, new BigInteger(16384)), rational.GetExactFraction());
        
        Assert.AreEqual(
            MathValue.DataType.Decimal,
            MathValue.Pow(new MathValue(10m), new MathValue(3)).Kind);
        
        var engine = new CalculatorEngine(new CalculatorOptions { MaxIntegerBits = 8 });
        Assert.IsFalse(engine.TryEvaluate("10^3", out _, out var diagnostic));
        Assert.AreEqual(EvaluationError.LimitExceeded, diagnostic.Code);
    }
}
