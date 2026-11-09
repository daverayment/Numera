using System.Collections.Frozen;
using System.Globalization;
using System.Numerics;
using System.Text;
using Numera.Ast;

namespace Numera;

/// <summary>
/// The result of evaluating an expression, including the computed value, updated context,
/// and any variables assigned during evaluation.
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
    /// Empty if no assignments occurred.
    /// </summary>
    public VariableCollection AssignedVariables { get; }

    /// <summary>
    /// Returns <c>true</c> if one or more variables were assigned or updated in this evaluation.
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
    /// Gets the engine or calculation options associated with this result, if specified; otherwise <c>null</c>.
    /// </summary>
    public CalculatorOptions? Options { get; }

    /// <summary>
    /// Gets the formatting options attached to this result, if specified; otherwise <c>null</c>.
    /// </summary>
    public ResultFormatOptions? FormatOptions => Options?.ResultFormat;

    /// <summary>
    /// Gets the culture associated with this result, if specified; otherwise <c>null</c>.
    /// </summary>
    public CultureInfo? Culture => Options?.Culture;

    /// <summary>
    /// Initializes a new instance of the <see cref="EvaluationResult"/> struct.
    /// </summary>
    /// <param name="value">The evaluated value of the expression.</param>
    /// <param name="context">The updated context containing the new
    /// <see cref="EvaluationContext.PreviousResult"/> (<c>ans</c>) and any new variable
    /// assignments merged into <see cref="EvaluationContext.Variables"/>.</param>
    /// <param name="assignedVariables">The variables assigned or updated by the
    /// expression, or <c>null</c> if no variables were assigned.</param>
    /// <param name="options">Optional calculator options to associate with this result.</param>
    public EvaluationResult(
        MathValue value,
        EvaluationContext context,
        VariableCollection? assignedVariables = null,
        CalculatorOptions? options = null)
    {
        Value = value;
        Context = context;
        AssignedVariables = assignedVariables ?? VariableCollection.Empty;
        Options = options;
    }

    // Member-forwarding to MathValue.

    /// <summary>
    /// Gets the data type of the current math value.
    /// </summary>
    public MathValue.DataType Type => Value.Kind;

    /// <summary>
    /// Gets the reduced verified exact fraction, or null when finite Exact provenance is unavailable.
    /// </summary>
    public (BigInteger Numerator, BigInteger Denominator)? ExactFraction => Value.ExactFraction;

    /// <summary>
    /// Gets the reduced fraction when the finite value has Exact calculation provenance.
    /// </summary>
    /// <returns>The verified exact numerator and positive denominator in lowest terms.</returns>
    /// <exception cref="InvalidOperationException">The value has no verified exact fractional representation.</exception>
    public (BigInteger Numerator, BigInteger Denominator) GetExactFraction() => Value.GetExactFraction();

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
    /// Returns true if the value is mathematically undefined (Not-a-Number); otherwise false.
    /// </summary>
    public bool IsNaN => Value.IsNaN;

    /// <summary>
    /// Returns true if the value is zero.
    /// </summary>
    public bool IsZero => Value.IsZero;

    /// <summary>
    /// Whether a verified-exact finite fractional representation is available.
    /// </summary>
    public bool HasExactFraction => Value.TryGetExactFraction(out _, out _);

    /// <summary>
    /// Gets the accuracy established for the calculation, independently of display rounding.
    /// </summary>
    public CalculationAccuracy Accuracy => Value.Accuracy;

    /// <summary>
    /// Forces the value into a standard 64-bit floating-point representation.
    /// </summary>
    public double AsDouble() => Value.AsDouble();

    /// <summary>
    /// Implicit conversion to <see cref="MathValue"/>.
    /// </summary>
    public static implicit operator MathValue(EvaluationResult result) => result.Value;

    /// <summary>
    /// Equality based on <see cref="MathValue"/>.
    /// </summary>
    public bool Equals(MathValue other) => Value.Equals(other);

    // Operator overloads.

    /// <summary>
    /// Adds two evaluation results.
    /// </summary>
    public static EvaluationResult operator +(EvaluationResult left, EvaluationResult right) =>
        Combine(left, right, BinaryOperator.Add);

    /// <summary>
    /// Subtracts the right evaluation result from the left.
    /// </summary>
    public static EvaluationResult operator -(EvaluationResult left, EvaluationResult right) =>
        Combine(left, right, BinaryOperator.Subtract);

    /// <summary>
    /// Multiplies two evaluation results.
    /// </summary>
    public static EvaluationResult operator *(EvaluationResult left, EvaluationResult right) =>
        Combine(left, right, BinaryOperator.Multiply);

    /// <summary>
    /// Divides the left evaluation result by the right.
    /// </summary>
    public static EvaluationResult operator /(EvaluationResult left, EvaluationResult right) =>
        Combine(left, right, BinaryOperator.Divide);

    /// <summary>
    /// Computes the remainder of dividing the left evaluation result by the right.
    /// </summary>
    public static EvaluationResult operator %(EvaluationResult left, EvaluationResult right) =>
        Combine(left, right, BinaryOperator.Modulo);

    /// <summary>
    /// Adds a math value to an evaluation result.
    /// </summary>
    public static EvaluationResult operator +(EvaluationResult left, MathValue right) =>
        Combine(left, right, BinaryOperator.Add);

    /// <summary>
    /// Adds an evaluation result to a math value.
    /// </summary>
    public static EvaluationResult operator +(MathValue left, EvaluationResult right) =>
        Combine(left, right, BinaryOperator.Add);

    /// <summary>
    /// Subtracts a math value from an evaluation result.
    /// </summary>
    public static EvaluationResult operator -(EvaluationResult left, MathValue right) =>
        Combine(left, right, BinaryOperator.Subtract);

    /// <summary>
    /// Subtracts an evaluation result from a math value.
    /// </summary>
    public static EvaluationResult operator -(MathValue left, EvaluationResult right) =>
        Combine(left, right, BinaryOperator.Subtract);

    /// <summary>
    /// Multiplies an evaluation result by a math value.
    /// </summary>
    public static EvaluationResult operator *(EvaluationResult left, MathValue right) =>
        Combine(left, right, BinaryOperator.Multiply);

    /// <summary>
    /// Multiplies a math value by an evaluation result.
    /// </summary>
    public static EvaluationResult operator *(MathValue left, EvaluationResult right) =>
        Combine(left, right, BinaryOperator.Multiply);

    /// <summary>
    /// Divides an evaluation result by a math value.
    /// </summary>
    public static EvaluationResult operator /(EvaluationResult left, MathValue right) =>
        Combine(left, right, BinaryOperator.Divide);

    /// <summary>
    /// Divides a math value by an evaluation result.
    /// </summary>
    public static EvaluationResult operator /(MathValue left, EvaluationResult right) =>
        Combine(left, right, BinaryOperator.Divide);

    /// <summary>
    /// Computes the remainder of dividing an evaluation result by a math value.
    /// </summary>
    public static EvaluationResult operator %(EvaluationResult left, MathValue right) =>
        Combine(left, right, BinaryOperator.Modulo);

    /// <summary>
    /// Computes the remainder of dividing a math value by an evaluation result.
    /// </summary>
    public static EvaluationResult operator %(MathValue left, EvaluationResult right) =>
        Combine(left, right, BinaryOperator.Modulo);

    /// <summary>
    /// Negates an evaluation result.
    /// </summary>
    public static EvaluationResult operator -(EvaluationResult operand)
    {
        var node = new UnaryNode(UnaryOperator.Negate, operand.GetExpressionNode());
        var options = operand.Options ?? CalculatorOptions.Default;
        AstValidator.Validate(node, options, allowUnknownFunctions: true, validateNodeInvariants: false);
        operand.Value.ValidateLimits(options.MaxIntegerBits);
        var val = -operand.Value;
        val.ValidateLimits(options.MaxIntegerBits);
        return new EvaluationResult(val, operand.Context.Advance(null, val, node), options: options);
    }

    /// <summary>
    /// Unary positive operator.
    /// </summary>
    public static EvaluationResult operator +(EvaluationResult operand)
    {
        var node = new UnaryNode(UnaryOperator.Positive, operand.GetExpressionNode());
        var options = operand.Options ?? CalculatorOptions.Default;
        AstValidator.Validate(node, options, allowUnknownFunctions: true, validateNodeInvariants: false);
        operand.Value.ValidateLimits(options.MaxIntegerBits);
        var val = operand.Value;
        return new EvaluationResult(val, operand.Context.Advance(null, val, node), options: options);
    }

    // Comparison operators with MathValue.

    /// <summary>Determines if an evaluation result equals a math value.</summary>
    public static bool operator ==(EvaluationResult left, MathValue right) => left.Value.Equals(right);
    /// <summary>Determines if a math value equals an evaluation result.</summary>
    public static bool operator ==(MathValue left, EvaluationResult right) => left.Equals(right.Value);
    /// <summary>Determines if an evaluation result does not equal a math value.</summary>
    public static bool operator !=(EvaluationResult left, MathValue right) => !left.Value.Equals(right);
    /// <summary>Determines if a math value does not equal an evaluation result.</summary>
    public static bool operator !=(MathValue left, EvaluationResult right) => !left.Equals(right.Value);
    /// <summary>Determines if an evaluation result is greater than a math value.</summary>
    public static bool operator >(EvaluationResult left, MathValue right) => left.Value > right;
    /// <summary>Determines if a math value is greater than an evaluation result.</summary>
    public static bool operator >(MathValue left, EvaluationResult right) => left > right.Value;
    /// <summary>Determines if an evaluation result is less than a math value.</summary>
    public static bool operator <(EvaluationResult left, MathValue right) => left.Value < right;
    /// <summary>Determines if a math value is less than an evaluation result.</summary>
    public static bool operator <(MathValue left, EvaluationResult right) => left < right.Value;
    /// <summary>Determines if an evaluation result is greater than or equal to a math value.</summary>
    public static bool operator >=(EvaluationResult left, MathValue right) => left.Value >= right;
    /// <summary>Determines if a math value is greater than or equal to an evaluation result.</summary>
    public static bool operator >=(MathValue left, EvaluationResult right) => left >= right.Value;
    /// <summary>Determines if an evaluation result is less than or equal to a math value.</summary>
    public static bool operator <=(EvaluationResult left, MathValue right) => left.Value <= right;
    /// <summary>Determines if a math value is less than or equal to an evaluation result.</summary>
    public static bool operator <=(MathValue left, EvaluationResult right) => left <= right.Value;

    // Explicit conversions.

    /// <summary>Converts the evaluation result explicitly to <see cref="decimal"/>.</summary>
    public static explicit operator decimal(EvaluationResult result) => result.Value.ToDecimal();
    /// <summary>Converts the evaluation result explicitly to <see cref="BigInteger"/>.</summary>
    public static explicit operator BigInteger(EvaluationResult result) => GetInteger(result.Value);
    /// <summary>Converts the evaluation result explicitly to <see cref="int"/>.</summary>
    public static explicit operator int(EvaluationResult result) => (int)GetInteger(result.Value);
    /// <summary>Converts the evaluation result explicitly to <see cref="long"/>.</summary>
    public static explicit operator long(EvaluationResult result) => (long)GetInteger(result.Value);
    /// <summary>Converts the evaluation result explicitly to <see cref="double"/>.</summary>
    public static explicit operator double(EvaluationResult result) => result.Value.AsDouble();

    // Radix representations.

    /// <summary>Returns the hexadecimal string representation of the result if integral.</summary>
    public string? ToHexString(bool prefix = true) => Value.ToHexString(prefix);
    /// <summary>Returns the binary string representation of the result if integral.</summary>
    public string? ToBinaryString(bool prefix = true) => Value.ToBinaryString(prefix);
    /// <summary>Returns the octal string representation of the result if integral.</summary>
    public string? ToOctalString(bool prefix = true) => Value.ToOctalString(prefix);

    // Presentation and formatting helpers.

    /// <summary>
    /// Formats this result into a presentation-ready card using the specified options and culture.
    /// </summary>
    /// <param name="options">Optional formatting options; defaults to result or standard display settings.</param>
    /// <param name="culture">Optional culture used for formatting; defaults to result or invariant culture.</param>
    /// <param name="prefixRadixOutput">Whether to include radix prefixes ("0x", "0b", "0o").</param>
    /// <returns>A <see cref="FormattedCalculation"/> representing the formatted result.</returns>
    public FormattedCalculation FormatResult(
        ResultFormatOptions? options = null, CultureInfo? culture = null, bool prefixRadixOutput = true) =>
        ResultFormatter.Format(this, options ?? FormatOptions, culture ?? Culture, prefixRadixOutput);

    /// <summary>
    /// Formats this result into a presentation-ready card using the specified culture and default or result options.
    /// </summary>
    /// <param name="culture">The culture used for formatting.</param>
    /// <param name="prefixRadixOutput">Whether to include radix prefixes ("0x", "0b", "0o").</param>
    /// <returns>A <see cref="FormattedCalculation"/> representing the formatted result.</returns>
    public FormattedCalculation FormatResult(
        CultureInfo culture, bool prefixRadixOutput = true) =>
        ResultFormatter.Format(this, FormatOptions, culture, prefixRadixOutput);

    /// <summary>
    /// Gets the primary human-facing formatted representation of the result's value.
    /// </summary>
    public string PrimaryDisplay => FormatDisplay(FormatOptions, Culture);

    /// <summary>
    /// Gets the invariant machine-readable string for this result.
    /// </summary>
    public string MachineReadable => Value.ToInvariantExpression();

    /// <summary>
    /// Gets a formatted summary of assigned variables (e.g. "a = 10, b = 20"), or
    /// <see langword="null"/> if no assignments occurred.
    /// </summary>
    public string? AssignmentSummary => HasAssignments ? AssignedVariables.ToString() : null;

    // Formatting interfaces.

    /// <inheritdoc />
    public override string ToString() => FormatDisplay(FormatOptions, Culture);
    /// <inheritdoc cref="IFormattable.ToString(string?, IFormatProvider?)" />
    public string ToString(IFormatProvider? formatProvider) => ToString(null, formatProvider);

    /// <inheritdoc />
    public string ToString(string? format, IFormatProvider? formatProvider) =>
        string.IsNullOrEmpty(format)
            ? FormatDisplay(FormatOptions, formatProvider)
            : Value.ToString(format, formatProvider);

    private string FormatDisplay(ResultFormatOptions? options, IFormatProvider? provider)
    {
        var (text, _) = ResultValueFormatter.Format(
            Value,
            options ?? FormatOptions ?? ResultFormatOptions.Default,
            provider ?? Culture ?? CultureInfo.InvariantCulture);
        return text;
    }

    /// <inheritdoc />
    public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
    {
        if (!format.IsEmpty)
        {
            return Value.TryFormat(destination, out charsWritten, format, provider);
        }

        string text = ToString(null, provider);
        bool success = text.AsSpan().TryCopyTo(destination);
        charsWritten = success ? text.Length : 0;
        return success;
    }

    /// <inheritdoc />
    public bool TryFormat(Span<byte> utf8Destination, out int bytesWritten, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
    {
        if (!format.IsEmpty)
        {
            return Value.TryFormat(utf8Destination, out bytesWritten, format, provider);
        }

        bool success = Encoding.UTF8.TryGetBytes(ToString(null, provider), utf8Destination, out bytesWritten);
        if (!success) bytesWritten = 0;
        return success;
    }

    /// <inheritdoc />
    public override int GetHashCode() => Value.GetHashCode();

    // Context and AST preservation helpers.

    private ExpressionNode GetExpressionNode() =>
        Context.PreviousExpression ?? new NumberNode(Value);

    private static BigInteger GetInteger(MathValue value) =>
        value.TryGetInteger(out var integer)
            ? integer
            : throw new InvalidCastException("The value is not an integer.");

    private static EvaluationResult Combine(
        EvaluationResult left, EvaluationResult right, BinaryOperator op)
    {
        var leftNode = left.GetExpressionNode();
        var rightNode = right.GetExpressionNode();
        var node = new BinaryNode(op, leftNode, rightNode);
        var options = left.Options ?? right.Options ?? CalculatorOptions.Default;
        AstValidator.Validate(node, options, allowUnknownFunctions: true, validateNodeInvariants: false);

        var resultValue = CalculateBinaryValue(left.Value, right.Value, node, options);

        var mergedVariables = left.Context.Variables.SetItems(right.Context.Variables);
        var mergedContext = new EvaluationContext(mergedVariables, resultValue, node);
        return new EvaluationResult(resultValue, mergedContext, options: options);
    }

    private static EvaluationResult Combine(
        EvaluationResult left, MathValue right, BinaryOperator op)
    {
        var leftNode = left.GetExpressionNode();
        var rightNode = new NumberNode(right);
        var node = new BinaryNode(op, leftNode, rightNode);
        var options = left.Options ?? CalculatorOptions.Default;
        AstValidator.Validate(node, options, allowUnknownFunctions: true, validateNodeInvariants: false);

        var resultValue = CalculateBinaryValue(left.Value, right, node, options);

        return new EvaluationResult(resultValue, left.Context.Advance(null, resultValue, node), options: options);
    }

    private static EvaluationResult Combine(
        MathValue left, EvaluationResult right, BinaryOperator op)
    {
        var leftNode = new NumberNode(left);
        var rightNode = right.GetExpressionNode();
        var node = new BinaryNode(op, leftNode, rightNode);
        var options = right.Options ?? CalculatorOptions.Default;
        AstValidator.Validate(node, options, allowUnknownFunctions: true, validateNodeInvariants: false);

        var resultValue = CalculateBinaryValue(left, right.Value, node, options);

        return new EvaluationResult(resultValue, right.Context.Advance(null, resultValue, node), options: options);
    }

    private static MathValue CalculateBinaryValue(
        MathValue left, MathValue right, BinaryNode node, CalculatorOptions options)
    {
        left.ValidateLimits(options.MaxIntegerBits);
        right.ValidateLimits(options.MaxIntegerBits);

        MathValue resultValue;
        if (PercentRules.IsMarkup(node))
        {
            var one = new MathValue(1);
            resultValue = node.Op is BinaryOperator.Add
                ? left * (one + right)
                : left * (one - right);
        }
        else
        {
            resultValue = node.Op switch
            {
                BinaryOperator.Add => left + right,
                BinaryOperator.Subtract => left - right,
                BinaryOperator.Multiply => left * right,
                BinaryOperator.Divide => left / right,
                BinaryOperator.Modulo => left % right,
                _ => throw new InvalidOperationException()
            };
        }

        resultValue.ValidateLimits(options.MaxIntegerBits);
        return resultValue;
    }
}
