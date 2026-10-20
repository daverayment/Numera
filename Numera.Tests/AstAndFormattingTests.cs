using Numera;
using Numera.Ast;

namespace Numera.Tests;

[TestClass]
public sealed class AstAndFormattingTests
{
    [TestMethod]
    public void Parse_Number_ReturnsNumberNode()
    {
        var node = Calculator.Parse("42");
        Assert.IsInstanceOfType<NumberNode>(node);
        Assert.AreEqual(42, ((NumberNode)node).Value.IntVal);
    }

    [TestMethod]
    public void Parse_BinaryAddition_ReturnsBinaryNode()
    {
        var node = Calculator.Parse("2 + 3");
        Assert.IsInstanceOfType<BinaryNode>(node);
        var binary = (BinaryNode)node;
        Assert.AreEqual(BinaryOperator.Add, binary.Op);
        Assert.IsInstanceOfType<NumberNode>(binary.Left);
        Assert.IsInstanceOfType<NumberNode>(binary.Right);
    }

    [TestMethod]
    public void Parse_Precedence_PreservesStructure()
    {
        // 2 + 3 * 4 should be 2 + (3 * 4)
        var node = Calculator.Parse("2 + 3 * 4");
        Assert.IsInstanceOfType<BinaryNode>(node);
        var addNode = (BinaryNode)node;
        Assert.AreEqual(BinaryOperator.Add, addNode.Op);
        Assert.AreEqual(2, ((NumberNode)addNode.Left).Value.IntVal);

        Assert.IsInstanceOfType<BinaryNode>(addNode.Right);
        var mulNode = (BinaryNode)addNode.Right;
        Assert.AreEqual(BinaryOperator.Multiply, mulNode.Op);
        Assert.AreEqual(3, ((NumberNode)mulNode.Left).Value.IntVal);
        Assert.AreEqual(4, ((NumberNode)mulNode.Right).Value.IntVal);
    }

    [TestMethod]
    public void Parse_ChainedComparison_ReturnsComparisonChainNode()
    {
        var node = Calculator.Parse("1 < 2 <= 3");
        Assert.IsInstanceOfType<ComparisonChainNode>(node);
        var chain = (ComparisonChainNode)node;
        Assert.AreEqual(3, chain.Operands.Count);
        Assert.AreEqual(2, chain.Operators.Count);
        Assert.AreEqual(ComparisonOperator.LessThan, chain.Operators[0]);
        Assert.AreEqual(ComparisonOperator.LessThanOrEqual, chain.Operators[1]);
    }

    [TestMethod]
    public void Parse_FunctionCall_ReturnsFunctionCallNode()
    {
        var node = Calculator.Parse("sqrt(16)");
        Assert.IsInstanceOfType<FunctionCallNode>(node);
        var fn = (FunctionCallNode)node;
        Assert.AreEqual("sqrt", fn.FunctionName);
        Assert.AreEqual(1, fn.Arguments.Count);
    }

    [TestMethod]
    public void TryParse_ValidExpression_ReturnsTrueAndNode()
    {
        bool success = Calculator.TryParse("2^8", out var node, out var diagnostic);
        Assert.IsTrue(success);
        Assert.IsNotNull(node);
        Assert.IsNull(diagnostic);
        Assert.IsInstanceOfType<BinaryNode>(node);
        Assert.AreEqual(BinaryOperator.Power, ((BinaryNode)node).Op);
    }

    [TestMethod]
    public void TryParse_InvalidSyntax_ReturnsFalseAndDiagnostic()
    {
        bool success = Calculator.TryParse("2 + * 3", out var node, out var diagnostic);
        Assert.IsFalse(success);
        Assert.IsNull(node);
        Assert.IsNotNull(diagnostic);
        Assert.AreEqual(EvaluationError.InvalidSyntax, diagnostic.Code);
    }

    [TestMethod]
    public void FormatExpression_Typographic_FormatsOperators()
    {
        string formatted = Calculator.FormatExpression("2 * pi * sqrt(16) / 2");
        Assert.AreEqual("2 × π × √(16) ÷ 2", formatted);
    }

    [TestMethod]
    public void FormatExpression_Typographic_FormatsSuperscriptPower()
    {
        string formatted = Calculator.FormatExpression("2^10");
        Assert.AreEqual("2¹⁰", formatted);
    }

    [TestMethod]
    public void FormatExpression_Typographic_FormatsUnicodeMinus()
    {
        string formatted = Calculator.FormatExpression("5 - 3");
        Assert.AreEqual("5 − 3", formatted);
    }

    [TestMethod]
    public void FormatExpression_MathJax_FormatsLaTeX()
    {
        string formatted = Calculator.FormatExpression("2 * pi * sqrt(16) / 2", ExpressionFormatOptions.LaTeX);
        Assert.AreEqual(@"\frac{2 \cdot \pi \cdot \sqrt{16}}{2}", formatted);
    }

    [TestMethod]
    public void FormatExpression_MathJax_PowerAndTrig()
    {
        string formattedPi = Calculator.FormatExpression("sin(pi^2)", ExpressionFormatOptions.LaTeX);
        Assert.AreEqual(@"\sin(\pi^{2})", formattedPi);
    }

    [TestMethod]
    public void FormatExpression_NormalizedAscii_StandardizesSpacing()
    {
        string formatted = Calculator.FormatExpression("2*3+4", ExpressionFormatOptions.Ascii);
        Assert.AreEqual("2 * 3 + 4", formatted);
    }

    [TestMethod]
    public void FormatExpression_ComparisonChain_Typographic()
    {
        string formatted = Calculator.FormatExpression("1 < 2 <= 3", ExpressionFormatOptions.Typographic);
        Assert.AreEqual("1 < 2 ≤ 3", formatted);
    }

    [TestMethod]
    public void Parse_UnaryOperators_ReturnUnaryNode()
    {
        var neg = Calculator.Parse("-5");
        Assert.IsInstanceOfType<UnaryNode>(neg);
        Assert.AreEqual(UnaryOperator.Negate, ((UnaryNode)neg).Op);

        var pos = Calculator.Parse("+5");
        Assert.IsInstanceOfType<UnaryNode>(pos);
        Assert.AreEqual(UnaryOperator.Positive, ((UnaryNode)pos).Op);

        var notOp = Calculator.Parse("!1");
        Assert.IsInstanceOfType<UnaryNode>(notOp);
        Assert.AreEqual(UnaryOperator.LogicalNot, ((UnaryNode)notOp).Op);

        var bitNot = Calculator.Parse("~1");
        Assert.IsInstanceOfType<UnaryNode>(bitNot);
        Assert.AreEqual(UnaryOperator.BitwiseNot, ((UnaryNode)bitNot).Op);

        var notWord = Calculator.Parse("not 1");
        Assert.IsInstanceOfType<UnaryNode>(notWord);
        Assert.AreEqual(UnaryOperator.LogicalNot, ((UnaryNode)notWord).Op);
    }

    [TestMethod]
    public void Parse_LogicalAndOrXor_ReturnsBinaryNode()
    {
        var andNode = Calculator.Parse("1 && 2 and 3");
        Assert.IsInstanceOfType<BinaryNode>(andNode);
        Assert.AreEqual(BinaryOperator.LogicalAnd, ((BinaryNode)andNode).Op);

        var orNode = Calculator.Parse("1 || 2 or 3");
        Assert.IsInstanceOfType<BinaryNode>(orNode);
        Assert.AreEqual(BinaryOperator.LogicalOr, ((BinaryNode)orNode).Op);

        var xorNode = Calculator.Parse("1 xor 2");
        Assert.IsInstanceOfType<BinaryNode>(xorNode);
        Assert.AreEqual(BinaryOperator.LogicalXor, ((BinaryNode)xorNode).Op);
    }

    [TestMethod]
    public void Parse_BitShifts_ReturnsBinaryNode()
    {
        var shl = Calculator.Parse("1 << 4");
        Assert.IsInstanceOfType<BinaryNode>(shl);
        Assert.AreEqual(BinaryOperator.ShiftLeft, ((BinaryNode)shl).Op);

        var shr = Calculator.Parse("16 >> 2");
        Assert.IsInstanceOfType<BinaryNode>(shr);
        Assert.AreEqual(BinaryOperator.ShiftRight, ((BinaryNode)shr).Op);
    }

    [TestMethod]
    public void Parse_RadixNumbers_ReturnsNumberNodeWithExactValue()
    {
        var hex = Calculator.Parse("0x2A");
        Assert.IsInstanceOfType<NumberNode>(hex);
        Assert.AreEqual(42, ((NumberNode)hex).Value.IntVal);

        var bin = Calculator.Parse("0b101010");
        Assert.IsInstanceOfType<NumberNode>(bin);
        Assert.AreEqual(42, ((NumberNode)bin).Value.IntVal);

        var oct = Calculator.Parse("0o52");
        Assert.IsInstanceOfType<NumberNode>(oct);
        Assert.AreEqual(42, ((NumberNode)oct).Value.IntVal);
    }

    [TestMethod]
    public void Parse_ConstantIdentifiers_PreservesRecognizedConstants()
    {
        var piNode = Calculator.Parse("pi");
        Assert.IsInstanceOfType<IdentifierNode>(piNode);
        Assert.AreEqual("pi", ((IdentifierNode)piNode).Name);

        var tauNode = Calculator.Parse("tau");
        Assert.IsInstanceOfType<IdentifierNode>(tauNode);
        Assert.AreEqual("tau", ((IdentifierNode)tauNode).Name);

        var eNode = Calculator.Parse("e");
        Assert.IsInstanceOfType<IdentifierNode>(eNode);
        Assert.AreEqual("e", ((IdentifierNode)eNode).Name);
    }

    [TestMethod]
    public void Parse_ComparisonOperators_MatchesAllVariations()
    {
        var eq = Calculator.Parse("1 == 1");
        Assert.IsInstanceOfType<ComparisonChainNode>(eq);
        Assert.AreEqual(ComparisonOperator.Equal, ((ComparisonChainNode)eq).Operators[0]);

        var ne1 = Calculator.Parse("1 != 2");
        Assert.IsInstanceOfType<ComparisonChainNode>(ne1);
        Assert.AreEqual(ComparisonOperator.NotEqual, ((ComparisonChainNode)ne1).Operators[0]);

        var ne2 = Calculator.Parse("1 ~= 2");
        Assert.IsInstanceOfType<ComparisonChainNode>(ne2);
        Assert.AreEqual(ComparisonOperator.NotEqual, ((ComparisonChainNode)ne2).Operators[0]);

        var gte = Calculator.Parse("3 >= 2");
        Assert.IsInstanceOfType<ComparisonChainNode>(gte);
        Assert.AreEqual(ComparisonOperator.GreaterThanOrEqual, ((ComparisonChainNode)gte).Operators[0]);

        var gt = Calculator.Parse("3 > 2");
        Assert.IsInstanceOfType<ComparisonChainNode>(gt);
        Assert.AreEqual(ComparisonOperator.GreaterThan, ((ComparisonChainNode)gt).Operators[0]);
    }

    [TestMethod]
    public void Parse_ReservedCharacters_ThrowsSyntaxError()
    {
        Assert.Throws<ExpressionException>(() => Calculator.Parse("[1, 2]"));
        Assert.Throws<ExpressionException>(() => Calculator.Parse("1; 2"));
    }

    [TestMethod]
    public void Parse_InvalidFunctionOrArity_ThrowsAppropriateError()
    {
        var exUnknown = Assert.Throws<ExpressionException>(() => Calculator.Parse("unknownFunc(1)"));
        Assert.AreEqual(EvaluationError.UnknownFunction, exUnknown.Diagnostic.Code);

        var exArity = Assert.Throws<ExpressionException>(() => Calculator.Parse("sqrt(1, 2)"));
        Assert.AreEqual(EvaluationError.InvalidArgumentCount, exArity.Diagnostic.Code);
    }

    [TestMethod]
    public void Parse_SafetyLimits_EnforcesMaxInputLengthAndNestingDepth()
    {
        var optionsLength = new EvaluationOptions { MaxInputLength = 5 };
        Assert.Throws<CalculationLimitException>(() => Calculator.Parse("1 + 2 + 3", optionsLength));

        var optionsDepth = new EvaluationOptions { MaxNestingDepth = 2 };
        Assert.Throws<CalculationLimitException>(() => Calculator.Parse("---1", optionsDepth));
    }

    [TestMethod]
    public void FormatExpression_MathJax_RootsAndFractions()
    {
        string cbrt = Calculator.FormatExpression("cbrt(27)", ExpressionFormatOptions.LaTeX);
        Assert.AreEqual(@"\sqrt[3]{27}", cbrt);

        string unaryPi = Calculator.FormatExpression("-pi", ExpressionFormatOptions.LaTeX);
        Assert.AreEqual(@"-\pi", unaryPi);

        string bitwise = Calculator.FormatExpression("~1", ExpressionFormatOptions.LaTeX);
        Assert.AreEqual(@"\sim 1", bitwise);

        string logicNot = Calculator.FormatExpression("!1", ExpressionFormatOptions.LaTeX);
        Assert.AreEqual(@"\neg 1", logicNot);

        string logicOr = Calculator.FormatExpression("1 || 0", ExpressionFormatOptions.LaTeX);
        Assert.AreEqual(@"1 \lor 0", logicOr);

        string logicXor = Calculator.FormatExpression("1 xor 0", ExpressionFormatOptions.LaTeX);
        Assert.AreEqual(@"1 \oplus 0", logicXor);

        string shifts = Calculator.FormatExpression("1 << 2", ExpressionFormatOptions.LaTeX);
        Assert.AreEqual(@"1 \ll 2", shifts);

        string shiftRight = Calculator.FormatExpression("4 >> 1", ExpressionFormatOptions.LaTeX);
        Assert.AreEqual(@"4 \gg 1", shiftRight);

        string mod = Calculator.FormatExpression("5 % 2", ExpressionFormatOptions.LaTeX);
        Assert.AreEqual(@"5 \bmod 2", mod);

        string comp = Calculator.FormatExpression("1 != 2", ExpressionFormatOptions.LaTeX);
        Assert.AreEqual(@"1 \neq 2", comp);
    }

    [TestMethod]
    public void FormatExpression_Typographic_ParenthesizationRules()
    {
        // Right child in subtraction: a - (b - c)
        string subRight = Calculator.FormatExpression("1 - (2 - 3)");
        Assert.AreEqual("1 − (2 − 3)", subRight);

        // Power left child: (a^b)^c
        string powLeft = Calculator.FormatExpression("(2^3)^4");
        Assert.AreEqual("(2³)⁴", powLeft);

        // Unary positive and bitwise
        string pos = Calculator.FormatExpression("+5", ExpressionFormatOptions.Typographic);
        Assert.AreEqual("+5", pos);

        string bitNot = Calculator.FormatExpression("~5", ExpressionFormatOptions.Typographic);
        Assert.AreEqual("~5", bitNot);

        string logicNot = Calculator.FormatExpression("!5", ExpressionFormatOptions.Typographic);
        Assert.AreEqual("!5", logicNot);

        // Dot multiplication
        string dot = Calculator.FormatExpression("2 * 3", ExpressionFormatOptions.Typographic with { Multiplication = MultiplicationSymbol.Dot });
        Assert.AreEqual("2 · 3", dot);

        // Slash division
        string slash = Calculator.FormatExpression("4 / 2", ExpressionFormatOptions.Typographic with { Division = DivisionSymbol.Slash });
        Assert.AreEqual("4 / 2", slash);
    }

    [TestMethod]
    public void FormatExpression_Latex_FunctionAndOperators()
    {
        // LaTeX trig/standard function
        string cos = Calculator.FormatExpression("cos(pi)", ExpressionFormatOptions.LaTeX);
        Assert.AreEqual(@"\cos(\pi)", cos);

        string ln = Calculator.FormatExpression("ln(e)", ExpressionFormatOptions.LaTeX);
        Assert.AreEqual(@"\ln(e)", ln);

        // LaTeX custom function (operatorname)
        string factorial = Calculator.FormatExpression("factorial(5)", ExpressionFormatOptions.LaTeX);
        Assert.AreEqual(@"\operatorname{factorial}(5)", factorial);

        // LaTeX comparisons
        string gte = Calculator.FormatExpression("3 >= 2", ExpressionFormatOptions.LaTeX);
        Assert.AreEqual(@"3 \ge 2", gte);

        string lte = Calculator.FormatExpression("2 <= 3", ExpressionFormatOptions.LaTeX);
        Assert.AreEqual(@"2 \le 3", lte);

        string eq = Calculator.FormatExpression("2 == 2", ExpressionFormatOptions.LaTeX);
        Assert.AreEqual(@"2 = 2", eq);

        // LaTeX multiplication options
        string cross = Calculator.FormatExpression("2 * 3", ExpressionFormatOptions.LaTeX with { Multiplication = MultiplicationSymbol.Cross });
        Assert.AreEqual(@"2 \times 3", cross);

        string asterisk = Calculator.FormatExpression("2 * 3", ExpressionFormatOptions.LaTeX with { Multiplication = MultiplicationSymbol.Asterisk });
        Assert.AreEqual(@"2 * 3", asterisk);

        // LaTeX division options
        string divSym = Calculator.FormatExpression("4 / 2", ExpressionFormatOptions.LaTeX with { Division = DivisionSymbol.Obelus });
        Assert.AreEqual(@"4 \div 2", divSym);

        // LaTeX unary positive
        string pos = Calculator.FormatExpression("+5", ExpressionFormatOptions.LaTeX);
        Assert.AreEqual("+5", pos);
    }

    [TestMethod]
    public void EvaluationOptions_Validate_ThrowsOnInvalidRanges()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new EvaluationOptions { MaxInputLength = 0 }.Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() => new EvaluationOptions { MaxInputLength = CalculationLimits.MaxInputLength + 1 }.Validate());

        Assert.Throws<ArgumentOutOfRangeException>(() => new EvaluationOptions { MaxNestingDepth = 0 }.Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() => new EvaluationOptions { MaxNestingDepth = CalculationLimits.MaxNestingDepth + 1 }.Validate());

        Assert.Throws<ArgumentOutOfRangeException>(() => new EvaluationOptions { AngleUnit = (AngleUnit)999 }.Validate());
    }

    [TestMethod]
    public void FunctionSignature_Properties_CanBeConstructedAndRead()
    {
        var sig = new FunctionSignature(2, 4);
        Assert.AreEqual(2, sig.MinimumArguments);
        Assert.AreEqual(4, sig.MaximumArguments);
    }

    [TestMethod]
    public void FormatExpression_Typographic_AllOperators()
    {
        string shl = Calculator.FormatExpression("1 << 2");
        Assert.AreEqual("1 << 2", shl);

        string shr = Calculator.FormatExpression("4 >> 1");
        Assert.AreEqual("4 >> 1", shr);

        string andOp = Calculator.FormatExpression("1 && 0");
        Assert.AreEqual("1 && 0", andOp);

        string orOp = Calculator.FormatExpression("1 || 0");
        Assert.AreEqual("1 || 0", orOp);

        string xorOp = Calculator.FormatExpression("1 xor 0");
        Assert.AreEqual("1 xor 0", xorOp);

        string modOp = Calculator.FormatExpression("5 % 2");
        Assert.AreEqual("5 % 2", modOp);

        string gte = Calculator.FormatExpression("3 >= 2");
        Assert.AreEqual("3 ≥ 2", gte);

        string lte = Calculator.FormatExpression("2 <= 3");
        Assert.AreEqual("2 ≤ 3", lte);

        string ne = Calculator.FormatExpression("1 != 2");
        Assert.AreEqual("1 != 2", ne);

        string eq = Calculator.FormatExpression("2 == 2");
        Assert.AreEqual("2 == 2", eq);

        // Greek tau
        string tau = Calculator.FormatExpression("tau");
        Assert.AreEqual("τ", tau);

        // Function with multiple arguments
        string minVal = Calculator.FormatExpression("min(1, 2, 3)");
        Assert.AreEqual("min(1, 2, 3)", minVal);

        // Negative power superscript
        string negPow = Calculator.FormatExpression("2^-3");
        Assert.AreEqual("2⁻³", negPow);
    }
}
