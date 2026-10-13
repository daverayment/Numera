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

    // References are mathematical constants rounded to double, not calls to the engine's
    // Math functions. Absolute tolerances check these cases, not general accuracy or
    // correct rounding for every input.
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
        // Each integer would overflow a direct double conversion, although their ratio
        // is near one. This checks scaled division, not recovery of the tiny difference.
        var result = Calculator.Evaluate("(2^2000+1)/(2^2000-1)");
        Assert.AreEqual(NumberValue.DataType.Double, result.Type);
        Assert.AreEqual(1.0, result.DblVal, 2e-15);
    }

    [TestMethod]
    public void NonFiniteResultsRemainAvailableToConsumers()
    {
        var nan = Calculator.Evaluate("sqrt(-7)");
        Assert.AreEqual(NumberValue.DataType.Double, nan.Type);
        Assert.IsTrue(nan.IsNaN);
        Assert.IsFalse(nan.IsFinite);
        Assert.IsTrue(double.IsNaN(nan.DblVal));

        var infinity = Calculator.Evaluate("exp(1000)");
        Assert.AreEqual(NumberValue.DataType.Double, infinity.Type);
        Assert.IsFalse(infinity.IsNaN);
        Assert.IsFalse(infinity.IsFinite);
        Assert.IsTrue(double.IsPositiveInfinity(infinity.DblVal));
    }

    [TestMethod]
    public void NumberValueFinitenessPropertiesReflectUnderlyingState()
    {
        var integer = new NumberValue(new BigInteger(42));
        Assert.IsTrue(integer.IsFinite);
        Assert.IsFalse(integer.IsNaN);

        var largeInteger = new NumberValue(BigInteger.Pow(10, 100));
        Assert.IsTrue(largeInteger.IsFinite);
        Assert.IsFalse(largeInteger.IsNaN);

        var dec = new NumberValue(123.456m);
        Assert.IsTrue(dec.IsFinite);
        Assert.IsFalse(dec.IsNaN);

        var finiteDouble = new NumberValue(3.14159);
        Assert.IsTrue(finiteDouble.IsFinite);
        Assert.IsFalse(finiteDouble.IsNaN);

        var nanDouble = new NumberValue(double.NaN);
        Assert.IsFalse(nanDouble.IsFinite);
        Assert.IsTrue(nanDouble.IsNaN);

        var posInf = new NumberValue(double.PositiveInfinity);
        Assert.IsFalse(posInf.IsFinite);
        Assert.IsFalse(posInf.IsNaN);

        var negInf = new NumberValue(double.NegativeInfinity);
        Assert.IsFalse(negInf.IsFinite);
        Assert.IsFalse(negInf.IsNaN);
    }

    [TestMethod]
    public void NumberValueTryGetIntegerExtractsOnlyExactWholeNumbers()
    {
        var integer = new NumberValue(new BigInteger(100));
        Assert.IsTrue(integer.TryGetInteger(out var intOut));
        Assert.AreEqual(new BigInteger(100), intOut);

        var wholeDec = new NumberValue(100.0m);
        Assert.IsTrue(wholeDec.TryGetInteger(out var decWholeOut));
        Assert.AreEqual(new BigInteger(100), decWholeOut);

        var fracDec = new NumberValue(100.25m);
        Assert.IsFalse(fracDec.TryGetInteger(out _));

        var wholeDouble = new NumberValue(100.0);
        Assert.IsTrue(wholeDouble.TryGetInteger(out var dblWholeOut));
        Assert.AreEqual(new BigInteger(100), dblWholeOut);

        var fracDouble = new NumberValue(100.5);
        Assert.IsFalse(fracDouble.TryGetInteger(out _));

        var nanDouble = new NumberValue(double.NaN);
        Assert.IsFalse(nanDouble.TryGetInteger(out _));

        var infDouble = new NumberValue(double.PositiveInfinity);
        Assert.IsFalse(infDouble.TryGetInteger(out _));
    }

    [TestMethod]
    [DataRow("100 + 0.5", NumberValue.DataType.Decimal, "100.5")]
    [DataRow("100 - 0.25", NumberValue.DataType.Decimal, "99.75")]
    [DataRow("5 * 1.5", NumberValue.DataType.Decimal, "7.5")]
    [DataRow("10 / 2", NumberValue.DataType.Integer, "5")]
    [DataRow("5 / 2", NumberValue.DataType.Decimal, "2.5")]
    public void MixedIntegerAndDecimalArithmeticPreservesExactness(string expression, NumberValue.DataType expectedType, string expectedValue)
    {
        var result = Calculator.Evaluate(expression);
        Assert.AreEqual(expectedType, result.Type);

        if (expectedType == NumberValue.DataType.Integer)
        {
            Assert.AreEqual(BigInteger.Parse(expectedValue, CultureInfo.InvariantCulture), result.IntVal);
        }
        else
        {
            Assert.AreEqual(decimal.Parse(expectedValue, CultureInfo.InvariantCulture), result.DecVal);
        }
    }

    [TestMethod]
    public void DecimalArithmeticOverflowFallsBackToDouble()
    {
        // decimal.MaxValue is ~7.9228162514264337593543950335e28.
        // Adding to it will overflow Decimal addition and should fall back gracefully to Double.
        var overflowSum = Calculator.Evaluate("79228162514264337593543950335 + 1.5");
        Assert.AreEqual(NumberValue.DataType.Double, overflowSum.Type);
        Assert.IsTrue(overflowSum.IsFinite);

        // Multiplication overflow.
        var overflowProd = Calculator.Evaluate("79228162514264337593543950335 * 2.0");
        Assert.AreEqual(NumberValue.DataType.Double, overflowProd.Type);
        Assert.IsTrue(overflowProd.IsFinite);
    }

    [TestMethod]
    public void DecimalLiteralUnderflowToZeroFallsBackToDouble()
    {
        // 0.00000000000000000000000000001 (29 decimal places) underflows decimal to 0m.
        // It should fall back to double rather than silently becoming zero or triggering
        // division by zero.
        var underflowLiteral = Calculator.Evaluate("0.00000000000000000000000000001");
        Assert.AreEqual(NumberValue.DataType.Double, underflowLiteral.Type);
        Assert.AreNotEqual(0.0, underflowLiteral.DblVal);
        Assert.AreEqual(1e-29, underflowLiteral.DblVal, 1e-35);

        // Division by an underflowing fraction must not trigger division by zero.
        var divisionResult = Calculator.Evaluate("1 / 0.00000000000000000000000000001");
        Assert.AreEqual(NumberValue.DataType.Double, divisionResult.Type);
        Assert.IsTrue(divisionResult.IsFinite);
        Assert.AreEqual(1e29, divisionResult.DblVal, 1e20);

        // Genuine zero with a decimal point remains Decimal.
        var genuineZero = Calculator.Evaluate("0.00");
        Assert.AreEqual(NumberValue.DataType.Decimal, genuineZero.Type);
        Assert.AreEqual(0m, genuineZero.DecVal);
    }

    [TestMethod]
    [DataRow("sign(sqrt(-1))", NumberValue.DataType.Decimal, "0")]
    [DataRow("sign(-42)", NumberValue.DataType.Integer, "-1")]
    [DataRow("sign(-42.5)", NumberValue.DataType.Decimal, "-1")]
    [DataRow("abs(-12345678901234567890)", NumberValue.DataType.Integer, "12345678901234567890")]
    [DataRow("abs(-12.34)", NumberValue.DataType.Decimal, "12.34")]
    [DataRow("ceil(10)", NumberValue.DataType.Integer, "10")]
    [DataRow("ceil(10.2)", NumberValue.DataType.Decimal, "11")]
    [DataRow("floor(10.8)", NumberValue.DataType.Decimal, "10")]
    [DataRow("round(2.5)", NumberValue.DataType.Decimal, "3")]
    [DataRow("round(-2.5)", NumberValue.DataType.Decimal, "-3")]
    [DataRow("factorial(0)", NumberValue.DataType.Integer, "1")]
    public void SpecialMathFunctionsPreserveRepresentationAndExactValues(string expression, NumberValue.DataType expectedType, string expectedValue)
    {
        var result = Calculator.Evaluate(expression);
        Assert.AreEqual(expectedType, result.Type);

        if (expectedType == NumberValue.DataType.Integer)
        {
            Assert.AreEqual(BigInteger.Parse(expectedValue, CultureInfo.InvariantCulture), result.IntVal);
        }
        else
        {
            Assert.AreEqual(decimal.Parse(expectedValue, CultureInfo.InvariantCulture), result.DecVal);
        }
    }

    [TestMethod]
    [DataRow("factorial(-1)")]
    [DataRow("factorial(2.5)")]
    [DataRow("root(-16, 2)")]
    [DataRow("randi(-5)")]
    public void MathDomainErrorsReturnNaN(string expression)
    {
        var result = Calculator.Evaluate(expression);
        Assert.AreEqual(NumberValue.DataType.Double, result.Type);
        Assert.IsTrue(result.IsNaN);
    }

    [TestMethod]
    public void NegativeOddRootYieldsRealResult()
    {
        var result = Calculator.Evaluate("root(-8, 3)");
        Assert.AreEqual(NumberValue.DataType.Double, result.Type);
        Assert.AreEqual(-2.0, result.DblVal, 1e-15);
    }

    // These cases remain in integer or representable decimal arithmetic, avoiding the
    // loss that would occur if intermediate results were narrowed to double.
    [TestMethod]
    [DataRow("100000000000000000000000000 + 1.0 - 100000000000000000000000000", "1.0")]

    // Decimal literals avoid the binary-double quotient 2.9999999999999996 for 0.3 / 0.1.
    [DataRow("0.3 / 0.1", "3")]

    // This integer and fractional increment fit together exactly in decimal precision.
    [DataRow("10^20 + 0.125 - 10^20", "0.125")]

    // Both operands remain integers even though the larger one exceeds decimal range.
    [DataRow("10^50 + 2^50 - 10^50", "1125899906842624")]
    public void AvoidsClassicFloatingPointCancellationAndAbsorption(string expression, string expected)
    {
        var result = Calculator.Evaluate(expression);
        Assert.AreEqual(expected, result.Type == NumberValue.DataType.Decimal
            ? result.DecVal.ToString(CultureInfo.InvariantCulture)
            : result.IntVal.ToString(CultureInfo.InvariantCulture));
    }

    [TestMethod]
    public void NumberValueFormattableDirectToStringMatchesStandardBehavior()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

            var intVal = new NumberValue(new BigInteger(1234567));
            Assert.AreEqual("1234567", intVal.ToString());
            Assert.AreEqual("1,234,567", intVal.ToString("N0"));
            Assert.AreEqual("1234567", $"{intVal}");

            var decVal = new NumberValue(1234.56m);
            Assert.AreEqual("1234.56", decVal.ToString());
            Assert.AreEqual("1234.6", decVal.ToString("F1"));
            Assert.AreEqual("1234.56", $"{decVal}");

            // Double defaults to G17 when format is empty/null
            var dblVal = new NumberValue(1.0 / 3.0);
            Assert.AreEqual((1.0 / 3.0).ToString("G17", CultureInfo.InvariantCulture), dblVal.ToString());
            Assert.AreEqual("0.33", dblVal.ToString("F2"));
            Assert.AreEqual((1.0 / 3.0).ToString("G17", CultureInfo.InvariantCulture), $"{dblVal}");

            // Special double values format accurately
            var nanVal = new NumberValue(double.NaN);
            Assert.AreEqual("NaN", nanVal.ToString());
            var infVal = new NumberValue(double.PositiveInfinity);
            Assert.AreEqual("Infinity", infVal.ToString());
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [TestMethod]
    public void NumberValueFormattableRespectsSuppliedCulture()
    {
        var germanCulture = CultureInfo.GetCultureInfo("de-DE");

        var decVal = new NumberValue(1234.56m);
        Assert.AreEqual("1234,56", decVal.ToString(null, germanCulture));
        Assert.AreEqual("1.234,56", decVal.ToString("N2", germanCulture));

        var dblVal = new NumberValue(3.5);
        Assert.AreEqual("3,5", dblVal.ToString(null, germanCulture));

        var intVal = new NumberValue(new BigInteger(1000000));
        Assert.AreEqual("1.000.000", intVal.ToString("N0", germanCulture));
    }
}
