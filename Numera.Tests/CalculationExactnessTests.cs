using System.Globalization;
using System.Numerics;
using System.Text.Json;

namespace Numera.Tests;

[TestClass]
public sealed class CalculationExactnessTests
{
    [TestMethod]
    public void ZeroAndOneAreExactIntegersWithExactFractions()
    {
        (MathValue Value, BigInteger Expected)[] constants =
        [
            (MathValue.Zero, BigInteger.Zero),
            (MathValue.One, BigInteger.One)
        ];

        foreach (var (value, expected) in constants)
        {
            Assert.AreEqual(MathValue.DataType.Integer, value.Kind);
            Assert.AreEqual(expected, value.IntegerValue);
            Assert.AreEqual(CalculationExactness.Exact, value.Exactness);
            Assert.IsTrue(value.IsFinite);
            Assert.IsFalse(value.IsNaN);
            Assert.IsTrue(value.TryGetExactFraction(out var numerator, out var denominator));
            Assert.AreEqual(expected, numerator);
            Assert.AreEqual(BigInteger.One, denominator);
            Assert.AreEqual((expected, BigInteger.One), value.GetExactFraction());
            Assert.AreEqual(value, value + MathValue.Zero);
            Assert.AreEqual(CalculationExactness.Exact, (value + MathValue.Zero).Exactness);
            Assert.AreEqual(value, value * MathValue.One);
            Assert.AreEqual(CalculationExactness.Exact, (value * MathValue.One).Exactness);

            var restored = MathValue.Deserialize(value.Serialize());
            Assert.AreEqual(expected, restored.IntegerValue);
            Assert.AreEqual(CalculationExactness.Exact, restored.Exactness);
        }
    }

    [TestMethod]
    public void DefaultZeroIsNumericallyEqualButDoesNotAssertExactness()
    {
        var value = default(MathValue);
        Assert.AreEqual(MathValue.DataType.Integer, value.Kind);
        Assert.AreEqual(BigInteger.Zero, value.IntegerValue);
        Assert.AreEqual(CalculationExactness.Unverified, value.Exactness);
        Assert.IsTrue(value == MathValue.Zero);
        Assert.AreEqual(value.GetHashCode(), MathValue.Zero.GetHashCode());
        Assert.IsFalse(value.TryGetExactFraction(out var numerator, out var denominator));
        Assert.AreEqual(BigInteger.Zero, numerator);
        Assert.AreEqual(BigInteger.Zero, denominator);
        Assert.IsNull(value.ExactFraction);
        Assert.IsFalse(EvaluationResultComparers.Content.Equals(
            new EvaluationResult(value, EvaluationContext.Empty),
            new EvaluationResult(MathValue.Zero, EvaluationContext.Empty)));

        var unverifiedCopy = MathValue.Zero.WithExactness(CalculationExactness.Unverified);
        Assert.AreEqual(CalculationExactness.Unverified, unverifiedCopy.Exactness);
        Assert.AreEqual(CalculationExactness.Exact, MathValue.Zero.Exactness);
        Assert.AreEqual(CalculationExactness.Exact, MathValue.One.Exactness);
    }

    [TestMethod]
    public void NonfiniteConstantsAreUnverifiedDoublesWithoutExactFractions()
    {
        (MathValue Value, double Expected)[] constants =
        [
            (MathValue.NaN, double.NaN),
            (MathValue.PositiveInfinity, double.PositiveInfinity),
            (MathValue.NegativeInfinity, double.NegativeInfinity)
        ];

        foreach (var (value, expected) in constants)
        {
            Assert.AreEqual(MathValue.DataType.Double, value.Kind);
            Assert.AreEqual(BitConverter.DoubleToInt64Bits(expected), BitConverter.DoubleToInt64Bits(value.DoubleValue));
            Assert.AreEqual(CalculationExactness.Unverified, value.Exactness);
            Assert.IsFalse(value.IsFinite);
            Assert.AreEqual(double.IsNaN(expected), value.IsNaN);
            Assert.IsFalse(value.TryGetExactFraction(out var numerator, out var denominator));
            Assert.AreEqual(BigInteger.Zero, numerator);
            Assert.AreEqual(BigInteger.Zero, denominator);
            Assert.IsNull(value.ExactFraction);
            Assert.ThrowsExactly<InvalidOperationException>(() => value.GetExactFraction());
            Assert.ThrowsExactly<ArgumentException>(() => value.WithExactness(CalculationExactness.Exact));
            Assert.AreEqual(new MathValue(expected), value);

            var restored = MathValue.Deserialize(value.Serialize());
            Assert.AreEqual(value, restored);
            Assert.AreEqual(MathValue.DataType.Double, restored.Kind);
            Assert.AreEqual(CalculationExactness.Unverified, restored.Exactness);
        }
    }

    [TestMethod]
    public void ConstantsDoNotCertifyUnverifiedZeroResults()
    {
        var result = CalculatorEngine.ForCulture(CultureInfo.InvariantCulture).Evaluate("ln(1)");
        Assert.IsTrue(result.Value == MathValue.Zero);
        Assert.AreEqual(CalculationExactness.Unverified, result.Exactness);
        Assert.AreEqual(CalculationExactness.Unverified, (result + MathValue.Zero).Exactness);
        Assert.AreEqual(CalculationExactness.Unverified, (MathValue.One * result).Exactness);
        Assert.IsFalse(result.HasExactFraction);

        var unverifiedOne = MathValue.One.WithExactness(CalculationExactness.Unverified);
        var zero = unverifiedOne - MathValue.One;
        Assert.AreEqual(MathValue.DataType.Integer, zero.Kind);
        Assert.IsTrue(zero == MathValue.Zero);
        Assert.AreEqual(CalculationExactness.Unverified, zero.Exactness);
        Assert.AreEqual(CalculationExactness.Unverified, (MathValue.Zero + default(MathValue)).Exactness);
        Assert.AreEqual(CalculationExactness.Exact, MathValue.Zero.Exactness);
    }

    [TestMethod]
    [DataRow("1/3", CalculationExactness.Exact, true, true)]
    [DataRow("1/8", CalculationExactness.Exact, false, true)]
    [DataRow("sqrt(4)", CalculationExactness.Exact, false, true)]
    [DataRow("ln(1)", CalculationExactness.Unverified, false, false)]
    [DataRow("floor(pi)", CalculationExactness.Unverified, false, false)]
    [DataRow("pi-pi", CalculationExactness.Unverified, false, false)]
    [DataRow("sqrt(2)", CalculationExactness.Unverified, true, false)]
    [DataRow("0 and sqrt(2)", CalculationExactness.Exact, false, true)]
    [DataRow("1 or sqrt(2)", CalculationExactness.Exact, false, true)]
    [DataRow("1>2<sqrt(2)", CalculationExactness.Exact, false, true)]
    public void GuideExamplesDistinguishExactnessDisplayRoundingAndFractionAvailability(
        string expression, CalculationExactness exactness, bool rounded, bool hasExactFraction)
    {
        var result = CalculatorEngine.ForCulture(CultureInfo.InvariantCulture).Evaluate(expression);
        var formatted = result.FormatResult();

        Assert.AreEqual(exactness, result.Exactness);
        Assert.AreEqual(exactness, formatted.Value.Exactness);
        Assert.AreEqual(rounded, formatted.IsDisplayRounded);
        Assert.AreEqual(hasExactFraction, result.HasExactFraction);
        Assert.AreEqual(hasExactFraction, formatted.FractionRepresentation is not null);
    }

    [TestMethod]
    [DataRow(double.NaN)]
    [DataRow(double.PositiveInfinity)]
    [DataRow(double.NegativeInfinity)]
    public void NonfiniteValuesCannotBeMarkedExactOrDeserializedAsExact(double input)
    {
        var value = new MathValue(input);
        var exception = Assert.ThrowsExactly<ArgumentException>(() => value.WithExactness(CalculationExactness.Exact));
        Assert.AreEqual("exactness", exception.ParamName);
        Assert.AreEqual(CalculationExactness.Unverified, value.Exactness);
        Assert.AreEqual(value, value.WithExactness(CalculationExactness.Unverified));
        Assert.AreEqual(value, MathValue.Deserialize(value.Serialize()));
        Assert.IsNull(value.ExactFraction);

        string text = input.ToString("R", CultureInfo.InvariantCulture);
        string invalidJson = $"{{\"Kind\":2,\"Exactness\":1,\"Value\":\"{text}\"}}";
        Assert.ThrowsExactly<FormatException>(() => MathValue.Deserialize(invalidJson));
    }

    [TestMethod]
    public void DecimalAndDoubleInputsHaveDifferentExactnessWithoutInspectingTheirDisplay()
    {
        var decimalValue = new MathValue(0.1m);
        var doubleValue = new MathValue(0.1);
        var binaryHalf = new MathValue(0.5);

        Assert.AreEqual(CalculationExactness.Exact, decimalValue.Exactness);
        Assert.AreEqual((BigInteger.One, new BigInteger(10)), decimalValue.GetExactFraction());
        Assert.AreEqual(CalculationExactness.Unverified, doubleValue.Exactness);
        Assert.AreNotEqual(decimalValue, doubleValue);
        Assert.AreEqual(CalculationExactness.Unverified, binaryHalf.Exactness);
        var formattedHalf = CalculatorEngine.ForCulture(CultureInfo.InvariantCulture).FormatResult(binaryHalf);
        Assert.AreEqual("0.5", formattedHalf.PrimaryDisplay);
        Assert.IsFalse(formattedHalf.IsDisplayRounded);
        Assert.IsNull(formattedHalf.FractionRepresentation);
    }

    [TestMethod]
    public void CallerAssertionDoesNotRecalculateOrReplaceTheStoredBinaryFraction()
    {
        var supplied = new MathValue(0.1);
        var asserted = supplied.WithExactness(CalculationExactness.Exact);

        Assert.AreEqual(CalculationExactness.Unverified, supplied.Exactness);
        Assert.AreEqual(CalculationExactness.Exact, asserted.Exactness);
        Assert.AreEqual(supplied.Kind, asserted.Kind);
        Assert.AreEqual(BitConverter.DoubleToInt64Bits(supplied.DoubleValue), BitConverter.DoubleToInt64Bits(asserted.DoubleValue));
        Assert.AreEqual(
            (BigInteger.Parse("3602879701896397"), BigInteger.Parse("36028797018963968")),
            asserted.GetExactFraction());
    }

    [TestMethod]
    public void ImportedApproximationCanBeLabelledWithoutChangingItsSuppliedValue()
    {
        var supplied = new MathValue((decimal)Math.Sqrt(2));
        var labelled = supplied.WithExactness(CalculationExactness.Unverified);

        Assert.AreEqual(CalculationExactness.Exact, supplied.Exactness);
        Assert.AreEqual(CalculationExactness.Unverified, labelled.Exactness);
        Assert.AreEqual(supplied, labelled);
        Assert.IsNull(labelled.ExactFraction);
    }

    [TestMethod]
    public void SerializedExactnessIsRequiredAndPreserved()
    {
        var supplied = MathValue.FromFraction(1, 3).WithExactness(CalculationExactness.Unverified);
        string json = supplied.Serialize();
        using var document = JsonDocument.Parse(json);
        Assert.AreEqual(0, document.RootElement.GetProperty("Exactness").GetInt32());
        var restored = MathValue.Deserialize(json);

        Assert.AreEqual(CalculationExactness.Unverified, restored.Exactness);
        Assert.AreEqual(supplied, restored);
        Assert.IsNull(restored.ExactFraction);
        Assert.ThrowsExactly<JsonException>(() => MathValue.Deserialize("""{"Kind":0,"Value":"1"}"""));
    }
}