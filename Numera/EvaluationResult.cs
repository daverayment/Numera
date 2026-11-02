using System.Collections.Frozen;
using System.Globalization;
using System.Numerics;
using Numera.Ast;

namespace Numera;

/// <summary>
/// The result of evaluating an expression, including the value, updated context, and any 
/// variables assigned by the expression.
/// </summary>
public readonly record struct EvaluationResult :
    IEquatable<MathValue>,
    IFormattable,
    ISpanFormattable,
    IUtf8SpanFormattable
{
    /// <summary>
    /// Gets the evaluated value of the expression.
    /// </summary>
    public MathValue Value { get; }

    /// <summary>
    /// Gets the updated context containing the new
    /// <see cref="EvaluationContext.PreviousResult"/> (<c>ans</c>) and any new variable
    /// assignments merged into <see cref="EvaluationContext.Variables"/>. Pass this into
    /// subsequent evaluations to chain session state.
    /// </summary>
    public EvaluationContext Context { get; }

    /// <summary>
    /// Gets the variables assigned or updated during this specific evaluation, if any.
    /// Empty if no assignments occurred. A subset of
    /// <see cref="EvaluationContext.Variables"/> in <see cref="Context"/>.
    /// </summary>
    public VariableCollection AssignedVariables { get; }

    /// <summary>
    /// Gets the <see cref="CalculatorEngine"/> used to evaluate the expression, which
    /// contains the current culture, formatting rules, and other options. Used when
    /// performing AST-preserving arithmetic operations.
    /// </summary>
    internal CalculatorEngine Engine { get; }

    /// <summary>
    /// Returns <c>true</c> if one or more variables were assigned or updated in this
    /// evaluation.
    /// </summary>
    public bool HasAssignments => AssignedVariables.Count > 0;

    /// <summary>
    /// Convenience helper: returns the single variable name if exactly one assignment
    /// occurred (e.g. "bill" from "bill = 65"), or <see langword="null"/> if zero or
    /// multiple assignments occurred.
    /// </summary>
    public string? SingleAssignedVariable =>
        AssignedVariables.Count == 1 ? AssignedVariables.Keys.First() : null;

    /// <summary>
    /// Gets the unique set of variable names referenced by the evaluated expression,
    /// or an empty set if the expression referenced no variables.
    /// </summary>
    public IReadOnlySet<string> ReferencedVariables =>
        Context.PreviousExpression?.GetReferencedVariables() ?? FrozenSet<string>.Empty;

    /// <summary>
    /// Gets all syntactic variable references occurring within the evaluated expression,
    /// including their identifier names and source text spans for tooltip/hover inspection.
    /// </summary>
    public IReadOnlyList<VariableReference> VariableReferences =>
        Context.PreviousExpression?.GetVariableReferences() ?? [];

    /// <summary>
    /// Initializes a new instance of the <see cref="EvaluationResult"/> struct.
    /// </summary>
    /// <param name="value">The evaluated value of the expression.</param>
    /// <param name="context">The updated context containing the new
    /// <see cref="EvaluationContext.PreviousResult"/> (<c>ans</c>) and any new variable
    /// assignments merged into <see cref="EvaluationContext.Variables"/>.</param>
    /// <param name="assignedVariables">The variables assigned or updated by the
    /// expression, or <c>null</c> if the expression did not assign any variable.</param>
    /// <param name="engine">The calculator engine used for the evaluation.</param>
    public EvaluationResult(
        MathValue value,
        EvaluationContext context,
        VariableCollection? assignedVariables = null,
        CalculatorEngine? engine = null)
    {
        Value = value;
        Context = context;
        AssignedVariables = assignedVariables ?? VariableCollection.Empty;
        Engine = engine ?? CalculatorEngine.Default;
    }

    // Member-forwarding to MathValue.
    /// <summary>
    /// Gets the data type of the current math value.
    /// </summary>
    public MathValue.DataType Type => Value.Kind;

    /// <summary>
    /// Gets the stored <see cref="System.Numerics.BigInteger"/> value when
    /// <see cref="Type"/> is <see cref="MathValue.DataType.Integer"/>; otherwise default.
    /// </summary>
    public BigInteger IntegerValue => Value.IntegerValue;

    /// <summary>
    /// Gets the stored <see cref="decimal"/> value when <see cref="Type"/> is
    /// <see cref="MathValue.DataType.Decimal"/>; otherwise default.
    /// </summary>
    public decimal DecimalValue => Value.DecimalValue;

    /// <summary>
    /// Gets the value as a double-precision floating-point number.
    /// </summary>
    public double DoubleValue => Value.DoubleValue;

    /// <summary>
    /// Returns true if the value is a finite number. Returns false for NaN or Infinity.
    /// </summary>
    public bool IsFinite => Value.IsFinite;

    /// <summary>
    /// Returns true if the value is mathematically undefined (Not-a-Number); otherwise
    /// false.
    /// </summary>
    public bool IsNaN => Value.IsNaN;

    /// <summary>
    /// Returns true if the value is zero.
    /// </summary>
    public bool IsZero => Value.IsZero;

    /// <summary>
    /// Forces the value into a standard 64-bit floating-point representation. This may
    /// result in loss of precision or magnitude.
    /// </summary>
    /// <returns></returns>
    public double AsDouble() => Value.AsDouble();

    /// <summary>
    /// Implicit conversion to <see cref="MathValue"/>. This allows an
    /// <see cref="EvaluationResult"/> to be used in contexts where a
    /// <see cref="MathValue"/> is expected, returning the evaluated value, like
    /// <c>MathValue val = Calculator.Evaluate("2 + 2");</c>
    /// </summary>
    /// <param name="result">The evaluation result to convert.</param>
    public static implicit operator MathValue(EvaluationResult result) => result.Value;

    /// <summary>
    /// Equality based on <see cref="MathValue"/>.
    /// </summary>
    public bool Equals(MathValue other) => Value.Equals(other);

    // AST-preserving arithmetic operators.

    /// <summary>
    /// Returns the sum of two evaluation results, combining their values using their
    /// original ASTs.
    /// </summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns>The sum of the two evaluation results.</returns>
    public static EvaluationResult operator +(EvaluationResult left, EvaluationResult right) =>
        CombineBinary(left, right, BinaryOperator.Add);

    /// <summary>
    /// Returns the difference of two evaluation results, combining their values using
    /// their original ASTs.
    /// </summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns>The result of subtracting <paramref name="right"/> from
    /// <paramref name="left"/>.</returns>
    public static EvaluationResult operator -(EvaluationResult left, EvaluationResult right) =>
        CombineBinary(left, right, BinaryOperator.Subtract);

    /// <summary>
    /// Returns the product of two evaluation results, combining their values using their
    /// original ASTs.
    /// </summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns>The product of the two evaluation results.</returns>
    public static EvaluationResult operator *(EvaluationResult left, EvaluationResult right) =>
        CombineBinary(left, right, BinaryOperator.Multiply);

    /// <summary>
    /// Returns the quotient of two evaluation results, combining their values using their
    /// original ASTs.
    /// </summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns>The quotient of the two evaluation results.</returns>
    public static EvaluationResult operator /(EvaluationResult left, EvaluationResult right) =>
        CombineBinary(left, right, BinaryOperator.Divide);

    /// <summary>
    /// Returns the remainder of dividing evaluation results, combining their values using
    /// their original ASTs.
    /// </summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns>The remainder dividing the two evaluation results.</returns>
    public static EvaluationResult operator %(EvaluationResult left, EvaluationResult right) =>
        CombineBinary(left, right, BinaryOperator.Modulo);

    // Overloads with MathValue (and primitives via implicit MathValue conversion).

    /// <summary>
    /// Returns the sum of an <see cref="EvaluationResult"/> and <see cref="MathValue"/>.
    /// </summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns>The sum of the evaluation result and the math value.</returns>
    public static EvaluationResult operator +(EvaluationResult left, MathValue right) =>
        CombineBinary(left, right, BinaryOperator.Add);

    /// <summary>
    /// Returns the sum of a <see cref="MathValue"/> and <see cref="EvaluationResult"/>.
    /// </summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns>The sum of the evaluation result and the math value.</returns>
    public static EvaluationResult operator +(MathValue left, EvaluationResult right) =>
        CombineBinary(left, right, BinaryOperator.Add);

    /// <summary>
    /// Returns the difference of two evaluation results, combining their values using
    /// their original ASTs.
    /// </summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns>The result of subtracting <paramref name="right"/> from
    /// <paramref name="left"/>.</returns>
    public static EvaluationResult operator -(EvaluationResult left, MathValue right) =>
        CombineBinary(left, right, BinaryOperator.Subtract);

    /// <summary>
    /// Returns the difference of two evaluation results, combining their values using
    /// their original ASTs.
    /// </summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns>The result of subtracting <paramref name="right"/> from
    /// <paramref name="left"/>.</returns>
    public static EvaluationResult operator -(MathValue left, EvaluationResult right) =>
        CombineBinary(left, right, BinaryOperator.Subtract);

    /// <summary>
    /// Returns the product of an <see cref="EvaluationResult"/> and
    /// <see cref="MathValue"/>.
    /// </summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns>The result of multiplying <paramref name="left"/> by
    /// <paramref name="right"/>.</returns>
    public static EvaluationResult operator *(EvaluationResult left, MathValue right) =>
        CombineBinary(left, right, BinaryOperator.Multiply);

    /// <summary>
    /// Returns the product of a <see cref="MathValue"/> and
    /// <see cref="EvaluationResult"/>.
    /// </summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns>The result of multiplying <paramref name="left"/> by
    /// <paramref name="right"/>.</returns>
    public static EvaluationResult operator *(MathValue left, EvaluationResult right) =>
        CombineBinary(left, right, BinaryOperator.Multiply);

    /// <summary>
    /// Returns the quotient of an <see cref="EvaluationResult"/> and
    /// <see cref="MathValue"/>.
    /// </summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns>The result of dividing <paramref name="left"/> by
    /// <paramref name="right"/>.</returns>
    public static EvaluationResult operator /(EvaluationResult left, MathValue right) =>
        CombineBinary(left, right, BinaryOperator.Divide);

    /// <summary>
    /// Returns the quotient of a <see cref="MathValue"/> and
    /// <see cref="EvaluationResult"/>.
    /// </summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns>The result of dividing <paramref name="left"/> by
    /// <paramref name="right"/>.</returns>
    public static EvaluationResult operator /(MathValue left, EvaluationResult right) =>
        CombineBinary(left, right, BinaryOperator.Divide);

    /// <summary>
    /// Returns the remainder of dividing an <see cref="EvaluationResult"/> by a
    /// <see cref="MathValue"/>.
    /// </summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns>The remainder dividing <paramref name="left"/> by
    /// <paramref name="right"/>.</returns>
    public static EvaluationResult operator %(EvaluationResult left, MathValue right) =>
        CombineBinary(left, right, BinaryOperator.Modulo);

    /// <summary>
    /// Returns the remainder of dividing a <see cref="MathValue"/> by an
    /// <see cref="EvaluationResult"/>.
    /// </summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns>The remainder from dividing <paramref name="left"/> by
    /// <paramref name="right"/>.</returns>
    public static EvaluationResult operator %(MathValue left, EvaluationResult right) =>
        CombineBinary(left, right, BinaryOperator.Modulo);

    // Unary

    /// <summary>
    /// Return the negation of an evaluation result, combining its value using its
    /// original AST.
    /// </summary>
    /// <param name="operand">The operand to negate.</param>
    /// <returns>The result of negating the operand.</returns>
    public static EvaluationResult operator -(EvaluationResult operand)
    {
        var node = new UnaryNode(UnaryOperator.Negate, operand.GetExpressionNode());
        return operand.Engine.Evaluate(node, operand.Context);
    }

    /// <summary>
    /// Return the positive of an evaluation result.
    /// </summary>
    /// <param name="operand">The operand to return as positive.</param>
    /// <returns>The result of returning the operand as positive.</returns>
    public static EvaluationResult operator +(EvaluationResult operand)
    {
        var node = new UnaryNode(UnaryOperator.Positive, operand.GetExpressionNode());
        return operand.Engine.Evaluate(node, operand.Context);
    }

    // Comparison operators with MathValue.

    /// <summary>
    /// Determines whether an evaluation result and a math value are equal.
    /// </summary>
    public static bool operator ==(EvaluationResult left, MathValue right) => left.Value.Equals(right);

    /// <summary>
    /// Determines whether a math value and an evaluation result are equal.
    /// </summary>
    public static bool operator ==(MathValue left, EvaluationResult right) => left.Equals(right.Value);

    /// <summary>
    /// Determines whether an evaluation result and a math value are not equal.
    /// </summary>
    public static bool operator !=(EvaluationResult left, MathValue right) => !left.Value.Equals(right);

    /// <summary>
    /// Determines whether a math value and an evaluation result are not equal.
    /// </summary>
    public static bool operator !=(MathValue left, EvaluationResult right) => !left.Equals(right.Value);

    /// <summary>
    /// Determines whether <paramref name="left"/> is greater than
    /// <paramref name="right"/> based on their <see cref="MathValue"/> values.
    /// </summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns><see langword="true"/> if <paramref name="left"/> is greater than
    /// <paramref name="right"/>; otherwise <see langword="false"/>.
    /// </returns>
    public static bool operator >(EvaluationResult left, MathValue right) => left.Value > right;

    /// <summary>
    /// Determines whether <paramref name="left"/> is greater than
    /// <paramref name="right"/> based on their <see cref="MathValue"/> values.
    /// </summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns><see langword="true"/> if <paramref name="left"/> is greater than
    /// <paramref name="right"/>; otherwise <see langword="false"/>.
    /// </returns>
    public static bool operator >(MathValue left, EvaluationResult right) => left > right.Value;

    /// <summary>
    /// Determines whether <paramref name="left"/> is less than <paramref name="right"/>
    /// based on their <see cref="MathValue"/> values.
    /// </summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns><see langword="true"/> if <paramref name="left"/> is less than
    /// <paramref name="right"/>; otherwise <see langword="false"/>.
    /// </returns>
    public static bool operator <(EvaluationResult left, MathValue right) => left.Value < right;

    /// <summary>
    /// Determines whether <paramref name="left"/> is less than <paramref name="right"/>
    /// based on their <see cref="MathValue"/> values.
    /// </summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns><see langword="true"/> if <paramref name="left"/> is less than
    /// <paramref name="right"/>; otherwise <see langword="false"/>.
    /// </returns>
    public static bool operator <(MathValue left, EvaluationResult right) => left < right.Value;

    /// <summary>
    /// Determines whether <paramref name="left"/> is greater than or equal to
    /// <paramref name="right"/> based on their <see cref="MathValue"/> values.
    /// </summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns><see langword="true"/> if <paramref name="left"/> is greater than or
    /// equal to <paramref name="right"/>; otherwise <see langword="false"/>.
    /// </returns>
    public static bool operator >=(EvaluationResult left, MathValue right) => left.Value >= right;

    /// <summary>
    /// Determines whether <paramref name="left"/> is greater than or equal to
    /// <paramref name="right"/> based on their <see cref="MathValue"/> values.
    /// </summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns><see langword="true"/> if <paramref name="left"/> is greater than or
    /// equal to <paramref name="right"/>; otherwise <see langword="false"/>.
    /// </returns>
    public static bool operator >=(MathValue left, EvaluationResult right) => left >= right.Value;

    /// <summary>
    /// Determines whether <paramref name="left"/> is less than or equal to
    /// <paramref name="right"/> based on their <see cref="MathValue"/> values.
    /// </summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns><see langword="true"/> if <paramref name="left"/> is less than or equal
    /// to <paramref name="right"/>; otherwise <see langword="false"/>.
    /// </returns>
    public static bool operator <=(EvaluationResult left, MathValue right) => left.Value <= right;

    /// <summary>
    /// Determines whether <paramref name="left"/> is less than or equal to
    /// <paramref name="right"/> based on their <see cref="MathValue"/> values.
    /// </summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns><see langword="true"/> if <paramref name="left"/> is less than or equal
    /// to <paramref name="right"/>; otherwise <see langword="false"/>.
    /// </returns>
    public static bool operator <=(MathValue left, EvaluationResult right) => left <= right.Value;

    // Explicit conversions to primitive types and BigInteger.

    /// <summary>
    /// Returns the result's value as a decimal.
    /// </summary>
    public static explicit operator decimal(EvaluationResult result) => result.Value.DecimalValue;

    /// <summary>
    /// Returns the result's value as a <see cref="BigInteger"/>.
    /// </summary>
    public static explicit operator BigInteger(EvaluationResult result) => result.Value.IntegerValue;

    /// <summary>
    /// Returns the result's value as an int.
    /// </summary>
    public static explicit operator int(EvaluationResult result) => (int)result.Value.IntegerValue;

    /// <summary>
    /// Returns the result's value as a long.
    /// </summary>
    public static explicit operator long(EvaluationResult result) => (long)result.Value.IntegerValue;

    /// <summary>
    /// Returns the result as a double.
    /// </summary>
    public static explicit operator double(EvaluationResult result) => result.Value.AsDouble();

    /// <summary>
    /// Returns the hexadecimal string representation of the underlying
    /// <see cref="MathValue" /> if it is an integer; otherwise <see langword="null" />.
    /// </summary>
    /// <param name="prefix">Whether to include the "0x" prefix.</param>
    /// <returns>The hexadecimal string representation or <see langword="null" /> if not
    /// an integer.</returns>
    public string? ToHexString(bool prefix = true) =>
        Value.ToHexString(prefix);

    /// <summary>
    /// Returns the binary string representation of the underlying
    /// <see cref="MathValue" /> if it is an integer; otherwise <see langword="null" />.
    /// </summary>
    /// <param name="prefix">Whether to include the "0b" prefix.</param>
    /// <returns>The binary string representation or <see langword="null" /> if not an
    /// integer.</returns>
    public string? ToBinaryString(bool prefix = true) =>
        Value.ToBinaryString(prefix);

    /// <summary>
    /// Returns the octal string representation of the underlying <see cref="MathValue" />
    /// if it is an integer; otherwise <see langword="null" />.
    /// </summary>
    /// <param name="prefix">Whether to include the "0o" prefix.</param>
    /// <returns>The octal string representation or <see langword="null" /> if not an
    /// integer.</returns>
    public string? ToOctalString(bool prefix = true) =>
        Value.ToOctalString(prefix);

    // Formatting interfaces: IFormattable, ISpanFormattable, IUtf8SpanFormattable.
    /// <summary>
    /// Formats the value of the current instance using the specified format string and
    /// format provider.
    /// </summary>
    /// <param name="format">A format string that defines the output representation.
    /// </param>
    /// <param name="formatProvider">An object that supplies culture-specific formatting
    /// information.</param>
    /// <returns>The string representation of the value of the current instance as
    /// specified by format and formatProvider.</returns>
    public string ToString(string? format, IFormatProvider? formatProvider) =>
        Value.ToString(format, formatProvider);

    /// <summary>
    /// Formats the result's value using the specified format provider and no format
    /// string.
    /// </summary>
    /// <param name="formatProvider">The format provider to use.</param>
    /// <returns>The formatted string representation of the value.</returns>
    public string ToString(IFormatProvider? formatProvider) =>
        ToString(null, formatProvider);

    /// <summary>
    /// Formats the value into a character span.
    /// </summary>
    public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format = default, IFormatProvider? provider = null) =>
        Value.TryFormat(destination, out charsWritten, format, provider);

    /// <summary>
    /// Formats the value into a UTF-8 byte span.
    /// </summary>
    public bool TryFormat(Span<byte> utf8Destination, out int bytesWritten, ReadOnlySpan<char> format = default, IFormatProvider? provider = null) =>
        Value.TryFormat(utf8Destination, out bytesWritten, format, provider);

    /// <summary>
    /// Returns the hash code of the underlying <see cref="MathValue" />.
    /// </summary>
    public override int GetHashCode() => Value.GetHashCode();

    /// <summary>
    /// Formats the value as a string using the underlying
    /// <see cref="MathValue.ToString()" />.
    /// </summary>
    public override string ToString() => Value.ToString();

    /// <summary>
    /// Gets a formatted summary of assigned variables (e.g. "a = 10, b = 20"), or
    /// <see langword="null"/> if no assignments occurred.
    /// </summary>
    public string? AssignmentSummary =>
        HasAssignments ? AssignedVariables.ToString() : null;

    /// <summary>
    /// Formats this result into a presentation-ready card using the engine's culture.
    /// </summary>
    /// <param name="culture">Optional <see cref="CultureInfo"/> to use for formatting, or
    /// <c>null</c> to use the engine's culture.</param>
    /// <param name="prefixRadixOutput">Whether to include the "0x", "0b", and "0o"
    /// prefixes.</param>
    /// <returns>A <see cref="FormattedCalculation"/> representing the formatted result.
    /// </returns>
    public FormattedCalculation FormatResult(CultureInfo? culture = null, bool prefixRadixOutput = true) =>
        (Engine ?? CalculatorEngine.Default).FormatResult(this, culture, prefixRadixOutput);

    /// <summary>
    /// Gets the localized, human-facing formatted representation of the result's value
    /// (e.g. "1002,5" in de-DE), formatted using the originating engine's culture.
    /// </summary>
    public string PrimaryDisplay => FormatResult().PrimaryDisplay;

    /// <summary>
    /// Gets the invariant machine-readable string for this result (e.g. "1002.5").
    /// </summary>
    public string MachineReadable => FormatResult().MachineReadable;

    // AST preservation and synthesis helpers.

    private ExpressionNode GetExpressionNode() =>
        Context.PreviousExpression ?? new NumberNode(Value);

    private static EvaluationResult CombineBinary(
        EvaluationResult left,
        EvaluationResult right,
        BinaryOperator op)
    {
        var engine = left.Engine ?? right.Engine ?? CalculatorEngine.Default;
        var leftNode = left.GetExpressionNode();
        var rightNode = right.GetExpressionNode();
        var binary = new BinaryNode(op, leftNode, rightNode);

        // Merge contexts: right variables overlay left variables.
        var mergedVariables = left.Context.Variables.SetItems(right.Context.Variables);
        var mergedContext = new EvaluationContext(mergedVariables, left.Value, leftNode);

        return engine.Evaluate(binary, mergedContext);
    }

    private static EvaluationResult CombineBinary(
        EvaluationResult left,
        MathValue right,
        BinaryOperator op)
    {
        var engine = left.Engine ?? CalculatorEngine.Default;
        var leftNode = left.GetExpressionNode();
        var rightNode = new NumberNode(right);
        var binary = new BinaryNode(op, leftNode, rightNode);

        return engine.Evaluate(binary, left.Context);
    }

    private static EvaluationResult CombineBinary(
        MathValue left,
        EvaluationResult right,
        BinaryOperator op)
    {
        var engine = right.Engine ?? CalculatorEngine.Default;
        var leftNode = new NumberNode(left);
        var rightNode = right.GetExpressionNode();
        var binary = new BinaryNode(op, leftNode, rightNode);

        return engine.Evaluate(binary, right.Context);
    }
}
