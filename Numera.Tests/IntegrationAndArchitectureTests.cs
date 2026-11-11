using System.Globalization;
using System.Numerics;

namespace Numera.Tests;

[TestClass]
public sealed class IntegrationAndArchitectureTests
{
    [TestMethod]
    public void RationalReduction_PreservesExactIdentity_AcrossVariableAndAnsChains()
    {
        var engine = new CalculatorEngine();

        // 1. Variable on left: a = 1 / 3, then a * 3
        var r1 = engine.Evaluate("a = 1 / 3");
        var r2 = engine.Evaluate("a * 3", r1.Context);
        Assert.AreEqual(1, r2.IntegerValue);

        // 2. Variable on right: 3 * a
        var r3 = engine.Evaluate("3 * a", r1.Context);
        Assert.AreEqual(1, r3.IntegerValue);

        // 3. Reciprocal: 1 / a == 3
        var r4 = engine.Evaluate("1 / a", r1.Context);
        Assert.AreEqual(3, r4.IntegerValue);

        // 4. Using 'ans' from expression 1 / 3
        var r5 = engine.Evaluate("1 / 3");
        var r6 = engine.Evaluate("ans * 3", r5.Context);
        Assert.AreEqual(1, r6.IntegerValue);

        // 5. Using 'ans' from assignment a = 1 / 3
        var r7 = engine.Evaluate("ans * 3", r1.Context);
        Assert.AreEqual(1, r7.IntegerValue);

        // 6. Right-hand side parenthesized binary subtrees: 3 * (1 / 3) and 6 / (2 / 3)
        Assert.AreEqual(1, Calculator.Evaluate("3 * (1 / 3)").IntegerValue);
        Assert.AreEqual(9, Calculator.Evaluate("6 / (2 / 3)").IntegerValue);
    }

    [TestMethod]
    public void EvaluationResult_ErgonomicsAndOperators_WorkWithPrimitives()
    {
        var result = Calculator.Evaluate("71 / 9");

        // Multiplication with primitive int.
        var multiplied = result * 9;
        Assert.AreEqual(71, multiplied.IntegerValue);

        // Arithmetic with primitives and EvaluationResult.
        Assert.AreEqual(80, (result * 9 + 9).IntegerValue);
        Assert.AreEqual(71, (9 * result).IntegerValue);

        // Comparisons.
        Assert.IsTrue(result > 7);
        Assert.IsTrue(result < 8);
        Assert.IsFalse(result == (71m / 9m));
        Assert.IsTrue(result == MathValue.FromFraction(71, 9));

        // Explicit casts.
        double d = (double)result;
        Assert.AreEqual(71.0 / 9.0, d, 1e-12);

        int x = (int)Calculator.Evaluate("42");
        Assert.AreEqual(42, x);
    }

    [TestMethod]
    public void StatisticalFunctions_PropagateNaNCorrectly()
    {
        var nanMedian = Calculator.Evaluate("median(1, 0 * exp(1000), 3)");
        Assert.IsTrue(nanMedian.IsNaN);

        var nanMax = Calculator.Evaluate("max(1, 0 * exp(1000), 3)");
        Assert.IsTrue(nanMax.IsNaN);

        var nanMin = Calculator.Evaluate("min(1, 0 * exp(1000), 3)");
        Assert.IsTrue(nanMin.IsNaN);
    }

    [TestMethod]
    public void RationalReduction_PresevesExactIdentity_ForThirdsAndNinths()
    {
        var engine = new CalculatorEngine();
        var context = EvaluationContext.Empty;

        // r1 = 71 / 9, followed by r1 * 9 => exact 71 (Integer).
        var r1 = engine.Evaluate("r1 = 71 / 9", context);
        var r1Mul = engine.Evaluate("r1 * 9", r1.Context);
        Assert.AreEqual(MathValue.DataType.Integer, r1Mul.Value.Kind);
        Assert.AreEqual("71", r1Mul.PrimaryDisplay);

        // r2 = 1 / 3, followed by r2 * 3 => exact 1 (Integer).
        var r2 = engine.Evaluate("r2 = 1 / 3", context);
        var r2Mul = engine.Evaluate("r2 * 3", r2.Context);
        Assert.AreEqual(MathValue.DataType.Integer, r2Mul.Value.Kind);
        Assert.AreEqual(BigInteger.One, r2Mul.Value.IntegerValue);
        Assert.AreEqual("1", r2Mul.PrimaryDisplay);

        // Commutative check: 3 * r2 => exact 1 (Integer).
        var r2Comm = engine.Evaluate("3 * r2", r2.Context);
        Assert.AreEqual(MathValue.DataType.Integer, r2Comm.Value.Kind);
        Assert.AreEqual(BigInteger.One, r2Comm.Value.IntegerValue);
        Assert.AreEqual("1", r2Comm.PrimaryDisplay);

        // Reciprocal check: 1 / r2 => exact 3 (Integer).
        var r2Recip = engine.Evaluate("1 / r2", r2.Context);
        Assert.AreEqual(MathValue.DataType.Integer, r2Recip.Value.Kind);
        Assert.AreEqual(new BigInteger(3), r2Recip.Value.IntegerValue);
        Assert.AreEqual("3", r2Recip.PrimaryDisplay);

        // Numerator unrolling test: a = 2 / 3.
        var a = engine.Evaluate("a = 2 / 3", context);
        var aMul = engine.Evaluate("6 * a", a.Context);
        Assert.AreEqual(MathValue.DataType.Integer, aMul.Value.Kind);
        Assert.AreEqual(new BigInteger(4), aMul.Value.IntegerValue);
        Assert.AreEqual("4", aMul.PrimaryDisplay);

        var aDiv = engine.Evaluate("6 / a", a.Context); // 6 / (2/3) = 6 * (3/2) = 9
        Assert.AreEqual(MathValue.DataType.Integer, aDiv.Value.Kind);
        Assert.AreEqual(new BigInteger(9), aDiv.Value.IntegerValue);
        Assert.AreEqual("9", aDiv.PrimaryDisplay);
    }

    [TestMethod]
    public void RationalReduction_SelfReferentialUpdate_DoesNotCauseCycleOrOverflow()
    {
        var engine = new CalculatorEngine();
        // var context = EvaluationContext.Empty;

        var r1 = engine.Evaluate("a = 1 / 3");

        // Round 2: a = a * 2 (updates a's AST to reference prior a)
        var r2 = engine.Evaluate("a = a * 2", r1.Context);

        // Round 3: a * 3 => (1/3 * 2) * 3 = 2
        var r3 = engine.Evaluate("a * 3", r2.Context);

        Assert.AreEqual(MathValue.DataType.Integer, r3.Value.Kind);
        Assert.AreEqual("2", r3.PrimaryDisplay);
        Assert.AreEqual(new BigInteger(2), r3.Value.IntegerValue);
        Assert.AreEqual(CalculationExactness.Exact, r3.Exactness);
    }

    [TestMethod]
    public void DecimalOverflow_WholeNumbers_PromoteToBigInteger()
    {
        // decimal.MaxValue is 79228162514264337593543950335.
        string maxStr = decimal.MaxValue.ToString(CultureInfo.InvariantCulture);
        var expectedSum = BigInteger.Multiply(new BigInteger(decimal.MaxValue), 2);

        // Sum of two decimal.MaxValue operands.
        var sumResult = Calculator.Evaluate($"{maxStr} + {maxStr}");
        Assert.AreEqual(MathValue.DataType.Integer, sumResult.Kind);
        Assert.AreEqual(expectedSum, sumResult.IntegerValue);

        // Multiply decimal.MaxValue by 2.0 (whole decimal).
        var mulResult = Calculator.Evaluate($"{maxStr} * 2.0");
        Assert.AreEqual(MathValue.DataType.Integer, mulResult.Kind);
        Assert.AreEqual(expectedSum, mulResult.IntegerValue);
    }

    [TestMethod]
    public void DecimalOverflow_FractionalNumbers_PreserveRational()
    {
        string maxStr = decimal.MaxValue.ToString(CultureInfo.InvariantCulture);

        var fracSum = Calculator.Evaluate($"{maxStr} + 0.5");
        Assert.AreEqual(MathValue.DataType.Rational, fracSum.Kind);
        Assert.AreEqual(new BigInteger(decimal.MaxValue) * 2 + 1, fracSum.Value.Numerator);
        Assert.AreEqual(new BigInteger(2), fracSum.Value.Denominator);
        Assert.AreEqual(CalculationExactness.Exact, fracSum.Exactness);
        Assert.IsTrue(fracSum.IsFinite);
    }

    // EvaluationResult operators and C# ergonomics.

    [TestMethod]
    public void EvaluationResult_Operators_SynthesizeAstAndReduceRationally()
    {
        var r1 = Calculator.Evaluate("71 / 9");

        // r1 * 9 in C# code should reduce to exact 71 (Integer).
        EvaluationResult r2 = r1 * 9;
        Assert.AreEqual(MathValue.DataType.Integer, r2.Kind);
        Assert.AreEqual(new BigInteger(71), r2.IntegerValue);

        // Chained C# operations.
        EvaluationResult r3 = (Calculator.Evaluate("1 / 3") * 6) / 2;
        Assert.AreEqual(MathValue.DataType.Integer, r3.Kind);
        Assert.AreEqual(BigInteger.One, r3.IntegerValue);

        // Comparison in C#.
        Assert.IsTrue(r1 * 9 == 71);
        Assert.IsTrue(r1 * 9 > 70);

        // Explicit casts.
        double d = (double)r1;
        Assert.AreEqual(71.0 / 9.0, d, 1e-12);

        int i = (int)r2;
        Assert.AreEqual(71, i);
    }

    // CalculatorEngine derivation (WithOptions and With...).

    [TestMethod]
    public void CalculatorEngine_WithOptions_DerivesNewEngineWithoutMutatingOriginal()
    {
        var originalEngine = CalculatorEngine.Default;
        Assert.AreEqual(AngleUnit.Radians, originalEngine.Options.AngleUnit);

        // Derive degrees engine and confirm original unchanged.
        var degreeEngine = originalEngine.WithAngleUnit(AngleUnit.Degrees);
        Assert.AreEqual(AngleUnit.Degrees, degreeEngine.Options.AngleUnit);
        Assert.AreEqual(AngleUnit.Radians, originalEngine.Options.AngleUnit);

        // Exact degree calculation on derived engine.
        Assert.AreEqual(1.0, degreeEngine.Evaluate("sin(90)").DoubleValue, 0.0);
        Assert.AreNotEqual(1.0, originalEngine.Evaluate("sin(90)").DoubleValue);

        // Derive via lambda.
        var customEngine = originalEngine.WithOptions(options => options with { MaxInputLength = 100 });
        Assert.AreEqual(100, customEngine.Options.MaxInputLength);
        Assert.AreEqual(CalculationLimits.MaxInputLength, originalEngine.Options.MaxInputLength);
    }

    // Context-sensitive error diagnostics.

    [TestMethod]
    public void ReservedIdentifierAssignment_ProducesEducationalMessage()
    {
        // Euler's number 'e'.
        var exE = Assert.Throws<ExpressionException>(() => Calculator.Evaluate("e = 5"));
        Assert.AreEqual(EvaluationError.InvalidSyntax, exE.Diagnostic.Code);
        Assert.Contains("Euler's number", exE.Diagnostic.Message);

        // Archimedes' constant 'pi'.
        var exPi = Assert.Throws<ExpressionException>(() => Calculator.Evaluate("pi = 3"));
        Assert.AreEqual(EvaluationError.InvalidSyntax, exPi.Diagnostic.Code);
        Assert.Contains("Archimedes' constant", exPi.Diagnostic.Message);

        // Built-in function 'sin'.
        var exSin = Assert.Throws<ExpressionException>(() => Calculator.Evaluate("sin = 10"));
        Assert.AreEqual(EvaluationError.InvalidSyntax, exSin.Diagnostic.Code);
        Assert.Contains("built-in function", exSin.Diagnostic.Message);
    }

    [TestMethod]
    public void SquareBrackets_GuideUserToParentheses()
    {
        var ex = Assert.Throws<ExpressionException>(() => Calculator.Evaluate("[1 + 2] * 3"));
        Assert.AreEqual(EvaluationError.InvalidSyntax, ex.Diagnostic.Code);
        Assert.Contains("round parentheses '()' for grouping", ex.Diagnostic.Message);
    }

    [TestMethod]
    public void FunctionArity_IncludesSignatureHint()
    {
        // round expects 1 or 2 arguments, received 3.
        var ex = Assert.Throws<ExpressionException>(() => Calculator.Evaluate("round(1, 2, 3)"));
        Assert.AreEqual(EvaluationError.InvalidArgumentCount, ex.Diagnostic.Code);
        Assert.Contains("requires between 1 and 2 arguments", ex.Diagnostic.Message);
        Assert.Contains("received 3", ex.Diagnostic.Message);
        Assert.Contains("round(x, [places])", ex.Diagnostic.Message);
    }

    // VariableCollection and debugging usability

    [TestMethod]
    public void VariableCollection_ToString_OutputsReadableSummary()
    {
        var vars = VariableCollection.CreateRange(
        [
            ("a", new MathValue(10m)),
            ("b", new MathValue(20m))
        ]);

        Assert.AreEqual("a = 10, b = 20", vars.ToString());

        vars = VariableCollection.Empty;
        Assert.AreEqual("(empty)", vars.ToString());
    }

    [TestMethod]
    public void EvaluationResult_AssignmentSummary_And_Formatting()
    {
        var res = Calculator.Evaluate("x = 42");

        // Direct result inspection.
        Assert.IsTrue(res.HasAssignments);
        Assert.AreEqual("x = 42", res.AssignmentSummary);
        Assert.AreEqual("42", res.PrimaryDisplay);

        // Directly format to presentation card.
        var card = res.FormatResult();
        Assert.AreEqual("42", card.PrimaryDisplay);
        Assert.AreEqual("42", card.MachineReadable);

        // Card preserves AssignedVariables and AssingmentSummary.
        Assert.IsNotNull(card.AssignedVariables);
        Assert.AreEqual(1, card.AssignedVariables.Count);
        Assert.AreEqual("x = 42", card.AssignmentSummary);
    }
}
