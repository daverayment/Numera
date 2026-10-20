using System.Globalization;
using System.Numerics;
using System.Text;

namespace Numera.Ast;

/// <summary>
/// Visitor that formats an <see cref="ExpressionNode"/> abstract syntax tree into
/// linear typographic Unicode or clean normalized ASCII text.
/// </summary>
internal sealed class TypographicVisitor : IExpressionVisitor<string>
{
    private readonly ExpressionFormatOptions _options;

    public TypographicVisitor(ExpressionFormatOptions options)
    {
        _options = options;
    }

    public string Visit(NumberNode node)
    {
        string text = node.Value.ToString(CultureInfo.InvariantCulture);
        if (_options.UseUnicodeMinus && text.StartsWith('-'))
        {
            return "−" + text[1..];
        }

        return text;
    }

    public string Visit(IdentifierNode node)
    {
        if (_options.UseGreekSymbols)
        {
            if (node.Name.Equals("pi", StringComparison.OrdinalIgnoreCase) || node.Name == "π" || node.Name == "Π")
            {
                return "π";
            }

            if (node.Name.Equals("tau", StringComparison.OrdinalIgnoreCase) || node.Name == "τ" || node.Name == "Τ")
            {
                return "τ";
            }
        }

        return node.Name;
    }

    public string Visit(UnaryNode node)
    {
        string operandText = FormatChild(node.Operand, node, isRight: false);
        return node.Op switch
        {
            UnaryOperator.Negate => (_options.UseUnicodeMinus ? "−" : "-") + operandText,
            UnaryOperator.Positive => "+" + operandText,
            UnaryOperator.LogicalNot => "!" + operandText,
            UnaryOperator.BitwiseNot => "~" + operandText,
            _ => operandText,
        };
    }

    public string Visit(BinaryNode node)
    {
        if (node.Op == BinaryOperator.Power && _options.UseSuperscriptPowers && TryGetSuperscript(node.Right, out string? superscripts))
        {
            string leftText = FormatChild(node.Left, node, isRight: false);
            return leftText + superscripts;
        }

        string left = FormatChild(node.Left, node, isRight: false);
        string right = FormatChild(node.Right, node, isRight: true);
        string opStr = GetBinaryOperatorSymbol(node.Op);

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
                sb.Append(GetComparisonOperatorSymbol(node.Operators[i - 1]));
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
        if (fn.Equals("sqrt", StringComparison.OrdinalIgnoreCase) && node.Arguments.Count == 1 && _options.Style == ExpressionFormatStyle.TypographicUnicode)
        {
            return $"√({node.Arguments[0].Accept(this)})";
        }

        string args = string.Join(", ", node.Arguments.Select(a => a.Accept(this)));
        return $"{fn}({args})";
    }

    private string GetBinaryOperatorSymbol(BinaryOperator op) => op switch
    {
        BinaryOperator.Add => "+",
        BinaryOperator.Subtract => _options.UseUnicodeMinus ? "−" : "-",
        BinaryOperator.Multiply => _options.Multiplication switch
        {
            MultiplicationSymbol.Cross => "×",
            MultiplicationSymbol.Dot => "·",
            _ => "*",
        },
        BinaryOperator.Divide => _options.Division switch
        {
            DivisionSymbol.Obelus => "÷",
            _ => "/",
        },
        BinaryOperator.Modulo => "%",
        BinaryOperator.Power => "^",
        BinaryOperator.ShiftLeft => "<<",
        BinaryOperator.ShiftRight => ">>",
        BinaryOperator.LogicalAnd => "&&",
        BinaryOperator.LogicalOr => "||",
        BinaryOperator.LogicalXor => "xor",
        _ => op.ToString(),
    };

    private string GetComparisonOperatorSymbol(ComparisonOperator op) => op switch
    {
        ComparisonOperator.Equal => "==",
        ComparisonOperator.NotEqual => "!=",
        ComparisonOperator.LessThan => "<",
        ComparisonOperator.LessThanOrEqual => _options.Style == ExpressionFormatStyle.TypographicUnicode ? "≤" : "<=",
        ComparisonOperator.GreaterThan => ">",
        ComparisonOperator.GreaterThanOrEqual => _options.Style == ExpressionFormatStyle.TypographicUnicode ? "≥" : ">=",
        _ => op.ToString(),
    };

    private string FormatChild(ExpressionNode child, ExpressionNode parent, bool isRight)
    {
        string text = child.Accept(this);
        if (NeedsParentheses(child, parent, isRight))
        {
            return $"({text})";
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
            // For power (right-associative): left child needs parens: (a^b)^c
            if (parent is BinaryNode { Op: BinaryOperator.Power })
            {
                return !isRight;
            }

            // For subtraction/division (left-associative non-commutative): right child needs parens: a - (b - c)
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
        _ => 10, // Number, Identifier, FunctionCall are primary
    };

    private bool TryGetSuperscript(ExpressionNode node, out string? result)
    {
        result = null;
        if (node is NumberNode { Value.Type: MathValue.DataType.Integer } numNode)
        {
            return TryFormatSuperscript(numNode.Value.IntVal.ToString(CultureInfo.InvariantCulture), out result);
        }

        if (node is UnaryNode { Op: UnaryOperator.Negate, Operand: NumberNode { Value.Type: MathValue.DataType.Integer } negNum })
        {
            return TryFormatSuperscript("-" + negNum.Value.IntVal.ToString(CultureInfo.InvariantCulture), out result);
        }

        return false;
    }

    private static bool TryFormatSuperscript(string digits, out string? result)
    {
        result = null;
        var sb = new StringBuilder(digits.Length);
        foreach (char c in digits)
        {
            char sup = c switch
            {
                '0' => '⁰',
                '1' => '¹',
                '2' => '²',
                '3' => '³',
                '4' => '⁴',
                '5' => '⁵',
                '6' => '⁶',
                '7' => '⁷',
                '8' => '⁸',
                '9' => '⁹',
                '-' => '⁻',
                _ => '\0',
            };

            if (sup == '\0')
            {
                return false;
            }

            sb.Append(sup);
        }

        result = sb.ToString();
        return true;
    }
}
