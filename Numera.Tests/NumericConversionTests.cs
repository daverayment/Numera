using System.Globalization;
using System.Numerics;

namespace Numera.Tests;

[TestClass]
public sealed class NumericConversionTests
{
    [TestMethod]
    public void WholeNumberConversionsWorkAcrossStorageAndExactness()
    {
        MathValue[] values =
        [
            default,
            new MathValue(42),
            new MathValue(-42),
            new MathValue(42.00m),
            new MathValue(-42.00m),
            new MathValue(42.0),
            new MathValue(-42.0),
            new MathValue(-0.0),
            MathValue.FromFraction(84, 2),
            new MathValue(42).WithExactness(CalculationExactness.Unverified),
            new MathValue(42.0).WithExactness(CalculationExactness.Exact)
        ];

        foreach (var value in values)
        {
            var result = Wrap(value);
            var expected = new BigInteger(value.ToDouble());
            Assert.AreEqual(expected, value.ToBigInteger());
            Assert.AreEqual(expected, result.ToBigInteger());
            Assert.AreEqual(expected, (BigInteger)value);
            Assert.AreEqual(expected, (BigInteger)result);
            Assert.AreEqual((int)expected, (int)value);
            Assert.AreEqual((int)expected, (int)result);
            Assert.AreEqual((long)expected, (long)value);
            Assert.AreEqual((long)expected, (long)result);
            Assert.IsTrue(value.TryGetInteger(out var original));
            Assert.IsTrue(result.TryGetInteger(out var forwarded));
            Assert.AreEqual(expected, original);
            Assert.AreEqual(expected, forwarded);
        }
    }

    [TestMethod]
    public void FractionalAndNonfiniteIntegerConversionsRejectRatherThanTruncate()
    {
        MathValue[] values =
        [
            MathValue.FromFraction(3, 2),
            MathValue.FromFraction(-3, 2),
            new MathValue(1.5m),
            new MathValue(-1.5m),
            new MathValue(1.5),
            new MathValue(-1.5),
            new MathValue(double.Epsilon),
            new MathValue(double.NaN),
            new MathValue(double.PositiveInfinity),
            new MathValue(double.NegativeInfinity)
        ];

        foreach (var value in values)
        {
            var result = Wrap(value);
            Assert.ThrowsExactly<InvalidCastException>(() => value.ToBigInteger());
            Assert.ThrowsExactly<InvalidCastException>(() => result.ToBigInteger());
            Assert.ThrowsExactly<InvalidCastException>(() => _ = (BigInteger)value);
            Assert.ThrowsExactly<InvalidCastException>(() => _ = (BigInteger)result);
            Assert.ThrowsExactly<InvalidCastException>(() => _ = (int)value);
            Assert.ThrowsExactly<InvalidCastException>(() => _ = (int)result);
            Assert.ThrowsExactly<InvalidCastException>(() => _ = (long)value);
            Assert.ThrowsExactly<InvalidCastException>(() => _ = (long)result);
            Assert.IsFalse(value.TryGetInteger(out var original));
            Assert.IsFalse(result.TryGetInteger(out var forwarded));
            Assert.AreEqual(BigInteger.Zero, original);
            Assert.AreEqual(BigInteger.Zero, forwarded);
        }
    }

    [TestMethod]
    public void FixedWidthIntegerConversionsEnforceExactRangeBoundaries()
    {
        foreach (var input in new[] { int.MinValue, int.MaxValue })
        {
            var value = new MathValue(input);
            Assert.AreEqual(input, (int)value);
            Assert.AreEqual(input, (int)Wrap(value));
        }

        foreach (var input in new[] { new BigInteger(int.MinValue) - 1, new BigInteger(int.MaxValue) + 1 })
        {
            var value = new MathValue(input);
            Assert.ThrowsExactly<OverflowException>(() => _ = (int)value);
            Assert.ThrowsExactly<OverflowException>(() => _ = (int)Wrap(value));
            Assert.AreEqual((long)input, (long)value);
            Assert.AreEqual((long)input, (long)Wrap(value));
        }

        foreach (var input in new[] { long.MinValue, long.MaxValue })
        {
            var value = new MathValue(input);
            Assert.AreEqual(input, (long)value);
            Assert.AreEqual(input, (long)Wrap(value));
        }

        foreach (var input in new[] { new BigInteger(long.MinValue) - 1, new BigInteger(long.MaxValue) + 1 })
        {
            var value = new MathValue(input);
            Assert.ThrowsExactly<OverflowException>(() => _ = (long)value);
            Assert.ThrowsExactly<OverflowException>(() => _ = (long)Wrap(value));
            Assert.AreEqual(input, value.ToBigInteger());
            Assert.AreEqual(input, (BigInteger)Wrap(value));
        }
    }

    [TestMethod]
    public void LargeWholeNumberConversionsDoNotPassThroughDouble()
    {
        var expected = (BigInteger.One << 200) + 1;
        var value = new MathValue(expected);
        Assert.AreEqual(expected, value.ToBigInteger());
        Assert.AreEqual(expected, Wrap(value).ToBigInteger());
        Assert.AreEqual(expected, (BigInteger)value);
        Assert.AreEqual(expected, (BigInteger)Wrap(value));

        var floating = new MathValue(double.MaxValue);
        var storedInteger = new BigInteger(double.MaxValue);
        Assert.AreEqual(storedInteger, floating.ToBigInteger());
        Assert.AreEqual(storedInteger, Wrap(floating).ToBigInteger());
        Assert.ThrowsExactly<OverflowException>(() => _ = (long)floating);
        Assert.ThrowsExactly<OverflowException>(() => _ = (long)Wrap(floating));
    }

    [TestMethod]
    public void DecimalConversionsPreserveExactValuesAndMidpointToEvenRounding()
    {
        var scale = BigInteger.Pow(10, 29);
        (MathValue Value, decimal Expected)[] cases =
        [
            (default, 0m),
            (new MathValue(42), 42m),
            (new MathValue(194.40m), 194.40m),
            (MathValue.FromFraction(1, 8), 0.125m),
            (MathValue.FromFraction(1, 3), 0.3333333333333333333333333333m),
            (MathValue.FromFraction(5, scale), 0m),
            (MathValue.FromFraction(15, scale), 0.0000000000000000000000000002m),
            (MathValue.FromFraction(-15, scale), -0.0000000000000000000000000002m),
            (new MathValue(0.1), (decimal)0.1),
            (new MathValue(decimal.MaxValue), decimal.MaxValue),
            (new MathValue(decimal.MinValue), decimal.MinValue)
        ];

        foreach (var (value, expected) in cases)
        {
            var result = Wrap(value);
            Assert.AreEqual(expected, value.ToDecimal());
            Assert.AreEqual(expected, (decimal)value);
            Assert.AreEqual(expected, result.ToDecimal());
            Assert.AreEqual(expected, (decimal)result);
        }

        var scaled = new MathValue(194.40m);
        CollectionAssert.AreEqual(decimal.GetBits(scaled.DecimalValue), decimal.GetBits(Wrap(scaled).ToDecimal()));
    }

    [TestMethod]
    public void DecimalConversionsRejectNonfiniteAndOutOfRangeValues()
    {
        MathValue[] values =
        [
            new MathValue(new BigInteger(decimal.MaxValue) + 1),
            new MathValue(new BigInteger(decimal.MinValue) - 1),
            new MathValue(BigInteger.One << 200),
            new MathValue(double.MaxValue),
            new MathValue(double.NaN),
            new MathValue(double.PositiveInfinity),
            new MathValue(double.NegativeInfinity)
        ];

        foreach (var value in values)
        {
            var result = Wrap(value);
            Assert.ThrowsExactly<OverflowException>(() => value.ToDecimal());
            Assert.ThrowsExactly<OverflowException>(() => _ = (decimal)value);
            Assert.ThrowsExactly<OverflowException>(() => result.ToDecimal());
            Assert.ThrowsExactly<OverflowException>(() => _ = (decimal)result);
        }
    }

    [TestMethod]
    public void DoubleConversionsRoundOnceAndRetainSpecialValues()
    {
        (MathValue Value, double Expected)[] cases =
        [
            (default, 0.0),
            (new MathValue((BigInteger.One << 53) + 1), 9007199254740992.0),
            (new MathValue((BigInteger.One << 53) + 3), 9007199254740996.0),
            (MathValue.FromFraction(1, 3), 1.0 / 3.0),
            (new MathValue(0.1m), 0.1),
            (new MathValue(BigInteger.One << 2000), double.PositiveInfinity),
            (new MathValue(-(BigInteger.One << 2000)), double.NegativeInfinity),
            (MathValue.FromFraction(1, BigInteger.One << 2000), 0.0),
            (MathValue.FromFraction(-1, BigInteger.One << 2000), -0.0),
            (new MathValue(-0.0), -0.0),
            (new MathValue(double.Epsilon), double.Epsilon),
            (new MathValue(double.NaN), double.NaN),
            (new MathValue(double.PositiveInfinity), double.PositiveInfinity),
            (new MathValue(double.NegativeInfinity), double.NegativeInfinity)
        ];

        foreach (var (value, expected) in cases)
        {
            var result = Wrap(value);
            long bits = BitConverter.DoubleToInt64Bits(expected);
            Assert.AreEqual(bits, BitConverter.DoubleToInt64Bits(value.ToDouble()));
            Assert.AreEqual(bits, BitConverter.DoubleToInt64Bits(value.AsDouble()));
            Assert.AreEqual(bits, BitConverter.DoubleToInt64Bits((double)value));
            Assert.AreEqual(bits, BitConverter.DoubleToInt64Bits(result.ToDouble()));
            Assert.AreEqual(bits, BitConverter.DoubleToInt64Bits(result.AsDouble()));
            Assert.AreEqual(bits, BitConverter.DoubleToInt64Bits((double)result));
        }
    }

    [TestMethod]
    public void ExactFractionForwardingPreservesAvailabilityAndOutputValues()
    {
        (MathValue Value, BigInteger Numerator, BigInteger Denominator)[] available =
        [
            (new MathValue(42), 42, 1),
            (new MathValue(0.5m), 1, 2),
            (MathValue.FromFraction(-2, 6), -1, 3),
            (new MathValue(0.5).WithExactness(CalculationExactness.Exact), 1, 2)
        ];
        foreach (var (value, numerator, denominator) in available)
        {
            var result = Wrap(value);
            Assert.IsTrue(value.TryGetExactFraction(out var originalNumerator, out var originalDenominator));
            Assert.IsTrue(result.TryGetExactFraction(out var forwardedNumerator, out var forwardedDenominator));
            Assert.AreEqual(numerator, originalNumerator);
            Assert.AreEqual(denominator, originalDenominator);
            Assert.AreEqual(numerator, forwardedNumerator);
            Assert.AreEqual(denominator, forwardedDenominator);
            Assert.AreEqual((numerator, denominator), result.GetExactFraction());
        }

        MathValue[] unavailable =
        [
            default,
            new MathValue(0.5),
            MathValue.FromFraction(1, 3).WithExactness(CalculationExactness.Unverified),
            new MathValue(double.NaN),
            new MathValue(double.PositiveInfinity),
            new MathValue(double.NegativeInfinity)
        ];
        foreach (var value in unavailable)
        {
            Assert.IsFalse(Wrap(value).TryGetExactFraction(out var numerator, out var denominator));
            Assert.AreEqual(BigInteger.Zero, numerator);
            Assert.AreEqual(BigInteger.Zero, denominator);
        }

        var unverified = Wrap(MathValue.FromFraction(2, 6).WithExactness(CalculationExactness.Unverified));
        Assert.AreEqual(BigInteger.One, unverified.Numerator);
        Assert.AreEqual(new BigInteger(3), unverified.Denominator);
        Assert.IsFalse(unverified.HasExactFraction);
    }

    [TestMethod]
    public void ConversionsDoNotChangeSourceRepresentationMetadataOrContext()
    {
        var engine = CalculatorEngine.ForCulture(CultureInfo.InvariantCulture);
        var result = engine.Evaluate("amount=194.40");
        var before = result.Value.Serialize();
        var context = result.Context;
        var options = result.Options;
        _ = result.ToDecimal();
        _ = result.ToDouble();
        Assert.IsFalse(result.TryGetInteger(out _));
        Assert.ThrowsExactly<InvalidCastException>(() => result.ToBigInteger());
        Assert.AreEqual(before, result.Value.Serialize());
        Assert.AreSame(context, result.Context);
        Assert.AreSame(options, result.Options);
        Assert.AreEqual(CalculationExactness.Exact, result.Exactness);

        var approximate = new MathValue(42.0);
        _ = approximate.ToBigInteger();
        _ = approximate.ToDecimal();
        Assert.AreEqual(MathValue.DataType.Double, approximate.Kind);
        Assert.AreEqual(CalculationExactness.Unverified, approximate.Exactness);
    }

    private static EvaluationResult Wrap(MathValue value) => new(value, EvaluationContext.Empty);
}