namespace Numera.Ast;

/// <summary>
/// Single source of truth for how percentages behave, shared by the parser, validator and evaluator.
/// </summary>
/// <remarks>
/// Numera follows what people expect from a typed percentage, not what a programming language would do:
/// <list type="number">
/// <item>A standalone <c>B%</c> is <c>B / 100</c>.</item>
/// <item>In <c>L + R%</c> or <c>L - R%</c> the percentage is a rate applied to <c>L</c>, and the result is
/// <c>L * (1 +/- r)</c>. Proportional scaling keeps every sign combination consistent with "R% of L" (a fee on a
/// debt grows the debt) and needs no special cases for negative bases.</item>
/// <item><c>L</c> is everything to the left, so chains compound like a running total: <c>100 + 10% + 20%</c> is 132.</item>
/// <item>If <c>L</c> is itself a percentage the arithmetic is plain, so <c>10% + 20%</c> is 0.3.</item>
/// <item>The rule looks at the parsed shape, never at the text, so parentheses and operator precedence decide.
/// Percentages are not tracked through arithmetic: in <c>10% * 2 + 5%</c> the left is a product, so it is marked up
/// (0.2 * 1.05). That keeps <c>200 * 15% + 10%</c> (a fee with 10% tax) working.</item>
/// <item><c>%</c> is a percentage unless an operand follows it. <c>10 % 3</c> is modulo, <c>10% + 3</c> is a
/// percentage. The words 'of', 'and', 'or' and 'xor' are not operands.</item>
/// </list>
/// </remarks>
internal static class PercentRules
{
    /// <summary>
    /// Whether the node is a percentage, ignoring unary signs, so that <c>-10%</c> is a negative rate
    /// and <c>100 + -10%</c> is a discount.
    /// </summary>
    public static bool IsPercentShaped(ExpressionNode node)
    {
        while (node is UnaryNode { Op: UnaryOperator.Negate or UnaryOperator.Positive } sign)
        {
            node = sign.Operand;
        }

        return node is UnaryNode { Op: UnaryOperator.Percent };
    }

    /// <summary>
    /// Whether an additive node applies its right operand as a markup or discount rate on its left operand.
    /// </summary>
    public static bool IsMarkup(BinaryNode node) =>
        node.Op is BinaryOperator.Add or BinaryOperator.Subtract &&
        IsPercentShaped(node.Right) &&
        !IsPercentShaped(node.Left);
}
