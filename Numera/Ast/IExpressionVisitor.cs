namespace Numera.Ast;

/// <summary>
/// Defines a visitor for traversing and processing Numera abstract syntax tree nodes.
/// </summary>
/// <typeparam name="T">The return type produced by visiting each node.</typeparam>
public interface IExpressionVisitor<T>
{
    /// <summary>Visits a <see cref="NumberNode"/> literal value.</summary>
    /// <param name="node">The number node.</param>
    /// <returns>The result of processing the node.</returns>
    T Visit(NumberNode node);

    /// <summary>Visits an <see cref="IdentifierNode"/> named constant or identifier.</summary>
    /// <param name="node">The identifier node.</param>
    /// <returns>The result of processing the node.</returns>
    T Visit(IdentifierNode node);

    /// <summary>Visits a <see cref="UnaryNode"/> unary operator expression.</summary>
    /// <param name="node">The unary node.</param>
    /// <returns>The result of processing the node.</returns>
    T Visit(UnaryNode node);

    /// <summary>Visits a <see cref="BinaryNode"/> binary operator expression.</summary>
    /// <param name="node">The binary node.</param>
    /// <returns>The result of processing the node.</returns>
    T Visit(BinaryNode node);

    /// <summary>Visits a <see cref="ComparisonChainNode"/> comparison or relational chain.</summary>
    /// <param name="node">The comparison chain node.</param>
    /// <returns>The result of processing the node.</returns>
    T Visit(ComparisonChainNode node);

    /// <summary>Visits a <see cref="FunctionCallNode"/> function call expression.</summary>
    /// <param name="node">The function call node.</param>
    /// <returns>The result of processing the node.</returns>
    T Visit(FunctionCallNode node);

    /// <summary>Visits an <see cref="AssignmentNode"/> assignment expression.</summary>
    /// <param name="node">The assignment node.</param>
    /// <returns>The result of processing the node.</returns>
    T Visit(AssignmentNode node);
}
