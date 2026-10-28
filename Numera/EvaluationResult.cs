namespace Numera;

/// <summary>
/// The result of evaluating an expression, including the value, updated context, and any 
/// variables assigned by the expression.
/// </summary>
public readonly record struct EvaluationResult : IEquatable<MathValue>
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
    public MathValue.DataType Type => Value.Type;

    /// <summary>
    /// Gets the stored <see cref="System.Numerics.BigInteger"/> value when
    /// <see cref="Type"/> is <see cref="MathValue.DataType.Integer"/>; otherwise default.
    /// </summary>
    public System.Numerics.BigInteger IntVal => Value.IntVal;

    /// <summary>
    /// Gets the stored <see cref="decimal"/> value when <see cref="Type"/> is
    /// <see cref="MathValue.DataType.Decimal"/>; otherwise default.
    /// </summary>
    public decimal DecVal => Value.DecVal;

    /// <summary>
    /// Gets the value as a double-precision floating-point number.
    /// </summary>
    public double DblVal => Value.DblVal;

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
    /// Returns the hash code of the underlying <see cref="MathValue" />.
    /// </summary>
    public override int GetHashCode() => Value.GetHashCode();

    /// <summary>
    /// Formats the value as a string using the underlying
    /// <see cref="MathValue.ToString()" />.
    /// </summary>
    public override string ToString() => Value.ToString();
}
