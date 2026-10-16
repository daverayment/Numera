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
    [DataRow("expm1(1)", 1.7182818284590452354, 6e-16)]
    [DataRow("log(10000)", 4.0, 1e-15)]
    [DataRow("log1p(1)", 0.6931471805599453094, 2e-16)]
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
        var nan = new NumberValue(double.NaN);
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
    [DataRow("sign(0)", NumberValue.DataType.Integer, "0")]
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
    [DataRow("factorial(-1)", EvaluationError.ArgumentOutOfRange)]
    [DataRow("factorial(2.5)", EvaluationError.IntegerRequired)]
    [DataRow("root(-16, 2)", EvaluationError.UndefinedMathResult)]
    [DataRow("randi(-5)", EvaluationError.ArgumentOutOfRange)]
    [DataRow("randi(0)", EvaluationError.ArgumentOutOfRange)]
    [DataRow("randi(1.5)", EvaluationError.IntegerRequired)]
    [DataRow("sqrt(-4)", EvaluationError.UndefinedMathResult)]
    [DataRow("log(-10)", EvaluationError.UndefinedMathResult)]
    [DataRow("log(0)", EvaluationError.UndefinedMathResult)]
    [DataRow("ln(-1)", EvaluationError.UndefinedMathResult)]
    [DataRow("log1p(-1)", EvaluationError.UndefinedMathResult)]
    [DataRow("log1p(-2)", EvaluationError.UndefinedMathResult)]
    [DataRow("mod(10, 0)", EvaluationError.DivisionByZero)]
    [DataRow("log2(0)", EvaluationError.UndefinedMathResult)]
    [DataRow("logn(-8, 2)", EvaluationError.UndefinedMathResult)]
    [DataRow("logn(8, 1)", EvaluationError.UndefinedMathResult)]
    [DataRow("arcsin(2)", EvaluationError.UndefinedMathResult)]
    [DataRow("arccos(-1.5)", EvaluationError.UndefinedMathResult)]
    [DataRow("arcosh(0.5)", EvaluationError.UndefinedMathResult)]
    [DataRow("artanh(1)", EvaluationError.UndefinedMathResult)]
    [DataRow("root(8, 0)", EvaluationError.UndefinedMathResult)]
    [DataRow("clamp(5, 10, 2)", EvaluationError.ArgumentOutOfRange)]
    [DataRow("(-4)^0.5", EvaluationError.UndefinedMathResult)]
    [DataRow("pow(-4, 0.5)", EvaluationError.UndefinedMathResult)]
    [DataRow("0^-1", EvaluationError.DivisionByZero)]
    [DataRow("pow(0, -1)", EvaluationError.DivisionByZero)]
    [DataRow("tan(pi/2)", EvaluationError.UndefinedMathResult)]
    [DataRow("tan(3*pi/2)", EvaluationError.UndefinedMathResult)]
    [DataRow("cot(0)", EvaluationError.UndefinedMathResult)]
    [DataRow("cot(pi)", EvaluationError.UndefinedMathResult)]
    public void MathDomainErrorsProduceStructuredDiagnostics(string expression, EvaluationError expectedError)
    {
        Assert.IsFalse(Calculator.TryEvaluate(expression, out _, out var diagnostic));
        Assert.IsNotNull(diagnostic);
        Assert.AreEqual(expectedError, diagnostic.Code);

        var ex = Assert.Throws<ExpressionException>(() => Calculator.Evaluate(expression));
        Assert.AreEqual(expectedError, ex.Diagnostic.Code);
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

    [TestMethod]
    [DataRow("sin(0)", 0.0)]
    [DataRow("sin(90)", 1.0)]
    [DataRow("sin(180)", 0.0)]
    [DataRow("sin(270)", -1.0)]
    [DataRow("sin(360)", 0.0)]
    [DataRow("cos(0)", 1.0)]
    [DataRow("cos(90)", 0.0)]
    [DataRow("cos(180)", -1.0)]
    [DataRow("cos(270)", 0.0)]
    [DataRow("cos(360)", 1.0)]
    [DataRow("tan(0)", 0.0)]
    [DataRow("tan(45)", 1.0)]
    [DataRow("tan(180)", 0.0)]
    [DataRow("tan(225)", 1.0)]
    public void TrigonometricFunctionsInDegreesModeEvaluateExactCardinalValues(string expression, double expected)
    {
        var options = new EvaluationOptions { AngleUnit = AngleUnit.Degrees };
        var result = Calculator.Evaluate(expression, options);

        Assert.AreEqual(NumberValue.DataType.Double, result.Type);
        Assert.AreEqual(expected, result.DblVal, 0.0);
    }

    [TestMethod]
    public void TrigonometricFunctionsSupportGradiansMode()
    {
        var options = new EvaluationOptions { AngleUnit = AngleUnit.Gradians };
        Assert.AreEqual(1.0, Calculator.Evaluate("sin(100)", options).DblVal, 1e-15);
        Assert.AreEqual(0.0, Calculator.Evaluate("cos(100)", options).DblVal, 1e-15);
        Assert.AreEqual(1.0, Calculator.Evaluate("tan(50)", options).DblVal, 1e-15);
    }

    [TestMethod]
    public void InverseTrigonometricFunctionsRespectAngleUnit()
    {
        var degOptions = new EvaluationOptions { AngleUnit = AngleUnit.Degrees };
        Assert.AreEqual(90.0, Calculator.Evaluate("arcsin(1)", degOptions).DblVal, 0.0);
        Assert.AreEqual(30.0, Calculator.Evaluate("arcsin(0.5)", degOptions).DblVal, 1e-14);
        Assert.AreEqual(180.0, Calculator.Evaluate("arccos(-1)", degOptions).DblVal, 0.0);
        Assert.AreEqual(90.0, Calculator.Evaluate("arccos(0)", degOptions).DblVal, 0.0);
        Assert.AreEqual(45.0, Calculator.Evaluate("arctan(1)", degOptions).DblVal, 0.0);
        Assert.AreEqual(0.0, Calculator.Evaluate("arctan(0)", degOptions).DblVal, 0.0);

        var gradOptions = new EvaluationOptions { AngleUnit = AngleUnit.Gradians };
        Assert.AreEqual(100.0, Calculator.Evaluate("arcsin(1)", gradOptions).DblVal, 0.0);
        Assert.AreEqual(200.0, Calculator.Evaluate("arccos(-1)", gradOptions).DblVal, 0.0);
        Assert.AreEqual(50.0, Calculator.Evaluate("arctan(1)", gradOptions).DblVal, 0.0);
    }

    [TestMethod]
    [DataRow("sin(rad(90))", 1.0)]
    [DataRow("sin(deg2rad(90))", 1.0)]
    [DataRow("sin(rad(180))", 0.0)]
    [DataRow("sin(rad(270))", -1.0)]
    [DataRow("cos(rad(0))", 1.0)]
    [DataRow("cos(rad(90))", 0.0)]
    [DataRow("cos(rad(180))", -1.0)]
    [DataRow("tan(rad(45))", 1.0)]
    [DataRow("tan(rad(180))", 0.0)]
    [DataRow("sin(grad(100))", 1.0)]
    [DataRow("sin(grad2rad(100))", 1.0)]
    [DataRow("cos(grad(200))", -1.0)]
    [DataRow("tan(grad(50))", 1.0)]
    public void TrigonometricFunctionsPreserveExactPrecisionWithRadAndGradHelpers(string expression, double expected)
    {
        var result = Calculator.Evaluate(expression);
        Assert.AreEqual(NumberValue.DataType.Double, result.Type);
        Assert.AreEqual(expected, result.DblVal, 0.0);
    }

    [TestMethod]
    [DataRow("rad2deg(pi)", 180.0)]
    [DataRow("deg(pi)", 180.0)]
    [DataRow("deg2rad(180)", Math.PI)]
    [DataRow("rad(180)", Math.PI)]
    [DataRow("deg2grad(180)", 200.0)]
    [DataRow("grad2deg(200)", 180.0)]
    [DataRow("grad2rad(200)", Math.PI)]
    [DataRow("rad2grad(pi)", 200.0)]
    public void AngleConversionHelpersProduceCorrectValues(string expression, double expected)
    {
        var result = Calculator.Evaluate(expression);
        Assert.AreEqual(expected, result.DblVal, 1e-12);
    }

    [TestMethod]
    [DataRow("0x10", "16")]
    [DataRow("0xFF", "255")]
    [DataRow("0x123456789ABCDEF0", "1311768467463790320")]
    [DataRow("0b1010", "10")]
    [DataRow("0b11111111", "255")]
    [DataRow("0o77", "63")]
    [DataRow("0o100", "64")]
    public void NonDecimalLiteralsParseAsExactIntegers(string expression, string expected)
    {
        var result = Calculator.Evaluate(expression);
        Assert.AreEqual(NumberValue.DataType.Integer, result.Type);
        Assert.AreEqual(expected, result.IntVal.ToString(CultureInfo.InvariantCulture));
    }

    [TestMethod]
    [DataRow("1e3", 1000.0)]
    [DataRow("1.5e2", 150.0)]
    [DataRow("2.5E-3", 0.0025)]
    [DataRow("1e-5", 0.00001)]
    [DataRow("1.23e+4", 12300.0)]
    public void ScientificNotationParsesCorrectly(string expression, double expected)
    {
        var result = Calculator.Evaluate(expression);
        var val = result.Type switch
        {
            NumberValue.DataType.Decimal => (double)result.DecVal,
            NumberValue.DataType.Double => result.DblVal,
            NumberValue.DataType.Integer => (double)result.IntVal,
            _ => throw new InvalidOperationException(),
        };
        Assert.AreEqual(expected, val, 1e-12);
    }

    [TestMethod]
    [DataRow("3 < 5", "1")]
    [DataRow("5 < 3", "0")]
    [DataRow("3 <= 3", "1")]
    [DataRow("3 <= 2", "0")]
    [DataRow("5 > 3", "1")]
    [DataRow("3 > 5", "0")]
    [DataRow("5 >= 5", "1")]
    [DataRow("4 >= 5", "0")]
    [DataRow("3 != 4", "1")]
    [DataRow("3 != 3", "0")]
    [DataRow("3 ~= 4", "1")]
    [DataRow("3 == 3", "1")]
    [DataRow("0.0 == -0.0", "1")]
    [DataRow("0.0 != -0.0", "0")]
    [DataRow("0.0 > -0.0", "0")]
    [DataRow("0.0 < -0.0", "0")]
    [DataRow("0.0 >= -0.0", "1")]
    [DataRow("0.0 <= -0.0", "1")]
    public void RelationalAndEqualityOperatorsEvaluateCorrectly(string expression, string expected)
    {
        var result = Calculator.Evaluate(expression);
        Assert.AreEqual(expected, result.DecVal.ToString(CultureInfo.InvariantCulture));
    }

    [TestMethod]
    [DataRow("1 and 1", "1")]
    [DataRow("1 and 0", "0")]
    [DataRow("0 or 1", "1")]
    [DataRow("0 or 0", "0")]
    [DataRow("1 xor 0", "1")]
    [DataRow("1 xor 1", "0")]
    [DataRow("0 xor 0", "0")]
    [DataRow("!0", "1")]
    [DataRow("!1", "0")]
    [DataRow("not 0", "1")]
    [DataRow("not 1", "0")]
    [DataRow("not (3 > 5)", "1")]
    [DataRow("2 + 3 < 10 and 4 * 2 == 8", "1")]
    public void LogicalOperatorsAndKeywordsEvaluateCorrectly(string expression, string expected)
    {
        var result = Calculator.Evaluate(expression);
        Assert.AreEqual(expected, result.DecVal.ToString(CultureInfo.InvariantCulture));
    }

    [TestMethod]
    [DataRow("1 or sqrt(-1)", "1")]
    [DataRow("0 and sqrt(-1)", "0")]
    public void LogicalKeywordsShortCircuitEvaluation(string expression, string expected)
    {
        var result = Calculator.Evaluate(expression);
        Assert.AreEqual(expected, result.DecVal.ToString(CultureInfo.InvariantCulture));
    }

    [TestMethod]
    [DataRow("trunc(3.7)", "3")]
    [DataRow("trunc(-3.7)", "-3")]
    [DataRow("trunc(5)", "5")]
    [DataRow("frac(3.75)", "0.75")]
    [DataRow("frac(-3.75)", "-0.75")]
    [DataRow("frac(5)", "0")]
    public void TruncAndFracPreserveRepresentationAndValues(string expression, string expected)
    {
        var result = Calculator.Evaluate(expression);
        var str = result.Type == NumberValue.DataType.Integer
            ? result.IntVal.ToString(CultureInfo.InvariantCulture)
            : result.DecVal.ToString(CultureInfo.InvariantCulture);
        Assert.AreEqual(expected, str);
    }

    [TestMethod]
    [DataRow("hypot(3, 4)", 5.0)]
    [DataRow("hypot(5, 12)", 13.0)]
    [DataRow("cbrt(27)", 3.0)]
    [DataRow("cbrt(-8)", -2.0)]
    [DataRow("lerp(10, 20, 0.5)", 15.0)]
    [DataRow("lerp(0, 100, 0.25)", 25.0)]
    public void MathUtilitiesEvaluateCorrectly(string expression, double expected)
    {
        var result = Calculator.Evaluate(expression);
        var actual = result.Type switch
        {
            NumberValue.DataType.Decimal => (double)result.DecVal,
            NumberValue.DataType.Integer => (double)result.IntVal,
            _ => result.DblVal,
        };
        Assert.AreEqual(expected, actual, 1e-12);
    }

    [TestMethod]
    [DataRow("clamp(5, 1, 10)", "5")]
    [DataRow("clamp(-2, 0, 10)", "0")]
    [DataRow("clamp(15, 0, 10)", "10")]
    [DataRow("clamp(3.5, 1.0, 5.0)", "3.5")]
    [DataRow("avg(10, 20, 30)", "20")]
    [DataRow("avg(2, 4)", "3")]
    [DataRow("sum(10, 20, 30)", "60")]
    [DataRow("sum(42)", "42")]
    [DataRow("sum(1.5, 2.5, 3.25)", "7.25")]
    [DataRow("mul(2, 3, 4)", "24")]
    [DataRow("mul(7)", "7")]
    [DataRow("mul(1.5, 2, 4)", "12.0")]
    [DataRow("mod(10, 3)", "1")]
    [DataRow("mod(-10, 3)", "-1")]
    [DataRow("mod(10.5, 3)", "1.5")]
    public void ClampAndAvgEvaluateCorrectly(string expression, string expected)
    {
        var result = Calculator.Evaluate(expression);
        var str = result.Type switch
        {
            NumberValue.DataType.Integer => result.IntVal.ToString(CultureInfo.InvariantCulture),
            NumberValue.DataType.Decimal => result.DecVal.ToString(CultureInfo.InvariantCulture),
            _ => result.DblVal.ToString(CultureInfo.InvariantCulture),
        };
        Assert.AreEqual(expected, str);
    }

    [TestMethod]
    public void Atan2RespectsAngleUnit()
    {
        var radResult = Calculator.Evaluate("atan2(1, 1)");
        Assert.AreEqual(Math.PI / 4.0, radResult.DblVal, 1e-12);

        var degOptions = new EvaluationOptions { AngleUnit = AngleUnit.Degrees };
        var degResult = Calculator.Evaluate("atan2(1, 1)", degOptions);
        Assert.AreEqual(45.0, degResult.DblVal, 0.0);

        var gradOptions = new EvaluationOptions { AngleUnit = AngleUnit.Gradians };
        var gradResult = Calculator.Evaluate("atan2(1, 1)", gradOptions);
        Assert.AreEqual(50.0, gradResult.DblVal, 0.0);
    }

    [TestMethod]
    [DataRow("1 << 4", "16")]
    [DataRow("16 >> 2", "4")]
    [DataRow("0xFF << 8", "65280")]
    [DataRow("0b1010 << 1", "20")]
    [DataRow("0o77 >> 1", "31")]
    [DataRow("shl(1, 10)", "1024")]
    [DataRow("shr(1024, 5)", "32")]
    [DataRow("band(0xFF, 0x0F)", "15")]
    [DataRow("bor(0xF0, 0x0F)", "255")]
    [DataRow("bxor(0xAA, 0x55)", "255")]
    [DataRow("bnot(0)", "-1")]
    [DataRow("bnot(-1)", "0")]
    public void BitwiseOperationsEvaluateCorrectly(string expression, string expected)
    {
        var result = Calculator.Evaluate(expression);
        Assert.AreEqual(NumberValue.DataType.Integer, result.Type);
        Assert.AreEqual(expected, result.IntVal.ToString(CultureInfo.InvariantCulture));
    }

    [TestMethod]
    [DataRow("1 + 2 << 3", "24")]       // (1 + 2) << 3 = 3 << 3 = 24 (additive binds tighter than shift)
    [DataRow("1 << 2 + 1", "8")]        // 1 << (2 + 1) = 1 << 3 = 8
    [DataRow("1 << 4 < 20", "1")]       // (1 << 4) < 20 = 16 < 20 = 1 (shift binds tighter than relational)
    [DataRow("1 << 4 == 16", "1")]      // (1 << 4) == 16 = 1 (shift binds tighter than equality)
    [DataRow("32 >> 2 + 1", "4")]       // 32 >> (2 + 1) = 32 >> 3 = 4
    public void BitwiseShiftPrecedenceIsCorrect(string expression, string expected)
    {
        var result = Calculator.Evaluate(expression);
        var actual = result.Type switch
        {
            NumberValue.DataType.Integer => result.IntVal.ToString(CultureInfo.InvariantCulture),
            NumberValue.DataType.Decimal => result.DecVal.ToString(CultureInfo.InvariantCulture),
            _ => result.DblVal.ToString(CultureInfo.InvariantCulture),
        };
        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    [DataRow("1 << -1", EvaluationError.ArgumentOutOfRange)]
    [DataRow("1 >> -2", EvaluationError.ArgumentOutOfRange)]
    [DataRow("shl(1, -1)", EvaluationError.ArgumentOutOfRange)]
    [DataRow("shr(1, -2)", EvaluationError.ArgumentOutOfRange)]
    [DataRow("1.5 << 2", EvaluationError.IntegerRequired)]
    [DataRow("1 << 2.5", EvaluationError.IntegerRequired)]
    [DataRow("band(1.5, 2)", EvaluationError.IntegerRequired)]
    [DataRow("bor(1, 2.5)", EvaluationError.IntegerRequired)]
    [DataRow("bxor(1.5, 2.5)", EvaluationError.IntegerRequired)]
    [DataRow("bnot(1.5)", EvaluationError.IntegerRequired)]
    public void BitwiseInvalidDomainProducesStructuredDiagnostics(string expression, EvaluationError expectedError)
    {
        Assert.IsFalse(Calculator.TryEvaluate(expression, out _, out var diagnostic));
        Assert.IsNotNull(diagnostic);
        Assert.AreEqual(expectedError, diagnostic.Code);

        var ex = Assert.Throws<ExpressionException>(() => Calculator.Evaluate(expression));
        Assert.AreEqual(expectedError, ex.Diagnostic.Code);
    }

    [TestMethod]
    public void ShiftLeftEnforcesBitCeiling()
    {
        // 1 << 16385 must exceed MaxIntegerBits (16384)
        Assert.Throws<CalculationLimitException>(() => Calculator.Evaluate("1 << 16385"));
    }

    [TestMethod]
    [DataRow("tan(90)", EvaluationError.UndefinedMathResult)]
    [DataRow("tan(270)", EvaluationError.UndefinedMathResult)]
    [DataRow("cot(0)", EvaluationError.UndefinedMathResult)]
    [DataRow("cot(180)", EvaluationError.UndefinedMathResult)]
    [DataRow("sec(90)", EvaluationError.UndefinedMathResult)]
    [DataRow("sec(270)", EvaluationError.UndefinedMathResult)]
    [DataRow("csc(0)", EvaluationError.UndefinedMathResult)]
    [DataRow("csc(180)", EvaluationError.UndefinedMathResult)]
    public void TangentAndCotangentSingularitiesInDegreesModeProduceDiagnostics(string expression, EvaluationError expectedError)
    {
        var degOptions = new EvaluationOptions { AngleUnit = AngleUnit.Degrees };
        Assert.IsFalse(Calculator.TryEvaluate(expression, out _, out var diagnostic, degOptions));
        Assert.IsNotNull(diagnostic);
        Assert.AreEqual(expectedError, diagnostic.Code);

        var ex = Assert.Throws<ExpressionException>(() => Calculator.Evaluate(expression, degOptions));
        Assert.AreEqual(expectedError, ex.Diagnostic.Code);
    }

    [TestMethod]
    [DataRow("cot(rad(90))", 0.0)]
    [DataRow("cot(pi/2)", 0.0)]
    [DataRow("tan(pi/4)", 1.0)]
    [DataRow("tan(-pi/4)", -1.0)]
    [DataRow("sin(pi)", 0.0)]
    [DataRow("cos(pi/2)", 0.0)]
    [DataRow("cos(3*pi/2)", 0.0)]
    public void RadiansModePreservesExactCardinalValuesForPiMultiples(string expression, double expected)
    {
        var result = Calculator.Evaluate(expression);
        Assert.AreEqual(NumberValue.DataType.Double, result.Type);
        Assert.AreEqual(expected, result.DblVal, 0.0);
    }

    [TestMethod]
    [DataRow("tan(100)", EvaluationError.UndefinedMathResult)]
    [DataRow("tan(300)", EvaluationError.UndefinedMathResult)]
    [DataRow("cot(0)", EvaluationError.UndefinedMathResult)]
    [DataRow("cot(200)", EvaluationError.UndefinedMathResult)]
    [DataRow("sec(100)", EvaluationError.UndefinedMathResult)]
    [DataRow("sec(300)", EvaluationError.UndefinedMathResult)]
    [DataRow("csc(0)", EvaluationError.UndefinedMathResult)]
    [DataRow("csc(200)", EvaluationError.UndefinedMathResult)]
    public void ReciprocalTrigonometricSingularitiesInGradiansModeProduceDiagnostics(string expression, EvaluationError expectedError)
    {
        var gradOptions = new EvaluationOptions { AngleUnit = AngleUnit.Gradians };
        Assert.IsFalse(Calculator.TryEvaluate(expression, out _, out var diagnostic, gradOptions));
        Assert.IsNotNull(diagnostic);
        Assert.AreEqual(expectedError, diagnostic.Code);

        var ex = Assert.Throws<ExpressionException>(() => Calculator.Evaluate(expression, gradOptions));
        Assert.AreEqual(expectedError, ex.Diagnostic.Code);
    }

    [TestMethod]
    [DataRow("sec(pi/2)", EvaluationError.UndefinedMathResult)]
    [DataRow("sec(3*pi/2)", EvaluationError.UndefinedMathResult)]
    [DataRow("csc(0)", EvaluationError.UndefinedMathResult)]
    [DataRow("csc(pi)", EvaluationError.UndefinedMathResult)]
    [DataRow("tan(rad(90))", EvaluationError.UndefinedMathResult)]
    [DataRow("cot(rad(180))", EvaluationError.UndefinedMathResult)]
    public void RadianAsymptotesAndConvertedSingularitiesProduceDiagnostics(string expression, EvaluationError expectedError)
    {
        Assert.IsFalse(Calculator.TryEvaluate(expression, out _, out var diagnostic));
        Assert.IsNotNull(diagnostic);
        Assert.AreEqual(expectedError, diagnostic.Code);

        var ex = Assert.Throws<ExpressionException>(() => Calculator.Evaluate(expression));
        Assert.AreEqual(expectedError, ex.Diagnostic.Code);
    }

    [TestMethod]
    [DataRow("sin(180)")]
    [DataRow("sin(360)")]
    [DataRow("sin(pi)")]
    [DataRow("cos(90)")]
    [DataRow("cos(270)")]
    [DataRow("cos(pi/2)")]
    [DataRow("tan(180)")]
    [DataRow("cot(90)")]
    public void TrigonometricCardinalZerosProducePositiveZero(string expression)
    {
        var degOptions = new EvaluationOptions { AngleUnit = AngleUnit.Degrees };
        var options = expression.Contains("pi") ? null : degOptions;
        var result = Calculator.Evaluate(expression, options);

        Assert.AreEqual(0.0, result.DblVal, 0.0);
        Assert.AreEqual(0L, BitConverter.DoubleToInt64Bits(result.DblVal), $"{expression} must evaluate to +0.0, not -0.0.");
    }

    [TestMethod]
    [DataRow("sin(100)", 1.0)]
    [DataRow("sin(200)", 0.0)]
    [DataRow("sin(300)", -1.0)]
    [DataRow("cos(100)", 0.0)]
    [DataRow("cos(200)", -1.0)]
    [DataRow("cos(400)", 1.0)]
    [DataRow("tan(50)", 1.0)]
    [DataRow("tan(150)", -1.0)]
    [DataRow("cot(50)", 1.0)]
    [DataRow("cot(100)", 0.0)]
    [DataRow("sec(200)", -1.0)]
    [DataRow("csc(100)", 1.0)]
    public void GradiansModeEvaluatesExactCardinalValues(string expression, double expected)
    {
        var gradOptions = new EvaluationOptions { AngleUnit = AngleUnit.Gradians };
        var result = Calculator.Evaluate(expression, gradOptions);
        Assert.AreEqual(NumberValue.DataType.Double, result.Type);
        Assert.AreEqual(expected, result.DblVal, 0.0);
    }

    [TestMethod]
    [DataRow("cos(pi / 2)", 0.0)]
    [DataRow("sin(pi)", 0.0)]
    [DataRow("cot(pi / 2)", 0.0)]
    [DataRow("tan(pi / 4)", 1.0)]
    public void TrigCardinalSnappingIsExact(string expression, double expected)
    {
        var result = Calculator.Evaluate(expression);
        Assert.AreEqual(expected, result.DblVal, 0.0);
    }

    [TestMethod]
    [DataRow("tan(pi / 2)", EvaluationError.UndefinedMathResult)]
    [DataRow("sec(pi / 2)", EvaluationError.UndefinedMathResult)]
    [DataRow("cot(0)", EvaluationError.UndefinedMathResult)]
    [DataRow("cot(pi)", EvaluationError.UndefinedMathResult)]
    public void TrigAsymptotesThrowUndefinedResult(string expression, EvaluationError expected)
    {
        Assert.IsFalse(Calculator.TryEvaluate(expression, out _, out var diagnostic));
        Assert.IsNotNull(diagnostic);
        Assert.AreEqual(expected, diagnostic.Code);
    }

    [TestMethod]
    public void AvoidsClassicFloatingPointArtifacts()
    {
        // Decimal preservation
        Assert.AreEqual(3.0m, Calculator.Evaluate("0.3 / 0.1").DecVal);
        Assert.AreEqual(194.40m, Calculator.Evaluate("3033554.11 - 3033359.71").DecVal);
        Assert.AreEqual(1m, Calculator.Evaluate("0.1 + 0.2 == 0.3").DecVal);

        // Large Integer preservation
        Assert.AreEqual(BigInteger.One << 64, Calculator.Evaluate("2^64").IntVal);
        Assert.AreEqual(0m, Calculator.Evaluate("2^64 - 1 == 2^64").DecVal);

        // Unary minus binds looser than exponentiation (-2^2 is -4, not +4)
        Assert.AreEqual(new BigInteger(-4), Calculator.Evaluate("-2^2").IntVal);
    }

    [TestMethod]
    [DataRow("5 == 5 == 5", "1")]
    [DataRow("5 == 5 == 6", "0")]
    [DataRow("1 == 1 == 1 == 1", "1")]
    [DataRow("1 < 2 < 3", "1")]
    [DataRow("1 < 3 < 2", "0")]
    [DataRow("3 > 2 > 1", "1")]
    [DataRow("1 <= 2 <= 2 < 3", "1")]
    [DataRow("10 >= 5 >= 5 > 0", "1")]
    [DataRow("1 < 2 <= 2 == 2 < 5", "1")]
    [DataRow("1 != 2 != 3", "1")]
    [DataRow("1 ~= 2 ~= 3", "1")]
    [DataRow("1 != 2 != 1", "1")]
    [DataRow("1 > 2 < (1 / 0)", "0")]
    [DataRow("1 == 2 == (1 / 0)", "0")]
    [DataRow("1 < 2 < 3 and 4 < 5 < 6", "1")]
    [DataRow("1 < 5 < 3 or 2 < 4 < 6", "1")]
    public void ChainedComparisonsEvaluateCorrectly(string expression, string expected)
    {
        var result = Calculator.Evaluate(expression);
        Assert.AreEqual(expected, result.DecVal.ToString(CultureInfo.InvariantCulture));
    }

    [TestMethod]
    public void NonCardinalLargeAnglesMaintainPrecisionWithCenteredRemainderReduction()
    {
        // 360,000,030 degrees is 1,000,000 full revolutions + 30 degrees.
        // Direct (360000030.0 / 180.0) consumes significand bits on the integer quotient,
        // whereas Math.IEEERemainder(v, 360.0) reduces to 30.0 degrees before division,
        // yielding exact sin(30 deg) = 0.5.
        var degOptions = new EvaluationOptions { AngleUnit = AngleUnit.Degrees };
        var sinResult = Calculator.Evaluate("sin(360000030)", degOptions);
        Assert.AreEqual(0.5, sinResult.DblVal, 1e-15);

        // 360,000,060 degrees -> 60 degrees; cos(60 deg) = 0.5
        var cosResult = Calculator.Evaluate("cos(360000060)", degOptions);
        Assert.AreEqual(0.5, cosResult.DblVal, 1e-15);

        // 400,000,050 gradians is 1,000,000 revolutions + 50 gradians (45 degrees).
        // Beyond MaxSnappingRevolutions (100.0 revs), it falls back to double.TanPi with reduced remainder.
        var gradOptions = new EvaluationOptions { AngleUnit = AngleUnit.Gradians };
        var tanGradResult = Calculator.Evaluate("tan(400000050)", gradOptions);
        Assert.AreEqual(1.0, tanGradResult.DblVal, 1e-15);

        // Non-cardinal angle with many revolutions in degrees:
        // 360000000 + 37 degrees -> evaluates identically to sin(37 deg) without revolution offset
        var baseSin = Calculator.Evaluate("sin(37)", degOptions);
        var largeSin = Calculator.Evaluate("sin(360000037)", degOptions);
        Assert.AreEqual(baseSin.DblVal, largeSin.DblVal, 1e-15);

        var baseCos = Calculator.Evaluate("cos(37)", degOptions);
        var largeCos = Calculator.Evaluate("cos(360000037)", degOptions);
        Assert.AreEqual(baseCos.DblVal, largeCos.DblVal, 1e-15);

        var baseTan = Calculator.Evaluate("tan(37)", degOptions);
        var largeTan = Calculator.Evaluate("tan(360000037)", degOptions);
        Assert.AreEqual(baseTan.DblVal, largeTan.DblVal, 1e-15);
    }

    [TestMethod]
    public void Expm1AndLog1pPreservePrecisionForSmallArguments()
    {
        // For x = 1e-5:
        // Direct exp(x) - 1 loses lower precision bits due to subtracting 1.0,
        // whereas expm1(x) and log1p(x) preserve full double precision.
        double x = 1e-5;
        var expm1Result = Calculator.Evaluate("expm1(1e-5)");
        Assert.AreEqual(NumberValue.DataType.Double, expm1Result.Type);
        Assert.AreEqual(double.ExpM1(x), expm1Result.DblVal, 0.0);

        var log1pResult = Calculator.Evaluate("log1p(1e-5)");
        Assert.AreEqual(NumberValue.DataType.Double, log1pResult.Type);
        Assert.AreEqual(double.LogP1(x), log1pResult.DblVal, 0.0);

        // At x = 0, expm1(0) == 0 and log1p(0) == 0 exactly.
        Assert.AreEqual(0.0, Calculator.Evaluate("expm1(0)").DblVal, 0.0);
        Assert.AreEqual(0.0, Calculator.Evaluate("log1p(0)").DblVal, 0.0);
    }

    [TestMethod]
    [DataRow("asin(0.5)", "arcsin(0.5)")]
    [DataRow("acos(0.5)", "arccos(0.5)")]
    [DataRow("atan(1)", "arctan(1)")]
    [DataRow("asinh(1)", "arsinh(1)")]
    [DataRow("acosh(2)", "arcosh(2)")]
    [DataRow("atanh(0.5)", "artanh(0.5)")]
    public void InverseTrigAndHyperbolicSynonymsMatchCanonical(string aliasExpr, string canonicalExpr)
    {
        var aliasResult = Calculator.Evaluate(aliasExpr);
        var canonicalResult = Calculator.Evaluate(canonicalExpr);
        Assert.AreEqual(canonicalResult.DblVal, aliasResult.DblVal, 0.0);
    }
}
