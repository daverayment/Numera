using System.Globalization;
using System.Numerics;

namespace Numera.Tests;

[TestClass]
public class NumericTests
{
    [TestMethod]
    public void IntegerExpressionsPreserveValuesBeyondDecimalRange()
    {
        var magnitude = BigInteger.Pow(7, 180);
        var result = Calculator.Evaluate("7^180+19");

        Assert.AreEqual(NumberValue.DataType.Integer, result.Type);
        Assert.AreEqual(magnitude + 19, result.IntVal);
        Assert.AreEqual(magnitude, Calculator.Evaluate("(7^180*13)/13").IntVal);
    }

    [TestMethod]
    public void ExtremaSelectAdjacentLargeIntegersWithoutNarrowing()
    {
        var magnitude = BigInteger.One << 240;
        var largest = Calculator.Evaluate("max(2^240,2^240+9,2^240+8)");
        var smallest = Calculator.Evaluate("min(2^240+9,2^240+8,2^240)");

        Assert.AreEqual(NumberValue.DataType.Integer, largest.Type);
        Assert.AreEqual(magnitude + 9, largest.IntVal);
        Assert.AreEqual(magnitude, smallest.IntVal);
    }

    [TestMethod]
    [DataRow("0.125+0.375", "0.5")]
    [DataRow("1.25*0.08", "0.1")]
    [DataRow("7/8", "0.875")]
    [DataRow("-0.0000000000000000000000000007", "-0.0000000000000000000000000007")]
    public void RepresentableDecimalArithmeticPreservesValue(string expression, string expected)
    {
        var result = Calculator.Evaluate(expression);

        Assert.AreEqual(NumberValue.DataType.Decimal, result.Type);
        Assert.AreEqual(decimal.Parse(expected, CultureInfo.InvariantCulture), result.DecVal);
    }

    [TestMethod]
    public void ExtremaPreserveTheSelectedRepresentation()
    {
        var integer = Calculator.Evaluate("max(3.75,4,3.5)");
        var fraction = Calculator.Evaluate("min(3.75,4,3.5)");
        var singleton = Calculator.Evaluate("max(0.0000000000000000000000000007)");

        Assert.AreEqual(NumberValue.DataType.Integer, integer.Type);
        Assert.AreEqual(new BigInteger(4), integer.IntVal);
        Assert.AreEqual(NumberValue.DataType.Decimal, fraction.Type);
        Assert.AreEqual(3.5m, fraction.DecVal);
        Assert.AreEqual(0.0000000000000000000000000007m, singleton.DecVal);
    }

    [TestMethod]
    public void IntegerAndDecimalComparisonDoesNotRoundToBinaryDouble()
    {
        var integer = new NumberValue(new BigInteger(1_000_000_000_000_000_001));
        var fraction = new NumberValue(1_000_000_000_000_000_000.5m);

        Assert.IsTrue(NumberValue.Compare(integer, fraction) > 0);
        Assert.IsTrue(NumberValue.Compare(fraction, integer) < 0);
        Assert.IsTrue(NumberValue.Compare(-integer, -fraction) < 0);
    }

    [TestMethod]
    public void FactorialMatchesIndependentIntegerProduct()
    {
        var expected = BigInteger.One;
        for (var factor = 1; factor <= 75; factor++)
        {
            expected *= factor;
        }

        var result = Calculator.Evaluate("factorial(75)");
        Assert.AreEqual(NumberValue.DataType.Integer, result.Type);
        Assert.AreEqual(expected, result.IntVal);
    }

    [TestMethod]
    public void IntegerPolynomialIdentityHoldsAcrossSignedInputs()
    {
        for (var x = -12; x <= 12; x++)
        {
            var expression = $"(({x})+1)^2-({x})^2-2*({x})";
            Assert.AreEqual(BigInteger.One, Calculator.Evaluate(expression).IntVal, expression);
        }
    }

    // Reference constants are decimal expansions of the mathematical values, not calls to the same Math functions as the engine.
    [TestMethod]
    [DataRow("sqrt(2)", 1.4142135623730950488, 4e-16)]
    [DataRow("ln(2)", 0.6931471805599453094, 2e-16)]
    [DataRow("sin(pi/6)", 0.5, 2e-16)]
    [DataRow("exp(1)", 2.7182818284590452354, 6e-16)]
    [DataRow("log(10000)", 4.0, 1e-15)]
    [DataRow("root(-125,3)", -5.0, 2e-15)]
    public void ApproximateFunctionsAgreeWithIndependentReferences(string expression, double expected, double tolerance)
    {
        var result = Calculator.Evaluate(expression);
        Assert.AreEqual(NumberValue.DataType.Double, result.Type);
        Assert.AreEqual(expected, result.DblVal, tolerance);
    }

    [TestMethod]
    public void LargeIntegerRatioCanHaveAFiniteFloatingPointResult()
    {
        var result = Calculator.Evaluate("(2^2000+1)/(2^2000-1)");
        Assert.AreEqual(NumberValue.DataType.Double, result.Type);
        Assert.AreEqual(1.0, result.DblVal, 2e-15);
    }

    [TestMethod]
    public void NonFiniteResultsRemainAvailableToConsumers()
    {
        Assert.IsTrue(double.IsNaN(Calculator.Evaluate("sqrt(-7)").DblVal));
        Assert.IsTrue(double.IsPositiveInfinity(Calculator.Evaluate("exp(1000)").DblVal));
    }

    /// Double-precision arithmetic can absorb small values into large values, but the
    /// engine preserves the small value in the result.
    [TestMethod]
    [DataRow("100000000000000000000000000 + 1.0 - 100000000000000000000000000", "1.0")]

    /// Ensure the engine does not fall into the classic double-precision division trap,
    /// e.g. 0.3 / 0.1 producing 2.9999999999999996 instead of 3.
    [DataRow("0.3 / 0.1", "3")]

    /// Test the promotion of the large integer to a decimal, doing an exact calculation,
    /// and not truncating.
    [DataRow("10^20 + 0.125 - 10^20", "0.125")]

    // Ensure the smaller value is not absorbed into the larger value, as would happen
    // with double-precision arithmetic.
    [DataRow("10^50 + 2^50 - 10^50", "1125899906842624")]
    public void AvoidsClassicFloatingPointCancellationAndAbsorption(string expression, string expected)
    {
        var result = Calculator.Evaluate(expression);
        Assert.AreEqual(expected, result.Type == NumberValue.DataType.Decimal
            ? result.DecVal.ToString(CultureInfo.InvariantCulture)
            : result.IntVal.ToString(CultureInfo.InvariantCulture));
    }
}
