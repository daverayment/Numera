using System.Globalization;
using System.Numerics;

namespace Numera.Tests;

[TestClass]
public sealed class FromRationalTests
{
    private static readonly BigInteger MaxMantissa = (BigInteger.One << 96) - BigInteger.One;

    [TestMethod]
    [DataRow(0, 1, "0")]
    [DataRow(0, 5, "0")]
    [DataRow(10, 2, "5")]
    [DataRow(-10, 2, "-5")]
    [DataRow(10, -2, "-5")]
    [DataRow(-10, -2, "5")]
    [DataRow(33, 11, "3")]
    [DataRow(100, 1, "100")]
    public void FromRational_ExactIntegers_ReturnIntegerKind(long n, long d, string expected)
    {
        var result = MathValue.FromRational(n, d);

        Assert.AreEqual(MathValue.DataType.Integer, result.Kind);
        Assert.AreEqual(
            BigInteger.Parse(expected, CultureInfo.InvariantCulture), result.IntegerValue);
    }

    [TestMethod]
    public void FromRational_LargeInteger_PreservesExactBigInteger()
    {
        var large = BigInteger.Pow(10, 50);
        var result = MathValue.FromRational(large * 3, 3);

        Assert.AreEqual(MathValue.DataType.Integer, result.Kind);
        Assert.AreEqual(large, result.IntegerValue);
    }

    // Terminating decimals (powers of 2 and 5).

    [TestMethod]
    [DataRow(1, 2, "0.5")]
    [DataRow(1, 4, "0.25")]
    [DataRow(1, 8, "0.125")]
    [DataRow(1, 16, "0.0625")]
    [DataRow(1, 32, "0.03125")]
    [DataRow(1, 64, "0.015625")]
    [DataRow(1, 5, "0.2")]
    [DataRow(1, 25, "0.04")]
    [DataRow(1, 125, "0.008")]
    [DataRow(3, 20, "0.15")]
    [DataRow(7, 40, "0.175")]
    [DataRow(13, 200, "0.065")]
    [DataRow(-1, 8, "-0.125")]
    [DataRow(1, -8, "-0.125")]
    [DataRow(-1, -8, "0.125")]
    public void FromRational_TerminatingFractions_ReturnExactDecimal(long n, long d, string expected)
    {
        var result = MathValue.FromRational(n, d); ;

        Assert.AreEqual(MathValue.DataType.Decimal, result.Kind);
        Assert.AreEqual(decimal.Parse(expected, CultureInfo.InvariantCulture), result.DecimalValue);
    }

    [TestMethod]
    public void TryExactDecimal_PowersOf2And5_MatchExpectedScaleAndMantissa()
    {
        // 1/8 => scale 3, mantissa 125 (0.125m).
        Assert.IsTrue(MathValue.TryExactDecimal(1, 8, out var result));
        Assert.AreEqual(0.125m, result);

        // 1/20 => scale 2, mantissa 5 (0.05m).
        Assert.IsTrue(MathValue.TryExactDecimal(1, 20, out result));
        Assert.AreEqual(0.05m, result);

        // 7/40 => scale 3, mantissa 175 (0.175m).
        Assert.IsTrue(MathValue.TryExactDecimal(7, 40, out result));
        Assert.AreEqual(0.175m, result);
    }

    // Scale boundaries.

    [TestMethod]
    public void FromRational_ScaleAt28Places_EvaluatesToExactDecimal()
    {
        // 1 / 2^28 = 0.0000000037252902984619140625.
        var den28 = BigInteger.One << 28;
        var result = MathValue.FromRational(BigInteger.One, den28);

        Assert.AreEqual(MathValue.DataType.Decimal, result.Kind);
        // Verify round-trip exactness.
        Assert.AreEqual(1.0000000000000000000000000000m, result.DecimalValue * (decimal)den28);

        // 1 / 5^28 = 0.0000000000000000000268435456, requires scale 28.
        var den5_28 = BigInteger.Pow(5, 28);
        result = MathValue.FromRational(BigInteger.One, den5_28);

        Assert.AreEqual(MathValue.DataType.Decimal, result.Kind);
        Assert.AreEqual(1.0000000000000000000000000000m, result.DecimalValue * (decimal)den5_28);
    }

    [TestMethod]
    public void FromRational_ScaleAt29Places_FallsBackToDoubleWithoutRoundingDecimal()
    {
        // 1 / 2^29 = 0.00000000186264514923095703125, requires scale 29.
        var den29 = BigInteger.One << 29;
        Assert.IsFalse(MathValue.TryExactDecimal(BigInteger.One, den29, out _));

        var result = MathValue.FromRational(BigInteger.One, den29);
        Assert.AreEqual(MathValue.DataType.Double, result.Kind);
        Assert.AreEqual(Math.ScaleB(1.0, -29), result.DoubleValue);

        // 1 / 5^29 = 0.00000000000000000000536870912, requires scale 29.
        var den5_29 = BigInteger.Pow(5, 29);
        Assert.IsFalse(MathValue.TryExactDecimal(BigInteger.One, den5_29, out _));

        result = MathValue.FromRational(BigInteger.One, den5_29);
        Assert.AreEqual(MathValue.DataType.Double, result.Kind);
    }

    [TestMethod]
    public void FromRational_ExtremeScaleOverflow_1Over2Pow80_ReturnsAccurateDouble()
    {
        // 1 / 2^80 terminates in R (needs 80 places), but would truncate to 4 digits. It
        // must not evaluate to decimal; it must evaluate to double with 16 digits of
        // precision.
        var den80 = BigInteger.One << 80;

        Assert.IsFalse(MathValue.TryExactDecimal(BigInteger.One, den80, out _));

        var result = MathValue.FromRational(BigInteger.One, den80);
        Assert.AreEqual(MathValue.DataType.Double, result.Kind);
        Assert.AreEqual(Math.ScaleB(1.0, -80), result.DoubleValue);
    }

    // Mantissa boundaries.

    [TestMethod]
    public void FromRational_MantissaAtMax_PreservesExactDecimal()
    {
        // Mantissa = 2^96 - 1.
        // If den = 10, scale = 1, factor = 1 (p2 = 0, p5 = 0 because twos=1, fives=1).
        // mantissa = num * 1 = 2^96 - 1, which is the maximum representable decimal mantissa.
        var num = MaxMantissa;
        var den = new BigInteger(10);

        Assert.IsTrue(MathValue.TryExactDecimal(num, den, out decimal decVal));
        Assert.AreEqual(7922816251426433759354395033.5m, decVal);

        var result = MathValue.FromRational(num, den);
        Assert.AreEqual(MathValue.DataType.Decimal, result.Kind);
        Assert.AreEqual(decVal, result.DecimalValue);
    }

    [TestMethod]
    public void FromRational_MantissaExceeding96Bits_FallsBackToDouble()
    {
        // num = 2^96, den = 10.
        // scale = 1, mantissa = 2^96 (exceeds 96-bit limit by 1).
        var num = BigInteger.One << 96;
        var den = new BigInteger(10);

        Assert.IsFalse(MathValue.TryExactDecimal(num, den, out _));

        var result = MathValue.FromRational(num, den);
        Assert.AreEqual(MathValue.DataType.Double, result.Kind);
    }

    [TestMethod]
    public void FromRational_LargeNumberDividedBy2_MantissaOverflow_FallsBackToDouble()
    {
        // 29-digit integer where dividing by 2 requires 30 digits:
        // num = 8 * 10^28 (> decimal.MaxValue), den = 2.
        // scale = 1, factor = 5, mantissa = 8e28 * 5 = 4e29 (> 2^96 - 1).
        var num = BigInteger.Pow(10, 28) * 8;
        var den = new BigInteger(2);
        var oddNum = num + 1; // ensure it doesn't divide evenly.

        Assert.IsFalse(MathValue.TryExactDecimal(oddNum, den, out _));

        var result = MathValue.FromRational(oddNum, den);
        Assert.AreEqual(MathValue.DataType.Double, result.Kind);
        Assert.AreEqual(4e28, result.DoubleValue, 1e20);
    }

    // GCD factor cancellation and non-terminating fractions.

    [TestMethod]
    public void FromRational_GCDPreReduction_AllowsTerminatingDecimalAfterCancellation()
    {
        // 7 / 14: denominator 14 has factor 7 (non-terminating). But GCD cancels 7,
        // leaving 1 / 2, which is terminating.
        var result = MathValue.FromRational(7, 14);
        Assert.AreEqual(MathValue.DataType.Decimal, result.Kind);
        Assert.AreEqual(0.5m, result.DecimalValue);

        // 9 / 12: denominator 12 has factor 3 (non-terminating). But GCD cancels 3,
        // leaving 3 / 4, which is terminating.
        result = MathValue.FromRational(9, 12);
        Assert.AreEqual(MathValue.DataType.Decimal, result.Kind);
        Assert.AreEqual(0.75m, result.DecimalValue);

        // 39 / 65: denominator 65 has factor 13 (non-terminating). But GCD cancels 13,
        // leaving 3 / 5, which is terminating.
        result = MathValue.FromRational(39, 65);
        Assert.AreEqual(MathValue.DataType.Decimal, result.Kind);
        Assert.AreEqual(0.6m, result.DecimalValue);
    }

    [TestMethod]
    [DataRow(1, 3)]
    [DataRow(2, 3)]
    [DataRow(1, 6)]
    [DataRow(1, 7)]
    [DataRow(1, 11)]
    [DataRow(1, 13)]
    [DataRow(5, 12)]
    public void FromRational_NonTerminatingFractions_FailExactDecimalAndProduceDouble(long n, long d)
    {
        Assert.IsFalse(MathValue.TryExactDecimal(n, d, out _));

        var result = MathValue.FromRational(n, d);
        Assert.AreEqual(MathValue.DataType.Double, result.Kind);
        Assert.AreEqual((double)n / d, result.DoubleValue, 1e-15);
    }

    [TestMethod]
    public void FromRational_DivisionByZero_ThrowsDivideByZeroException()
    {
        Assert.ThrowsExactly<DivideByZeroException>(() => MathValue.FromRational(1, 0));
        Assert.ThrowsExactly<DivideByZeroException>(() => MathValue.FromRational(0, 0));
        Assert.ThrowsExactly<DivideByZeroException>(() => MathValue.FromRational(-5, 0));
    }
}
