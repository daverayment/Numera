using System.Globalization;
using System.Numerics;
using System.Text.Json;

namespace Numera.Tests;

[TestClass]
public sealed class CalculationExactnessTests
{
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