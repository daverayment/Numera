using System.Globalization;
using System.Numerics;

namespace Numera.Tests;

[TestClass]
public class LanguageTests
{
    [TestMethod]
    [DataRow("8+3*7", 29)]
    [DataRow("[8+3]*7", 77)]
    [DataRow("3^2^3", 6561)]
    [DataRow("-3^2", -9)]
    [DataRow("(-3)^2", 9)]
    [DataRow("20/5/2", 2)]
    [DataRow("MAX(4,min(9,6,7),5)", 6)]
    public void PrecedenceAndGroupingHaveDefinedIntegerResults(string input, int expected)
    {
        Assert.AreEqual(new BigInteger(expected), Calculator.Evaluate(input).IntVal);
    }

    [TestMethod]
    [DataRow("1 || 9/0", 1)]
    [DataRow("0 && factorial(2000)", 0)]
    [DataRow("0 || 3 == 3 && 1", 1)]
    [DataRow("4 ~= 5", 1)]
    [DataRow("1 || sqrt(-4)", 1)]
    public void LogicalOperatorsShortCircuitEvaluation(string input, int expected)
    {
        Assert.AreEqual(expected, Calculator.Evaluate(input).AsDouble());
    }

    [TestMethod]
    [DataRow("8+", EvaluationError.InvalidSyntax)]
    [DataRow("[8+2)", EvaluationError.InvalidSyntax)]
    [DataRow("3..1", EvaluationError.InvalidSyntax)]
    [DataRow("1 || min()", EvaluationError.InvalidArgumentCount)]
    [DataRow("0 && pow(4)", EvaluationError.InvalidArgumentCount)]
    [DataRow("1 || absent(7)", EvaluationError.UnknownFunction)]
    [DataRow("0 && absent", EvaluationError.UnknownConstant)]
    [DataRow("min(7,)", EvaluationError.InvalidSyntax)]
    [DataRow("9/0", EvaluationError.DivisionByZero)]
    [DataRow("sqrt(-1)", EvaluationError.UndefinedMathResult)]
    [DataRow("factorial(1.2)", EvaluationError.IntegerRequired)]
    [DataRow("factorial(-5)", EvaluationError.ArgumentOutOfRange)]
    [DataRow("1 << -1", EvaluationError.ArgumentOutOfRange)]
    [DataRow("1.5 << 2", EvaluationError.IntegerRequired)]
    public void ExpectedFailuresHaveStableDiagnosticCodes(string input, EvaluationError code)
    {
        // Skipped operands still require valid syntax, names, and arity. Assert categories
        // rather than wording so consumers can rely on the structured diagnostic contract.
        Assert.IsFalse(Calculator.TryEvaluate(input, out _, out var diagnostic));
        Assert.IsNotNull(diagnostic);
        Assert.AreEqual(code, diagnostic.Code);
    }

    [TestMethod]
    public void SyntaxDiagnosticsIdentifySourceLocations()
    {
        var exception = Assert.Throws<ExpressionException>(() => Calculator.Evaluate("5+unknown(2)"));
        Assert.AreEqual(EvaluationError.UnknownFunction, exception.Diagnostic.Code);
        Assert.AreEqual(2, exception.Diagnostic.Position);
        Assert.AreEqual(7, exception.Diagnostic.Length);
        Assert.IsFalse(Calculator.TryEvaluate("5+", out _, out var diagnostic));
        Assert.AreEqual(2, diagnostic!.Position);
        Assert.AreEqual(0, diagnostic.Length);
    }

    [TestMethod]
    public void SuccessfulTryEvaluationReturnsValueAndNoDiagnostic()
    {
        Assert.IsTrue(Calculator.TryEvaluate("11*13", out var value, out var diagnostic));
        Assert.AreEqual(new BigInteger(143), value.IntVal);
        Assert.IsNull(diagnostic);
    }

    [TestMethod]
    public void InvalidApiArgumentsAreNotReportedAsExpressionErrors()
    {
        Assert.Throws<ArgumentNullException>(() => Calculator.TryEvaluate(null!, out _, out _));
        Assert.Throws<ArgumentOutOfRangeException>(() => Calculator.Evaluate("1", new EvaluationOptions { MaxNestingDepth = 0 }));
    }

    [TestMethod]
    public void FunctionMetadataDescribesVariadicAndFixedArities()
    {
        Assert.IsTrue(Functions.TryGetSignature("mAx", out var maximum));
        Assert.AreEqual(1, maximum.MinimumArguments);
        Assert.IsNull(maximum.MaximumArguments);
        Assert.IsTrue(maximum.AllowsMultipleArguments);
        Assert.IsTrue(Functions.TryGetSignature("sqrt", out var squareRoot));
        Assert.AreEqual(1, squareRoot.MaximumArguments);
        Assert.IsFalse(squareRoot.AllowsMultipleArguments);
        Assert.IsFalse(Functions.TryGetSignature("notafunction", out _));
    }

    [TestMethod]
    public void InvariantSyntaxDoesNotDependOnCurrentCulture()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            // A comma-decimal culture must not change the core grammar: dots remain
            // decimal points and commas remain function argument separators.
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
            Assert.AreEqual(1.75m, Calculator.Evaluate("min(2.5,1.75,3)").DecVal);
            Assert.IsFalse(Calculator.TryEvaluate("1,75", out _, out _));
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [TestMethod]
    public void RandomFunctionsRespectTheirRange()
    {
        for (var i = 0; i < 50; i++)
        {
            var fraction = Calculator.Evaluate("rand()").DblVal;
            var integer = Calculator.Evaluate("randi(17)").IntVal;
            Assert.IsTrue(fraction >= 0 && fraction < 1);
            Assert.IsTrue(integer >= 0 && integer < 17);
        }
    }

    [TestMethod]
    [DataRow("PI", 3.1415926535897931)]
    [DataRow("Pi", 3.1415926535897931)]
    [DataRow("pi", 3.1415926535897931)]
    [DataRow("π", 3.1415926535897931)]
    [DataRow("E", 2.7182818284590451)]
    [DataRow("e", 2.7182818284590451)]
    public void ConstantsAreCaseInsensitive(string constantName, double expectedValue)
    {
        Assert.AreEqual(expectedValue, Calculator.Evaluate(constantName).DblVal, 1e-15);
    }

    [TestMethod]
    public void BracketPairingsMustMatch()
    {
        // Matching parentheses and brackets are valid
        Assert.AreEqual(new BigInteger(6), Calculator.Evaluate("(1+2)*2").IntVal);
        Assert.AreEqual(new BigInteger(6), Calculator.Evaluate("[1+2]*2").IntVal);

        // Mismatched bracket types fail
        Assert.IsFalse(Calculator.TryEvaluate("(1+2]", out _, out var diag1));
        Assert.AreEqual(EvaluationError.InvalidSyntax, diag1!.Code);

        Assert.IsFalse(Calculator.TryEvaluate("[1+2)", out _, out var diag2));
        Assert.AreEqual(EvaluationError.InvalidSyntax, diag2!.Code);
    }

    [TestMethod]
    public void EqualityChainsFoldLeft()
    {
        // 2 == 2 is 1. Then 1 == 1 is 1.
        Assert.AreEqual(1m, Calculator.Evaluate("2 == 2 == 1").DecVal);

        // 2 == 3 is 0. Then 0 == 0 is 1.
        Assert.AreEqual(1m, Calculator.Evaluate("2 == 3 == 0").DecVal);

        // 2 == 2 is 1. Then 1 == 3 is 0.
        Assert.AreEqual(0m, Calculator.Evaluate("2 == 2 == 3").DecVal);
    }

    [TestMethod]
    public void UnaryChainsAndExponentiationPrecedence()
    {
        // - - 5 is 5
        Assert.AreEqual(new BigInteger(5), Calculator.Evaluate("- - 5").IntVal);

        // + - + 5 is -5
        Assert.AreEqual(new BigInteger(-5), Calculator.Evaluate("+ - + 5").IntVal);

        // Powers bind tighter than leading sign: -2^4 is -(2^4) = -16
        Assert.AreEqual(new BigInteger(-16), Calculator.Evaluate("-2^4").IntVal);

        // Explicit grouping: (-2)^4 is 16
        Assert.AreEqual(new BigInteger(16), Calculator.Evaluate("(-2)^4").IntVal);

        // Negative exponent in power: 2^-1 is 0.5
        Assert.AreEqual(0.5, Calculator.Evaluate("2^-1").DblVal, 1e-15);
    }

    [TestMethod]
    public void AllEvaluationExceptionsInheritFromNumeraException()
    {
        // Syntax failure.
        var syntaxEx = Assert.Throws<ExpressionException>(() => Calculator.Evaluate("1 +"));
        Assert.IsInstanceOfType<NumeraException>(syntaxEx);
        Assert.AreEqual(EvaluationError.InvalidSyntax, syntaxEx.Diagnostic.Code);

        // Limit failure.
        var limitEx = Assert.Throws<CalculationLimitException>(() => Calculator.Evaluate("2^10001"));
        Assert.IsInstanceOfType<NumeraException>(limitEx);
        Assert.AreEqual(EvaluationError.LimitExceeded, limitEx.Diagnostic.Code);

        // Division by zero in expression.
        var divEx = Assert.Throws<ExpressionException>(() => Calculator.Evaluate("1 / 0"));
        Assert.IsInstanceOfType<NumeraException>(divEx);
        Assert.AreEqual(EvaluationError.DivisionByZero, divEx.Diagnostic.Code);
        Assert.IsInstanceOfType<DivideByZeroException>(divEx.InnerException);

        // Consumers can catch all engine failures with a single catch (NumeraException).
        NumeraException? caught = null;
        try
        {
            Calculator.Evaluate("unknownFunc(1)");
        }
        catch (NumeraException ex)
        {
            caught = ex;
        }

        Assert.IsNotNull(caught);
        Assert.AreEqual(EvaluationError.UnknownFunction, caught.Diagnostic.Code);
    }
}
