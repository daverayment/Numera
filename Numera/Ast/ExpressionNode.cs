namespace Numera.Ast;

/// <summary>
/// Abstract base record for all nodes in the Numera abstract syntax tree.
/// </summary>
public abstract record ExpressionNode
{
    /// <summary>
    /// Accepts a visitor to process this syntax tree node.
    /// </summary>
    /// <typeparam name="T">The return type produced by the visitor.</typeparam>
    /// <param name="visitor">The visitor instance.</param>
    /// <returns>The result produced by visiting this node.</returns>
    public abstract T Accept<T>(IExpressionVisitor<T> visitor);
}

/// <summary>
/// Represents a literal number (integer, decimal, or double) in the syntax tree.
/// </summary>
/// <param name="Value">The calculated or parsed <see cref="MathValue"/>.</param>
public sealed record NumberNode(MathValue Value) : ExpressionNode
{
    /// <inheritdoc />
    public override T Accept<T>(IExpressionVisitor<T> visitor) => visitor.Visit(this);
}

/// <summary>
/// Represents a named identifier or mathematical constant (e.g. "pi", "tau", "e", "x").
/// </summary>
/// <param name="Name">The identifier name.</param>
public sealed record IdentifierNode(string Name) : ExpressionNode
{
    /// <inheritdoc />
    public override T Accept<T>(IExpressionVisitor<T> visitor) => visitor.Visit(this);
}

/// <summary>
/// Represents a unary operator applied to an operand (e.g. "-x", "!flag", "~bits").
/// </summary>
/// <param name="Op">The unary operator.</param>
/// <param name="Operand">The operand expression node.</param>
public sealed record UnaryNode(UnaryOperator Op, ExpressionNode Operand) : ExpressionNode
{
    /// <inheritdoc />
    public override T Accept<T>(IExpressionVisitor<T> visitor) => visitor.Visit(this);
}

/// <summary>
/// Represents a binary operator expression (e.g. "a + b", "a * b", "2^3").
/// </summary>
/// <param name="Op">The binary operator.</param>
/// <param name="Left">The left operand node.</param>
/// <param name="Right">The right operand node.</param>
public sealed record BinaryNode(BinaryOperator Op, ExpressionNode Left, ExpressionNode Right) : ExpressionNode
{
    /// <inheritdoc />
    public override T Accept<T>(IExpressionVisitor<T> visitor) => visitor.Visit(this);
}

/// <summary>
/// Represents a comparison or chained comparison (e.g. "x == y", "1 &lt; x &lt;= 10", "a == b == c").
/// </summary>
/// <param name="Operands">The ordered list of operands being compared (must have at least 2 operands).</param>
/// <param name="Operators">The comparison operators between successive operands.</param>
public sealed record ComparisonChainNode(
    IReadOnlyList<ExpressionNode> Operands,
    IReadOnlyList<ComparisonOperator> Operators) : ExpressionNode
{
    /// <inheritdoc />
    public override T Accept<T>(IExpressionVisitor<T> visitor) => visitor.Visit(this);
}

/// <summary>
/// Represents a function call expression (e.g. "sqrt(16)", "max(1, 2, 3)").
/// </summary>
/// <param name="FunctionName">The name of the function.</param>
/// <param name="Arguments">The argument expression nodes.</param>
public sealed record FunctionCallNode(string FunctionName, IReadOnlyList<ExpressionNode> Arguments) : ExpressionNode
{
    /// <inheritdoc />
    public override T Accept<T>(IExpressionVisitor<T> visitor) => visitor.Visit(this);
}
