using System.Globalization;
using System.Text;

namespace Numera.Ast;

/// <summary>
/// Visitor that formats an <see cref="ExpressionNode"/> abstract syntax tree into
/// MathJax / KaTeX / LaTeX markup.
/// </summary>
internal sealed class MathJaxVisitor : IExpressionVisitor<string>
{
    private readonly ExpressionFormatOptions _options;

    public MathJaxVisitor(ExpressionFormatOptions options)
    {
        _options = options;
    }

    public string Visit(NumberNode node)
    {
        if (node.Value.Kind == MathValue.DataType.Double)
        {
            double d = node.Value.DoubleValue;
            if (double.IsNaN(d))
            {
                return @"\operatorname{NaN}";
            }

            if (double.IsPositiveInfinity(d))
            {
                return @"\infty";
            }

            if (double.IsNegativeInfinity(d))
            {
                return @"-\infty";
            }

            string raw = d.ToString("G", CultureInfo.InvariantCulture);
            return FormatScientificLatex(raw);
        }

        string text = node.Value.ToString(CultureInfo.InvariantCulture);
        return FormatScientificLatex(text);
    }

    private static string FormatScientificLatex(string text)
    {
        int eIndex = text.IndexOfAny(['e', 'E']);
        if (eIndex < 0)
        {
            return text;
        }

        string mantissa = text[..eIndex];
        int exp = int.Parse(text.AsSpan(eIndex + 1), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);

        return $@"{mantissa} \times 10^{{{exp.ToString(CultureInfo.InvariantCulture)}}}";
    }

    public string Visit(IdentifierNode node)
    {
        if (node.Constant is ConstantKind kind)
        {
            return kind switch
            {
                ConstantKind.Pi => @"\pi",
                ConstantKind.Tau => @"\tau",
                ConstantKind.Phi => @"\phi",
                ConstantKind.E => "e",
                ConstantKind.True => @"\mathbf{true}",
                ConstantKind.False => @"\mathbf{false}",
                _ => node.Name,
            };
        }

        // Adjacent italic letters read as a product of single-letter variables, so multi-
        // letter names are set upright as a single operator-style token.
        return node.Name.Length > 1 && node.Name.All(char.IsLetter)
            ? $@"\operatorname{{{node.Name}}}"
            : node.Name;
    }

    public string Visit(UnaryNode node)
    {
        string operandText = FormatChild(node.Operand, node, isRight: false);
        return node.Op switch
        {
            UnaryOperator.Negate => "-" + operandText,
            UnaryOperator.Positive => "+" + operandText,
            UnaryOperator.LogicalNot => @"\neg " + operandText,
            UnaryOperator.BitwiseNot => @"\sim " + operandText,
            UnaryOperator.Factorial => operandText + "!",
            UnaryOperator.Percent => operandText + @"\%",
            _ => throw new ArgumentOutOfRangeException(nameof(node), node.Op, "Unknown unary operator."),
        };
    }

    public string Visit(BinaryNode node)
    {
        // Power nodes (and nodes without a flattenable left child) take the direct path.
        // Power must not enter the loop below, since its left child is not part of the
        // spine (see BinaryNode.Height).
        if (node.Op == BinaryOperator.Power ||
            node.Left is not BinaryNode { Op: not BinaryOperator.Power })
        {
            return Compose(node, node.Left.Accept(this));
        }

        // Format the left spine iteratively so long flat chains (e.g. thousands of
        // additive terms) cannot overflow the call stack. Each step receives the
        // formatted text of its left child.
        var spine = new Stack<BinaryNode>();
        ExpressionNode current = node;
        while (current is BinaryNode { Op: not BinaryOperator.Power } bin)
        {
            spine.Push(bin);
            current = bin.Left;
        }

        string text = current.Accept(this);
        while (spine.Count > 0)
        {
            text = Compose(spine.Pop(), text);
        }

        return text;
    }

    /// <summary>
    /// Formats one binary node given the already-formatted text of its left operand
    /// (before any parenthesisation, which depends on this node). The right operand is
    /// formatted recursively.
    /// </summary>
    private string Compose(BinaryNode node, string rawLeft)
    {
        if (node.IsImplicit && node.Op == BinaryOperator.Multiply)
        {
            // Juxtaposition binds tighter than explicit * / %, so such a left operand
            // must be grouped or it would reparse with a different structure, e.g.
            // (a/b)x as a/(bx). A \frac is self-grouping.
            string leftImplicit = LeftNeedsGroupingForJuxtaposition(node.Left)
                ? Wrap(rawLeft)
                : Parenthesize(rawLeft, node.Left, node, isRight: false);
            string rightImplicit = FormatChild(node.Right, node, isRight: true);

            return CanJuxtapose(node.Left, rightImplicit)
                ? $"{leftImplicit} {rightImplicit}"
                : $"{leftImplicit} {GetBinaryOperatorLatex(BinaryOperator.Multiply)} {rightImplicit}";
        }

        // 2D fraction formatting if division
        if (node.Op == BinaryOperator.Divide &&
            _options.Division == DivisionSymbol.Fraction)
        {
            string num = rawLeft;
            string den = node.Right.Accept(this);
            return $@"\frac{{{num}}}{{{den}}}";
        }

        if (node.Op == BinaryOperator.Power)
        {
            string expText = node.Right.Accept(this);
            if (node.Left is BinaryNode { Op: BinaryOperator.Divide } &&
                _options.Division == DivisionSymbol.Fraction)
            {
                string baseText = rawLeft;
                return $@"\left({baseText}\right)^{{{expText}}}";
            }

            string normalBase = Parenthesize(rawLeft, node.Left, node, isRight: false);
            return $"{normalBase}^{{{expText}}}";
        }

        string left = Parenthesize(rawLeft, node.Left, node, isRight: false);
        string right = FormatChild(node.Right, node, isRight: true);
        string opStr = GetBinaryOperatorLatex(node.Op);

        return $"{left} {opStr} {right}";
    }

    public string Visit(ComparisonChainNode node)
    {
        var sb = new StringBuilder();
        int compPrec = GetPrecedence(node);
        for (int i = 0; i < node.Operands.Count; i++)
        {
            if (i > 0)
            {
                sb.Append(' ');
                sb.Append(GetComparisonOperatorLatex(node.Operators[i - 1]));
                sb.Append(' ');
            }

            var operand = node.Operands[i];
            string operandText = operand.Accept(this);
            if (GetPrecedence(operand) <= compPrec)
            {
                sb.Append(Wrap(operandText));
            }
            else
            {
                sb.Append(operandText);
            }
        }

        return sb.ToString();
    }

    public string Visit(FunctionCallNode node)
    {
        string fn = node.FunctionName;
        if (fn.Equals("sqrt", StringComparison.OrdinalIgnoreCase) && node.Arguments.Count == 1)
        {
            return $@"\sqrt{{{node.Arguments[0].Accept(this)}}}";
        }

        if (fn.Equals("cbrt", StringComparison.OrdinalIgnoreCase) && node.Arguments.Count == 1)
        {
            return $@"\sqrt[3]{{{node.Arguments[0].Accept(this)}}}";
        }

        if (fn.Equals("abs", StringComparison.OrdinalIgnoreCase) && node.Arguments.Count == 1)
        {
            return $@"\left|{node.Arguments[0].Accept(this)}\right|";
        }

        if (fn.Equals("floor", StringComparison.OrdinalIgnoreCase) && node.Arguments.Count == 1)
        {
            return $@"\left\lfloor {node.Arguments[0].Accept(this)}\right\rfloor";
        }

        if (fn.Equals("ceil", StringComparison.OrdinalIgnoreCase) && node.Arguments.Count == 1)
        {
            return $@"\left\lceil {node.Arguments[0].Accept(this)}\right\rceil";
        }

        // Standard trigonometric / common functions.
        if (TryGetLatexFunctionName(fn, out string? latexFn))
        {
            string arg = string.Join(", ", node.Arguments.Select(a => a.Accept(this)));
            return $@"\{latexFn}({arg})";
        }

        string defaultArgs = string.Join(", ", node.Arguments.Select(a => a.Accept(this)));
        string escapedFn = fn.Replace("_", @"\_", StringComparison.Ordinal);
        return $@"\operatorname{{{escapedFn}}}({defaultArgs})";
    }

    public string Visit(AssignmentNode node) =>
        $"{node.VariableName} = {node.Value.Accept(this)}";

    private static bool TryGetLatexFunctionName(string fn, out string? latexFn)
    {
        latexFn = fn.ToLowerInvariant() switch
        {
            "asin" => "arcsin",
            "acos" => "arccos",
            "atan" => "arctan",
            "sin" or "cos" or "tan" or "csc" or "sec" or "cot" or
            "sinh" or "cosh" or "tanh" or "coth" or
            "ln" or "log" or "exp" or "min" or "max" or "gcd" or "deg" => fn.ToLowerInvariant(),
            _ => null,
        };

        return latexFn != null;
    }

    private string GetBinaryOperatorLatex(BinaryOperator op) => op switch
    {
        BinaryOperator.Add => "+",
        BinaryOperator.Subtract => "-",
        BinaryOperator.Multiply => _options.Multiplication switch
        {
            MultiplicationSymbol.Cross => @"\times",
            MultiplicationSymbol.Dot => @"\cdot",
            _ => "*",
        },
        BinaryOperator.Divide => @"\div",
        BinaryOperator.Modulo => @"\bmod",
        BinaryOperator.ShiftLeft => @"\ll",
        BinaryOperator.ShiftRight => @"\gg",
        BinaryOperator.LogicalAnd => @"\land",
        BinaryOperator.LogicalOr => @"\lor",
        BinaryOperator.LogicalXor => @"\oplus",
        BinaryOperator.Of => @"\mathbin{\text{of}}",
        _ => throw new ArgumentOutOfRangeException(nameof(op), op, "Unknown binary operator."),
    };

    private static string GetComparisonOperatorLatex(ComparisonOperator op) => op switch
    {
        ComparisonOperator.Equal => "=",
        ComparisonOperator.NotEqual => @"\neq",
        ComparisonOperator.LessThan => "<",
        ComparisonOperator.LessThanOrEqual => @"\le",
        ComparisonOperator.GreaterThan => ">",
        ComparisonOperator.GreaterThanOrEqual => @"\ge",
        _ => throw new ArgumentOutOfRangeException(nameof(op), op, "Unknown comparison operator."),
    };

    private string FormatChild(ExpressionNode child, ExpressionNode parent, bool isRight)
    {
        return Parenthesize(child.Accept(this), child, parent, isRight);
    }

    private bool LeftNeedsGroupingForJuxtaposition(ExpressionNode left) =>
        left is BinaryNode { Op: BinaryOperator.Multiply or BinaryOperator.Divide or BinaryOperator.Modulo, IsImplicit: false } &&
        !(left is BinaryNode { Op: BinaryOperator.Divide } && _options.Division == DivisionSymbol.Fraction);

    /// <summary>
    /// Decides whether the right operand can sit directly beside the left one without
    /// being misread.
    /// </summary>
    private static bool CanJuxtapose(ExpressionNode left, string right)
    {
        if (right.Length == 0)
        {
            return false;
        }

        // A fraction beside a number reads as a mixed number (2 \frac{1}{2} is 2.5, not 1).
        if (right.StartsWith(@"\frac", StringComparison.Ordinal))
        {
            return false;
        }

        // A parenthesised group after an identifier or constant reads as a function call.
        if (right.StartsWith('(') ||
            right.StartsWith(@"\left(", StringComparison.Ordinal))
        {
            return left is not IdentifierNode;
        }

        // Otherwise the right side must start with a letter or a LaTeX command
        // (\pi, \sin, \sqrt...).
        return right[0] == '\\' || char.IsLetter(right[0]);
    }

    private static string Wrap(string text) =>
        ContainsTallLatex(text) ? $@"\left({text}\right)" : $"({text})";

    private string Parenthesize(string text, ExpressionNode child, ExpressionNode parent, bool isRight) =>
        NeedsParentheses(child, parent, isRight) ? Wrap(text) : text;

    private static bool ContainsTallLatex(string text) =>
        text.Contains(@"\frac", StringComparison.Ordinal) ||
        text.Contains(@"\sqrt", StringComparison.Ordinal);

    private bool NeedsParentheses(ExpressionNode child, ExpressionNode parent, bool isRight)
    {
        // When division is rendered as a 2D fraction (\frac{a}{b}), it has natural visual grouping
        // and does not need parentheses even inside lower/equal precedence contexts.
        if (child is BinaryNode { Op: BinaryOperator.Divide } && _options.Division == DivisionSymbol.Fraction)
        {
            return false;
        }

        int childPrec = GetPrecedence(child);
        int parentPrec = GetPrecedence(parent);

        if (childPrec < parentPrec)
        {
            return true;
        }

        if (childPrec == parentPrec)
        {
            if (parent is BinaryNode { Op: BinaryOperator.Power })
            {
                return !isRight;
            }

            // At equal precedence, right operand of binary operator defaults to parentheses
            // unless the child is a binary node with the same associative operator.
            if (isRight && parent is BinaryNode parentBin)
            {
                if (child is BinaryNode childBin && childBin.Op == parentBin.Op && IsAssociative(parentBin.Op))
                {
                    return false;
                }

                return true;
            }

            if (parent is UnaryNode { Op: UnaryOperator.Negate or UnaryOperator.Positive } &&
                child is UnaryNode { Op: UnaryOperator.Negate or UnaryOperator.Positive })
            {
                return true;
            }

            if (parent is UnaryNode { Op: UnaryOperator.Factorial } &&
                child is UnaryNode { Op: UnaryOperator.Factorial })
            {
                return true;
            }
        }

        // Avoid ambiguity like a - (-b) or a + (-b)
        if (isRight && child is UnaryNode && parent is BinaryNode { Op: BinaryOperator.Add or BinaryOperator.Subtract or BinaryOperator.Divide or BinaryOperator.Modulo })
        {
            return true;
        }

        return false;
    }

    private static bool IsAssociative(BinaryOperator op) => op switch
    {
        BinaryOperator.Add or BinaryOperator.Multiply or
        BinaryOperator.LogicalAnd or BinaryOperator.LogicalOr or BinaryOperator.LogicalXor => true,
        _ => false,
    };

    private static bool RendersScientific(NumberNode n) =>
        n.Value.Kind == MathValue.DataType.Double &&
        double.IsFinite(n.Value.DoubleValue) &&
        n.Value.DoubleValue.ToString("G", CultureInfo.InvariantCulture).AsSpan().IndexOfAny('E', 'e') >= 0;

    private static int GetPrecedence(ExpressionNode node) => node switch
    {
        BinaryNode b => b.Op switch
        {
            BinaryOperator.Power => 10,
            BinaryOperator.Multiply or BinaryOperator.Divide or BinaryOperator.Modulo or BinaryOperator.Of => 8,
            BinaryOperator.Add or BinaryOperator.Subtract => 7,
            BinaryOperator.ShiftLeft or BinaryOperator.ShiftRight => 6,
            BinaryOperator.LogicalAnd => 3,
            BinaryOperator.LogicalXor => 2,
            BinaryOperator.LogicalOr => 1,
            _ => 0,
        },
        UnaryNode u => u.Op switch
        {
            UnaryOperator.Factorial or UnaryOperator.Percent => 11,
            UnaryOperator.LogicalNot => 4,
            _ => 9,
        },
        ComparisonChainNode => 5,
        NumberNode n when RendersScientific(n) => 8,
        _ => 12,
    };
}
