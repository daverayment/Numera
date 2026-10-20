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
        return node.Value.ToString(CultureInfo.InvariantCulture);
    }

    public string Visit(IdentifierNode node)
    {
        if (node.Name.Equals("pi", StringComparison.OrdinalIgnoreCase) || node.Name == "π" || node.Name == "Π")
        {
            return @"\pi";
        }

        if (node.Name.Equals("tau", StringComparison.OrdinalIgnoreCase) || node.Name == "τ" || node.Name == "Τ")
        {
            return @"\tau";
        }

        if (node.Name.Equals("phi", StringComparison.OrdinalIgnoreCase) || node.Name == "ϕ" || node.Name == "Φ")
        {
            return @"\phi";
        }

        return node.Name;
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
            _ => operandText,
        };
    }

    public string Visit(BinaryNode node)
    {
        // 2D fraction formatting if division
        if (node.Op == BinaryOperator.Divide && _options.Division == DivisionSymbol.Fraction)
        {
            string num = node.Left.Accept(this);
            string den = node.Right.Accept(this);
            return $@"\frac{{{num}}}{{{den}}}";
        }

        if (node.Op == BinaryOperator.Power)
        {
            string baseText = FormatChild(node.Left, node, isRight: false);
            string expText = node.Right.Accept(this);
            return $"{baseText}^{{{expText}}}";
        }

        string left = FormatChild(node.Left, node, isRight: false);
        string right = FormatChild(node.Right, node, isRight: true);
        string opStr = GetBinaryOperatorLatex(node.Op);

        return $"{left} {opStr} {right}";
    }

    public string Visit(ComparisonChainNode node)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < node.Operands.Count; i++)
        {
            if (i > 0)
            {
                sb.Append(' ');
                sb.Append(GetComparisonOperatorLatex(node.Operators[i - 1]));
                sb.Append(' ');
            }

            var operand = node.Operands[i];
            sb.Append(operand.Accept(this));
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

        // Standard trigonometric / common functions
        if (IsLatexFunction(fn))
        {
            string arg = string.Join(", ", node.Arguments.Select(a => a.Accept(this)));
            return $@"\{fn.ToLowerInvariant()}({arg})";
        }

        string defaultArgs = string.Join(", ", node.Arguments.Select(a => a.Accept(this)));
        return $@"\operatorname{{{fn}}}({defaultArgs})";
    }

    private static bool IsLatexFunction(string fn) => fn.ToLowerInvariant() switch
    {
        "sin" or "cos" or "tan" or "csc" or "sec" or "cot" or
        "sinh" or "cosh" or "tanh" or "coth" or
        "ln" or "log" or "exp" or "min" or "max" or "gcd" or "deg" => true,
        _ => false,
    };

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
        _ => op.ToString(),
    };

    private static string GetComparisonOperatorLatex(ComparisonOperator op) => op switch
    {
        ComparisonOperator.Equal => "=",
        ComparisonOperator.NotEqual => @"\neq",
        ComparisonOperator.LessThan => "<",
        ComparisonOperator.LessThanOrEqual => @"\le",
        ComparisonOperator.GreaterThan => ">",
        ComparisonOperator.GreaterThanOrEqual => @"\ge",
        _ => op.ToString(),
    };

    private string FormatChild(ExpressionNode child, ExpressionNode parent, bool isRight)
    {
        string text = child.Accept(this);
        if (NeedsParentheses(child, parent, isRight))
        {
            return $@"({text})";
        }

        return text;
    }

    private static bool NeedsParentheses(ExpressionNode child, ExpressionNode parent, bool isRight)
    {
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

            if (isRight && parent is BinaryNode { Op: BinaryOperator.Subtract or BinaryOperator.Divide or BinaryOperator.Modulo })
            {
                return true;
            }
        }

        return false;
    }

    private static int GetPrecedence(ExpressionNode node) => node switch
    {
        BinaryNode b => b.Op switch
        {
            BinaryOperator.Power => 9,
            BinaryOperator.Multiply or BinaryOperator.Divide or BinaryOperator.Modulo => 7,
            BinaryOperator.Add or BinaryOperator.Subtract => 6,
            BinaryOperator.ShiftLeft or BinaryOperator.ShiftRight => 5,
            BinaryOperator.LogicalAnd => 3,
            BinaryOperator.LogicalXor => 2,
            BinaryOperator.LogicalOr => 1,
            _ => 0,
        },
        UnaryNode => 8,
        ComparisonChainNode => 4,
        _ => 10,
    };
}
