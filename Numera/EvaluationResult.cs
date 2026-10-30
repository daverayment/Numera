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
    /// Initializes a new instance of the <see cref="EvaluationResult"/> struct.
    /// </summary>
    /// <param name="value">The evaluated value of the expression.</param>
    /// <param name="context">The updated context containing the new
    /// <see cref="EvaluationContext.PreviousResult"/> (<c>ans</c>) and any new variable
    /// assignments merged into <see cref="EvaluationContext.Variables"/>.</param>
    /// <param name="assignedVariables">The variables assigned or updated by the
    /// expression, or <c>null</c> if the expression did not assign any variable.</param>
    public EvaluationResult(
        MathValue value,
        EvaluationContext context,
        VariableCollection? assignedVariables = null)
    {
        Value = value;
        Context = context;
        AssignedVariables = assignedVariables ?? VariableCollection.Empty;
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
    public System.Numerics.BigInteger IntegerValue => Value.IntegerValue;

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

    /// <summary>
    /// Determines whether an evaluation result and a math value are equal.
    /// </summary>
    public static bool operator ==(EvaluationResult left, MathValue right) => left.Value.Equals(right);

    /// <summary>
    /// Determines whether an evaluation result and a math value are not equal.
    /// </summary>
    public static bool operator !=(EvaluationResult left, MathValue right) => !left.Value.Equals(right);

    /// <summary>
    /// Determines whether a math value and an evaluation result are equal.
    /// </summary>
    public static bool operator ==(MathValue left, EvaluationResult right) => left.Equals(right.Value);

    /// <summary>
    /// Determines whether a math value and an evaluation result are not equal.
    /// </summary>
    public static bool operator !=(MathValue left, EvaluationResult right) => !left.Equals(right.Value);

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
}
