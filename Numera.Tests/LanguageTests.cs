using System.Globalization;
using System.Numerics;
using Numera.Ast;

namespace Numera.Tests;

[TestClass]
public class LanguageTests
{
    [TestMethod]
    [DataRow("8+3*7", 29)]
    [DataRow("(8+3)*7", 77)]
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
        Assert.Throws<ArgumentNullException>(() => Calculator.TryEvaluate((string)null!, out _, out _));
        Assert.Throws<ArgumentNullException>(() => Calculator.TryEvaluate((ExpressionNode)null!, out _, out _));
        Assert.Throws<ArgumentOutOfRangeException>(() => new CalculatorEngine(new CalculatorOptions { MaxNestingDepth = 0 }));
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
    [DataRow("PI", 3.1415926535897931, "3.1415926535897932384626433833")]
    [DataRow("Pi", 3.1415926535897931, "3.1415926535897932384626433833")]
    [DataRow("pi", 3.1415926535897931, "3.1415926535897932384626433833")]
    [DataRow("π", 3.1415926535897931, "3.1415926535897932384626433833")]
    [DataRow("E", 2.7182818284590451, "2.7182818284590452353602874714")]
    [DataRow("e", 2.7182818284590451, "2.7182818284590452353602874714")]
    public void ConstantsAreCaseInsensitive(string constantName, double expectedDouble, string expectedDecimal)
    {
        var result = Calculator.Evaluate(constantName);
        Assert.AreEqual(MathValue.DataType.Decimal, result.Type);
        Assert.AreEqual(decimal.Parse(expectedDecimal, CultureInfo.InvariantCulture), result.DecVal);
        Assert.AreEqual(expectedDouble, result.AsDouble(), 1e-15);
    }

    [TestMethod]
    public void ParenthesesGroupExpressionsAndSquareBracketsAndSemicolonsAreReserved()
    {
        // Parentheses group as expected.
        Assert.AreEqual(new BigInteger(6), Calculator.Evaluate("(1+2)*2").IntVal);

        // Square brackets are rejected and reserved for future vector/matrix syntax.
        Assert.IsFalse(Calculator.TryEvaluate("[1+2]*2", out _, out var bracketDiag));
        Assert.AreEqual(EvaluationError.InvalidSyntax, bracketDiag!.Code);
        Assert.IsTrue(bracketDiag.Message.Contains("reserved", StringComparison.OrdinalIgnoreCase));

        Assert.IsFalse(Calculator.TryEvaluate("(1+2]", out _, out var mismatchedDiag));
        Assert.AreEqual(EvaluationError.InvalidSyntax, mismatchedDiag!.Code);

        // Semicolons are rejected and reserved for future matrix row separation.
        Assert.IsFalse(Calculator.TryEvaluate("1;2", out _, out var semiDiag));
        Assert.AreEqual(EvaluationError.InvalidSyntax, semiDiag!.Code);
        Assert.IsTrue(semiDiag.Message.Contains("reserved", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void ComparisonChainsFollowMathematicalConjunctiveSemanticsRatherThanProgrammingLanguagePrecedence()
    {
        // In languages like C/C++/C#, `<` binds tighter than `==`, so:
        // `1 < 2 == 1` evaluates to `(1 < 2) == 1` -> `true == 1` -> true (1).
        // Under conjunctive chaining, all comparison operators share equal precedence:
        // `1 < 2 == 1` means `(1 < 2) and (2 == 1)` -> true and false -> false (0).
        Assert.AreEqual(0m, Calculator.Evaluate("1 < 2 == 1").DecVal);

        // Similarly:
        // C# `2 > 1 == 1` -> `(2 > 1) == 1` -> 1
        // Numera: `2 > 1 and 1 == 1` -> 1
        Assert.AreEqual(1m, Calculator.Evaluate("2 > 1 == 1").DecVal);

        // C# `1 < 3 == 2` -> `(1 < 3) == 2` -> `1 == 2` -> 0
        // Numera: `1 < 3 and 3 == 2` -> false -> 0
        Assert.AreEqual(0m, Calculator.Evaluate("1 < 3 == 2").DecVal);

        // Mixed relational & equality chains:
        // `0 < 5 == 5 < 10` -> `0 < 5 and 5 == 5 and 5 < 10` -> 1
        Assert.AreEqual(1m, Calculator.Evaluate("0 < 5 == 5 < 10").DecVal);

        // `0 < 5 == 6 < 10` -> `0 < 5 and 5 == 6 ...` -> 0
        Assert.AreEqual(0m, Calculator.Evaluate("0 < 5 == 6 < 10").DecVal);

        // Under traditional left-fold equality:
        // `2 == 2 == 1` was `(2 == 2) == 1` -> `1 == 1` -> 1.
        // Under conjunctive chaining:
        // `2 == 2 == 1` means `2 == 2 and 2 == 1` -> 0.
        Assert.AreEqual(0m, Calculator.Evaluate("2 == 2 == 1").DecVal);

        // `2 == 3 == 0` was `(2 == 3) == 0` -> `0 == 0` -> 1.
        // Under conjunctive chaining: `2 == 3 and 3 == 0` -> 0.
        Assert.AreEqual(0m, Calculator.Evaluate("2 == 3 == 0").DecVal);

        // `2 == 2 == 2` is 1.
        Assert.AreEqual(1m, Calculator.Evaluate("2 == 2 == 2").DecVal);

        // Users can still explicitly group if they want boolean-as-number comparisons:
        Assert.AreEqual(1m, Calculator.Evaluate("(1 < 2) == 1").DecVal);
        Assert.AreEqual(1m, Calculator.Evaluate("(2 == 2) == 1").DecVal);
        Assert.AreEqual(1m, Calculator.Evaluate("(2 == 3) == 0").DecVal);
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

    [TestMethod]
    public void PrefixLogicalNotBindsLooserThanComparisonMatchingPythonConvention()
    {
        // "not 1 == 0" parses as "not (1 == 0)", so not(0) -> 1.
        Assert.AreEqual(1m, Calculator.Evaluate("not 1 == 0").DecVal);
        // "not 0 == 0" parses as "not (0 == 0)", so not(1) -> 0.
        Assert.AreEqual(0m, Calculator.Evaluate("not 0 == 0").DecVal);
        // "!1 == 0" also parses as "!(1 == 0)", so !(0) -> 1.
        Assert.AreEqual(1m, Calculator.Evaluate("!1 == 0").DecVal);
        Assert.AreEqual(0m, Calculator.Evaluate("!0 == 0").DecVal);

        // AST verification matches precedence and structure: logical NOT wraps the comparison chain:
        var ast = Calculator.Parse("not 1 == 0");
        Assert.IsInstanceOfType<UnaryNode>(ast);
        var notNode = (UnaryNode)ast;
        Assert.AreEqual(UnaryOperator.LogicalNot, notNode.Op);
        Assert.IsInstanceOfType<ComparisonChainNode>(notNode.Operand);
        var chain = (ComparisonChainNode)notNode.Operand;
        Assert.AreEqual(ComparisonOperator.Equal, chain.Operators[0]);

        // Same for "!"
        var bangAst = Calculator.Parse("!1 == 0");
        Assert.IsInstanceOfType<UnaryNode>(bangAst);
        var bangNode = (UnaryNode)bangAst;
        Assert.AreEqual(UnaryOperator.LogicalNot, bangNode.Op);
        Assert.IsInstanceOfType<ComparisonChainNode>(bangNode.Operand);

        // Logical NOT binds tighter than logical AND: "not 1 == 0 and 1" is "(not (1 == 0)) and 1" -> 1
        Assert.AreEqual(1m, Calculator.Evaluate("not 1 == 0 and 1").DecVal);

        // Arithmetic unary still binds tighter than power:
        Assert.AreEqual(new BigInteger(-16), Calculator.Evaluate("-2^4").IntVal);
        // And logical not of arithmetic term:
        Assert.AreEqual(0m, Calculator.Evaluate("not 1 + 1").DecVal); // not (2) -> 0
    }

    [TestMethod]
    public void RequireExplicitLogicalNegation_EnforcesParenthesesAroundComparisons()
    {
        var strict = new CalculatorEngine(new CalculatorOptions { RequireExplicitLogicalNegation = true });

        // Unparenthesized logical NOT on comparison chain should fail in strict mode
        var ex1 = Assert.Throws<ExpressionException>(() => strict.Evaluate("not 1 == 0"));
        Assert.AreEqual(EvaluationError.AmbiguousLogicalNegation, ex1.Diagnostic.Code);

        var ex2 = Assert.Throws<ExpressionException>(() => strict.Evaluate("!1 == 0"));
        Assert.AreEqual(EvaluationError.AmbiguousLogicalNegation, ex2.Diagnostic.Code);

        var ex3 = Assert.Throws<ExpressionException>(() => strict.Parse("not 1 < 2 <= 3"));
        Assert.AreEqual(EvaluationError.AmbiguousLogicalNegation, ex3.Diagnostic.Code);

        // Partial parentheses around only the left/right operand do not span the comparison and must still be rejected
        var ex4 = Assert.Throws<ExpressionException>(() => strict.Parse("not (1) == 0"));
        Assert.AreEqual(EvaluationError.AmbiguousLogicalNegation, ex4.Diagnostic.Code);

        var ex5 = Assert.Throws<ExpressionException>(() => strict.Evaluate("not (1) == 0"));
        Assert.AreEqual(EvaluationError.AmbiguousLogicalNegation, ex5.Diagnostic.Code);

        var ex6 = Assert.Throws<ExpressionException>(() => strict.Parse("not (1) == (0)"));
        Assert.AreEqual(EvaluationError.AmbiguousLogicalNegation, ex6.Diagnostic.Code);

        var ex7 = Assert.Throws<ExpressionException>(() => strict.Evaluate("not (1) == (0)"));
        Assert.AreEqual(EvaluationError.AmbiguousLogicalNegation, ex7.Diagnostic.Code);

        // Explicitly parenthesized logical NOT remains valid in strict mode (including nested groups)
        Assert.AreEqual(1m, strict.Evaluate("not (1 == 0)").DecVal);
        Assert.AreEqual(1m, strict.Evaluate("!(1 == 0)").DecVal);
        Assert.AreEqual(1m, strict.Evaluate("not ((1) == 0)").DecVal);
        Assert.AreEqual(0m, strict.Evaluate("not (1 < 2 <= 3)").DecVal);
        Assert.IsNotNull(strict.Parse("not (1 == 0)"));
        Assert.IsNotNull(strict.Parse("not ((1) == 0)"));

        // Logical NOT applied to non-comparison does not require extra parentheses
        Assert.AreEqual(0m, strict.Evaluate("not 1").DecVal);
        Assert.AreEqual(1m, strict.Evaluate("!0").DecVal);
    }

    [TestMethod]
    public void LogicalNegationInOperandPosition_ProducesTargetedDiagnostic()
    {
        foreach (string expr in new[] { "2 * not 1", "1 == !2" })
        {
            var evalEx = Assert.Throws<ExpressionException>(() => Calculator.Evaluate(expr));
            Assert.AreEqual(EvaluationError.InvalidSyntax, evalEx.Diagnostic.Code);
            Assert.Contains("Logical negation is only allowed at the start of a logical operand", evalEx.Diagnostic.Message);
            Assert.Contains("parentheses", evalEx.Diagnostic.Message);

            var parseEx = Assert.Throws<ExpressionException>(() => Calculator.Parse(expr));
            Assert.AreEqual(EvaluationError.InvalidSyntax, parseEx.Diagnostic.Code);
            Assert.Contains("Logical negation is only allowed at the start of a logical operand", parseEx.Diagnostic.Message);
            Assert.Contains("parentheses", parseEx.Diagnostic.Message);
        }
    }

    [TestMethod]
    public void ScientificNotationLiterals_UnderflowAndPrecisionSemantics()
    {
        // Non-zero mantissa that underflows IEEE 754 double throws NumericOverflow rather than silently flushing to 0
        var evalUnderflow = Assert.Throws<ExpressionException>(() => Calculator.Evaluate("1e-999"));
        Assert.AreEqual(EvaluationError.NumericOverflow, evalUnderflow.Diagnostic.Code);

        var parseUnderflow = Assert.Throws<ExpressionException>(() => Calculator.Parse("1e-999"));
        Assert.AreEqual(EvaluationError.NumericOverflow, parseUnderflow.Diagnostic.Code);

        // Zero mantissa with extreme negative exponent remains exact zero
        Assert.AreEqual(0m, Calculator.Evaluate("0e-999").DecVal);
        Assert.AreEqual(0m, ((NumberNode)Calculator.Parse("0e-999")).Value.DecVal);

        // 1e5 and 100000.0 both parse as exact Decimal within decimal range
        var sci1e5 = Calculator.Evaluate("1e5");
        var dec100k = Calculator.Evaluate("100000.0");
        Assert.AreEqual(MathValue.DataType.Decimal, sci1e5.Type);
        Assert.AreEqual(MathValue.DataType.Decimal, dec100k.Type);
        Assert.AreEqual(100000m, sci1e5.DecVal);
        Assert.AreEqual(100000.0m, dec100k.DecVal);
        Assert.AreEqual(sci1e5.DecVal, dec100k.DecVal);
    }

    [TestMethod]
    public void GreekBlock_ConstantCatalogAndIdentifierRecognitionAgree()
    {
        for (char c = '\u0370'; c <= '\u03FF'; c++)
        {
            bool resolves = ConstantCatalog.TryResolve(c.ToString(), out _, out _);
            bool isConstLetter = ConstantCatalog.IsConstantLetter(c);
            Assert.AreEqual(resolves, isConstLetter, $"Mismatch at U+{(int)c:X4} ('{c}')");
        }
    }

    [TestMethod]
    public void Parser_FactorialAndExclamationDisambiguation()
    {
        // Postfix factorial is parsed natively
        Assert.AreEqual(new BigInteger(120), Calculator.Evaluate("5!").IntVal);

        // Inequality != is not mistaken for factorial
        Assert.AreEqual(1m, Calculator.Evaluate("5 != 3").DecVal);

        // Leading prefix logical negation ! is left intact
        Assert.AreEqual(1m, Calculator.Evaluate("!0").DecVal);
        Assert.AreEqual(1m, Calculator.Evaluate("!1 == 0").DecVal);

        // Combined postfix factorial, !=, and prefix !
        Assert.AreEqual(1m, Calculator.Evaluate("! (3! != 6)").DecVal);
    }

    [TestMethod]
    public void Differential_AstParserAndEvaluatorAgreeAcrossGeneratedGrammar()
    {
        var rng = new Random(20260405);
        for (int i = 0; i < 250; i++)
        {
            string raw = GenerateRandomExpression(rng, maxDepth: 3);
            string normalized = ExpressionNormalizer.Normalize(raw);

            bool evalOk = Calculator.TryEvaluate(normalized, out var evalResult, out var evalDiag);
            bool parseOk = Calculator.TryParse(normalized, out var ast, out var parseDiag);

            // Structural syntax errors must not silently pass one parser while failing the other
            if (!parseOk)
            {
                Assert.IsFalse(evalOk, $"AstParser rejected '{normalized}' ({parseDiag?.Code}), but Evaluator succeeded with {evalResult}.");
                continue;
            }

            // Round-trip: AST -> Ascii formatter -> AstParser -> structurally equal AST
            string formattedAscii = Calculator.FormatExpression(ast!, ExpressionFormatOptions.Ascii);
            var reparsedAst = Calculator.Parse(formattedAscii);
            Assert.AreEqual(ast, reparsedAst, $"AST round-trip mismatch for '{normalized}' -> '{formattedAscii}'.");

            // Evaluate the formatted AST via the real evaluator and compare with direct evaluation
            bool reEvalOk = Calculator.TryEvaluate(formattedAscii, out var reEvalResult, out var reEvalDiag);
            Assert.AreEqual(evalOk, reEvalOk, $"Evaluation outcome mismatch between '{normalized}' ({evalDiag?.Code}) and '{formattedAscii}' ({reEvalDiag?.Code}).");
            if (evalOk)
            {
                if (evalResult.IsNaN && reEvalResult.IsNaN)
                {
                    continue;
                }

                Assert.AreEqual(0, MathValue.Compare(evalResult, reEvalResult), $"Value mismatch for '{normalized}' ({evalResult}) vs '{formattedAscii}' ({reEvalResult}).");
            }
        }
    }

    private static string GenerateRandomExpression(Random rng, int maxDepth) =>
        GenerateLogicalNode(rng, maxDepth);

    private static string GenerateLogicalNode(Random rng, int depth)
    {
        if (depth <= 0 || rng.Next(4) == 0)
        {
            return GenerateLogicalNotNode(rng, depth);
        }

        string left = GenerateLogicalNotNode(rng, depth - 1);
        string right = GenerateLogicalNotNode(rng, depth - 1);
        string op = rng.Next(3) switch
        {
            0 => " && ",
            1 => " || ",
            _ => " xor ",
        };
        return left + op + right;
    }

    private static string GenerateLogicalNotNode(Random rng, int depth)
    {
        if (depth > 0 && rng.Next(4) == 0)
        {
            string prefix = rng.Next(2) == 0 ? "not " : "!";
            return prefix + GenerateComparisonNode(rng, depth - 1);
        }

        return GenerateComparisonNode(rng, depth);
    }

    private static string GenerateComparisonNode(Random rng, int depth)
    {
        string first = GenerateShiftNode(rng, depth - 1);
        if (depth <= 0 || rng.Next(3) != 0)
        {
            return first;
        }

        int chainLen = rng.Next(1, 3);
        var sb = new System.Text.StringBuilder(first);
        for (int i = 0; i < chainLen; i++)
        {
            string op = rng.Next(6) switch
            {
                0 => " == ",
                1 => " != ",
                2 => " < ",
                3 => " <= ",
                4 => " > ",
                _ => " >= ",
            };
            sb.Append(op).Append(GenerateShiftNode(rng, depth - 1));
        }

        return sb.ToString();
    }

    private static string GenerateShiftNode(Random rng, int depth)
    {
        string left = GenerateArithmeticNode(rng, depth - 1);
        if (depth > 0 && rng.Next(5) == 0)
        {
            string op = rng.Next(2) == 0 ? " << " : " >> ";
            string shiftAmt = rng.Next(0, 4).ToString(CultureInfo.InvariantCulture);
            return $"{left}{op}{shiftAmt}";
        }

        return left;
    }

    private static string GenerateArithmeticNode(Random rng, int depth)
    {
        if (depth <= 0)
        {
            return GenerateAtom(rng);
        }

        return rng.Next(5) switch
        {
            0 => $"{GenerateArithmeticNode(rng, depth - 1)} + {GenerateArithmeticNode(rng, depth - 1)}",
            1 => $"{GenerateArithmeticNode(rng, depth - 1)} - {GenerateArithmeticNode(rng, depth - 1)}",
            2 => $"{GenerateArithmeticNode(rng, depth - 1)} * {GenerateArithmeticNode(rng, depth - 1)}",
            3 => $"-{GenerateAtom(rng)}^{rng.Next(0, 4)}",
            _ => $"({GenerateLogicalNode(rng, depth - 1)})",
        };
    }

    private static string GenerateAtom(Random rng) =>
        rng.Next(7) switch
        {
            0 => "0",
            1 => "1",
            2 => "2",
            3 => "3",
            4 => "5!",
            5 => "pi",
            _ => "-2",
        };

    [TestMethod]
    public void StructureAndPrecedence_TargetedPatterns()
    {
        // -2^2: power binds tighter than leading unary minus: -(2^2) = -4
        var negPow = Calculator.Parse("-2^2");
        Assert.IsInstanceOfType<UnaryNode>(negPow);
        var negPowNode = (UnaryNode)negPow;
        Assert.AreEqual(UnaryOperator.Negate, negPowNode.Op);
        Assert.IsInstanceOfType<BinaryNode>(negPowNode.Operand);
        Assert.AreEqual(BinaryOperator.Power, ((BinaryNode)negPowNode.Operand).Op);
        Assert.AreEqual(new BigInteger(-4), Calculator.Evaluate("-2^2").IntVal);

        // 2^-3^2: right-associative power with unary minus exponent: 2^(-(3^2)) = 2^-9
        var chainedPow = Calculator.Parse("2^-3^2");
        Assert.IsInstanceOfType<BinaryNode>(chainedPow);
        var chainedPowBin = (BinaryNode)chainedPow;
        Assert.AreEqual(BinaryOperator.Power, chainedPowBin.Op);
        Assert.AreEqual(2, ((NumberNode)chainedPowBin.Left).Value.IntVal);
        Assert.IsInstanceOfType<UnaryNode>(chainedPowBin.Right);
        var expNeg = (UnaryNode)chainedPowBin.Right;
        Assert.AreEqual(UnaryOperator.Negate, expNeg.Op);
        Assert.IsInstanceOfType<BinaryNode>(expNeg.Operand);
        var expPow = (BinaryNode)expNeg.Operand;
        Assert.AreEqual(BinaryOperator.Power, expPow.Op);
        Assert.AreEqual(3, ((NumberNode)expPow.Left).Value.IntVal);
        Assert.AreEqual(2, ((NumberNode)expPow.Right).Value.IntVal);
        Assert.AreEqual(Math.Pow(2, -9), Calculator.Evaluate("2^-3^2").DblVal, 1e-15);

        // a < b < c: conjunctive ComparisonChainNode
        var cmpChain = Calculator.Parse("1 < 2 < 3");
        Assert.IsInstanceOfType<ComparisonChainNode>(cmpChain);
        var cmpChainNode = (ComparisonChainNode)cmpChain;
        Assert.HasCount(3, cmpChainNode.Operands);
        Assert.HasCount(2, cmpChainNode.Operators);
        Assert.AreEqual(ComparisonOperator.LessThan, cmpChainNode.Operators[0]);
        Assert.AreEqual(ComparisonOperator.LessThan, cmpChainNode.Operators[1]);
        Assert.AreEqual(1m, Calculator.Evaluate("1 < 2 < 3").DecVal);
        Assert.AreEqual(0m, Calculator.Evaluate("1 < 3 < 2").DecVal);

        // not a == b: logical NOT wrapping ComparisonChainNode
        var notEq = Calculator.Parse("not 4 == 5");
        Assert.IsInstanceOfType<UnaryNode>(notEq);
        var notEqNode = (UnaryNode)notEq;
        Assert.AreEqual(UnaryOperator.LogicalNot, notEqNode.Op);
        Assert.IsInstanceOfType<ComparisonChainNode>(notEqNode.Operand);
        Assert.AreEqual(1m, Calculator.Evaluate("not 4 == 5").DecVal);
        Assert.AreEqual(0m, Calculator.Evaluate("not 5 == 5").DecVal);
    }

    [TestMethod]
    public void AstNodesContainSourceTextSpansAndPreserveStructuralEquality()
    {
        var node1 = Calculator.Parse("12 + 34");
        var node2 = new BinaryNode(
            BinaryOperator.Add,
            new NumberNode(new MathValue(new BigInteger(12))),
            new NumberNode(new MathValue(new BigInteger(34))));

        // Spans are attached by AstParser
        Assert.IsTrue(node1.Span.HasValue);
        Assert.AreEqual(0, node1.Span.Start);
        Assert.AreEqual(7, node1.Span.Length); // "12 + 34" is 7 chars

        // Structural equality ignores spans (node2 has default Span, node1 has a populated Span)
        Assert.AreEqual(node2, node1);
        Assert.AreEqual(node2.GetHashCode(), node1.GetHashCode());
    }

    [TestMethod]
    public void EvaluateAstNode_ProducesIdenticalResultToEvaluateString()
    {
        string expression = "3 * 4 + 2^5";
        var parsed = Calculator.Parse(expression);

        var valFromString = Calculator.Evaluate(expression);
        var valFromAst = Calculator.Evaluate(parsed);

        Assert.AreEqual(valFromString, valFromAst);

        Assert.IsTrue(Calculator.TryEvaluate(parsed, out var tryVal, out var diag));
        Assert.IsNull(diag);
        Assert.AreEqual(valFromString, tryVal);
    }

    [TestMethod]
    public void RuntimeErrorsEnrichDiagnosticWithOffendingAstSpan()
    {
        // Division by zero in subexpression: "10 + (5 / 0)"
        // The error should pinpoint "5 / 0" (Start: 6, Length: 5)
        var divEx = Assert.Throws<ExpressionException>(() => Calculator.Evaluate("10 + (5 / 0)"));
        Assert.AreEqual(EvaluationError.DivisionByZero, divEx.Diagnostic.Code);
        Assert.AreEqual(6, divEx.Diagnostic.Position);
        Assert.AreEqual(5, divEx.Diagnostic.Length);

        // Division by zero inside compound expression: "10 + (5 / 0) * 2"
        // The error should stay pinpointed on "5 / 0" (Start: 6, Length: 5)
        var divEx2 = Assert.Throws<ExpressionException>(() => Calculator.Evaluate("10 + (5 / 0) * 2"));
        Assert.AreEqual(EvaluationError.DivisionByZero, divEx2.Diagnostic.Code);
        Assert.AreEqual(6, divEx2.Diagnostic.Position);
        Assert.AreEqual(5, divEx2.Diagnostic.Length);

        // Doubly-parenthesized division: "((5 / 0))" -> division span is [2, 7)
        var divEx3 = Assert.Throws<ExpressionException>(() => Calculator.Evaluate("((5 / 0))"));
        Assert.AreEqual(EvaluationError.DivisionByZero, divEx3.Diagnostic.Code);
        Assert.AreEqual(2, divEx3.Diagnostic.Position);
        Assert.AreEqual(5, divEx3.Diagnostic.Length);

        // TryEvaluate also reports the enriched subexpression span
        Assert.IsFalse(Calculator.TryEvaluate("10 + (9 / 0)", out _, out var diagnostic));
        Assert.IsNotNull(diagnostic);
        Assert.AreEqual(EvaluationError.DivisionByZero, diagnostic.Code);
        Assert.AreEqual(6, diagnostic.Position);
        Assert.AreEqual(5, diagnostic.Length);

        // Math domain error in subexpression: "10 + sqrt(-4)"
        // The error should pinpoint "sqrt(-4)" (Start: 5, Length: 8)
        var domainEx = Assert.Throws<ExpressionException>(() => Calculator.Evaluate("10 + sqrt(-4)"));
        Assert.AreEqual(EvaluationError.UndefinedMathResult, domainEx.Diagnostic.Code);
        Assert.AreEqual(5, domainEx.Diagnostic.Position);
        Assert.AreEqual(8, domainEx.Diagnostic.Length);
    }

    [TestMethod]
    public void LargeAdditiveChain_EvaluatesCorrectlyWithinConfiguredLimits()
    {
        // Repeated left-associative terms just fitting within the maximum input length
        // should evaluate to the correct sum without stack overflow.
        int terms = CalculationLimits.MaxInputLength / 2;
        string expr = string.Join("+", Enumerable.Repeat("1", terms));

        var result = Calculator.Evaluate(expr);
        Assert.AreEqual(new BigInteger(terms), result.IntVal);
    }

    [TestMethod]
    public void AstNodesSpansExcludeTrailingWhitespace()
    {
        // Trailing whitespace after constants or literals within expressions must not inflate node spans
        // Index positions:
        // "  pi   +  42   *  cos(  0  )  "
        //  0123456789012345678901234567890
        //    pi: start 2, length 2
        //    42: start 10, length 2
        //    cos(  0  ): start 18, length 11
        var expr = "  pi   +  42   *  cos(  0  )  ";
        var parsed = (BinaryNode)new CalculatorEngine(new CalculatorOptions { Normalization = null }).Parse(expr);

        var leftPi = (IdentifierNode)parsed.Left;
        Assert.AreEqual("pi", leftPi.Name);
        Assert.AreEqual(2, leftPi.Span.Start);
        Assert.AreEqual(2, leftPi.Span.Length); // exactly 'pi', does not include the trailing spaces before '+'

        var rightMul = (BinaryNode)parsed.Right;
        var num = (NumberNode)rightMul.Left;
        Assert.AreEqual(10, num.Span.Start);
        Assert.AreEqual(2, num.Span.Length); // exactly '42'

        var func = (FunctionCallNode)rightMul.Right;
        Assert.AreEqual("cos", func.FunctionName);
        Assert.AreEqual(18, func.Span.Start);
        Assert.AreEqual(10, func.Span.Length); // "cos(  0  )" has 10 characters (18..28)
    }

    [TestMethod]
    [DataRow("true", 1)]
    [DataRow("True", 1)]
    [DataRow("TRUE", 1)]
    [DataRow("false", 0)]
    [DataRow("False", 0)]
    [DataRow("FALSE", 0)]
    [DataRow("true && false", 0)]
    [DataRow("true || false", 1)]
    [DataRow("not false", 1)]
    [DataRow("not true", 0)]
    [DataRow("true == 1", 1)]
    [DataRow("false == 0", 1)]
    [DataRow("5 * true + 3 * false", 5)]
    public void BooleanLiteralsEvaluateAsConstants(string expression, int expected)
    {
        var result = Calculator.Evaluate(expression);
        Assert.AreEqual(new BigInteger(expected), result.IntVal != 0 ? result.IntVal : (BigInteger)result.DecVal);
    }

    [TestMethod]
    public void CallerSuppliedVariablesAndPreviousResultAnsAreSupported()
    {
        // 1. Standalone 'ans' convenience via PreviousResult
        var optionsAns = new EvaluationContext(
            variables: null, previousResult: new MathValue(42m));
        var ansResult = Calculator.Evaluate("ans * 2 + 1", optionsAns);
        Assert.AreEqual(85m, ansResult.DecVal);

        // Case-insensitivity check for Ans
        var ansUpperResult = Calculator.Evaluate("ANS / 2", optionsAns);
        Assert.AreEqual(21m, ansUpperResult.DecVal);

        // 2. Custom dictionary of caller-supplied variables
        var variables = new Dictionary<string, MathValue>(StringComparer.OrdinalIgnoreCase)
        {
            ["x"] = new MathValue(new BigInteger(10)),
            ["y"] = new MathValue(2.5m),
        };
        var optionsVars = new EvaluationContext(variables, null);
        var varsResult = Calculator.Evaluate("3 * x + 4 * y", optionsVars);
        Assert.AreEqual(40m, varsResult.DecVal);

        // 3. Unbound variable produces UnknownConstant diagnostic
        Assert.IsFalse(Calculator.TryEvaluate("z + 1", out _, out var diagnostic, optionsVars));
        Assert.IsNotNull(diagnostic);
        Assert.AreEqual(EvaluationError.UnknownConstant, diagnostic.Code);
    }
}
