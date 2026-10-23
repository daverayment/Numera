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
        if (_options.UseGreekSymbols && node.Constant is ConstantKind kind)
        {
            return kind switch
            {
                ConstantKind.Pi => "π",
                ConstantKind.Tau => "τ",
                ConstantKind.Phi => "ϕ",
                ConstantKind.True => "true",
                ConstantKind.False => "false",
                _ => node.Name,
            };
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
            UnaryOperator.Factorial => operandText + "!",
            UnaryOperator.Percent => operandText + "%",
            _ => throw new ArgumentOutOfRangeException(nameof(node), node.Op, "Unknown unary operator."),
        };
    }

    public string Visit(BinaryNode node)
    {
        // Power nodes (and nodes without a flattenable left child) take the direct path. Power must
        // not enter the loop below, since its left child is not part of the spine (see BinaryNode.Height).
        if (node.Op == BinaryOperator.Power || node.Left is not BinaryNode { Op: not BinaryOperator.Power })
        {
            return Compose(node, node.Left.Accept(this));
        }

        // Format the left spine iteratively so long flat chains (e.g. thousands of additive terms)
        // cannot overflow the call stack. Each step receives the formatted text of its left child.
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
    /// Formats one binary node given the already-formatted text of its left operand (before any
    /// parenthesisation, which depends on this node). The right operand is formatted recursively.
    /// </summary>
    private string Compose(BinaryNode node, string rawLeft)
    {
        string leftText = Parenthesize(rawLeft, node.Left, node, isRight: false);

        if (node.IsImplicit && node.Op == BinaryOperator.Multiply)
        {
            // Juxtaposition binds tighter than explicit * / %, so such a left operand must be grouped or
            // it would reparse with a different structure, e.g. (a/b)x as a/(bx).
            string leftImplicit = node.Left is BinaryNode { Op: BinaryOperator.Multiply or BinaryOperator.Divide or BinaryOperator.Modulo, IsImplicit: false }
                ? $"({rawLeft})"
                : leftText;
            string rightImplicit = FormatChild(node.Right, node, isRight: true);

            // Avoid ambiguities like 2(3) -> 23, 2(-x) -> 2-x, or x(y+1) looking like a function call.
            bool isAmbiguousJuxtaposition =
                (node.Left is IdentifierNode && rightImplicit.StartsWith('(')) ||
                (rightImplicit.Length > 0 && !rightImplicit.StartsWith('(') && !char.IsLetter(rightImplicit[0]));

            if (isAmbiguousJuxtaposition)
            {
                string explicitOp = GetBinaryOperatorSymbol(BinaryOperator.Multiply);
                return $"{leftImplicit} {explicitOp} {rightImplicit}";
            }

            return $"{leftImplicit} {rightImplicit}";
        }

        if (node.Op == BinaryOperator.Power && _options.UseSuperscriptPowers && TryGetSuperscript(node.Right, out string? superscripts))
        {
            return leftText + superscripts;
        }

        string left = leftText;
        string right = FormatChild(node.Right, node, isRight: true);
        string opStr = GetBinaryOperatorSymbol(node.Op);

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
                sb.Append(GetComparisonOperatorSymbol(node.Operators[i - 1]));
                sb.Append(' ');
            }

            var operand = node.Operands[i];
            string operandText = operand.Accept(this);
            if (GetPrecedence(operand) <= compPrec)
            {
                sb.Append('(').Append(operandText).Append(')');
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
        BinaryOperator.Of => "of",
        _ => throw new ArgumentOutOfRangeException(nameof(op), op, "Unknown binary operator."),
    };

    private string GetComparisonOperatorSymbol(ComparisonOperator op) => op switch
    {
        ComparisonOperator.Equal => "==",
        ComparisonOperator.NotEqual => "!=",
        ComparisonOperator.LessThan => "<",
        ComparisonOperator.LessThanOrEqual => _options.Style == ExpressionFormatStyle.TypographicUnicode ? "≤" : "<=",
        ComparisonOperator.GreaterThan => ">",
        ComparisonOperator.GreaterThanOrEqual => _options.Style == ExpressionFormatStyle.TypographicUnicode ? "≥" : ">=",
        _ => throw new ArgumentOutOfRangeException(nameof(op), op, "Unknown comparison operator."),
    };

    private string FormatChild(ExpressionNode child, ExpressionNode parent, bool isRight)
    {
        return Parenthesize(child.Accept(this), child, parent, isRight);
    }

    private static string Parenthesize(string text, ExpressionNode child, ExpressionNode parent, bool isRight) =>
        NeedsParentheses(child, parent, isRight) ? $"({text})" : text;

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

            //At equal precedence, right operand of binary operator defaults to parentheses
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
        _ => 12, // Number, Identifier, FunctionCall are primary
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
