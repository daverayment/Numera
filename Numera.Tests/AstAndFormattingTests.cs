using System.Numerics;
using System.Text;
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
        Assert.HasCount(3, chain.Operands);
        Assert.HasCount(2, chain.Operators);
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
        Assert.HasCount(1, fn.Arguments);
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
    public void Parse_ParenthesizedExpression_SpansEncompassParentheses()
    {
        // (1+2)*3: outer binary span must encompass grouping parentheses: [0, 7)
        // while the inner grouped node maintains its tight child span: [1, 4)
        var node1 = Calculator.Parse("(1+2)*3");
        Assert.IsInstanceOfType<BinaryNode>(node1);
        var bin1 = (BinaryNode)node1;
        Assert.AreEqual(new TextSpan(0, 7), bin1.Span);
        Assert.AreEqual(new TextSpan(1, 3), bin1.Left.Span);
        Assert.AreEqual(new TextSpan(6, 1), bin1.Right.Span);

        // 3*(1+2): outer binary span is [0, 7) and inner node remains tight: [3, 6)
        var node2 = Calculator.Parse("3*(1+2)");
        Assert.IsInstanceOfType<BinaryNode>(node2);
        var bin2 = (BinaryNode)node2;
        Assert.AreEqual(new TextSpan(0, 7), bin2.Span);
        Assert.AreEqual(new TextSpan(0, 1), bin2.Left.Span);
        Assert.AreEqual(new TextSpan(3, 3), bin2.Right.Span);

        // -(1+2): unary negation encompasses the group: [0, 6)
        var node3 = Calculator.Parse("-(1+2)");
        Assert.IsInstanceOfType<UnaryNode>(node3);
        var un3 = (UnaryNode)node3;
        Assert.AreEqual(new TextSpan(0, 6), un3.Span);
        Assert.AreEqual(new TextSpan(2, 3), un3.Operand.Span);

        // not (a == b): logical negation outer span includes 'not' through closing ')': [0, 12)
        var options = new EvaluationOptions
        {
            Variables = new Dictionary<string, MathValue> { ["a"] = new(1m), ["b"] = new(2m) }
        };
        var node4 = Calculator.Parse("not (a == b)", options);
        Assert.IsInstanceOfType<UnaryNode>(node4);
        var un4 = (UnaryNode)node4;
        Assert.AreEqual(new TextSpan(0, 12), un4.Span);
        Assert.AreEqual(new TextSpan(5, 6), un4.Operand.Span);

        // 10 + (5 / 0) with trailing whitespace: root binary span ignores trailing spaces: [0, 12)
        var node5 = Calculator.Parse("10 + (5 / 0)   ");
        Assert.IsInstanceOfType<BinaryNode>(node5);
        var bin5 = (BinaryNode)node5;
        Assert.AreEqual(new TextSpan(0, 12), bin5.Span);
        Assert.AreEqual(new TextSpan(0, 2), bin5.Left.Span);
        Assert.AreEqual(new TextSpan(6, 5), bin5.Right.Span);
    }

    [TestMethod]
    public void Parse_ConstantIdentifiers_PreservesRecognizedConstants()
    {
        var piNode = (IdentifierNode)Calculator.Parse("pi");
        Assert.AreEqual("pi", piNode.Name);
        Assert.AreEqual(ConstantKind.Pi, piNode.Constant);

        var tauNode = (IdentifierNode)Calculator.Parse("tau");
        Assert.AreEqual("tau", tauNode.Name);
        Assert.AreEqual(ConstantKind.Tau, tauNode.Constant);

        var phiNode = (IdentifierNode)Calculator.Parse("phi");
        Assert.AreEqual("phi", phiNode.Name);
        Assert.AreEqual(ConstantKind.Phi, phiNode.Constant);

        var phiSymNode = (IdentifierNode)Calculator.Parse("ϕ");
        Assert.AreEqual("ϕ", phiSymNode.Name);
        Assert.AreEqual(ConstantKind.Phi, phiSymNode.Constant);

        var eNode = (IdentifierNode)Calculator.Parse("e");
        Assert.AreEqual("e", eNode.Name);
        Assert.AreEqual(ConstantKind.E, eNode.Constant);
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

        // Right-associative power chains and nested groupings increment nesting height.
        // With MaxNestingDepth = 3, "2^3^4" creates BinaryNode(^, 2, BinaryNode(^, 3, 4)) with height 3 (allowed),
        // but "2^3^4^5" has height 4 and must be rejected.
        var chainAllowed = Calculator.Parse("2^3^4", new EvaluationOptions { MaxNestingDepth = 3 });
        Assert.AreEqual(3, chainAllowed.Height);

        Assert.Throws<CalculationLimitException>(() =>
            Calculator.Parse("2^3^4^5", new EvaluationOptions { MaxNestingDepth = 3 }));
    }

    [TestMethod]
    public void FormatExpression_MathJax_RootsAndFractions()
    {
        string cbrt = Calculator.FormatExpression("cbrt(27)", ExpressionFormatOptions.LaTeX);
        Assert.AreEqual(@"\sqrt[3]{27}", cbrt);

        string unaryPi = Calculator.FormatExpression("-pi", ExpressionFormatOptions.LaTeX);
        Assert.AreEqual(@"-\pi", unaryPi);

        string unaryPhi = Calculator.FormatExpression("phi", ExpressionFormatOptions.LaTeX);
        Assert.AreEqual(@"\phi", unaryPhi);

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

        // Greek phi
        string phi = Calculator.FormatExpression("phi");
        Assert.AreEqual("ϕ", phi);

        // Function with multiple arguments
        string minVal = Calculator.FormatExpression("min(1, 2, 3)");
        Assert.AreEqual("min(1, 2, 3)", minVal);

        // Negative power superscript
        string negPow = Calculator.FormatExpression("2^-3");
        Assert.AreEqual("2⁻³", negPow);
    }

    [TestMethod]
    public void FormatExpression_PrecedenceAndAssociativity_ParenthesizesCorrectly()
    {
        // 2 * (3 % 4) should retain parentheses around 3 % 4 because Modulo is not associative with Multiply
        string mulMod = Calculator.FormatExpression("2 * (3 % 4)", ExpressionFormatOptions.LaTeX);
        Assert.AreEqual(@"2 \cdot (3 \bmod 4)", mulMod);

        string mulModTypo = Calculator.FormatExpression("2 * (3 % 4)", ExpressionFormatOptions.Typographic);
        Assert.AreEqual("2 × (3 % 4)", mulModTypo);

        // 2 << (3 << 4) should parenthesize right operand of shift
        string shiftSh = Calculator.FormatExpression("2 << (3 << 4)", ExpressionFormatOptions.LaTeX);
        Assert.AreEqual(@"2 \ll (3 \ll 4)", shiftSh);

        // Associative operators drop parens on the right: 2 + (3 + 4) -> 2 + 3 + 4, 2 * (3 * 4) -> 2 * 3 * 4
        string addAdd = Calculator.FormatExpression("2 + (3 + 4)", ExpressionFormatOptions.Typographic);
        Assert.AreEqual("2 + 3 + 4", addAdd);

        string mulMul = Calculator.FormatExpression("2 * (3 * 4)", ExpressionFormatOptions.Typographic);
        Assert.AreEqual("2 × 3 × 4", mulMul);

        // Unary right operand parenthesized: 2 - (-3) -> 2 - (-3)
        string subNeg = Calculator.FormatExpression("2 - (-3)", ExpressionFormatOptions.LaTeX);
        Assert.AreEqual("2 - (-3)", subNeg);

        string subNegTypo = Calculator.FormatExpression("2 - (-3)", ExpressionFormatOptions.Typographic);
        Assert.AreEqual("2 − (−3)", subNegTypo);
    }

    [TestMethod]
    public void FormatExpression_ComparisonChainNode_ParenthesizesLowerPrecedenceOperands()
    {
        // (1 && 0) < 2 should format with parens around 1 && 0
        string logicInComp = Calculator.FormatExpression("(1 && 0) < 2", ExpressionFormatOptions.Typographic);
        Assert.AreEqual("(1 && 0) < 2", logicInComp);

        string logicInCompLatex = Calculator.FormatExpression("(1 && 0) < 2", ExpressionFormatOptions.LaTeX);
        Assert.AreEqual(@"(1 \land 0) < 2", logicInCompLatex);
    }

    [TestMethod]
    public void FormatExpression_MathJax_NumbersFunctionsAndDelimiters()
    {
        // Scientific notation double parsed from input
        string sciFormatted = Calculator.FormatExpression("1E-30", ExpressionFormatOptions.LaTeX);
        Assert.AreEqual(@"1 \times 10^{-30}", sciFormatted);

        // Infinities and NaN
        var posInf = new NumberNode(new MathValue(double.PositiveInfinity));
        Assert.AreEqual(@"\infty", posInf.Accept(new MathJaxVisitor(ExpressionFormatOptions.LaTeX)));

        var negInf = new NumberNode(new MathValue(double.NegativeInfinity));
        Assert.AreEqual(@"-\infty", negInf.Accept(new MathJaxVisitor(ExpressionFormatOptions.LaTeX)));

        var nan = new NumberNode(new MathValue(double.NaN));
        Assert.AreEqual(@"\operatorname{NaN}", nan.Accept(new MathJaxVisitor(ExpressionFormatOptions.LaTeX)));

        // Inverse trigonometric functions: asin, acos, atan
        string asin = Calculator.FormatExpression("asin(1)", ExpressionFormatOptions.LaTeX);
        Assert.AreEqual(@"\arcsin(1)", asin);

        string acos = Calculator.FormatExpression("acos(1)", ExpressionFormatOptions.LaTeX);
        Assert.AreEqual(@"\arccos(1)", acos);

        string atan = Calculator.FormatExpression("atan(1)", ExpressionFormatOptions.LaTeX);
        Assert.AreEqual(@"\arctan(1)", atan);

        // Delimiter functions: abs, floor, ceil
        string abs = Calculator.FormatExpression("abs(1)", ExpressionFormatOptions.LaTeX);
        Assert.AreEqual(@"\left|1\right|", abs);

        string floor = Calculator.FormatExpression("floor(1)", ExpressionFormatOptions.LaTeX);
        Assert.AreEqual(@"\left\lfloor 1\right\rfloor", floor);

        string ceil = Calculator.FormatExpression("ceil(1)", ExpressionFormatOptions.LaTeX);
        Assert.AreEqual(@"\left\lceil 1\right\rceil", ceil);

        // Tall fraction base in power
        string tallPow = Calculator.FormatExpression("(1 / 2)^2", ExpressionFormatOptions.LaTeX);
        Assert.AreEqual(@"\left(\frac{1}{2}\right)^{2}", tallPow);
    }

    [TestMethod]
    public void FormatExpression_LogicalNotWithComparison_RendersParenthesesAroundComparison()
    {
        // Typographic format: "not 1 == 0" has AST UnaryNode(LogicalNot, ComparisonChainNode)
        // Since LogicalNot has lower precedence (4) than Comparison (5), operand of LogicalNot does not strictly need parens
        // if child (5) > parent (4), so "!(1 == 0)" or "!1 == 0" depending on precedence.
        // But in TypographicVisitor: UnaryNode formats operand using FormatChild(node.Operand, node, isRight: false).
        // childPrec = GetPrecedence(ComparisonChain) = 5.
        // parentPrec = GetPrecedence(UnaryNode with LogicalNot) = 4.
        // childPrec (5) > parentPrec (4), so child does not need parens in standard expression notation: "!1 == 0".
        // When parent is LogicalAnd (prec 3): "not 1 == 0 and 1" -> "!1 == 0 && 1".
        string typ = Calculator.FormatExpression("not 1 == 0");
        Assert.AreEqual("!1 == 0", typ);

        string latex = Calculator.FormatExpression("not 1 == 0", ExpressionFormatOptions.LaTeX);
        Assert.AreEqual(@"\neg 1 = 0", latex);
    }

    [TestMethod]
    public void FormatExpression_RoundTrips_TargetedExpressionsPreservePrecedence()
    {
        // 2 * (3 % 4): multiplication and modulo share precedence (8), but modulo is not associative with multiply,
        // so right child retains necessary parentheses.
        string modInMul = Calculator.FormatExpression("2 * (3 % 4)", ExpressionFormatOptions.Ascii);
        Assert.AreEqual("2 * (3 % 4)", modInMul);
        Assert.AreEqual(Calculator.Parse("2 * (3 % 4)"), Calculator.Parse(modInMul));

        // 2 << (3 << 4): shift is non-associative, right operand gets parens.
        string shiftInShift = Calculator.FormatExpression("2 << (3 << 4)", ExpressionFormatOptions.Ascii);
        Assert.AreEqual("2 << (3 << 4)", shiftInShift);
        Assert.AreEqual(Calculator.Parse("2 << (3 << 4)"), Calculator.Parse(shiftInShift));

        // (1 and 2) < 3: logical AND has lower precedence (3) than comparison (5), so child requires parens.
        string andInCmp = Calculator.FormatExpression("(1 && 2) < 3", ExpressionFormatOptions.Ascii);
        Assert.AreEqual("(1 && 2) < 3", andInCmp);
        Assert.AreEqual(Calculator.Parse("(1 && 2) < 3"), Calculator.Parse(andInCmp));

        // (1 < 2) == 3: comparison chain vs parenthesized boolean-as-number comparison.
        // Under conjunctive chaining `1 < 2 == 3` would be a 3-operand chain, so `(1 < 2) == 3`
        // must retain grouping parentheses around `(1 < 2)`.
        string cmpInEq = Calculator.FormatExpression("(1 < 2) == 3", ExpressionFormatOptions.Ascii);
        Assert.AreEqual("(1 < 2) == 3", cmpInEq);
        Assert.AreEqual(Calculator.Parse("(1 < 2) == 3"), Calculator.Parse(cmpInEq));

        // 2 - (-3): right unary negation inside subtraction must be parenthesized to avoid `2 - -3`.
        string subNeg = Calculator.FormatExpression("2 - (-3)", ExpressionFormatOptions.Ascii);
        Assert.AreEqual("2 - (-3)", subNeg);
        Assert.AreEqual(Calculator.Parse("2 - (-3)"), Calculator.Parse(subNeg));
    }

    [TestMethod]
    public void FormatExpression_MathJax_ScientificNumbersAndCosmeticParentheses()
    {
        // Construct AST nodes with Double scientific value 1e-5 (which formats as "1E-05" via ToString("G"))
        var sciNum = new NumberNode(new MathValue(1e-5));

        // Exponent leading zero is stripped: 1E-05 -> 1 \times 10^{-5}
        Assert.AreEqual(@"1 \times 10^{-5}", Calculator.FormatExpression(sciNum, ExpressionFormatOptions.LaTeX));

        // 1e-5^2 parenthesizes the scientific base so it doesn't emit a double superscript
        var powNode = new BinaryNode(BinaryOperator.Power, sciNum, new NumberNode(new MathValue(new BigInteger(2))));
        Assert.AreEqual(@"(1 \times 10^{-5})^{2}", Calculator.FormatExpression(powNode, ExpressionFormatOptions.LaTeX));

        // pi % 1e-5 parenthesizes the scientific right operand
        var modNode = new BinaryNode(BinaryOperator.Modulo, new IdentifierNode("pi", ConstantKind.Pi), sciNum);
        Assert.AreEqual(@"\pi \bmod (1 \times 10^{-5})", Calculator.FormatExpression(modNode, ExpressionFormatOptions.LaTeX));

        // pi / 1e-5 in Fraction and Obelus division modes
        var divNode = new BinaryNode(BinaryOperator.Divide, new IdentifierNode("pi", ConstantKind.Pi), sciNum);
        Assert.AreEqual(@"\frac{\pi}{1 \times 10^{-5}}", Calculator.FormatExpression(divNode, ExpressionFormatOptions.LaTeX with { Division = DivisionSymbol.Fraction }));
        Assert.AreEqual(@"\pi \div (1 \times 10^{-5})", Calculator.FormatExpression(divNode, ExpressionFormatOptions.LaTeX with { Division = DivisionSymbol.Obelus }));

        // 1e5 vs 100000.0 parsed from text
        Assert.AreEqual("100000", Calculator.FormatExpression("1e5", ExpressionFormatOptions.LaTeX));
        Assert.AreEqual("100000.0", Calculator.FormatExpression("100000.0", ExpressionFormatOptions.LaTeX));

        // Double unary negation prints as -(-x) rather than --x
        Assert.AreEqual("-(-1)", Calculator.FormatExpression("--1", ExpressionFormatOptions.Ascii));
        Assert.AreEqual("−(−1)", Calculator.FormatExpression("--1", ExpressionFormatOptions.Typographic));
        Assert.AreEqual("-(-1)", Calculator.FormatExpression("--1", ExpressionFormatOptions.LaTeX));

        // Tall inner content (\frac, \sqrt) scales enclosing parentheses with \left( ... \right)
        Assert.AreEqual(@"2 \cdot \left(\sqrt{3} + 1\right)", Calculator.FormatExpression("2 * (sqrt(3) + 1)", ExpressionFormatOptions.LaTeX));

        // Underscore in unknown function name is escaped in \operatorname
        var customFn = new FunctionCallNode("my_func", [new NumberNode(new MathValue(new BigInteger(1)))]);
        Assert.AreEqual(@"\operatorname{my\_func}(1)", Calculator.FormatExpression(customFn, ExpressionFormatOptions.LaTeX));
    }

    [TestMethod]
    public void Factorial_EvaluationAndPrecedence_WorksCorrectly()
    {
        // Basic evaluation
        Assert.AreEqual(new BigInteger(120), Calculator.Evaluate("5!").IntVal);
        Assert.AreEqual(new BigInteger(120), Calculator.Evaluate("5 !").IntVal);
        Assert.AreEqual(new BigInteger(1), Calculator.Evaluate("0!").IntVal);
        Assert.AreEqual(new BigInteger(1), Calculator.Evaluate("1!").IntVal);

        // Ambiguity and comparisons
        Assert.AreEqual(1m, Calculator.Evaluate("5! != 3").DecVal);
        Assert.AreEqual(1m, Calculator.Evaluate("5! == 120").DecVal);
        Assert.AreEqual(1m, Calculator.Evaluate("5!==120").DecVal);

        // Precedence: postfix factorial binds tighter than prefix negate
        Assert.AreEqual(new BigInteger(-6), Calculator.Evaluate("-3!").IntVal);

        // Precedence with power
        // 2^3! parses as 2^(3!) = 2^6 = 64
        Assert.AreEqual(new BigInteger(64), Calculator.Evaluate("2^3!").IntVal);
        // (3!)^2 = 6^2 = 36; 3!^2 parsed as (3!)^2
        Assert.AreEqual(new BigInteger(36), Calculator.Evaluate("3!^2").IntVal);

        // Nested factorial (n!)!
        Assert.AreEqual(new BigInteger(720), Calculator.Evaluate("(3!)!").IntVal);

        // (n+1)!
        var options = new EvaluationOptions { Variables = new Dictionary<string, MathValue> { ["n"] = new(2m) } };
        Assert.AreEqual(new BigInteger(6), Calculator.Evaluate("(n+1)!", options).IntVal);
    }

    [TestMethod]
    public void Factorial_DoubleBang_ThrowsAmbiguitySyntaxError()
    {
        var ex = Assert.Throws<ExpressionException>(() => Calculator.Parse("3!!"));
        Assert.AreEqual(EvaluationError.InvalidSyntax, ex.Diagnostic.Code);
    }

    [TestMethod]
    public void Factorial_DisabledOption_RejectsPostfix()
    {
        var options = new EvaluationOptions { AllowPostfixFactorial = false };
        var ex = Assert.Throws<ExpressionException>(() => Calculator.Parse("5!", options));
        Assert.AreEqual(EvaluationError.InvalidSyntax, ex.Diagnostic.Code);
    }

    [TestMethod]
    public void Factorial_Formatting_RendersExpectedOutput()
    {
        var parsed = Calculator.Parse("5!");
        Assert.AreEqual("5!", Calculator.FormatExpression(parsed, ExpressionFormatOptions.Ascii));
        Assert.AreEqual("5!", Calculator.FormatExpression(parsed, ExpressionFormatOptions.Typographic));
        Assert.AreEqual("5!", Calculator.FormatExpression(parsed, ExpressionFormatOptions.LaTeX));

        var nested = Calculator.Parse("(3!)!");
        Assert.AreEqual("(3!)!", Calculator.FormatExpression(nested, ExpressionFormatOptions.Ascii));
        Assert.AreEqual("(3!)!", Calculator.FormatExpression(nested, ExpressionFormatOptions.Typographic));
        Assert.AreEqual("(3!)!", Calculator.FormatExpression(nested, ExpressionFormatOptions.LaTeX));
    }

    [TestMethod]
    public void ImplicitMultiplication_EvaluationAndAst_WorksCorrectly()
    {
        var options = new EvaluationOptions
        {
            Variables = new Dictionary<string, MathValue> { ["x"] = new(3m), ["y"] = new(4m) }
        };

        // Juxtaposition with variable and grouping
        Assert.AreEqual(6m, Calculator.Evaluate("2x", options).DecVal);
        Assert.AreEqual(12m, Calculator.Evaluate("x y", options).DecVal);
        Assert.AreEqual(new BigInteger(14), Calculator.Evaluate("2(3 + 4)").IntVal);
        Assert.AreEqual(new BigInteger(21), Calculator.Evaluate("(1 + 2)(3 + 4)").IntVal);

        // AST structure verification: IsImplicit is true
        var ast = Calculator.Parse("2x", options);
        Assert.IsInstanceOfType<BinaryNode>(ast);
        var bin = (BinaryNode)ast;
        Assert.AreEqual(BinaryOperator.Multiply, bin.Op);
        Assert.IsTrue(bin.IsImplicit);

        // Precedence: 2x^2 parses as 2*(x^2) = 2*9 = 18
        Assert.AreEqual(18m, Calculator.Evaluate("2x^2", options).DecVal);

        // Greek tokens as standalone symbols
        Assert.AreEqual(Calculator.Evaluate("2 * pi").DecVal, Calculator.Evaluate("2π").DecVal);
    }

    [TestMethod]
    public void ImplicitMultiplication_Formatting_RendersJuxtaposition()
    {
        var ast = Calculator.Parse("2(3 + 4)");
        Assert.AreEqual("2 (3 + 4)", Calculator.FormatExpression(ast, ExpressionFormatOptions.Ascii));
        Assert.AreEqual("2 (3 + 4)", Calculator.FormatExpression(ast, ExpressionFormatOptions.Typographic));
        Assert.AreEqual(@"2 (3 + 4)", Calculator.FormatExpression(ast, ExpressionFormatOptions.LaTeX));
    }

    [TestMethod]
    public void ImplicitMultiplication_DisabledOption_RejectsImplicit()
    {
        var options = new EvaluationOptions
        {
            AllowImplicitMultiplication = false,
            Variables = new Dictionary<string, MathValue> { ["x"] = new(3m) }
        };
        var ex = Assert.Throws<ExpressionException>(() => Calculator.Parse("2x", options));
        Assert.AreEqual(EvaluationError.InvalidSyntax, ex.Diagnostic.Code);

        // Strict mode on pi(2) reports unknown function instead of unexpected '('
        var exPi = Assert.Throws<ExpressionException>(() => Calculator.Parse("pi(2)", options));
        Assert.AreEqual(EvaluationError.UnknownFunction, exPi.Diagnostic.Code);
    }

    [TestMethod]
    public void ImplicitMultiplication_UnicodeVariablesAndKeywordBoundaries()
    {
        var options = new EvaluationOptions
        {
            Variables = new Dictionary<string, MathValue>
            {
                ["θ"] = new(5m),
                ["android"] = new(10m),
                ["x"] = new(2m)
            }
        };

        // Unicode variables like θ work cleanly
        Assert.AreEqual(10m, Calculator.Evaluate("2θ", options).DecVal);

        // Keyword boundary: android is a variable, not the keyword 'and'
        Assert.AreEqual(20m, Calculator.Evaluate("2 android", options).DecVal);

        // Function without parentheses reports requires parentheses error
        var exSin = Assert.Throws<ExpressionException>(() => Calculator.Parse("sin"));
        Assert.AreEqual(EvaluationError.InvalidSyntax, exSin.Diagnostic.Code);
    }

    [TestMethod]
    public void ImplicitMultiplication_SpansAndEquivalence()
    {
        var options = new EvaluationOptions
        {
            Variables = new Dictionary<string, MathValue>
            {
                ["x"] = new(3m)
            }
        };

        // Spans: (2)(3) is [0, 6), 2pi is [0, 3), -2x is [0, 3)
        var node1 = Calculator.Parse("(2)(3)");
        Assert.AreEqual(new TextSpan(0, 6), node1.Span);

        var node2 = Calculator.Parse("2pi");
        Assert.AreEqual(new TextSpan(0, 3), node2.Span);

        var node3 = Calculator.Parse("-2x", options);
        Assert.AreEqual(new TextSpan(0, 3), node3.Span);

        // AST Equivalence: 2*x and 2x produce structurally equal BinaryNodes
        var explicitNode = Calculator.Parse("2 * x", options);
        var implicitNode = Calculator.Parse("2x", options);
        Assert.AreEqual(explicitNode, implicitNode);
        Assert.AreEqual(explicitNode.GetHashCode(), implicitNode.GetHashCode());
    }

    [TestMethod]
    public void Ast_Equality_IgnoresSpansAcrossAllNodeTypes()
    {
        var num1 = new NumberNode(new MathValue(42m), new TextSpan(0, 2));
        var num2 = new NumberNode(new MathValue(42m), new TextSpan(10, 5));
        Assert.AreEqual(num1, num2);
        Assert.AreEqual(num1.GetHashCode(), num2.GetHashCode());

        var un1 = new UnaryNode(UnaryOperator.Negate, num1, new TextSpan(0, 3));
        var un2 = new UnaryNode(UnaryOperator.Negate, num2, new TextSpan(20, 4));
        Assert.AreEqual(un1, un2);
        Assert.AreEqual(un1.GetHashCode(), un2.GetHashCode());

        var id1 = new IdentifierNode("x", null, new TextSpan(0, 1));
        var id2 = new IdentifierNode("x", null, new TextSpan(5, 1));
        Assert.AreEqual(id1, id2);
        Assert.AreEqual(id1.GetHashCode(), id2.GetHashCode());

        var comp1 = new ComparisonChainNode(
            new ExpressionNode[] { num1, num2 },
            new ComparisonOperator[] { ComparisonOperator.Equal },
            new TextSpan(0, 5));
        var comp2 = new ComparisonChainNode(
            new ExpressionNode[] { num1, num2 },
            new ComparisonOperator[] { ComparisonOperator.Equal },
            new TextSpan(10, 8));
        Assert.AreEqual(comp1, comp2);
        Assert.AreEqual(comp1.GetHashCode(), comp2.GetHashCode());

        var call1 = new FunctionCallNode("sin", new ExpressionNode[] { num1 }, new TextSpan(0, 6));
        var call2 = new FunctionCallNode("sin", new ExpressionNode[] { num1 }, new TextSpan(20, 6));
        Assert.AreEqual(call1, call2);
        Assert.AreEqual(call1.GetHashCode(), call2.GetHashCode());
    }

    [TestMethod]
    public void IdentifierNode_ConstantsAndVariables_EqualityAndHashEquivalence()
    {
        // 2PI, 2pi, and 2π compare equal and hash equal
        var piUpper = Calculator.Parse("PI");
        var piLower = Calculator.Parse("pi");
        var piGreek = Calculator.Parse("π");
        Assert.AreEqual(piUpper, piLower);
        Assert.AreEqual(piLower, piGreek);
        Assert.AreEqual(piUpper.GetHashCode(), piLower.GetHashCode());
        Assert.AreEqual(piLower.GetHashCode(), piGreek.GetHashCode());

        // Function call names compare equal regardless of casing: SIN(x) == sin(x)
        var sinUpper = Calculator.Parse("SIN(1)");
        var sinLower = Calculator.Parse("sin(1)");
        Assert.AreEqual(sinUpper, sinLower);
        Assert.AreEqual(sinUpper.GetHashCode(), sinLower.GetHashCode());

        // Variable identifiers compare case-insensitively
        var varUpper = new IdentifierNode("ABC");
        var varLower = new IdentifierNode("abc");
        Assert.AreEqual(varUpper, varLower);
        Assert.AreEqual(varUpper.GetHashCode(), varLower.GetHashCode());
    }

    [TestMethod]
    public void DeepTrees_ParseOrLimitSafelyWithoutCrashing()
    {
        // Deep nested function calls: sin(sin(...(1)...))
        int depth = 200;
        string deepSin = new string('s', 0);
        var sbSin = new StringBuilder();
        for (int i = 0; i < depth; i++) sbSin.Append("sin(");
        sbSin.Append('1');
        for (int i = 0; i < depth; i++) sbSin.Append(')');
        string sinExpr = sbSin.ToString();

        // 200-deep nesting exceeds MaxNestingDepth (128) and rejects with CalculationLimitException
        Assert.Throws<CalculationLimitException>(() => Calculator.Parse(sinExpr));

        // 200-deep parenthesised comparisons: (((1 == 1) == 1)...)
        var sbComp = new StringBuilder();
        for (int i = 0; i < depth; i++) sbComp.Append('(');
        sbComp.Append("1 == 1");
        for (int i = 0; i < depth; i++) sbComp.Append(" == 1)");
        string compExpr = sbComp.ToString();

        Assert.Throws<CalculationLimitException>(() => Calculator.Parse(compExpr));
    }

    [TestMethod]
    public void FlatSum_5000Terms_EvaluatesOrLimitsSafelyWithoutCrashing()
    {
        // Construct a flat chain of 5,000 terms: 1 + 1 + 1 + ...
        // Since MaxInputLength is 4096, parsing from raw string limits early,
        // but evaluating a manually built 5,000-term left-spine BinaryNode tests the spine visitor.
        ExpressionNode root = new NumberNode(new MathValue(1m));
        for (int i = 1; i < 5000; i++)
        {
            root = new BinaryNode(BinaryOperator.Add, root, new NumberNode(new MathValue(1m)));
        }

        // Height of flat left spine of leaves is 2 (1 for leaf + 1 for binary operator)
        Assert.AreEqual(2, root.Height);

        // Manually built tree with 10,000 nodes exceeds nodeCount safety bound in AstValidator
        Assert.Throws<CalculationLimitException>(() => Calculator.Evaluate(root));
    }

    [TestMethod]
    public void AstValidator_RejectsMalformedManualAsts()
    {
        // Null child in unary node
        var invalidUnary = new UnaryNode(UnaryOperator.Negate, null!);
        Assert.Throws<ExpressionException>(() => Calculator.Evaluate(invalidUnary));

        // Malformed ComparisonChainNode: Operators.Count != Operands.Count - 1
        var invalidComp = new ComparisonChainNode(
            new ExpressionNode[] { new NumberNode(new MathValue(1m)), new NumberNode(new MathValue(2m)) },
            Array.Empty<ComparisonOperator>());
        Assert.Throws<ExpressionException>(() => Calculator.Evaluate(invalidComp));

        // Function call with invalid arity
        var invalidCall = new FunctionCallNode("sqrt", Array.Empty<ExpressionNode>());
        Assert.Throws<ExpressionException>(() => Calculator.Evaluate(invalidCall));
    }
}
