using System.Globalization;
using System.Numerics;

namespace Numera.Tests;

[TestClass]
public sealed class RationalMathValueTests
{
    [TestMethod]
    public void FractionsNormalizeAndSelectOnlyExactRepresentations()
    {
        var third = MathValue.FromFraction(-2, -6);
        Assert.AreEqual(MathValue.DataType.Rational, third.Kind);
        Assert.AreEqual(BigInteger.One, third.Numerator);
        Assert.AreEqual(new BigInteger(3), third.Denominator);
        Assert.AreEqual(CalculationAccuracy.Exact, third.Accuracy);
        Assert.AreEqual(MathValue.DataType.Integer, MathValue.FromFraction(6, 3).Kind);
        Assert.AreEqual(MathValue.DataType.Integer, MathValue.FromFraction(0, -3).Kind);
        Assert.AreEqual(MathValue.DataType.Rational, MathValue.FromFraction(1, 8).Kind);
        Assert.AreEqual((BigInteger.One, new BigInteger(8)), MathValue.FromFraction(1, 8).GetExactFraction());
        Assert.AreEqual(0.125m, MathValue.FromFraction(1, 8).ToDecimal());
        Assert.AreEqual(MathValue.DataType.Rational, MathValue.FromFraction(1, BigInteger.One << 29).Kind);
        Assert.ThrowsExactly<DivideByZeroException>(() => MathValue.FromFraction(1, 0));
        Assert.ThrowsExactly<InvalidOperationException>(() => _ = new MathValue(1).Numerator);
        Assert.ThrowsExactly<InvalidOperationException>(() => _ = new MathValue(1m).Denominator);
    }

    [TestMethod]
    public void DecimalArithmeticDoesNotRoundOrFallback()
    {
        Assert.AreEqual(new MathValue(0.3m), new MathValue(0.1m) + new MathValue(0.2m));
        var belowDecimal = new MathValue(1e-28m) / new MathValue(10);
        Assert.AreEqual(MathValue.DataType.Rational, belowDecimal.Kind);
        Assert.AreEqual(BigInteger.Pow(10, 29), belowDecimal.Denominator);
        var recovered = belowDecimal * new MathValue(10);
        Assert.AreEqual(MathValue.DataType.Rational, recovered.Kind);
        Assert.AreEqual(new MathValue(1e-28m).GetExactFraction(), recovered.GetExactFraction());

        var enormous = new MathValue(BigInteger.One << 300);
        var sum = enormous + new MathValue(0.1m);
        Assert.AreEqual(MathValue.DataType.Rational, sum.Kind);
        Assert.AreEqual(enormous, sum - new MathValue(0.1m));
        Assert.AreEqual(new MathValue(BigInteger.Pow(10, 29)), new MathValue(decimal.MaxValue) + new MathValue(20771837485735662406456049665m));
    }

    [TestMethod]
    public void RationalOperationsRetainCancellationAndRemainder()
    {
        var third = MathValue.FromFraction(1, 3);
        var seventh = MathValue.FromFraction(1, 7);
        Assert.AreEqual(new MathValue(1), third * new MathValue(3));
        Assert.AreEqual(new MathValue(1), third + third + third);
        Assert.AreEqual(MathValue.FromFraction(4, 21), third - seventh);
        Assert.AreEqual(MathValue.FromFraction(7, 3), third / seventh);
        Assert.AreEqual(MathValue.FromFraction(1, 21), third % seventh);
        Assert.AreEqual(MathValue.FromFraction(-1, 21), -third % seventh);
        Assert.ThrowsExactly<DivideByZeroException>(() => _ = third / new MathValue(0));
        Assert.ThrowsExactly<DivideByZeroException>(() => _ = third % new MathValue(0));
    }

    [TestMethod]
    public void IntegerPowersRemainExactIncludingNegativeExponents()
    {
        Assert.AreEqual(MathValue.FromFraction(1, 243), MathValue.Pow(new MathValue(3), new MathValue(-5)));
        Assert.AreEqual(new MathValue(9), MathValue.Pow(MathValue.FromFraction(1, 3), new MathValue(-2)));
        Assert.AreEqual(MathValue.FromFraction(-8, 27), MathValue.Pow(MathValue.FromFraction(-2, 3), new MathValue(3)));
        var small = MathValue.Pow(new MathValue(10), new MathValue(-1000));
        Assert.AreEqual(MathValue.DataType.Rational, small.Kind);
        Assert.AreEqual(BigInteger.Pow(10, 1000), small.Denominator);
        var approximate = MathValue.Pow(new MathValue(2), MathValue.FromFraction(1, 2));
        Assert.AreEqual(MathValue.DataType.Double, approximate.Kind);
        Assert.AreEqual(CalculationAccuracy.Unverified, approximate.Accuracy);
        Assert.ThrowsExactly<DivideByZeroException>(() => MathValue.Pow(new MathValue(0), new MathValue(-1)));
        Assert.ThrowsExactly<CalculationLimitException>(() => MathValue.Pow(new MathValue(10), new MathValue(-10000)));
        Assert.ThrowsExactly<CalculationLimitException>(() => MathValue.Pow(new MathValue(1), new MathValue(10001)));
        var negativeTiny = MathValue.FromFraction(-1, BigInteger.One << 2000);
        Assert.ThrowsExactly<DivideByZeroException>(() => MathValue.Pow(new MathValue(0), negativeTiny));
        Assert.ThrowsExactly<ExpressionException>(() => MathValue.Pow(negativeTiny, MathValue.FromFraction(1, 2)));
    }

    [TestMethod]
    [DataRow(1)]
    [DataRow(-1)]
    public void NegativeBasesRejectFiniteFractionalExponentsBeyondTheDoubleRange(int exponentSign)
    {
        var magnitude = BigInteger.Pow(10, 400);
        var exponent = MathValue.FromFraction(exponentSign * (2 * magnitude + 1), 2);
        MathValue[] negativeBases =
        [
            new MathValue(-2),
            new MathValue(-2m),
            new MathValue(-2.0),
            new MathValue(-magnitude),
            MathValue.FromFraction(-1, magnitude)
        ];

        Assert.IsTrue(exponent.IsFinite);
        Assert.IsTrue(double.IsInfinity(exponent.AsDouble()));
        foreach (var negativeBase in negativeBases)
        {
            var exception = Assert.ThrowsExactly<ExpressionException>(() => MathValue.Pow(negativeBase, exponent));
            Assert.AreEqual(EvaluationError.UndefinedMathResult, exception.Diagnostic.Code);
        }
    }

    [TestMethod]
    public void NonfinitePowerExponentsRetainNativeDoubleSemantics()
    {
        foreach (double exponent in new[] { double.PositiveInfinity, double.NegativeInfinity, double.NaN })
        {
            var result = MathValue.Pow(new MathValue(-2), new MathValue(exponent));

            Assert.AreEqual(Math.Pow(-2, exponent), result.AsDouble());
            Assert.AreEqual(MathValue.DataType.Double, result.Kind);
            Assert.AreEqual(CalculationAccuracy.Unverified, result.Accuracy);
        }
    }

    [TestMethod]
    public void AccuracyIsIndependentOfRepresentationAndPoisonsReachedOperations()
    {
        Assert.AreEqual(CalculationAccuracy.Unverified, default(MathValue).Accuracy);
        Assert.AreEqual(CalculationAccuracy.Exact, new MathValue(1).Accuracy);
        Assert.AreEqual(CalculationAccuracy.Exact, new MathValue(1m).Accuracy);
        Assert.AreEqual(CalculationAccuracy.Unverified, new MathValue(1.0).Accuracy);
        var precise = MathValue.FromFraction(1, 3);
        var unknown = precise.WithAccuracy(CalculationAccuracy.Unverified);
        Assert.IsTrue(precise.TryGetExactFraction(out _, out _));
        Assert.IsFalse(unknown.TryGetExactFraction(out _, out _));
        Assert.IsTrue(new MathValue(0.5).WithAccuracy(CalculationAccuracy.Exact).TryGetExactFraction(out var trustedNum, out var trustedDen));
        Assert.AreEqual(BigInteger.One, trustedNum);
        Assert.AreEqual(new BigInteger(2), trustedDen);
        Assert.IsFalse(new MathValue(double.PositiveInfinity).WithAccuracy(CalculationAccuracy.Exact).TryGetExactFraction(out _, out _));
        Assert.IsTrue(MathValue.TryToRational(unknown, out var n, out var d));
        Assert.AreEqual(BigInteger.One, n);
        Assert.AreEqual(new BigInteger(3), d);
        foreach (var value in new[]
        {
            -unknown, unknown + precise, unknown - precise, unknown * precise, unknown / precise,
            unknown % precise, MathValue.Pow(unknown, new MathValue(2)),
            MathValue.AreEqual(unknown, precise), MathValue.LessThan(unknown, precise),
            MathValue.LogicalAnd(unknown, precise), MathValue.LogicalOr(unknown, precise),
            MathValue.LogicalXor(unknown, precise), MathValue.LogicalNot(unknown),
        })
        {
            Assert.AreEqual(CalculationAccuracy.Unverified, value.Accuracy);
        }

        var integral = new MathValue(6).WithAccuracy(CalculationAccuracy.Unverified);
        foreach (var value in new[]
        {
            MathValue.BitwiseAnd(integral, new MathValue(3)),
            MathValue.BitwiseOr(integral, new MathValue(3)),
            MathValue.BitwiseXor(integral, new MathValue(3)),
            MathValue.BitwiseNot(integral),
            MathValue.ShiftLeft(integral, new MathValue(1)),
            MathValue.ShiftRight(integral, new MathValue(1)),
        })
        {
            Assert.AreEqual(CalculationAccuracy.Unverified, value.Accuracy);
        }

        Assert.AreEqual(precise, unknown);
        Assert.AreEqual(precise.GetHashCode(), unknown.GetHashCode());
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => precise.WithAccuracy((CalculationAccuracy)20));
    }

    [TestMethod]
    public void MixedDoublesUseBinaryExactComparisonButUnverifiedArithmetic()
    {
        var third = MathValue.FromFraction(1, 3);
        var rounded = new MathValue(1.0 / 3.0);
        Assert.AreNotEqual(third, rounded);
        Assert.IsTrue(third > rounded);
        Assert.AreNotEqual(new MathValue(0.1m), new MathValue(0.1));
        var half = MathValue.FromFraction(1, 2);
        Assert.AreEqual(half, new MathValue(0.5));
        Assert.AreEqual(half.GetHashCode(), new MathValue(0.5).GetHashCode());
        foreach (var value in new[] { third + rounded, third - rounded, third * rounded, third / rounded, third % rounded })
        {
            Assert.AreEqual(MathValue.DataType.Double, value.Kind);
            Assert.AreEqual(CalculationAccuracy.Unverified, value.Accuracy);
        }
        Assert.AreEqual(new MathValue(double.NaN), new MathValue(double.NaN));
        Assert.IsTrue(MathValue.AreEqual(new MathValue(double.NaN), new MathValue(double.NaN)).IsZero);
        Assert.IsFalse(MathValue.AreNotEqual(new MathValue(double.NaN), new MathValue(double.NaN)).IsZero);
        Assert.IsTrue(third < new MathValue(double.PositiveInfinity));
        Assert.IsTrue(third > new MathValue(double.NegativeInfinity));
        var enormous = new MathValue(BigInteger.One << 2000);
        Assert.AreEqual(0.0, (enormous / new MathValue(double.PositiveInfinity)).DoubleValue);
        Assert.AreEqual(double.PositiveInfinity, (new MathValue(double.PositiveInfinity) / enormous).DoubleValue);
        Assert.AreEqual(long.MinValue, BitConverter.DoubleToInt64Bits((new MathValue(-0.0) / third).DoubleValue));
    }

    [TestMethod]
    public void CancellationPrecedesProductsAndValidateLimitsEnforcesCeilings()
    {
        var first = MathValue.FromFraction(32749, 32719);
        var reciprocal = MathValue.FromFraction(32719, 32749);
        Assert.AreEqual(new MathValue(1), MathValue.Multiply(first, reciprocal));
        Assert.AreEqual(new MathValue(1), MathValue.Divide(first, first));
        Assert.ThrowsExactly<CalculationLimitException>(() => new MathValue(65537).ValidateLimits(16));
        Assert.ThrowsExactly<CalculationLimitException>(() => MathValue.FromFraction(1, 65537).ValidateLimits(16));
        Assert.AreEqual(new MathValue(1.0), MathValue.Divide(new MathValue(65535), new MathValue(65535.0)));
        Assert.AreEqual(new MathValue(65536), new MathValue(65535) + new MathValue(1));
    }

    [TestMethod]
    public void ExplicitConversionsAndInvariantExpressionsPreserveTheirContract()
    {
        var third = MathValue.FromFraction(1, 3);
        Assert.AreEqual("1/3", third.ToInvariantExpression());
        Assert.AreEqual("1/3", third.ToString("R", CultureInfo.InvariantCulture));
        Assert.IsFalse(third.ToString(CultureInfo.InvariantCulture).Contains('/'));
        Assert.AreEqual("0.33", third.ToString("F2", CultureInfo.InvariantCulture));
        Assert.AreEqual("0,33", third.ToString("F2", CultureInfo.GetCultureInfo("de-DE")));
        Assert.AreEqual("0.33333", third.ToString("G5", CultureInfo.InvariantCulture));
        Assert.AreEqual("1,666.67", MathValue.FromFraction(5000, 3).ToString("N2", CultureInfo.InvariantCulture));
        Assert.AreEqual("0.0000000000000000000000000000", MathValue.FromFraction(5, BigInteger.Pow(10, 29)).ToString("F28", CultureInfo.InvariantCulture));
        Assert.AreEqual("0.0000000000000000000000000002", MathValue.FromFraction(15, BigInteger.Pow(10, 29)).ToString("F28", CultureInfo.InvariantCulture));
        Assert.ThrowsExactly<CalculationLimitException>(() => third.ToString("F1001", CultureInfo.InvariantCulture));
        var oversizedSeparators = (NumberFormatInfo)CultureInfo.InvariantCulture.NumberFormat.Clone();
        oversizedSeparators.NumberGroupSeparator = new string(' ', 16384);
        Assert.ThrowsExactly<CalculationLimitException>(() => MathValue.FromFraction(5000, 3).ToString("N2", oversizedSeparators));
        Assert.AreEqual(1.0 / 3.0, third.ToDouble());
        Assert.AreEqual(0.3333333333333333333333333333m, third.ToDecimal());
        var tie = MathValue.FromFraction(5, BigInteger.Pow(10, 29));
        Assert.AreEqual(0m, tie.ToDecimal());
        Assert.AreEqual(2e-28m, MathValue.FromFraction(15, BigInteger.Pow(10, 29)).ToDecimal());
        Assert.AreEqual(-2e-28m, MathValue.FromFraction(-15, BigInteger.Pow(10, 29)).ToDecimal());
        Assert.ThrowsExactly<OverflowException>(() => new MathValue(BigInteger.One << 200).ToDecimal());
        Assert.AreEqual("0.1", new MathValue(0.1m).ToInvariantExpression());
        Assert.AreEqual("3602879701896397/36028797018963968", new MathValue(0.1).ToInvariantExpression());
        Assert.IsNull(third.ToHexString());
        Assert.ThrowsExactly<ExpressionException>(() => MathValue.BitwiseNot(third));
    }

    [TestMethod]
    public void SerializationPreservesKindValueAndProvenance()
    {
        foreach (var value in new[]
        {
            default(MathValue), new MathValue(12), new MathValue(12.30m), new MathValue(0.1),
            new MathValue(-0.0), new MathValue(double.NaN), new MathValue(double.PositiveInfinity),
            MathValue.FromFraction(-123, 7), MathValue.FromFraction(1, 3).WithAccuracy(CalculationAccuracy.Unverified),
        })
        {
            var restored = MathValue.Deserialize(value.Serialize());
            Assert.AreEqual(value, restored);
            Assert.AreEqual(value.Kind, restored.Kind);
            Assert.AreEqual(value.Accuracy, restored.Accuracy);
            Assert.AreEqual(value.ToInvariantExpression(), restored.ToInvariantExpression());
            if (value.Kind == MathValue.DataType.Double && value.IsZero)
            {
                Assert.AreEqual(BitConverter.DoubleToInt64Bits(value.DoubleValue), BitConverter.DoubleToInt64Bits(restored.DoubleValue));
            }
        }
        Assert.ThrowsExactly<CalculationLimitException>(() => MathValue.Deserialize(new MathValue(65536).Serialize(), maxIntegerBits: 16));
        Assert.ThrowsExactly<FormatException>(() => MathValue.Deserialize("{\"Kind\":1,\"Accuracy\":1,\"Value\":\"0.123456789012345678901234567891\"}"));
        Assert.ThrowsExactly<CalculationLimitException>(() => MathValue.Deserialize(new string('1', 25001)));
        Assert.ThrowsExactly<System.Text.Json.JsonException>(() => MathValue.Deserialize("{"));
        Assert.ThrowsExactly<FormatException>(() => MathValue.Deserialize("null"));
        Assert.ThrowsExactly<System.Text.Json.JsonException>(() => MathValue.Deserialize("{\"Kind\":0,\"Accuracy\":1}"));
        Assert.ThrowsExactly<System.Text.Json.JsonException>(() => MathValue.Deserialize("{\"Accuracy\":1,\"Value\":\"1\"}"));
        Assert.ThrowsExactly<System.Text.Json.JsonException>(() => MathValue.Deserialize("{\"Kind\":0,\"Value\":\"1\"}"));
        Assert.ThrowsExactly<FormatException>(() => MathValue.Deserialize("{\"Kind\":0,\"Accuracy\":1,\"Value\":null}"));
        Assert.ThrowsExactly<FormatException>(() => MathValue.Deserialize("{\"Kind\":100,\"Accuracy\":1,\"Value\":\"1\"}"));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => MathValue.Deserialize("{\"Kind\":0,\"Accuracy\":100,\"Value\":\"1\"}"));
        Assert.ThrowsExactly<DivideByZeroException>(() => MathValue.Deserialize("{\"Kind\":3,\"Accuracy\":1,\"Value\":\"1/0\",\"Numerator\":\"1\",\"Denominator\":\"0\"}"));
        Assert.ThrowsExactly<FormatException>(() => MathValue.Deserialize("{\"Kind\":3,\"Accuracy\":1,\"Value\":\"2/6\",\"Numerator\":\"2\",\"Denominator\":\"6\"}"));
        Assert.ThrowsExactly<CalculationLimitException>(() => MathValue.Deserialize("{\"Kind\":0,\"Accuracy\":1,\"Value\":\"" + new string('9', 10000) + "\"}"));
    }

    [TestMethod]
    public void FiniteDoubleExpressionsReevaluateToExactStoredBinaryValues()
    {
        foreach (double input in new[] { 0.1, 0.1 + 0.2, double.Epsilon, -double.Epsilon, double.MaxValue })
        {
            var value = new MathValue(input);
            Assert.AreEqual(value, Calculator.Evaluate(value.ToInvariantExpression()).Value);
        }
        Assert.AreEqual("NaN", new MathValue(double.NaN).ToInvariantExpression());
        Assert.AreEqual("Infinity", new MathValue(double.PositiveInfinity).ToInvariantExpression());
        Assert.AreEqual("-Infinity", new MathValue(double.NegativeInfinity).ToInvariantExpression());
    }

    [TestMethod]
    public void IntegerOperationsPreflightIndependentIntermediateLimitsWithoutRejectingShrinkingResults()
    {
        Assert.ThrowsExactly<CalculationLimitException>(() => MathValue.ShiftLeft(1, 16385));
        Assert.AreEqual(new MathValue(-255), MathValue.Negate(255));
        Assert.AreEqual(new MathValue(255), MathValue.BitwiseNot(-256));
        Assert.AreEqual(new MathValue(255), MathValue.BitwiseAnd(511, 255));
        Assert.AreEqual(new MathValue(-1), MathValue.BitwiseOr(256, -1));
        Assert.AreEqual(new MathValue(0), MathValue.BitwiseXor(511, 511));
        Assert.AreEqual(new MathValue(128), MathValue.ShiftLeft(64, 1));
        Assert.AreEqual(new MathValue(255), MathValue.ShiftRight(511, 1));
        Assert.AreEqual(new MathValue(-128), MathValue.ShiftRight(-256, 1));
        Assert.AreEqual(new MathValue(-128), MathValue.ShiftRight(-511, 2));
        Assert.AreEqual(new MathValue(510), MathValue.ShiftLeft(255, 1));
        Assert.AreEqual(new MathValue(-256), MathValue.BitwiseNot(255));
    }

    [TestMethod]
    public void BitwisePreflightMatchesExactSignedResultsAcrossIndependentLimits()
    {
        var random = new Random(65537);
        for (int i = 0; i < 500; i++)
        {
            int n = random.Next(-65535, 65536);
            int m = random.Next(-65535, 65536);
            for (int operation = 0; operation < 3; operation++)
            {
                var expected = new BigInteger(operation switch
                {
                    0 => n & m,
                    1 => n | m,
                    2 => n ^ m,
                    _ => throw new InvalidOperationException(),
                });
                MathValue calculated = operation switch
                {
                    0 => MathValue.BitwiseAnd(n, m),
                    1 => MathValue.BitwiseOr(n, m),
                    2 => MathValue.BitwiseXor(n, m),
                    _ => throw new InvalidOperationException(),
                };
                Assert.AreEqual(new MathValue(expected), calculated);
            }
            int shift = random.Next(0, 20);
            var shifted = new BigInteger(n) >> shift;
            Assert.AreEqual(new MathValue(shifted), MathValue.ShiftRight(n, shift));
        }
    }

    [TestMethod]
    public void NativeRepresentationSpanFormattingKeepsAllocationFreeFastPaths()
    {
        MathValue[] values = [new MathValue(12345), new MathValue(123.45m), new MathValue(123.45)];
        Span<char> characters = stackalloc char[64];
        Span<byte> utf8 = stackalloc byte[64];
        for (int i = 0; i < 32; i++)
        {
            foreach (var value in values)
            {
                value.TryFormat(characters, out _, "G", CultureInfo.InvariantCulture);
                value.TryFormat(utf8, out _, "G", CultureInfo.InvariantCulture);
            }
        }

        long before = GC.GetAllocatedBytesForCurrentThread();
        bool succeeded = true;
        for (int i = 0; i < 1000; i++)
        {
            foreach (var value in values)
            {
                succeeded &= value.TryFormat(characters, out _, "G", CultureInfo.InvariantCulture);
                succeeded &= value.TryFormat(utf8, out _, "G", CultureInfo.InvariantCulture);
            }
        }

        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.IsTrue(succeeded);
        Assert.AreEqual(0L, allocated);
        foreach (var value in values)
        {
            Assert.IsFalse(value.TryFormat(characters[..1], out int charsWritten, "G", CultureInfo.InvariantCulture));
            Assert.AreEqual(0, charsWritten);
            Assert.IsFalse(value.TryFormat(utf8[..1], out int bytesWritten, "G", CultureInfo.InvariantCulture));
            Assert.AreEqual(0, bytesWritten);
        }
    }

    [TestMethod]
    public void RandomizedRationalIdentitiesUseExactNumeratorsAndDenominators()
    {
        var random = new Random(1729);
        for (int i = 0; i < 300; i++)
        {
            var an = new BigInteger(random.Next(-10000, 10001));
            var ad = new BigInteger(random.Next(1, 10001));
            var bn = new BigInteger(random.Next(1, 10001));
            var bd = new BigInteger(random.Next(1, 10001));
            var a = MathValue.FromFraction(an, ad);
            var b = MathValue.FromFraction(bn, bd);
            Assert.AreEqual(MathValue.FromFraction(an * bd + bn * ad, ad * bd), a + b);
            Assert.AreEqual(MathValue.FromFraction(an * bd - bn * ad, ad * bd), a - b);
            Assert.AreEqual(MathValue.FromFraction(an * bn, ad * bd), a * b);
            Assert.AreEqual(MathValue.FromFraction(an * bd, ad * bn), a / b);
            Assert.AreEqual(MathValue.FromFraction((an * bd) % (bn * ad), ad * bd), a % b);
            Assert.AreEqual((an * bd).CompareTo(bn * ad), MathValue.Compare(a, b));
            Assert.AreEqual(a, (a / b) * b);
        }
    }
}
