using System.Numerics;

namespace Numera.Ast;

/// <summary>
/// Validates abstract syntax trees iteratively (O(n), no recursion) before evaluation or
/// formatting, guaranteeing invariants for manually constructed ASTs. The validator
/// itself cannot overflow the stack. Note that left spines of binary nodes do not count
/// towards nesting height (matching <see cref="ExpressionNode.Height"/>), so downstream
/// recursive visitors are bounded only by the node-count limit, not by the nesting limit.
/// </summary>
internal static class AstValidator
{
    // Named to avoid confusion with System.Diagnostics.StackFrame.
    private sealed class ValidationFrame(ExpressionNode node)
    {
        public ExpressionNode Node { get; } = node;
        public int ChildIndex { get; set; }
        public int MaxChildHeight { get; set; }
    }

    /// <summary>
    /// Validates the given abstract syntax tree (AST) against the specified evaluation
    /// options.
    /// </summary>
    /// <param name="root">The tree to validate.</param>
    /// <param name="options">The evaluation options the tree will be checked against.
    /// </param>
    /// <param name="allowUnknownFunctions">Formatting intentionally supports custom
    /// function names (rendered escaped), so the formatting path skips the unknown-
    /// function check. Evaluation keeps it, mirroring what the parser enforces.</param>
    public static void Validate(ExpressionNode root, EvaluationOptions options,
        bool allowUnknownFunctions = false)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(options);

        int maxDepth = options.MaxNestingDepth;
        long maxNodes = (long)options.MaxInputLength * 2;
        long nodeCount = 0;

        // Post-order iterative traversal using an explicit stack.
        var stack = new Stack<ValidationFrame>();
        stack.Push(new ValidationFrame(root));

        while (stack.Count > 0)
        {
            var frame = stack.Peek();
            var node = frame.Node;

            if (frame.ChildIndex == 0)
            {
                nodeCount++;
                if (nodeCount > maxNodes)
                {
                    throw new CalculationLimitException();
                }

                // Node-level invariant checks on first visit.
                ValidateNodeInvariants(node, allowUnknownFunctions);
            }

            if (frame.ChildIndex < ChildCount(node))
            {
                var child = GetChild(node, frame.ChildIndex) ??
                    throw Invalid(node, EvaluationError.InvalidSyntax, "AST contains a null child node.");
                frame.ChildIndex++;
                stack.Push(new ValidationFrame(child));
            }
            else
            {
                // All children visited; compute height.
                stack.Pop();
                int currentHeight = 1 + frame.MaxChildHeight;
                if (currentHeight > maxDepth)
                {
                    throw new CalculationLimitException();
                }

                if (stack.Count > 0)
                {
                    var parentFrame = stack.Peek();

                    // Mirrors BinaryNode.Height: only a non-power binary left child of a
                    // non-power binary is flattened into the spine. The child index (not
                    // reference equality) identifies the left child, since a node
                    // instance may be shared as both operands.
                    bool isSpineLeft =
                        parentFrame.Node is BinaryNode { Op: not BinaryOperator.Power } &&
                        parentFrame.ChildIndex == 1 &&
                        node is BinaryNode { Op: not BinaryOperator.Power };

                    int contribution = isSpineLeft ? currentHeight - 1 : currentHeight;
                    if (contribution > parentFrame.MaxChildHeight)
                    {
                        parentFrame.MaxChildHeight = contribution;
                    }
                }
            }
        }
    }

    private static ExpressionException Invalid(ExpressionNode node, EvaluationError code, string message) =>
        new(node.Span.HasValue
            ? new EvaluationDiagnostic(code, message, node.Span.Start, node.Span.Length)
            : new EvaluationDiagnostic(code, message));

    private static void ValidateNodeInvariants(ExpressionNode node, bool allowUnknownFunctions)
    {
        switch (node)
        {
            case NumberNode numberNode:
                switch (numberNode.Value.Type)
                {
                    case MathValue.DataType.Integer
                        when BigInteger.Abs(numberNode.Value.IntVal).GetBitLength() > CalculationLimits.MaxIntegerBits:
                        throw new CalculationLimitException();

                    case MathValue.DataType.Double when !double.IsFinite(numberNode.Value.DblVal):
                        throw Invalid(node, EvaluationError.InvalidSyntax, "Number literal must be finite.");
                }
                break;

            case ComparisonChainNode comp:
                if (comp.Operands is null || comp.Operators is null)
                {
                    throw Invalid(node, EvaluationError.InvalidSyntax, "Comparison chain cannot have null operands or operators.");
                }
                if (comp.Operands.Count < 2 || comp.Operators.Count != comp.Operands.Count - 1)
                {
                    throw Invalid(node, EvaluationError.InvalidSyntax, "Comparison chain must have at least 2 operands and exactly (Operands.Count - 1) operators.");
                }
                foreach (var op in comp.Operators)
                {
                    if (!Enum.IsDefined(op))
                    {
                        throw Invalid(node, EvaluationError.InvalidSyntax, "Comparison chain contains an undefined operator.");
                    }
                }
                break;

            case FunctionCallNode call:
                if (call.FunctionName is null || call.Arguments is null)
                {
                    throw Invalid(node, EvaluationError.InvalidSyntax, "Function call cannot have null name or arguments.");
                }
                if (!FunctionArity.TryGet(call.FunctionName, out var signature))
                {
                    if (allowUnknownFunctions)
                    {
                        break;
                    }

                    throw Invalid(node, EvaluationError.UnknownFunction, $"Unknown function '{call.FunctionName}'.");
                }
                if (call.Arguments.Count < signature.MinimumArguments ||
                    (signature.MaximumArguments is int maxArgs && call.Arguments.Count > maxArgs))
                {
                    throw Invalid(node, EvaluationError.InvalidArgumentCount, $"Invalid argument count for '{call.FunctionName}'.");
                }
                break;

            case BinaryNode binary:
                if (binary.Left is null || binary.Right is null)
                {
                    throw Invalid(node, EvaluationError.InvalidSyntax, "Binary expression cannot have null operands.");
                }
                if (!Enum.IsDefined(binary.Op))
                {
                    throw Invalid(node, EvaluationError.InvalidSyntax, "Binary expression has an undefined operator.");
                }
                if (binary.Op == BinaryOperator.Of && !PercentRules.IsPercentShaped(binary.Left))
                {
                    throw Invalid(node, EvaluationError.InvalidSyntax, "Only a percentage can appear before 'of'.");
                }
                // The formatter renders implicit nodes
                if (binary.IsImplicit && binary.Op != BinaryOperator.Multiply)
                {
                    throw Invalid(node, EvaluationError.InvalidSyntax, "Only multiplication can be implicit.");
                }
                break;

            case UnaryNode unary:
                if (unary.Operand is null)
                {
                    throw Invalid(node, EvaluationError.InvalidSyntax, "Unary expression cannot have a null operand.");
                }
                if (!Enum.IsDefined(unary.Op))
                {
                    throw Invalid(node, EvaluationError.InvalidSyntax, "Unary expression has an undefined operator.");
                }
                break;

            case IdentifierNode identifier:
                if (identifier.Name is null)
                {
                    throw Invalid(node, EvaluationError.InvalidSyntax, "Identifier cannot have a null name.");
                }
                if (identifier.Constant is { } declared)
                {
                    if (!ConstantCatalog.TryResolve(identifier.Name, out var resolved, out _) || resolved != declared)
                    {
                        throw Invalid(node, EvaluationError.InvalidSyntax, $"Identifier '{identifier.Name}' is inconsistent with constant '{declared}'.");
                    }
                }
                else if (ConstantCatalog.TryResolve(identifier.Name, out _, out _))
                {
                    // The parser resolves constants before variables, so an unresolved constant name
                    // would silently be looked up as a variable.
                    throw Invalid(node, EvaluationError.InvalidSyntax, $"Identifier '{identifier.Name}' is a constant and must be marked as one.");
                }
                break;
        }
    }

    private static int ChildCount(ExpressionNode node) => node switch
    {
        BinaryNode => 2,
        UnaryNode => 1,
        FunctionCallNode fn => fn.Arguments.Count,
        ComparisonChainNode comp => comp.Operands.Count,
        _ => 0,
    };

    private static ExpressionNode GetChild(ExpressionNode node, int index) => node switch
    {
        BinaryNode bin => index == 0 ? bin.Left : bin.Right,
        UnaryNode un => un.Operand,
        FunctionCallNode fn => fn.Arguments[index],
        ComparisonChainNode comp => comp.Operands[index],
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
}
