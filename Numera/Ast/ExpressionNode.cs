using System.Runtime.CompilerServices;

namespace Numera.Ast;

/// <summary>
/// Represents a zero-based UTF-16 character span within the source expression.
/// Attached to <see cref="ExpressionNode"/> instances so runtime evaluation diagnostics
/// can identify the exact offending subexpression without re-parsing.
/// </summary>
/// <param name="Start">The zero-based UTF-16 starting character offset.</param>
/// <param name="Length">The number of UTF-16 code units in the span.</param>
public readonly record struct TextSpan(int Start, int Length)
{
    /// <summary>
    /// Gets a value indicating whether this span represents a non-empty source range.
    /// </summary>
    public bool HasValue => Length > 0;

    /// <summary>
    /// Creates a span covering the range from the start of <paramref name="first"/> to the end of <paramref name="last"/>.
    /// </summary>
    public static TextSpan Combine(TextSpan first, TextSpan last)
    {
        if (!first.HasValue) return last;
        if (!last.HasValue) return first;
        int end = Math.Max(first.Start + first.Length, last.Start + last.Length);
        int start = Math.Min(first.Start, last.Start);
        return new TextSpan(start, end - start);
    }
}

/// <summary>
/// Abstract base record for all nodes in the Numera abstract syntax tree.
/// </summary>
/// <param name="Span">The source text span represented by the expression node.</param>
public abstract record ExpressionNode(TextSpan Span = default)
{
    /// <summary>
    /// Compares two AST nodes for structural equality, ignoring source span metadata.
    /// Uses EqualityContract to ensure type identity without incorporating Span.
    /// </summary>
    public virtual bool Equals(ExpressionNode? other) =>
        other is not null && EqualityComparer<Type>.Default.Equals(EqualityContract, other.EqualityContract);

    /// <inheritdoc />
    public override int GetHashCode() => EqualityComparer<Type>.Default.GetHashCode(EqualityContract);

    /// <summary>
    /// Gets the structural nesting height of this subtree (leaves have height 1).
    /// Diagnostic only: this is recursive, so it can overflow the stack on hostile trees. Enforce
    /// limits with <see cref="AstValidator"/>, which is iterative and O(n).
    /// </summary>
    public virtual int Height => 1;

    /// <summary>
    /// Accepts a visitor to process this syntax tree node.
    /// </summary>
    /// <typeparam name="T">The return type produced by the visitor.</typeparam>
    /// <param name="visitor">The visitor instance.</param>
    /// <returns>The result produced by visiting this node.</returns>
    public T Accept<T>(IExpressionVisitor<T> visitor)
    {
        // Central guard for every visitor: deep trees that pass validation (e.g. long left spines)
        // fail with a limit exception instead of crashing the process with a stack overflow.
        if (!RuntimeHelpers.TryEnsureSufficientExecutionStack())
        {
            throw new CalculationLimitException();
        }

        return AcceptCore(visitor);
    }

    /// <summary>
    /// Dispatches to the visitor method for the concrete node type. Restricted to this assembly so
    /// every dispatch goes through the stack guard in <see cref="Accept{T}"/>.
    /// </summary>
    private protected abstract T AcceptCore<T>(IExpressionVisitor<T> visitor);
}

/// <summary>
/// Represents a literal number (integer, decimal, or double) in the syntax tree.
/// </summary>
/// <param name="Value">The calculated or parsed <see cref="MathValue"/>.</param>
/// <param name="Span">The source text span represented by the expression node.</param>
public sealed record NumberNode(MathValue Value, TextSpan Span = default) : ExpressionNode(Span)
{
    /// <inheritdoc />
    private protected override T AcceptCore<T>(IExpressionVisitor<T> visitor) => visitor.Visit(this);

    /// <summary>
    /// Compares two number nodes for value equality, ignoring source span metadata.
    /// </summary>
    public bool Equals(NumberNode? other) =>
        other is not null && Value.Equals(other.Value);

    /// <inheritdoc />
    public override int GetHashCode() => Value.GetHashCode();
}

/// <summary>
/// Represents a named identifier or mathematical constant (e.g. "pi", "tau", "e", "x").
/// </summary>
/// <param name="Name">The identifier name as parsed from the source expression.</param>
/// <param name="Constant">The resolved <see cref="ConstantKind"/> if recognized as a
/// mathematical constant; otherwise null.</param>
/// <param name="Span">The source text span represented by the expression node.</param>
public sealed record IdentifierNode(string Name, ConstantKind? Constant = null, TextSpan Span = default) : ExpressionNode(Span)
{
    /// <inheritdoc />
    private protected override T AcceptCore<T>(IExpressionVisitor<T> visitor) => visitor.Visit(this);

    /// <summary>
    /// Compares two identifier nodes for structural equality, ignoring source span metadata.
    /// Mathematical constants are compared by <see cref="ConstantKind"/>, while variables
    /// are compared case-insensitively to match dictionary evaluation semantics.
    /// </summary>
    public bool Equals(IdentifierNode? other) =>
        other is not null &&
        (Constant is { } kind
            ? other.Constant == kind
            : other.Constant is null && string.Equals(Name, other.Name, StringComparison.OrdinalIgnoreCase));

    /// <inheritdoc />
    public override int GetHashCode() =>
        Constant is { } kind
            ? HashCode.Combine(1, (int)kind)
            : HashCode.Combine(2, StringComparer.OrdinalIgnoreCase.GetHashCode(Name));
}

/// <summary>
/// Represents a unary operator applied to an operand (e.g. "-x", "!flag", "~bits", "n!").
/// Supports prefix operators and postfix factorial.
/// </summary>
/// <param name="Op">The unary operator.</param>
/// <param name="Operand">The operand expression node.</param>
/// <param name="Span">The source text span represented by the expression node.</param>
public sealed record UnaryNode(UnaryOperator Op, ExpressionNode Operand, TextSpan Span = default) : ExpressionNode(Span)
{
    /// <inheritdoc />
    public override int Height => 1 + Operand.Height;

    /// <inheritdoc />
    private protected override T AcceptCore<T>(IExpressionVisitor<T> visitor) => visitor.Visit(this);

    /// <summary>
    /// Compares two unary nodes for structural equality, ignoring source span metadata.
    /// </summary>
    public bool Equals(UnaryNode? other) =>
        other is not null && Op == other.Op && Operand.Equals(other.Operand);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(Op, Operand);
}

/// <summary>
/// Represents a binary operator expression (e.g. "a + b", "a * b", "2^3").
/// </summary>
/// <param name="Op">The binary operator.</param>
/// <param name="Left">The left operand node.</param>
/// <param name="Right">The right operand node.</param>
/// <param name="Span">The source text span represented by the expression node.</param>
/// <param name="IsImplicit">True if multiplication was written implicitly by juxtaposition (e.g. "2pi").</param>
public sealed record BinaryNode(
    BinaryOperator Op,
    ExpressionNode Left,
    ExpressionNode Right,
    TextSpan Span = default,
    bool IsImplicit = false) : ExpressionNode(Span)
{
    /// <inheritdoc />
    public override int Height
    {
        get
        {
            // Traverse left-associative binary spines iteratively to avoid call-stack
            // overflow on long flat chains (e.g. thousands of additive terms) and
            // measure nesting depth rather than flat chain length. Right-associative
            // power chains increment nesting height.
            if (Op == BinaryOperator.Power)
            {
                return 1 + Math.Max(Left.Height, Right.Height);
            }

            int maxRight = Right.Height;
            ExpressionNode currentLeft = Left;
            while (currentLeft is BinaryNode leftBin && leftBin.Op != BinaryOperator.Power)
            {
                int rh = leftBin.Right.Height;
                if (rh > maxRight)
                {
                    maxRight = rh;
                }

                currentLeft = leftBin.Left;
            }

            int leftmostHeight = currentLeft.Height;
            return 1 + Math.Max(leftmostHeight, maxRight);
        }
    }

    /// <inheritdoc />
    private protected override T AcceptCore<T>(IExpressionVisitor<T> visitor) => visitor.Visit(this);

    /// <summary>
    /// Compares two binary nodes for structural equality, ignoring source span metadata
    /// and implicit multiplication syntax (<see cref="IsImplicit"/>).
    /// Iterative along the left spine to prevent stack overflow on long chains.
    /// </summary>
    public bool Equals(BinaryNode? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        BinaryNode? a = this;
        BinaryNode? b = other;
        while (true)
        {
            if (a.Op != b.Op || !a.Right.Equals(b.Right))
            {
                return false;
            }

            if (a.Left is BinaryNode leftA && b.Left is BinaryNode leftB)
            {
                a = leftA;
                b = leftB;
                continue;
            }

            return a.Left.Equals(b.Left);
        }
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        var hash = new HashCode();
        ExpressionNode current = this;
        while (current is BinaryNode bin)
        {
            hash.Add(bin.Op);
            hash.Add(bin.Right);
            current = bin.Left;
        }

        hash.Add(current);
        return hash.ToHashCode();
    }
}

/// <summary>
/// Represents a comparison or chained comparison (e.g. "x == y", "1 &lt; x &lt;= 10",
/// "a == b == c").
/// </summary>
public sealed record ComparisonChainNode : ExpressionNode
{
    /// <summary>
    /// Gets the ordered list of operands being compared (must have at least 2 operands).
    /// </summary>
    public IReadOnlyList<ExpressionNode> Operands { get; }

    /// <summary>
    /// Gets the comparison operators between successive operands.
    /// </summary>
    public IReadOnlyList<ComparisonOperator> Operators { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="ComparisonChainNode"/>. Collections are
    /// always copied and wrapped read-only (not cast-able back to arrays) so callers
    /// cannot mutate the node afterwards; null is rejected so construction bugs surface
    /// here rather than being hidden.
    /// </summary>
    /// <param name="operands">The ordered operands being compared (must have at least 2).
    /// </param>
    /// <param name="operators">The comparison operators between successive operands.
    /// </param>
    /// <param name="span">The source text span represented by the expression node.
    /// </param>
    public ComparisonChainNode(
        IReadOnlyList<ExpressionNode> operands,
        IReadOnlyList<ComparisonOperator> operators,
        TextSpan span = default)
        : base(span)
    {
        ArgumentNullException.ThrowIfNull(operands);
        ArgumentNullException.ThrowIfNull(operators);
        Operands = Array.AsReadOnly(operands.ToArray());
        Operators = Array.AsReadOnly(operators.ToArray());
    }
    /// <inheritdoc />
    public override int Height
    {
        get
        {
            int max = 0;
            foreach (var op in Operands)
            {
                int h = op.Height;
                if (h > max) max = h;
            }
            return 1 + max;
        }
    }

    /// <inheritdoc />
    private protected override T AcceptCore<T>(IExpressionVisitor<T> visitor) => visitor.Visit(this);

    /// <summary>
    /// Compares two comparison chain nodes for structural equality across operands and
    /// operators.
    /// </summary>
    public bool Equals(ComparisonChainNode? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return Operands.SequenceEqual(other.Operands) && Operators.SequenceEqual(other.Operators);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var op in Operands)
        {
            hash.Add(op);
        }

        foreach (var op in Operators)
        {
            hash.Add(op);
        }

        return hash.ToHashCode();
    }
}

/// <summary>
/// Represents a function call expression (e.g. "sqrt(16)", "max(1, 2, 3)").
/// </summary>
public sealed record FunctionCallNode : ExpressionNode
{
    /// <summary>
    /// Gets the name of the function.
    /// </summary>
    public string FunctionName { get; }

    /// <summary>
    /// Gets the argument expression nodes.
    /// </summary>
    public IReadOnlyList<ExpressionNode> Arguments { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="FunctionCallNode"/>. Arguments are always
    /// copied and wrapped read-only so callers cannot mutate the node afterwards; null is
    /// rejected so construction bugs surface here rather than being hidden.
    /// </summary>
    /// <param name="functionName">The name of the function.</param>
    /// <param name="arguments">The argument expression nodes.</param>
    /// <param name="span">The source text span represented by the expression node.
    /// </param>
    public FunctionCallNode(
        string functionName,
        IReadOnlyList<ExpressionNode> arguments,
        TextSpan span = default)
        : base(span)
    {
        ArgumentNullException.ThrowIfNull(functionName);
        ArgumentNullException.ThrowIfNull(arguments);
        FunctionName = functionName;
        Arguments = Array.AsReadOnly(arguments.ToArray());
    }

    /// <inheritdoc />
    public override int Height
    {
        get
        {
            int max = 0;
            foreach (var arg in Arguments)
            {
                int h = arg.Height;
                if (h > max) max = h;
            }
            return 1 + max;
        }
    }

    /// <inheritdoc />
    private protected override T AcceptCore<T>(IExpressionVisitor<T> visitor) => visitor.Visit(this);

    /// <summary>
    /// Compares two function call nodes for structural equality across the function name
    /// and argument expressions.
    /// </summary>
    public bool Equals(FunctionCallNode? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return string.Equals(FunctionName, other.FunctionName, StringComparison.OrdinalIgnoreCase) &&
               Arguments.SequenceEqual(other.Arguments);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(FunctionName, StringComparer.OrdinalIgnoreCase);
        foreach (var arg in Arguments)
        {
            hash.Add(arg);
        }

        return hash.ToHashCode();
    }
}

/// <summary>
/// Represents a variable assignment expression (e.g. <c>bill = 65 + 15%</c> or
/// <c>a = b = 100</c>).
/// </summary>
/// <remarks>
/// <para>
/// <b>Syntactic Span vs. Semantic Definition:</b>
/// <list type="bullet">
///   <item>
///     <see cref="ExpressionNode.Span"/> represents the complete source text span of the
///     assignment expression, including the variable name, equals sign, and value
///     expression.
///   </item>
///   <item>
///     <see cref="AssignmentNode.Definition"/> represents solely the source text of the
///     value expression being assigned to the variable, excluding the target variable
///     name and equals sign.
///   </item>
/// </list>
/// </para>
/// <para>
/// <b>Chained Assignments (<c>a = b = 100</c>):</b><br/><br/>
/// In chained assignments, each node in the chain has a progressively wider
/// <see cref="ExpressionNode.Span"/> encompassing its sub-statement, but all nodes share
/// the same <see cref="AssignmentNode.Definition"/> text of the final value being
/// assigned.
/// </para>
/// </remarks>
public sealed record AssignmentNode : ExpressionNode
{
    /// <summary>
    /// Gets the identifier name of the variable being assigned (e.g. <c>"bill"</c>).
    /// </summary>
    public string VariableName { get; }

    /// <summary>
    /// Gets the expression node yielding the assigned value. In chained assignments, this
    /// is the next <see cref="AssignmentNode"/> in the chain, or the final value
    /// expression.
    /// </summary>
    public ExpressionNode Value { get; }

    /// <summary>
    /// Gets the source text of the terminal value expression being assigned, excluding
    /// the variable name and equals sign (e.g. <c>"105 + 15%"</c> or <c>"100"</c>).
    /// Distinct from <see cref="ExpressionNode.Span"/>, which covers the entire
    /// assignment statement. In chained assignments, all nodes share the same
    /// <see cref="Definition"/> text of the final value being assigned.
    /// </summary>
    public string? Definition { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="AssignmentNode"/>.
    /// </summary>
    /// <param name="variableName">The identifider name of the target variable.</param>
    /// <param name="value">The expression node yielding the assigned value.</param>
    /// <param name="definition">The text of the terminal expression assigned to the
    /// variable.</param>
    /// <param name="span">The source text span encompassing the full assignment
    /// statement.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="variableName"/>
    /// or <paramref name="value"/> is null.</exception>"
    public AssignmentNode(string variableName, ExpressionNode value,
        string? definition = null, TextSpan span = default) : base(span)
    {
        ArgumentNullException.ThrowIfNull(variableName);
        ArgumentNullException.ThrowIfNull(value);

        VariableName = variableName;
        Value = value;
        Definition = definition;
    }

    /// <summary>
    /// Gets the structural nesting height of this subtree (leaves have height 1).
    /// </summary>
    public override int Height => Value.Height + 1;

    private protected override T AcceptCore<T>(IExpressionVisitor<T> visitor) => visitor.Visit(this);

    /// <summary>
    /// Compares two assignment nodes for structual equality across variable names (case-
    /// insensitive) and assigned value expression, ignoring source span metadata.
    /// </summary>
    public bool Equals(AssignmentNode? other) =>
        other is not null &&
        string.Equals(VariableName, other.VariableName, StringComparison.OrdinalIgnoreCase) &&
        Value.Equals(other.Value);

    /// <inheritdoc />
    public override int GetHashCode() => 
        HashCode.Combine(StringComparer.OrdinalIgnoreCase.GetHashCode(VariableName), Value);
}
