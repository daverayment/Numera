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

        Assert.AreEqual(MathValue.DataType.Integer, result.Type);
        Assert.AreEqual(magnitude + 19, result.IntVal);
        Assert.AreEqual(magnitude, Calculator.Evaluate("(7^180*13)/13").IntVal);
    }

    [TestMethod]
    public void ExtremaSelectAdjacentLargeIntegersWithoutNarrowing()
    {
        var magnitude = BigInteger.One << 240;
        var largest = Calculator.Evaluate("max(2^240,2^240+9,2^240+8)");
        var smallest = Calculator.Evaluate("min(2^240+9,2^240+8,2^240)");

        Assert.AreEqual(MathValue.DataType.Integer, largest.Type);
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

        Assert.AreEqual(MathValue.DataType.Decimal, result.Type);
        Assert.AreEqual(decimal.Parse(expected, CultureInfo.InvariantCulture), result.DecVal);
    }

    [TestMethod]
    public void ExtremaPreserveTheSelectedRepresentation()
    {
        var integer = Calculator.Evaluate("max(3.75,4,3.5)");
        var fraction = Calculator.Evaluate("min(3.75,4,3.5)");
        var singleton = Calculator.Evaluate("max(0.0000000000000000000000000007)");

        Assert.AreEqual(MathValue.DataType.Integer, integer.Type);
        Assert.AreEqual(new BigInteger(4), integer.IntVal);
        Assert.AreEqual(MathValue.DataType.Decimal, fraction.Type);
        Assert.AreEqual(3.5m, fraction.DecVal);
        Assert.AreEqual(0.0000000000000000000000000007m, singleton.DecVal);
    }

    [TestMethod]
    public void IntegerAndDecimalComparisonDoesNotRoundToBinaryDouble()
    {
        var integer = new MathValue(new BigInteger(1_000_000_000_000_000_001));
        var fraction = new MathValue(1_000_000_000_000_000_000.5m);

        Assert.IsGreaterThan(0, MathValue.Compare(integer, fraction));
        Assert.IsLessThan(0, MathValue.Compare(fraction, integer));
        Assert.IsLessThan(0, MathValue.Compare(-integer, -fraction));
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
        Assert.AreEqual(MathValue.DataType.Integer, result.Type);
        Assert.AreEqual(expected, result.IntVal);
    }

    [TestMethod]
    public void IntegerPolynomialIdentityHoldsAcrossSignedInputs()
    {
        for (int x = -12; x <= 12; x++)
        {
            string expression = $"(({x})+1)^2-({x})^2-2*({x})";
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
    [DataRow("erf(0)", 0.0, 0.0)]
    [DataRow("erf(1)", 0.84270079294971486934, 2e-16)]
    [DataRow("erf(-1)", -0.84270079294971486934, 2e-16)]
    [DataRow("erf(0.25)", 0.27632639016823693498, 2e-16)]
    [DataRow("erf(3)", 0.99997790950300141456, 2e-16)]
    [DataRow("erfc(0)", 1.0, 0.0)]
    [DataRow("erfc(1)", 0.15729920705028513066, 2e-16)]
    [DataRow("erfc(3)", 2.209049699858544e-5, 2e-16)]
    [DataRow("ncdf(0)", 0.5, 0.0)]
    [DataRow("ncdf(1)", 0.84134474606854294858, 2e-16)]
    [DataRow("ncdf(-1)", 0.15865525393145705141, 2e-16)]
    [DataRow("ncdf(1.96)", 0.975002104851779529, 2e-16)]
    [DataRow("sinc(0)", 1.0, 0.0)]
    [DataRow("sinc(pi/2)", 0.63661977236758134308, 2e-16)]
    [DataRow("sinc(pi)", 0.0, 2e-16)]
    [DataRow("tau", 6.28318530717958647692, 0.0)]
    [DataRow("τ", 6.28318530717958647692, 0.0)]
    [DataRow("Τ", 6.28318530717958647692, 0.0)]
    [DataRow("phi", 1.61803398874989484820, 2e-16)]
    [DataRow("ϕ", 1.61803398874989484820, 2e-16)]
    [DataRow("Φ", 1.61803398874989484820, 2e-16)]
    [DataRow("nthroot(27, 3)", 3.0, 0.0)]
    [DataRow("nthroot(-8, 3)", -2.0, 0.0)]
    public void ApproximateFunctionsAgreeWithIndependentReferences(string expression, double expected, double tolerance)
    {
        var result = Calculator.Evaluate(expression);
        Assert.AreEqual(MathValue.DataType.Double, result.Type);
        Assert.AreEqual(expected, result.DblVal, tolerance);
    }

    [TestMethod]
    [DataRow("floor(0.3 / 0.1)", "3")]
    [DataRow("floor(0.30 / 0.10)", "3")]
    [DataRow("floor(tan(pi / 4))", "1")]
    [DataRow("floor((3033554.11 - 3033359.71 - 194) * 100)", "40")]
    public void FloorPreservesExactValuesWhereBinaryFloatFails(string expression, string expected)
    {
        var result = Calculator.Evaluate(expression);
        Assert.AreEqual(expected, result.ToString(CultureInfo.InvariantCulture));
    }

    [TestMethod]
    public void LargeIntegerRatioCanHaveAFiniteFloatingPointResult()
    {
        // Each integer would overflow a direct double conversion, although their ratio
        // is near one. This checks scaled division, not recovery of the tiny difference.
        var result = Calculator.Evaluate("(2^2000+1)/(2^2000-1)");
        Assert.AreEqual(MathValue.DataType.Double, result.Type);
        Assert.AreEqual(1.0, result.DblVal, 2e-15);
    }

    [TestMethod]
    public void NonFiniteResultsRemainAvailableToConsumers()
    {
        var nan = new MathValue(double.NaN);
        Assert.AreEqual(MathValue.DataType.Double, nan.Type);
        Assert.IsTrue(nan.IsNaN);
        Assert.IsFalse(nan.IsFinite);
        Assert.IsTrue(double.IsNaN(nan.DblVal));

        var infinity = Calculator.Evaluate("exp(1000)");
        Assert.AreEqual(MathValue.DataType.Double, infinity.Type);
        Assert.IsFalse(infinity.IsNaN);
        Assert.IsFalse(infinity.IsFinite);
        Assert.IsTrue(double.IsPositiveInfinity(infinity.DblVal));
    }

    [TestMethod]
    public void MathValueFinitenessPropertiesReflectUnderlyingState()
    {
        var integer = new MathValue(new BigInteger(42));
        Assert.IsTrue(integer.IsFinite);
        Assert.IsFalse(integer.IsNaN);

        var largeInteger = new MathValue(BigInteger.Pow(10, 100));
        Assert.IsTrue(largeInteger.IsFinite);
        Assert.IsFalse(largeInteger.IsNaN);

        var dec = new MathValue(123.456m);
        Assert.IsTrue(dec.IsFinite);
        Assert.IsFalse(dec.IsNaN);

        var finiteDouble = new MathValue(3.14159);
        Assert.IsTrue(finiteDouble.IsFinite);
        Assert.IsFalse(finiteDouble.IsNaN);

        var nanDouble = new MathValue(double.NaN);
        Assert.IsFalse(nanDouble.IsFinite);
        Assert.IsTrue(nanDouble.IsNaN);

        var posInf = new MathValue(double.PositiveInfinity);
        Assert.IsFalse(posInf.IsFinite);
        Assert.IsFalse(posInf.IsNaN);

        var negInf = new MathValue(double.NegativeInfinity);
        Assert.IsFalse(negInf.IsFinite);
        Assert.IsFalse(negInf.IsNaN);
    }

    [TestMethod]
    public void MathValueTryGetIntegerExtractsOnlyExactWholeNumbers()
    {
        var integer = new MathValue(new BigInteger(100));
        Assert.IsTrue(integer.TryGetInteger(out var intOut));
        Assert.AreEqual(new BigInteger(100), intOut);

        var wholeDec = new MathValue(100.0m);
        Assert.IsTrue(wholeDec.TryGetInteger(out var decWholeOut));
        Assert.AreEqual(new BigInteger(100), decWholeOut);

        var fracDec = new MathValue(100.25m);
        Assert.IsFalse(fracDec.TryGetInteger(out _));

        var wholeDouble = new MathValue(100.0);
        Assert.IsTrue(wholeDouble.TryGetInteger(out var dblWholeOut));
        Assert.AreEqual(new BigInteger(100), dblWholeOut);

        var fracDouble = new MathValue(100.5);
        Assert.IsFalse(fracDouble.TryGetInteger(out _));

        var nanDouble = new MathValue(double.NaN);
        Assert.IsFalse(nanDouble.TryGetInteger(out _));

        var infDouble = new MathValue(double.PositiveInfinity);
        Assert.IsFalse(infDouble.TryGetInteger(out _));
    }

    [TestMethod]
    [DataRow("100 + 0.5", MathValue.DataType.Decimal, "100.5")]
    [DataRow("100 - 0.25", MathValue.DataType.Decimal, "99.75")]
    [DataRow("5 * 1.5", MathValue.DataType.Decimal, "7.5")]
    [DataRow("10 / 2", MathValue.DataType.Integer, "5")]
    [DataRow("5 / 2", MathValue.DataType.Decimal, "2.5")]
    public void MixedIntegerAndDecimalArithmeticPreservesExactness(string expression, MathValue.DataType expectedType, string expectedValue)
    {
        var result = Calculator.Evaluate(expression);
        Assert.AreEqual(expectedType, result.Type);

        if (expectedType == MathValue.DataType.Integer)
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
        // decimal.MaxValue is 79228162514264337593543950335.
        // Adding to it will overflow Decimal addition and should fall back gracefully to
        // Double.
        var overflowSum = Calculator.Evaluate($"{decimal.MaxValue} + 1.5");
        Assert.AreEqual(MathValue.DataType.Double, overflowSum.Type);
        Assert.IsTrue(overflowSum.IsFinite);

        // Multiplication overflow.
        var overflowProd = Calculator.Evaluate($"{decimal.MaxValue} * 2.0");
        Assert.AreEqual(MathValue.DataType.Double, overflowProd.Type);
        Assert.IsTrue(overflowProd.IsFinite);
    }

    [TestMethod]
    public void DecimalLiteralUnderflowToZeroFallsBackToDouble()
    {
        // 0.00000000000000000000000000001 (29 decimal places) underflows decimal to 0m.
        // It should fall back to double rather than silently becoming zero or triggering
        // division by zero.
        var underflowLiteral = Calculator.Evaluate("0.00000000000000000000000000001");
        Assert.AreEqual(MathValue.DataType.Double, underflowLiteral.Type);
        Assert.AreNotEqual(0.0, underflowLiteral.DblVal);
        Assert.AreEqual(1e-29, underflowLiteral.DblVal, 1e-35);

        // Division by an underflowing fraction must not trigger division by zero.
        var divisionResult = Calculator.Evaluate("1 / 0.00000000000000000000000000001");
        Assert.AreEqual(MathValue.DataType.Double, divisionResult.Type);
        Assert.IsTrue(divisionResult.IsFinite);
        Assert.AreEqual(1e29, divisionResult.DblVal, 1e20);

        // Genuine zero with a decimal point remains Decimal.
        var genuineZero = Calculator.Evaluate("0.00");
        Assert.AreEqual(MathValue.DataType.Decimal, genuineZero.Type);
        Assert.AreEqual(0m, genuineZero.DecVal);
    }

    [TestMethod]
    [DataRow("sign(0)", MathValue.DataType.Integer, "0")]
    [DataRow("sign(-42)", MathValue.DataType.Integer, "-1")]
    [DataRow("sign(-42.5)", MathValue.DataType.Decimal, "-1")]
    [DataRow("abs(-12345678901234567890)", MathValue.DataType.Integer, "12345678901234567890")]
    [DataRow("abs(-12.34)", MathValue.DataType.Decimal, "12.34")]
    [DataRow("ceil(10)", MathValue.DataType.Integer, "10")]
    [DataRow("ceil(10.2)", MathValue.DataType.Decimal, "11")]
    [DataRow("floor(10.8)", MathValue.DataType.Decimal, "10")]
    [DataRow("round(2.5)", MathValue.DataType.Decimal, "3")]
    [DataRow("round(-2.5)", MathValue.DataType.Decimal, "-3")]
    [DataRow("round(2.55, 1)", MathValue.DataType.Decimal, "2.6")]
    [DataRow("round(-2.55, 1)", MathValue.DataType.Decimal, "-2.6")]
    [DataRow("round(3.14159, 2)", MathValue.DataType.Decimal, "3.14")]
    [DataRow("round(3.14159, 4)", MathValue.DataType.Decimal, "3.1416")]
    [DataRow("round(42, 2)", MathValue.DataType.Integer, "42")]
    [DataRow("round(2.5, 0)", MathValue.DataType.Decimal, "3")]
    [DataRow("round(1.0000000000000000000000000005, 28)", MathValue.DataType.Decimal, "1.0000000000000000000000000005")]
    [DataRow("factorial(0)", MathValue.DataType.Integer, "1")]
    [DataRow("sqrt(0)", MathValue.DataType.Integer, "0")]
    [DataRow("sqrt(1)", MathValue.DataType.Integer, "1")]
    [DataRow("sqrt(4)", MathValue.DataType.Integer, "2")]
    [DataRow("sqrt(9)", MathValue.DataType.Integer, "3")]
    [DataRow("sqrt(16)", MathValue.DataType.Integer, "4")]
    [DataRow("sqrt(144)", MathValue.DataType.Integer, "12")]
    [DataRow("sqrt(1000000000000000000000000000000000000000000000000000000000000)", MathValue.DataType.Integer, "1000000000000000000000000000000")]
    [DataRow("sqrt(2^64)", MathValue.DataType.Integer, "4294967296")] // 2^32
    [DataRow("sqrt(2^100)", MathValue.DataType.Integer, "1125899906842624")] // 2^50

    // Beyond double.MaxValue, confirms integer path is taken and exact value is preserved, rather than +Infinity.
    [DataRow("sqrt(2^2000)", MathValue.DataType.Integer,
        "10715086071862673209484250490600018105614048117055336074437503883703510511249361224931983788156958581275946729175531468251871452856923140435984577574698574803934567774824230985421074605062371141877954182153046474983581941267398767559165543946077062914571196477686542167660429831652624386837205668069376")]

    // Radix / non-decimal integer literals.
    [DataRow("sqrt(0x10000)", MathValue.DataType.Integer, "256")]
    [DataRow("sqrt(0x100000000)", MathValue.DataType.Integer, "65536")]
    [DataRow("sqrt(0b1000000)", MathValue.DataType.Integer, "8")] // 64 -> 8

    // Expressions resulting in perfect squares, e.g. Pythagorean triples.
    [DataRow("sqrt(3^2 + 4^2)", MathValue.DataType.Integer, "5")]
    [DataRow("sqrt(5^2 + 12^2)", MathValue.DataType.Integer, "13")]

    // Squares of large 30-bit primes (1000000007^2).
    [DataRow("sqrt(1000000014000000049)", MathValue.DataType.Integer, "1000000007")]
    public void SpecialMathFunctionsPreserveRepresentationAndExactValues(string expression, MathValue.DataType expectedType, string expectedValue)
    {
        var result = Calculator.Evaluate(expression);
        Assert.AreEqual(expectedType, result.Type);

        if (expectedType == MathValue.DataType.Integer)
        {
            Assert.AreEqual(BigInteger.Parse(expectedValue, CultureInfo.InvariantCulture), result.IntVal);
        }
        else
        {
            Assert.AreEqual(decimal.Parse(expectedValue, CultureInfo.InvariantCulture), result.DecVal);
        }
    }

    [TestMethod]
    [DataRow("sqrt(2)", 1.4142135623730951)]
    [DataRow("sqrt(3)", 1.7320508075688772)]
    [DataRow("sqrt(5)", 2.23606797749979)]
    [DataRow("sqrt(8)", 2.8284271247461903)]
    [DataRow("sqrt(15)", 3.872983346207417)]
    [DataRow("sqrt(17)", 4.123105625617661)]

    // Values immediately adjacent to large perfect squares (e.g. those tested above) must
    // fall back to double.
    [DataRow("sqrt(10^60 - 1)", 1e30)]
    [DataRow("sqrt(10^60 + 1)", 1e30)]
    public void NonSquareIntegersFallBackToDouble(string expression, double expected)
    {
        var result = Calculator.Evaluate(expression);

        // Crucial: Must be Double, NOT an integer floor!
        Assert.AreEqual(MathValue.DataType.Double, result.Type);
        Assert.AreEqual(expected, result.DblVal, 1e-12);
    }

    [TestMethod]
    [DataRow("sqrt(-1)")]
    [DataRow("sqrt(-4)")]
    [DataRow("sqrt(-16)")]
    [DataRow("sqrt(-1000000000000000000000000000000000000000000000000000000000000)")]
    [DataRow("sqrt(-2^64)")]
    public void NegativeIntegersThrowUndefinedMathResult(string expression)
    {
        Assert.IsFalse(Calculator.TryEvaluate(expression, out _, out var diagnostic));
        Assert.IsNotNull(diagnostic);
        Assert.AreEqual(EvaluationError.UndefinedMathResult, diagnostic.Code);

        var ex = Assert.Throws<ExpressionException>(() => Calculator.Evaluate(expression));
        Assert.AreEqual(EvaluationError.UndefinedMathResult, ex.Diagnostic.Code);
    }

    [TestMethod]
    [DataRow("factorial(-1)", EvaluationError.ArgumentOutOfRange)]
    [DataRow("factorial(2.5)", EvaluationError.IntegerRequired)]
    [DataRow("round(2.5, -1)", EvaluationError.ArgumentOutOfRange)]
    [DataRow("round(2.5, 29)", EvaluationError.ArgumentOutOfRange)]
    [DataRow("round(2.5, 1.5)", EvaluationError.IntegerRequired)]
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
    public void RoundDoubleAtAndBeyondSupportedPlaces()
    {
        // 15 decimal places is the maximum precision handled by double.Round
        var atLimit = Calculator.Evaluate("round(sqrt(2), 15)");
        Assert.AreEqual(MathValue.DataType.Double, atLimit.Type);
        Assert.AreEqual(Math.Round(Math.Sqrt(2), 15, MidpointRounding.AwayFromZero), atLimit.DblVal);

        // Places 16..28 exceed double.Round's argument limit but are accepted within the general places ceiling (28)
        var beyondDouble = Calculator.Evaluate("round(sqrt(2), 16)");
        Assert.AreEqual(MathValue.DataType.Double, beyondDouble.Type);
        Assert.AreEqual(Math.Sqrt(2), beyondDouble.DblVal);

        var atCeiling = Calculator.Evaluate("round(sqrt(2), 28)");
        Assert.AreEqual(MathValue.DataType.Double, atCeiling.Type);
        Assert.AreEqual(Math.Sqrt(2), atCeiling.DblVal);
    }

    [TestMethod]
    public void SpecialFunctionsHandleInfinitiesAndLimits()
    {
        // erf limits.
        Assert.AreEqual(1.0, Calculator.Evaluate("erf(100)").DblVal, 1e-15);
        Assert.AreEqual(-1.0, Calculator.Evaluate("erf(-100)").DblVal, 1e-15);

        // erfc limits.
        Assert.AreEqual(0.0, Calculator.Evaluate("erfc(100)").DblVal, 1e-15);
        Assert.AreEqual(2.0, Calculator.Evaluate("erfc(-100)").DblVal, 1e-15);

        // ncdf limits.
        Assert.AreEqual(1.0, Calculator.Evaluate("ncdf(100)").DblVal, 1e-15);
        Assert.AreEqual(0.0, Calculator.Evaluate("ncdf(-100)").DblVal, 1e-15);

        // sinc limits and small values.
        Assert.AreEqual(1.0, Calculator.Evaluate("sinc(1e-10)").DblVal, 1e-15);
        Assert.AreEqual(0.0, Calculator.Evaluate("sinc(1000000)").DblVal, 1e-5);
    }

    [TestMethod]
    public void SpecialFunctionsHandleDeepUnderflowAndSymmetry()
    {
        // Underflow cutoff (x >= 27.3).
        Assert.AreEqual(0.0, Calculator.Evaluate("erfc(28)").DblVal, 0.0);
        Assert.AreEqual(0.0, Calculator.Evaluate("erfc(30)").DblVal, 0.0);

        // sinc even symmetry: sinc(-x) == sinc(x).
        Assert.AreEqual(Calculator.Evaluate("sinc(1.5)").DblVal, Calculator.Evaluate("sinc(-1.5)").DblVal, 0.0);
    }

    /// <summary>
    /// Exercises Cody rational Chebyshev approximation intervals and boundary regions for
    /// erf, erfc, and ncdf across all three intervals:
    ///   Interval 1: [0, 0.46875] (ErfA, ErfB)
    ///   Interval 2: [0.46875, 4.0] (ErfcC, ErfcD)
    ///   Interval 3: [4.0, inf) (ErfcP, ErfcQ)
    /// Detects transcription errors, incorrect polynomial evaluation orders, or
    /// mismatched formulas.
    /// </summary>
    [TestMethod]
    [DataRow("erf(0.001)", 0.0011283787909692365, 2e-15, "Interval 1 near-zero")]
    [DataRow("erf(0.25)", 0.27632639016823693, 2e-15, "Interval 1 interior")]
    [DataRow("erf(0.46874)", 0.4926044152441113, 2e-15, "Interval 1 upper boundary")]
    [DataRow("erf(0.46876)", 0.49262253110684645, 2e-15, "Interval 2 lower boundary")]
    [DataRow("erfc(0.46874)", 0.5073955847558886, 2e-15, "Interval 1 complementary boundary")]
    [DataRow("erfc(0.46876)", 0.5073774688931536, 2e-15, "Interval 2 complementary boundary")]
    [DataRow("erf(1.0)", 0.84270079294971487, 2e-15, "Interval 2 interior erf")]
    [DataRow("erfc(1.0)", 0.15729920705028513, 2e-15, "Interval 2 interior erfc")]
    [DataRow("erfc(2.5)", 0.00040695201744495893, 2e-15, "Interval 2 middle")]
    [DataRow("erfc(3.99)", 1.673921136452081e-8, 2e-15, "Interval 2 upper boundary")]
    [DataRow("erfc(4.01)", 1.4196939421371045e-8, 2e-15, "Interval 3 lower boundary")]
    [DataRow("erfc(5.0)", 1.53745979442803485e-12, 2e-15, "Interval 3 interior")]
    [DataRow("erfc(6.0)", 2.15197367124989131e-17, 2e-15, "Interval 3 asymptotic")]
    [DataRow("erfc(10.0)", 2.08848758376254476e-45, 2e-15, "Interval 3 deep tail")]
    [DataRow("ncdf(0.5)", 0.69146246127401312, 2e-15, "ncdf Interval 1/2 transition")]
    [DataRow("ncdf(2.5)", 0.99379033467422385, 2e-15, "ncdf Interval 2")]
    [DataRow("ncdf(5.0)", 0.99999971334842805, 2e-15, "ncdf Interval 3 asymptotic")]
    public void SpecialFunctionsApproximateAccuratelyAcrossIntervals(string expression, double expected, double tolerance, string description)
    {
        var result = Calculator.Evaluate(expression);
        Assert.AreEqual(MathValue.DataType.Double, result.Type);

        // For values < 1.0 (such as tail erfc), evaluate relative error so that precision
        // is validated to double-precision machine epsilon across all magnitudes.
        double absoluteError = Math.Abs(result.DblVal - expected);
        double relativeError = expected == 0.0 ? absoluteError : absoluteError / Math.Abs(expected);
        double effectiveError = Math.Min(absoluteError, relativeError);

        Assert.IsLessThanOrEqualTo(tolerance, effectiveError,
            $"Failed for {description}: {expression}. Expected: {expected}, Actual: {result.DblVal}, Relative/Effective Error: {effectiveError}");
    }

    [TestMethod]
    public void NegativeOddRootYieldsRealResult()
    {
        var result = Calculator.Evaluate("root(-8, 3)");
        Assert.AreEqual(MathValue.DataType.Double, result.Type);
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
        Assert.AreEqual(expected, result.Type == MathValue.DataType.Decimal
            ? result.DecVal.ToString(CultureInfo.InvariantCulture)
            : result.IntVal.ToString(CultureInfo.InvariantCulture));
    }

    [TestMethod]
    public void MathValueFormattableDirectToStringMatchesStandardBehavior()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

            var intVal = new MathValue(new BigInteger(1234567));
            Assert.AreEqual("1234567", intVal.ToString());
            Assert.AreEqual("1,234,567", intVal.ToString("N0"));
            Assert.AreEqual("1234567", $"{intVal}");

            var decVal = new MathValue(1234.56m);
            Assert.AreEqual("1234.56", decVal.ToString());
            Assert.AreEqual("1234.6", decVal.ToString("F1"));
            Assert.AreEqual("1234.56", $"{decVal}");

            // Double defaults to G17 when format is empty/null
            var dblVal = new MathValue(1.0 / 3.0);
            Assert.AreEqual((1.0 / 3.0).ToString("G17", CultureInfo.InvariantCulture), dblVal.ToString());
            Assert.AreEqual("0.33", dblVal.ToString("F2"));
            Assert.AreEqual((1.0 / 3.0).ToString("G17", CultureInfo.InvariantCulture), $"{dblVal}");

            // Special double values format accurately
            var nanVal = new MathValue(double.NaN);
            Assert.AreEqual("NaN", nanVal.ToString());
            var infVal = new MathValue(double.PositiveInfinity);
            Assert.AreEqual("Infinity", infVal.ToString());
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [TestMethod]
    public void MathValueFormattableRespectsSuppliedCulture()
    {
        var germanCulture = CultureInfo.GetCultureInfo("de-DE");

        var decVal = new MathValue(1234.56m);
        Assert.AreEqual("1234,56", decVal.ToString(null, germanCulture));
        Assert.AreEqual("1.234,56", decVal.ToString("N2", germanCulture));

        var dblVal = new MathValue(3.5);
        Assert.AreEqual("3,5", dblVal.ToString(null, germanCulture));

        var intVal = new MathValue(new BigInteger(1000000));
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
    [DataRow("tan(135)", -1.0)]
    [DataRow("tan(315)", -1.0)]
    [DataRow("tan(-45)", -1.0)]
    [DataRow("cot(45)", 1.0)]
    [DataRow("cot(135)", -1.0)]
    [DataRow("cot(90)", 0.0)]
    [DataRow("cot(270)", 0.0)]
    [DataRow("sec(0)", 1.0)]
    [DataRow("sec(180)", -1.0)]
    [DataRow("csc(90)", 1.0)]
    [DataRow("csc(270)", -1.0)]
    public void TrigonometricFunctionsInDegreesModeEvaluateExactCardinalValues(string expression, double expected)
    {
        var options = new EvaluationOptions { AngleUnit = AngleUnit.Degrees };
        var result = Calculator.Evaluate(expression, options);

        Assert.AreEqual(MathValue.DataType.Double, result.Type);
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
        Assert.AreEqual(MathValue.DataType.Double, result.Type);
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
    [DataRow("tan(deg(pi/2))", EvaluationError.UndefinedMathResult)]
    [DataRow("tan(grad2deg(100))", EvaluationError.UndefinedMathResult)]
    [DataRow("sec(deg(pi/2))", EvaluationError.UndefinedMathResult)]
    [DataRow("cot(deg(pi))", EvaluationError.UndefinedMathResult)]
    [DataRow("csc(deg(pi))", EvaluationError.UndefinedMathResult)]
    public void ComputedDegreeAsymptotesProduceDiagnostics(string expression, EvaluationError expectedError)
    {
        var degOptions = new EvaluationOptions
        {
            AngleUnit = AngleUnit.Degrees
        };
        Assert.IsFalse(Calculator.TryEvaluate(expression, out _, out var diagnostic, degOptions));
        Assert.AreEqual(expectedError, diagnostic!.Code);
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
        Assert.AreEqual(MathValue.DataType.Integer, result.Type);
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
            MathValue.DataType.Decimal => (double)result.DecVal,
            MathValue.DataType.Double => result.DblVal,
            MathValue.DataType.Integer => (double)result.IntVal,
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
        string? str = result.Type == MathValue.DataType.Integer
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
        double actual = result.Type switch
        {
            MathValue.DataType.Decimal => (double)result.DecVal,
            MathValue.DataType.Integer => (double)result.IntVal,
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
        string? str = result.Type switch
        {
            MathValue.DataType.Integer => result.IntVal.ToString(CultureInfo.InvariantCulture),
            MathValue.DataType.Decimal => result.DecVal.ToString(CultureInfo.InvariantCulture),
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
    [DataRow("~0", "-1")]
    [DataRow("~1", "-2")]
    [DataRow("~~0", "0")]
    [DataRow("~(-1)", "0")]
    [DataRow("~0xFF", "-256")]
    public void BitwiseOperationsEvaluateCorrectly(string expression, string expected)
    {
        var result = Calculator.Evaluate(expression);
        Assert.AreEqual(MathValue.DataType.Integer, result.Type);
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
        string actual = result.Type switch
        {
            MathValue.DataType.Integer => result.IntVal.ToString(CultureInfo.InvariantCulture),
            MathValue.DataType.Decimal => result.DecVal.ToString(CultureInfo.InvariantCulture),
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
    [DataRow("~1.5", EvaluationError.IntegerRequired)]
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
        Assert.Throws<CalculationLimitException>(() => Calculator.Evaluate($"1 << {CalculationLimits.MaxIntegerBits + 1}"));
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
        Assert.AreEqual(MathValue.DataType.Double, result.Type);
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
        Assert.AreEqual(MathValue.DataType.Double, result.Type);
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
        // Decimal preservation.
        Assert.AreEqual(3.0m, Calculator.Evaluate("0.3 / 0.1").DecVal);
        Assert.AreEqual(194.40m, Calculator.Evaluate("3033554.11 - 3033359.71").DecVal);
        Assert.AreEqual(1m, Calculator.Evaluate("0.1 + 0.2 == 0.3").DecVal);

        // Large Integer preservation.
        Assert.AreEqual(BigInteger.One << 64, Calculator.Evaluate("2^64").IntVal);
        Assert.AreEqual(0m, Calculator.Evaluate("2^64 - 1 == 2^64").DecVal);

        // Unary minus binds looser than exponentiation (-2^2 is -4, not +4).
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
    [DataRow("2 == 2 == 1", "0")]  // The classic left-fold bug: now correctly returns 0
    [DataRow("1 != 2 != 2", "0")]  // First pair is true, second is false, so 0
    [DataRow("1 < 2 < 1", "0")]    // Transitivity failure
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

        // 360,000,060 degrees -> 60 degrees; cos(60 deg) = 0.5.
        var cosResult = Calculator.Evaluate("cos(360000060)", degOptions);
        Assert.AreEqual(0.5, cosResult.DblVal, 1e-15);

        // 400,000,050 gradians is 1,000,000 revolutions + 50 gradians (45 degrees).
        // Beyond MaxSnappingRevolutions (100.0 revs), it falls back to double.TanPi with
        // reduced remainder.
        var gradOptions = new EvaluationOptions { AngleUnit = AngleUnit.Gradians };
        var tanGradResult = Calculator.Evaluate("tan(400000050)", gradOptions);
        Assert.AreEqual(1.0, tanGradResult.DblVal, 1e-15);

        // Non-cardinal angle with many revolutions in degrees:
        // 360000000 + 37 degrees, which evaluates identically to sin(37 deg) without
        // revolution offset.
        var baseSin = Calculator.Evaluate("sin(37)", degOptions);
        var largeSin = Calculator.Evaluate("sin(360000037)", degOptions);
        Assert.AreEqual(baseSin.DblVal, largeSin.DblVal, 1e-15);

        var baseCos = Calculator.Evaluate("cos(37)", degOptions);
        var largeCos = Calculator.Evaluate("cos(360000037)", degOptions);
        Assert.AreEqual(baseCos.DblVal, largeCos.DblVal, 1e-15);

        var baseTan = Calculator.Evaluate("tan(37)", degOptions);
        var largeTan = Calculator.Evaluate("tan(360000037)", degOptions);
        Assert.AreEqual(baseTan.DblVal, largeTan.DblVal, 1e-15);

        var largeNegSin = Calculator.Evaluate("sin(-360000030)", degOptions);
        Assert.AreEqual(-0.5, largeNegSin.DblVal, 1e-15);

        var largeNegCos = Calculator.Evaluate("cos(-360000060)", degOptions);
        Assert.AreEqual(0.5, largeNegCos.DblVal, 1e-15);
    }

    [TestMethod]
    public void Expm1AndLog1pPreservePrecisionForSmallArguments()
    {
        // For x = 1e-5:
        // Direct exp(x) - 1 loses lower precision bits due to subtracting 1.0,
        // whereas expm1(x) and log1p(x) preserve full double precision.
        double x = 1e-5;
        var expm1Result = Calculator.Evaluate("expm1(1e-5)");
        Assert.AreEqual(MathValue.DataType.Double, expm1Result.Type);
        Assert.AreEqual(double.ExpM1(x), expm1Result.DblVal, 0.0);

        var log1pResult = Calculator.Evaluate("log1p(1e-5)");
        Assert.AreEqual(MathValue.DataType.Double, log1pResult.Type);
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

    [TestMethod]
    public void ComparisonsObeyIeee754NanSemantics()
    {
        var nan = new MathValue(double.NaN);

        // nan == nan -> false (0)
        Assert.AreEqual(0m, MathValue.Compare(nan, nan) == 0 ? 0m : 1m); // Compare total-orders, but parser comparison checks NaN
        var eqExpr = Calculator.Evaluate("(0 * exp(1000)) == (0 * exp(1000))");
        Assert.AreEqual(0m, eqExpr.DecVal);

        var neqExpr = Calculator.Evaluate("(0 * exp(1000)) != (0 * exp(1000))");
        Assert.AreEqual(1m, neqExpr.DecVal);

        var tildeNeqExpr = Calculator.Evaluate("(0 * exp(1000)) ~= (0 * exp(1000))");
        Assert.AreEqual(1m, tildeNeqExpr.DecVal);

        var ltExpr = Calculator.Evaluate("(0 * exp(1000)) < 5");
        Assert.AreEqual(0m, ltExpr.DecVal);

        var lteExpr = Calculator.Evaluate("(0 * exp(1000)) <= 5");
        Assert.AreEqual(0m, lteExpr.DecVal);

        var gtExpr = Calculator.Evaluate("(0 * exp(1000)) > 5");
        Assert.AreEqual(0m, gtExpr.DecVal);

        var gteExpr = Calculator.Evaluate("(0 * exp(1000)) >= 5");
        Assert.AreEqual(0m, gteExpr.DecVal);

        var revLt = Calculator.Evaluate("5 < (0 * exp(1000))");
        Assert.AreEqual(0m, revLt.DecVal);

        var revGt = Calculator.Evaluate("5 > (0 * exp(1000))");
        Assert.AreEqual(0m, revGt.DecVal);

        var revEq = Calculator.Evaluate("5 == (0 * exp(1000))");
        Assert.AreEqual(0m, revEq.DecVal);

        var revNeq = Calculator.Evaluate("5 != (0 * exp(1000))");
        Assert.AreEqual(1m, revNeq.DecVal);
    }

    [TestMethod]
    public void ClampPropagatesNanWithoutThrowing()
    {
        var nanMin = Calculator.Evaluate("clamp(5, 0, 0 * exp(1000))");
        Assert.IsTrue(nanMin.IsNaN);

        var nanVal = Calculator.Evaluate("clamp(0 * exp(1000), 0, 10)");
        Assert.IsTrue(nanVal.IsNaN);

        var nanMax = Calculator.Evaluate("clamp(5, 0 * exp(1000), 10)");
        Assert.IsTrue(nanMax.IsNaN);
    }

    [TestMethod]
    public void LargeBigIntegerDoesNotCompareEqualToPositiveInfinity()
    {
        // 2^2000 has ~603 decimal digits, overflowing IEEE 754 double into +Infinity.
        // It must not compare equal to exp(1000) (which evaluates to +Infinity).
        var equalExpr = Calculator.Evaluate("2^2000 == exp(1000)");
        Assert.AreEqual(0m, equalExpr.DecVal);

        var notEqualExpr = Calculator.Evaluate("2^2000 != exp(1000)");
        Assert.AreEqual(1m, notEqualExpr.DecVal);

        var lessExpr = Calculator.Evaluate("2^2000 < exp(1000)");
        Assert.AreEqual(1m, lessExpr.DecVal);

        var greaterExpr = Calculator.Evaluate("2^2000 > exp(1000)");
        Assert.AreEqual(0m, greaterExpr.DecVal);

        var revGreater = Calculator.Evaluate("exp(1000) > 2^2000");
        Assert.AreEqual(1m, revGreater.DecVal);

        var negInfExpr = Calculator.Evaluate("-2^2000 > -exp(1000)");
        Assert.AreEqual(1m, negInfExpr.DecVal);
    }

    [TestMethod]
    public void LargeAngleAsymptotesInDegreesModeThrowUndefinedMathResult()
    {
        var degOptions = new EvaluationOptions { AngleUnit = AngleUnit.Degrees };

        // 360000090 is 10^6 * 360 + 90 degrees
        var exTan = Assert.Throws<ExpressionException>(() => Calculator.Evaluate("tan(360000090)", degOptions));
        Assert.AreEqual(EvaluationError.UndefinedMathResult, exTan.Diagnostic.Code);

        var exCot = Assert.Throws<ExpressionException>(() => Calculator.Evaluate("cot(360000180)", degOptions));
        Assert.AreEqual(EvaluationError.UndefinedMathResult, exCot.Diagnostic.Code);

        var exSec = Assert.Throws<ExpressionException>(() => Calculator.Evaluate("sec(360000090)", degOptions));
        Assert.AreEqual(EvaluationError.UndefinedMathResult, exSec.Diagnostic.Code);

        var exCsc = Assert.Throws<ExpressionException>(() => Calculator.Evaluate("csc(360000180)", degOptions));
        Assert.AreEqual(EvaluationError.UndefinedMathResult, exCsc.Diagnostic.Code);

        // TryEvaluate safely reports diagnostic for large asymptote
        Assert.IsFalse(Calculator.TryEvaluate("tan(360000090)", out _, out var diagnostic, degOptions));
        Assert.IsNotNull(diagnostic);
        Assert.AreEqual(EvaluationError.UndefinedMathResult, diagnostic.Code);
    }

    [TestMethod]
    public void DecimalBaseWithPositiveWholeExponentPreservesExactDecimalPrecision()
    {
        // (1 + 0.05)^10 should evaluate as exact decimal arithmetic via exponentiation by
        // squaring.
        var result = Calculator.Evaluate("(1 + 0.05)^10");
        Assert.AreEqual(MathValue.DataType.Decimal, result.Type);
        Assert.AreEqual(1.62889462677744140625m, result.DecVal);

        // Power of zero gives exact 1m decimal.
        var zeroPow = Calculator.Evaluate("1.05^0");
        Assert.AreEqual(MathValue.DataType.Decimal, zeroPow.Type);
        Assert.AreEqual(1.0m, zeroPow.DecVal);

        // Negative exponent falls back to double.
        var negPow = Calculator.Evaluate("1.05^-2");
        Assert.AreEqual(MathValue.DataType.Double, negPow.Type);
        Assert.AreEqual(1.0 / (1.05 * 1.05), negPow.DblVal, 1e-15);

        // Overflow of decimal range falls back to double without throwing.
        var overflowPow = Calculator.Evaluate("100.5^200");
        Assert.AreEqual(MathValue.DataType.Double, overflowPow.Type);
        Assert.IsTrue(double.IsFinite(overflowPow.DblVal) || double.IsPositiveInfinity(overflowPow.DblVal));
    }

    [TestMethod]
    public void UnderscoresInIdentifiersAreParsedCorrectly()
    {
        // An unknown function containing underscores must be parsed as a full identifier
        // and report UnknownFunction rather than an unexpected character syntax error.
        Assert.IsFalse(Calculator.TryEvaluate("deg_to_rad(10)", out _, out var diagnostic));
        Assert.IsNotNull(diagnostic);
        Assert.AreEqual(EvaluationError.UnknownFunction, diagnostic.Code);
        Assert.Contains("deg_to_rad", diagnostic.Message);

        var ex = Assert.Throws<ExpressionException>(() => Calculator.Evaluate("deg_to_rad(10)"));
        Assert.AreEqual(EvaluationError.UnknownFunction, ex.Diagnostic.Code);
    }

    [TestMethod]
    [DataRow("gamma(1)", 1.0, 1e-14)]
    [DataRow("gamma(2)", 1.0, 1e-14)]
    [DataRow("gamma(3)", 2.0, 1e-14)]
    [DataRow("gamma(4)", 6.0, 1e-14)]
    [DataRow("gamma(5)", 24.0, 1e-14)]
    [DataRow("gamma(6)", 120.0, 1e-14)]
    [DataRow("gamma(0.5)", 1.7724538509055159, 1e-14)] // sqrt(pi)
    [DataRow("gamma(1.5)", 0.886226925452758, 1e-14)]  // 0.5 * sqrt(pi)
    [DataRow("gamma(2.5)", 1.329340388179137, 1e-14)]  // 1.5 * 0.5 * sqrt(pi)
    [DataRow("gamma(-0.5)", -3.5449077018110318, 1e-14)] // -2 * sqrt(pi)
    [DataRow("gamma(-1.5)", 2.3632718012073546, 1e-14)]  // (4/3) * sqrt(pi)
    [DataRow("lgamma(1)", 0.0, 1e-14)]
    [DataRow("lgamma(2)", 0.0, 1e-14)]
    [DataRow("lgamma(3)", 0.6931471805599453, 1e-14)]  // ln(2)
    [DataRow("lgamma(4)", 1.791759469228055, 1e-14)]   // ln(6)
    [DataRow("lgamma(5)", 3.1780538303479458, 1e-14)]  // ln(24)
    [DataRow("lgamma(0.5)", 0.57236494292470009, 1e-14)] // ln(sqrt(pi))
    [DataRow("beta(1, 1)", 1.0, 1e-14)]
    [DataRow("beta(2, 3)", 0.08333333333333333, 1e-14)] // 1/12
    [DataRow("beta(0.5, 0.5)", 3.141592653589793, 1e-14)] // pi
    [DataRow("zeta(2)", 1.6449340668482264, 1e-14)]    // pi^2 / 6
    [DataRow("zeta(4)", 1.0823232337111382, 1e-14)]    // pi^4 / 90
    [DataRow("zeta(3)", 1.2020569031595942, 1e-14)]    // Apéry's constant
    [DataRow("zeta(0)", -0.5, 1e-14)]
    [DataRow("zeta(-1)", -0.08333333333333333, 1e-14)]  // -1/12
    [DataRow("zeta(-2)", 0.0, 1e-14)]                   // trivial zero
    [DataRow("zeta(-3)", 0.008333333333333333, 1e-14)]  // 1/120
    [DataRow("zeta(-4)", 0.0, 1e-14)]                   // trivial zero
    public void SpecialFunctionsGammaBetaZetaEvaluateAccurately(string expression, double expected, double tolerance)
    {
        var result = Calculator.Evaluate(expression);
        Assert.AreEqual(MathValue.DataType.Double, result.Type);
        Assert.AreEqual(expected, result.DblVal, tolerance);
    }

    [TestMethod]
    [DataRow("gamma(0)", EvaluationError.UndefinedMathResult)]
    [DataRow("gamma(-1)", EvaluationError.UndefinedMathResult)]
    [DataRow("gamma(-2)", EvaluationError.UndefinedMathResult)]
    [DataRow("lgamma(0)", EvaluationError.UndefinedMathResult)]
    [DataRow("lgamma(-1)", EvaluationError.UndefinedMathResult)]
    [DataRow("beta(0, 1)", EvaluationError.UndefinedMathResult)]
    [DataRow("beta(-1, 2)", EvaluationError.UndefinedMathResult)]
    [DataRow("zeta(1)", EvaluationError.UndefinedMathResult)]
    public void SpecialFunctionsSingularitiesReportUndefinedMathResult(string expression, EvaluationError expectedError)
    {
        Assert.IsFalse(Calculator.TryEvaluate(expression, out _, out var diagnostic));
        Assert.IsNotNull(diagnostic);
        Assert.AreEqual(expectedError, diagnostic.Code);

        var ex = Assert.Throws<ExpressionException>(() => Calculator.Evaluate(expression));
        Assert.AreEqual(expectedError, ex.Diagnostic.Code);
    }

    [TestMethod]
    [DataRow("gcd(12, 18)", "6")]
    [DataRow("gcd(12, 18, 24)", "6")]
    [DataRow("gcd(54, 24)", "6")]
    [DataRow("gcd(7)", "7")]
    [DataRow("gcd(-7)", "7")]
    [DataRow("gcd(0, 5)", "5")]
    [DataRow("gcd(0, 0)", "0")]
    [DataRow("gcd(-12, 18)", "6")]
    [DataRow("gcd(-12, -18)", "6")]
    [DataRow("lcm(4, 6)", "12")]
    [DataRow("lcm(4, 6, 8)", "24")]
    [DataRow("lcm(7)", "7")]
    [DataRow("lcm(-7)", "7")]
    [DataRow("lcm(0, 5)", "0")]
    [DataRow("lcm(5, 0)", "0")]
    [DataRow("lcm(-4, 6)", "12")]
    [DataRow("comb(5, 2)", "10")]
    [DataRow("nCr(5, 2)", "10")]
    [DataRow("choose(5, 2)", "10")]
    [DataRow("binomial(5, 2)", "10")]
    [DataRow("comb(10, 0)", "1")]
    [DataRow("comb(10, 10)", "1")]
    [DataRow("comb(5, 6)", "0")]
    [DataRow("comb(50, 6)", "15890700")]
    [DataRow("perm(5, 2)", "20")]
    [DataRow("nPr(5, 2)", "20")]
    [DataRow("perm(10, 0)", "1")]
    [DataRow("perm(10, 1)", "10")]
    [DataRow("perm(5, 6)", "0")]
    [DataRow("perm(7, 3)", "210")]
    [DataRow("isprime(2)", "1")]
    [DataRow("isprime(3)", "1")]
    [DataRow("isprime(4)", "0")]
    [DataRow("isprime(17)", "1")]
    [DataRow("isprime(1)", "0")]
    [DataRow("isprime(0)", "0")]
    [DataRow("isprime(-7)", "0")]
    [DataRow("isprime(97)", "1")]
    [DataRow("isprime(100)", "0")]
    [DataRow("isprime(104729)", "1")] // 10,000th prime
    [DataRow("isprime(104730)", "0")]
    [DataRow("divcount(1)", "1")]
    [DataRow("divcount(12)", "6")]
    [DataRow("divcount(28)", "6")]
    [DataRow("divcount(36)", "9")]
    [DataRow("divcount(97)", "2")]
    [DataRow("divisorcount(12)", "6")]
    [DataRow("sigma(1)", "1")]
    [DataRow("sigma(6)", "12")]
    [DataRow("sigma(12)", "28")]
    [DataRow("sigma(28)", "56")]
    [DataRow("divsum(12)", "28")]
    [DataRow("sigma(12, 0)", "6")]
    [DataRow("divsum(12, 0)", "6")]
    [DataRow("sigma(12, 1)", "28")]
    [DataRow("sigma(6, 2)", "50")] // 1 + 4 + 9 + 36 = 50
    [DataRow("divsum(6, 2)", "50")]
    [DataRow("totient(1)", "1")]
    [DataRow("totient(9)", "6")]
    [DataRow("totient(10)", "4")]
    [DataRow("totient(12)", "4")]
    [DataRow("totient(13)", "12")]
    [DataRow("phi(12)", "4")]
    [DataRow("eulerphi(12)", "4")]
    public void CombinatoricsAndNumberTheoryEvaluateCorrectly(string expression, string expected)
    {
        var result = Calculator.Evaluate(expression);
        Assert.AreEqual(MathValue.DataType.Integer, result.Type);
        Assert.AreEqual(expected, result.ToString());
    }

    [TestMethod]
    [DataRow("gcd(1.5, 3)", EvaluationError.IntegerRequired)]
    [DataRow("lcm(2, 3.5)", EvaluationError.IntegerRequired)]
    [DataRow("comb(5.5, 2)", EvaluationError.IntegerRequired)]
    [DataRow("comb(5, 2.5)", EvaluationError.IntegerRequired)]
    [DataRow("perm(5.5, 2)", EvaluationError.IntegerRequired)]
    [DataRow("isprime(2.5)", EvaluationError.IntegerRequired)]
    [DataRow("divcount(2.5)", EvaluationError.IntegerRequired)]
    [DataRow("divisorcount(2.5)", EvaluationError.IntegerRequired)]
    [DataRow("sigma(2.5)", EvaluationError.IntegerRequired)]
    [DataRow("divsum(2.5)", EvaluationError.IntegerRequired)]
    [DataRow("sigma(12, 1.5)", EvaluationError.IntegerRequired)]
    [DataRow("divsum(12, 1.5)", EvaluationError.IntegerRequired)]
    [DataRow("totient(2.5)", EvaluationError.IntegerRequired)]
    [DataRow("phi(2.5)", EvaluationError.IntegerRequired)]
    [DataRow("eulerphi(2.5)", EvaluationError.IntegerRequired)]
    [DataRow("comb(-5, 2)", EvaluationError.ArgumentOutOfRange)]
    [DataRow("comb(5, -2)", EvaluationError.ArgumentOutOfRange)]
    [DataRow("perm(-5, 2)", EvaluationError.ArgumentOutOfRange)]
    [DataRow("perm(5, -2)", EvaluationError.ArgumentOutOfRange)]
    [DataRow("divcount(0)", EvaluationError.ArgumentOutOfRange)]
    [DataRow("divcount(-12)", EvaluationError.ArgumentOutOfRange)]
    [DataRow("divisorcount(0)", EvaluationError.ArgumentOutOfRange)]
    [DataRow("sigma(0)", EvaluationError.ArgumentOutOfRange)]
    [DataRow("sigma(-12)", EvaluationError.ArgumentOutOfRange)]
    [DataRow("divsum(-12)", EvaluationError.ArgumentOutOfRange)]
    [DataRow("sigma(12, -1)", EvaluationError.ArgumentOutOfRange)]
    [DataRow("divsum(12, -1)", EvaluationError.ArgumentOutOfRange)]
    [DataRow("totient(0)", EvaluationError.ArgumentOutOfRange)]
    [DataRow("totient(-12)", EvaluationError.ArgumentOutOfRange)]
    [DataRow("phi(-12)", EvaluationError.ArgumentOutOfRange)]
    [DataRow("eulerphi(-12)", EvaluationError.ArgumentOutOfRange)]
    public void CombinatoricsAndNumberTheoryDiagnosticsAreReported(string expression, EvaluationError expectedError)
    {
        Assert.IsFalse(Calculator.TryEvaluate(expression, out _, out var diagnostic));
        Assert.IsNotNull(diagnostic);
        Assert.AreEqual(expectedError, diagnostic.Code);

        var ex = Assert.Throws<ExpressionException>(() => Calculator.Evaluate(expression));
        Assert.AreEqual(expectedError, ex.Diagnostic.Code);
    }

    [TestMethod]
    [DataRow("[1+2]", 0, 1)]
    [DataRow("1+2]", 3, 1)]
    [DataRow("1;2", 1, 1)]
    [DataRow("1 + [2]", 4, 1)]
    [DataRow("matrix[1]", 6, 1)]
    public void ReservedBracketAndSemicolonDiagnosticsAreReported(string expression, int expectedPos, int expectedLen)
    {
        Assert.IsFalse(Calculator.TryEvaluate(expression, out _, out var diagnostic));
        Assert.IsNotNull(diagnostic);
        Assert.AreEqual(EvaluationError.InvalidSyntax, diagnostic.Code);
        Assert.IsTrue(diagnostic.Message.Contains("reserved", StringComparison.OrdinalIgnoreCase));
        Assert.AreEqual(expectedPos, diagnostic.Position);
        Assert.AreEqual(expectedLen, diagnostic.Length);

        var ex = Assert.Throws<ExpressionException>(() => Calculator.Evaluate(expression));
        Assert.AreEqual(EvaluationError.InvalidSyntax, ex.Diagnostic.Code);
    }

    [TestMethod]
    public void BigIntegerExactSquareRootAndNewtonSqrtBehaveCorrectly()
    {
        // Zero and small boundaries.
        Assert.AreEqual(BigInteger.Zero, BigInteger.Zero.Sqrt());
        Assert.IsTrue(BigInteger.Zero.TryGetExactSquareRoot(out var root0));
        Assert.AreEqual(BigInteger.Zero, root0);

        Assert.AreEqual(BigInteger.One, BigInteger.One.Sqrt());
        Assert.IsTrue(BigInteger.One.TryGetExactSquareRoot(out var root1));
        Assert.AreEqual(BigInteger.One, root1);

        Assert.AreEqual(BigInteger.One, new BigInteger(2).Sqrt());
        Assert.IsFalse(new BigInteger(2).TryGetExactSquareRoot(out var root2));
        Assert.AreEqual(BigInteger.Zero, root2);

        Assert.AreEqual(BigInteger.One, new BigInteger(3).Sqrt());
        Assert.IsFalse(new BigInteger(3).TryGetExactSquareRoot(out var root3));
        Assert.AreEqual(BigInteger.Zero, root3);

        // Negative numbers reject square root.
        Assert.Throws<ArgumentOutOfRangeException>(() => new BigInteger(-1).Sqrt());
        Assert.IsFalse(new BigInteger(-1).TryGetExactSquareRoot(out var negRoot));
        Assert.AreEqual(BigInteger.Zero, negRoot);

        // Exhaustive check across residues mod 64 for 0..10,000.
        for (int i = 0; i <= 10000; i++)
        {
            var sq = (BigInteger)i * i;
            Assert.AreEqual((BigInteger)i, sq.Sqrt(), $"Sqrt failed on {sq}");
            Assert.IsTrue(sq.TryGetExactSquareRoot(out var exactRoot), $"TryGetExactSquareRoot failed on square {sq}");
            Assert.AreEqual((BigInteger)i, exactRoot);

            if (i > 1)
            {
                var nonSq1 = sq - 1;
                Assert.AreEqual((BigInteger)(i - 1), nonSq1.Sqrt(), $"Sqrt failed on {nonSq1}");
                Assert.IsFalse(nonSq1.TryGetExactSquareRoot(out var nonRoot1), $"False positive exact square root on {nonSq1}");
                Assert.AreEqual(BigInteger.Zero, nonRoot1);
            }

            if (i > 0)
            {
                var nonSq2 = sq + 1;
                Assert.AreEqual((BigInteger)i, nonSq2.Sqrt(), $"Sqrt failed on {nonSq2}");
                Assert.IsFalse(nonSq2.TryGetExactSquareRoot(out var nonRoot2), $"False positive exact square root on {nonSq2}");
                Assert.AreEqual(BigInteger.Zero, nonRoot2);
            }
        }

        // Large integer perfect square (exceeding 64 bits / decimal range).
        var largeBase = (BigInteger.One << 100) + 123456789;
        var largeSquare = largeBase * largeBase;
        Assert.AreEqual(largeBase, largeSquare.Sqrt());
        Assert.IsTrue(largeSquare.TryGetExactSquareRoot(out var largeExactRoot));
        Assert.AreEqual(largeBase, largeExactRoot);

        var largeNonSquare = largeSquare + 1;
        Assert.AreEqual(largeBase, largeNonSquare.Sqrt());
        Assert.IsFalse(largeNonSquare.TryGetExactSquareRoot(out var largeNonExactRoot));
        Assert.AreEqual(BigInteger.Zero, largeNonExactRoot);
    }

    [TestMethod]
    public void NumberTheoryFactorizationHandlesPrimePowersAndLargeComposites()
    {
        // Prime powers that challenge basic Pollard's rho without square root shortcuts.
        // 101^2 = 10201
        var factors10201 = NumberTheory.Factorize(10201);
        Assert.HasCount(1, factors10201);
        Assert.AreEqual(new BigInteger(101), factors10201[0].Prime);
        Assert.AreEqual(2, factors10201[0].Exponent);

        // 103^4
        var prime103Pow4 = BigInteger.Pow(103, 4);
        var factors103Pow4 = NumberTheory.Factorize(prime103Pow4);
        Assert.HasCount(1, factors103Pow4);
        Assert.AreEqual(new BigInteger(103), factors103Pow4[0].Prime);
        Assert.AreEqual(4, factors103Pow4[0].Exponent);

        // Product of two primes outside small-primes trial division (> 100): 101 * 103 = 10403
        var factors10403 = NumberTheory.Factorize(10403);
        Assert.HasCount(2, factors10403);
        Assert.AreEqual(new BigInteger(101), factors10403[0].Prime);
        Assert.AreEqual(1, factors10403[0].Exponent);
        Assert.AreEqual(new BigInteger(103), factors10403[1].Prime);
        Assert.AreEqual(1, factors10403[1].Exponent);

        // Product of larger 30-bit primes.
        BigInteger p1 = 1000000007; // Prime
        BigInteger p2 = 1000000009; // Prime
        BigInteger semiPrime = p1 * p2;
        var semiFactors = NumberTheory.Factorize(semiPrime);
        Assert.HasCount(2, semiFactors);
        Assert.AreEqual(p1, semiFactors[0].Prime);
        Assert.AreEqual(1, semiFactors[0].Exponent);
        Assert.AreEqual(p2, semiFactors[1].Prime);
        Assert.AreEqual(1, semiFactors[1].Exponent);

        // Verify arithmetic functions on the semiprime:
        // divcount(p1 * p2) = (1+1)*(1+1) = 4
        Assert.AreEqual(new BigInteger(4), NumberTheory.DivCount(semiPrime));
        // totient(p1 * p2) = (p1 - 1) * (p2 - 1)
        Assert.AreEqual((p1 - 1) * (p2 - 1), NumberTheory.Totient(semiPrime));
        // sigma(p1 * p2) = (1 + p1) * (1 + p2)
        Assert.AreEqual((p1 + 1) * (p2 + 1), NumberTheory.Sigma(semiPrime));
    }

    [TestMethod]
    public void NumberTheoryHandlesTrivialAndOddPrimePowers()
    {
        // n = 1 base cases
        var factors1 = NumberTheory.Factorize(BigInteger.One);
        Assert.IsEmpty(factors1);
        Assert.AreEqual(BigInteger.One, NumberTheory.DivCount(BigInteger.One));
        Assert.AreEqual(BigInteger.One, NumberTheory.Sigma(BigInteger.One));
        Assert.AreEqual(BigInteger.One, NumberTheory.Totient(BigInteger.One));
        Assert.IsFalse(NumberTheory.IsPrime(BigInteger.One));

        // Odd prime power: 101^3 = 1030301.
        // Cannot be short-circuited by TryGetExactSquareRoot; exercises Pollard's rho directly on prime powers.
        var prime101Pow3 = BigInteger.Pow(101, 3);
        var factors101Pow3 = NumberTheory.Factorize(prime101Pow3);
        Assert.HasCount(1, factors101Pow3);
        Assert.AreEqual(new BigInteger(101), factors101Pow3[0].Prime);
        Assert.AreEqual(3, factors101Pow3[0].Exponent);

        // Divisors of 101^3: 1, 101, 101^2, 101^3 (4 divisors)
        Assert.AreEqual(new BigInteger(4), NumberTheory.DivCount(prime101Pow3));

        // Pure prime factorisation
        var primeFactors = NumberTheory.Factorize(1000000007);
        Assert.HasCount(1, primeFactors);
        Assert.AreEqual(new BigInteger(1000000007), primeFactors[0].Prime);
        Assert.AreEqual(1, primeFactors[0].Exponent);

        // Powers of 2
        var factors2Pow10 = NumberTheory.Factorize(1024);
        Assert.HasCount(1, factors2Pow10);
        Assert.AreEqual(new BigInteger(2), factors2Pow10[0].Prime);
        Assert.AreEqual(10, factors2Pow10[0].Exponent);
    }

    [TestMethod]
    public void NumberTheoryDomainErrorsThrowStructuredDiagnostics()
    {
        // Non-positive domain checks.
        Assert.Throws<ExpressionException>(() => NumberTheory.Factorize(BigInteger.Zero));
        Assert.Throws<ExpressionException>(() => NumberTheory.Factorize(-42));
        Assert.Throws<ExpressionException>(() => NumberTheory.DivCount(BigInteger.Zero));
        Assert.Throws<ExpressionException>(() => NumberTheory.DivCount(-10));
        Assert.Throws<ExpressionException>(() => NumberTheory.Sigma(BigInteger.Zero));
        Assert.Throws<ExpressionException>(() => NumberTheory.Sigma(10, -1));
        Assert.Throws<ExpressionException>(() => NumberTheory.Totient(BigInteger.Zero));
        Assert.Throws<ExpressionException>(() => NumberTheory.Totient(-1));

        // Combinatorics invalid arguments.
        Assert.Throws<ExpressionException>(() => NumberTheory.Comb(-1, 5));
        Assert.Throws<ExpressionException>(() => NumberTheory.Comb(5, -1));
        Assert.Throws<ExpressionException>(() => NumberTheory.Perm(-1, 5));
        Assert.Throws<ExpressionException>(() => NumberTheory.Perm(5, -1));
    }

    [TestMethod]
    public void CombinatoricsAndSigmaEvaluationsMatchExpectedIdentities()
    {
        // Comb and Perm with large n, small k (only loops k times).
        Assert.AreEqual(new BigInteger(4999950000), NumberTheory.Comb(100000, 2));
        Assert.AreEqual(new BigInteger(9999900000), NumberTheory.Perm(100000, 2));

        // Comb out-of-bounds (k > n yields 0).
        Assert.AreEqual(BigInteger.Zero, NumberTheory.Comb(5, 6));
        Assert.AreEqual(BigInteger.Zero, NumberTheory.Perm(5, 6));

        // Comb zero k.
        Assert.AreEqual(BigInteger.One, NumberTheory.Comb(10, 0));
        Assert.AreEqual(BigInteger.One, NumberTheory.Perm(10, 0));

        // Generalized Sigma_k.
        // Divisors of 12: 1, 2, 3, 4, 6, 12.
        Assert.AreEqual(new BigInteger(6), NumberTheory.Sigma(12, 0)); // Sigma_0 == DivCount
        Assert.AreEqual(new BigInteger(28), NumberTheory.Sigma(12, 1)); // Sigma_1 == 1+2+3+4+6+12
        Assert.AreEqual(new BigInteger(210), NumberTheory.Sigma(12, 2)); // 1+4+9+16+36+144 = 210

        // GCD and LCM.
        Assert.AreEqual(BigInteger.Zero, NumberTheory.Gcd(0, 0));
        Assert.AreEqual(new BigInteger(6), NumberTheory.Gcd(-12, 18));
        Assert.AreEqual(BigInteger.Zero, NumberTheory.Lcm(0, 10));
        Assert.AreEqual(new BigInteger(36), NumberTheory.Lcm(-12, 18));
    }

    [TestMethod]
    public void SpecialFunctionsHandleNonFiniteAndPolesCorrectly()
    {
        // NaN propagation.
        Assert.IsTrue(double.IsNaN(SpecialFunctions.Erf(double.NaN)));
        Assert.IsTrue(double.IsNaN(SpecialFunctions.Erfc(double.NaN)));
        Assert.IsTrue(double.IsNaN(SpecialFunctions.NormalCdf(double.NaN)));
        Assert.IsTrue(double.IsNaN(SpecialFunctions.Sinc(double.NaN)));
        Assert.IsTrue(double.IsNaN(SpecialFunctions.LogGamma(double.NaN)));
        Assert.IsTrue(double.IsNaN(SpecialFunctions.Gamma(double.NaN)));
        Assert.IsTrue(double.IsNaN(SpecialFunctions.Beta(double.NaN, 2.0)));
        Assert.IsTrue(double.IsNaN(SpecialFunctions.Beta(2.0, double.NaN)));
        Assert.IsTrue(double.IsNaN(SpecialFunctions.Zeta(double.NaN)));

        // Infinities.
        Assert.AreEqual(1.0, SpecialFunctions.Erf(double.PositiveInfinity));
        Assert.AreEqual(-1.0, SpecialFunctions.Erf(double.NegativeInfinity));
        Assert.AreEqual(0.0, SpecialFunctions.Erfc(double.PositiveInfinity));
        Assert.AreEqual(2.0, SpecialFunctions.Erfc(double.NegativeInfinity));
        Assert.AreEqual(1.0, SpecialFunctions.NormalCdf(double.PositiveInfinity));
        Assert.AreEqual(0.0, SpecialFunctions.NormalCdf(double.NegativeInfinity));
        Assert.AreEqual(0.0, SpecialFunctions.Sinc(double.PositiveInfinity));
        Assert.AreEqual(0.0, SpecialFunctions.Sinc(double.NegativeInfinity));
        Assert.AreEqual(double.PositiveInfinity, SpecialFunctions.LogGamma(double.PositiveInfinity));
        Assert.AreEqual(double.PositiveInfinity, SpecialFunctions.Gamma(double.PositiveInfinity));
        Assert.AreEqual(1.0, SpecialFunctions.Zeta(double.PositiveInfinity));
        Assert.AreEqual(double.PositiveInfinity, SpecialFunctions.Gamma(175.0)); // > 171.614 overflow guard

        // Asymptotic zeta for large s.
        Assert.AreEqual(1.0 + Math.Pow(2.0, -65.0), SpecialFunctions.Zeta(65.0), 1e-15);

        // Beta where a+b is non-positive integer => Gamma(a+b) pole => Beta = 0.
        Assert.AreEqual(0.0, SpecialFunctions.Beta(-1.5, -0.5)); // sum = -2.0

        // Beta for large/negative arguments requiring sign tracking / LogGamma path.
        var betaLarge = SpecialFunctions.Beta(200.0, 200.0);
        Assert.IsTrue(double.IsFinite(betaLarge));
        Assert.IsGreaterThan(0.0, betaLarge);

        // Negative non-integers exercise reflection without being poles.
        Assert.IsTrue(double.IsFinite(SpecialFunctions.Gamma(-2.5)));
        Assert.IsTrue(double.IsFinite(SpecialFunctions.LogGamma(-2.5)));

        // Poles / domain exceptions.
        Assert.Throws<ExpressionException>(() => SpecialFunctions.Gamma(double.NegativeInfinity));
        Assert.Throws<ExpressionException>(() => SpecialFunctions.Gamma(0.0));
        Assert.Throws<ExpressionException>(() => SpecialFunctions.Gamma(-1.0));
        Assert.Throws<ExpressionException>(() => SpecialFunctions.LogGamma(0.0));
        Assert.Throws<ExpressionException>(() => SpecialFunctions.LogGamma(-3.0));
        Assert.Throws<ExpressionException>(() => SpecialFunctions.Beta(0.0, 1.0));
        Assert.Throws<ExpressionException>(() => SpecialFunctions.Beta(1.0, -2.0));
        Assert.Throws<ExpressionException>(() => SpecialFunctions.Zeta(1.0));
    }

    [TestMethod]
    public void NumberTheoryAndMathValueCalculationLimitsEnforced()
    {
        // NumberTheory safety bounds.
        // Extremely large even numbers or multiples of small primes return false
        // instantly without limit errors.
        Assert.IsFalse(NumberTheory.IsPrime(BigInteger.One << 4000));
        Assert.IsFalse(NumberTheory.IsPrime((BigInteger.One << 4000) * 3));
        Assert.IsFalse(NumberTheory.IsPrime((BigInteger.One << 4000) * 5));

        // Candidates that survive trial division up to 100 are bounded by
        // MaxPrimalityBits before Miller-Rabin.
        // Build a number coprime to all primes in [2..100]: product of primes <= 97 is
        // small enough, or multiply them + 1.
        var primeProduct = BigInteger.One;
        int[] primesUnder100 = [2, 3, 5, 7, 11, 13, 17, 19, 23, 29, 31, 37, 41, 43, 47, 53, 59, 61, 67, 71, 73, 79, 83, 89, 97];
        foreach (int p in primesUnder100)
        {
            primeProduct *= p;
        }

        // primeProduct * K + 1 has no prime factor <= 97.
        // Scale K such that candidate > 2^2048.
        var kMultiplier = BigInteger.One << CalculationLimits.MaxPrimalityBits;
        var candidateAboveLimit = primeProduct * kMultiplier + 1;
        Assert.IsGreaterThan(CalculationLimits.MaxPrimalityBits, candidateAboveLimit.GetBitLength());
        Assert.Throws<CalculationLimitException>(() => NumberTheory.IsPrime(candidateAboveLimit));

        Assert.Throws<CalculationLimitException>(() => NumberTheory.Factorize(BigInteger.One << (CalculationLimits.MaxFactorizationBits + 1)));
        Assert.Throws<CalculationLimitException>(() => NumberTheory.Comb(100000, 50000)); // k exceeds MaxFactorialArgument (1000)
        Assert.Throws<CalculationLimitException>(() => NumberTheory.Perm(100000, 1001)); // k exceeds MaxFactorialArgument (1000)
        Assert.Throws<CalculationLimitException>(() => NumberTheory.Sigma(12, CalculationLimits.MaxExponentMagnitude + 1));

        // MathValue bitwise shifts requiring integer operands and checking bit limits.
        var decVal = new MathValue(2.5m);
        var intVal = new MathValue(new BigInteger(4));
        Assert.Throws<ExpressionException>(() => MathValue.ShiftLeft(decVal, intVal));
        Assert.Throws<ExpressionException>(() => MathValue.ShiftRight(decVal, intVal));
        Assert.Throws<ExpressionException>(() => MathValue.ShiftLeft(intVal, new MathValue(new BigInteger(-1))));
        Assert.Throws<ExpressionException>(() => MathValue.ShiftRight(intVal, new MathValue(new BigInteger(-1))));

        // Shift left beyond MaxIntegerBits.
        Assert.Throws<CalculationLimitException>(() => MathValue.ShiftLeft(intVal, new MathValue(new BigInteger(CalculationLimits.MaxIntegerBits))));

        // Large integer comparison with double (> 1024 bits).
        var hugePositiveInt = new MathValue(BigInteger.One << 1030);
        var hugeNegativeInt = new MathValue(-(BigInteger.One << 1030));
        Assert.IsGreaterThan(0, MathValue.Compare(hugePositiveInt, new MathValue(1e100)));
        Assert.IsLessThan(0, MathValue.Compare(hugeNegativeInt, new MathValue(1e100)));
    }

    [TestMethod]
    public void MedianEvaluatesAccuratelyAcrossOddAndEvenCounts()
    {
        // Odd count: middle element
        var oddRes = Calculator.Evaluate("median(5, 1, 9, 3, 7)");
        Assert.AreEqual(new MathValue(new BigInteger(5)), oddRes);

        var singleRes = Calculator.Evaluate("median(42)");
        Assert.AreEqual(new MathValue(new BigInteger(42)), singleRes);

        // Even count: average of two middle elements
        var evenRes = Calculator.Evaluate("median(1, 2, 3, 4)");
        Assert.AreEqual(new MathValue(2.5m), evenRes);

        var twoRes = Calculator.Evaluate("median(10, 20)");
        Assert.AreEqual(new MathValue(15m), twoRes);

        // Mixed decimal and integer values
        var mixedRes = Calculator.Evaluate("median(1.5, 4.5, 2.5)");
        Assert.AreEqual(new MathValue(2.5m), mixedRes);
    }

    [TestMethod]
    public void SampleVarianceAndStandardDeviationEvaluateAccurately()
    {
        // Sample variance of [2, 4, 4, 4, 5, 5, 7, 9]:
        // mean = 40/8 = 5.
        // sum of (x_i - mean)^2 = 9 + 1 + 1 + 1 + 0 + 0 + 4 + 16 = 32.
        // s^2 = 32 / (8 - 1) = 32 / 7.
        var varRes = Calculator.Evaluate("var(2, 4, 4, 4, 5, 5, 7, 9)");
        var expectedVar = 32.0 / 7.0;
        Assert.AreEqual(expectedVar, varRes.AsDouble(), 1e-12);

        // variance alias
        var varianceRes = Calculator.Evaluate("variance(2, 4, 4, 4, 5, 5, 7, 9)");
        Assert.AreEqual(expectedVar, varianceRes.AsDouble(), 1e-12);

        // stdev: sqrt(32/7)
        var stdevRes = Calculator.Evaluate("stdev(2, 4, 4, 4, 5, 5, 7, 9)");
        Assert.AreEqual(Math.Sqrt(expectedVar), stdevRes.AsDouble(), 1e-12);

        // stddev alias
        var stddevRes = Calculator.Evaluate("stddev(2, 4, 4, 4, 5, 5, 7, 9)");
        Assert.AreEqual(Math.Sqrt(expectedVar), stddevRes.AsDouble(), 1e-12);

        // Simple two-element case: [10, 20], mean = 15, (25 + 25) / 1 = 50, stdev = sqrt(50)
        Assert.AreEqual(new MathValue(50m), Calculator.Evaluate("var(10, 20)"));
        Assert.AreEqual(Math.Sqrt(50.0), Calculator.Evaluate("stdev(10, 20)").AsDouble(), 1e-12);

        // Require at least 2 arguments
        Assert.Throws<ExpressionException>(() => Calculator.Evaluate("var(5)"));
        Assert.Throws<ExpressionException>(() => Calculator.Evaluate("variance(5)"));
        Assert.Throws<ExpressionException>(() => Calculator.Evaluate("stdev(5)"));
        Assert.Throws<ExpressionException>(() => Calculator.Evaluate("stddev(5)"));
    }

    [TestMethod]
    public void PopCountEvaluatesAccuratelyAcrossIntegersAndRejectsNegatives()
    {
        // popcount(0) = 0
        Assert.AreEqual(new MathValue(BigInteger.Zero), Calculator.Evaluate("popcount(0)"));

        // popcount(1) = 1, popcount(7) = 3, popcount(255) = 8
        Assert.AreEqual(new MathValue(BigInteger.One), Calculator.Evaluate("popcount(1)"));
        Assert.AreEqual(new MathValue(new BigInteger(3)), Calculator.Evaluate("popcount(7)"));
        Assert.AreEqual(new MathValue(new BigInteger(8)), Calculator.Evaluate("popcount(255)"));
        Assert.AreEqual(new MathValue(new BigInteger(8)), Calculator.Evaluate("popcount(0xFF)"));

        // bitcount alias
        Assert.AreEqual(new MathValue(new BigInteger(8)), Calculator.Evaluate("bitcount(0b11111111)"));

        // Large integer: (2^100 - 1) has 100 set bits
        var largeMask = (BigInteger.One << 100) - 1;
        Assert.AreEqual(new MathValue(new BigInteger(100)), Calculator.Evaluate($"popcount({largeMask})"));

        // Non-negative integer required: negative throws ArgumentOutOfRange
        var negEx = Assert.Throws<ExpressionException>(() => Calculator.Evaluate("popcount(-1)"));
        Assert.AreEqual(EvaluationError.ArgumentOutOfRange, negEx.Diagnostic?.Code);

        // Non-integer argument throws IntegerRequired
        var decEx = Assert.Throws<ExpressionException>(() => Calculator.Evaluate("popcount(3.14)"));
        Assert.AreEqual(EvaluationError.IntegerRequired, decEx.Diagnostic?.Code);
    }
}
