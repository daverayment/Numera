using System.Collections.Frozen;
using System.Globalization;
using System.Numerics;
using System.Text;
using Numera.Ast;

namespace Numera;

/// <summary>
/// An expression's value, updated variables and previous answer, and details of the
/// evaluation.
/// </summary>
/// <remarks>
/// <para>
/// Arithmetic uses the values already calculated; it does not run the original
/// expressions or their functions again. Each operation returns a new result with an
/// updated <c>ans</c>, a combined expression for inspection, and no new assignments.
/// Addition and subtraction also respect calculator percentage rules, as described on
/// those operators.
/// </para>
/// <para>
/// When combining two results, their variables are merged; the right result wins on name
/// conflicts. The left result's options are used if present, otherwise the right
/// result's, otherwise <see cref="CalculatorOptions.Default"/>. An operation with only
/// one result keeps that result's variables and uses its options or the defaults.
/// Calculation limits apply to the values, intermediate arithmetic, and combined
/// expression.
/// </para>
/// <para>
/// Comparisons with <see cref="MathValue"/> compare numbers, ignoring other result
/// details. They treat two NaNs as equal and sort NaN before other values, unlike
/// comparisons within evaluated expressions. Equality between two results also considers
/// their context, assignments, and options.
/// </para>
/// <para>
/// Use the constructor when creating a result yourself. A default-initialized result has
/// no usable context or assignment collection.
/// </para>
/// </remarks>
public readonly record struct EvaluationResult :
    IEquatable<MathValue>,
    IFormattable,
    ISpanFormattable,
    IUtf8SpanFormattable
{
    /// <summary>
    /// Gets the calculated value.
    /// </summary>
    public MathValue Value { get; }

    /// <summary>
    /// Gets the updated variables and previous answer (<c>ans</c>).
    /// </summary>
    /// <remarks>Pass this context to the next evaluation to continue the calculation.
    /// </remarks>
    public EvaluationContext Context { get; }

    /// <summary>
    /// Gets the variables assigned or updated by this evaluation, or an empty collection
    /// if none.
    /// </summary>
    /// <remarks>
    /// For all current variables, use <see cref="EvaluationContext.Variables"/> on
    /// <see cref="Context"/>. Arithmetic on existing results makes no new assignments.
    /// </remarks>
    public VariableCollection AssignedVariables { get; }

    /// <summary>
    /// Gets whether this evaluation assigned or updated any variables.
    /// </summary>
    public bool HasAssignments => AssignedVariables.Count > 0;

    /// <summary>
    /// Gets the name of the only variable assigned, or <see langword="null"/> if none or
    /// several were assigned.
    /// </summary>
    public string? SingleAssignedVariable =>
        AssignedVariables.Count == 1 ? AssignedVariables.Keys.First() : null;

    /// <summary>
    /// Gets the distinct variable names read by the expression, or an empty set if none
    /// are recorded.
    /// </summary>
    /// <remarks>
    /// Includes variables in branches that were not evaluated, but not constants or names
    /// used only as assignment targets. Names are compared case-insensitively.
    /// </remarks>
    public IReadOnlySet<string> ReferencedVariables =>
        Context.PreviousExpression?.GetReferencedVariables() ?? FrozenSet<string>.Empty;

    /// <summary>
    /// Gets each variable reference and its source position, including repeated uses of a
    /// name.
    /// </summary>
    /// <remarks>
    /// Follows the same rules as <see cref="ReferencedVariables"/> and is empty if no
    /// references are recorded. After arithmetic on results, positions still refer to the
    /// original expressions, not the combined display text.
    /// </remarks>
    public IReadOnlyList<VariableReference> VariableReferences =>
        Context.PreviousExpression?.GetVariableReferences() ?? [];

    /// <summary>
    /// Gets the calculator options used for this result, or <see langword="null"/> if
    /// none were supplied.
    /// </summary>
    /// <remarks>
    /// Results from an engine keep its options for display formatting and later
    /// arithmetic. Without options, library defaults apply.
    /// </remarks>
    public CalculatorOptions? Options { get; }

    /// <summary>
    /// Gets the result's display options, or <see langword="null"/> if none were
    /// supplied.
    /// </summary>
    /// <remarks>
    /// Taken from <see cref="CalculatorOptions.ResultFormat"/> in <see cref="Options"/>.
    /// Display formatting falls back to <see cref="ResultFormatOptions.Default"/> when
    /// absent.
    /// </remarks>
    public ResultFormatOptions? FormatOptions => Options?.ResultFormat;

    /// <summary>
    /// Gets the result's culture, or <see langword="null"/> if no calculator options were
    /// supplied.
    /// </summary>
    /// <remarks>
    /// Display formatting uses current culture when absent. Supplying a format provider
    /// overrides the result's culture.
    /// </remarks>
    public CultureInfo? Culture => Options?.Culture;

    /// <summary>
    /// Creates a result from a value and a context.
    /// </summary>
    /// <param name="value">The calculated value.</param>
    /// <param name="context">The variables and previous answer to store with the result.
    /// </param>
    /// <param name="assignedVariables">Variables assigned by this evaluation, or
    /// <see langword="null"/> for an empty collection.</param>
    /// <param name="options">Options for display and later arithmetic, or
    /// <see langword="null"/> for defaults.</param>
    /// <remarks>
    /// Stores the supplied data without evaluating an expression or checking calculation
    /// limits. It does not update the context's variables or <c>ans</c>; supply a context
    /// that already represents the state you want.
    /// </remarks>
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
    /// Gets how the number is stored: Integer, Decimal, Rational, or Double.
    /// </summary>
    /// <remarks>The storage type alone does not tell you whether the result is exact.
    /// </remarks>
    public MathValue.DataType Type => Value.Kind;

    /// <summary>
    /// Gets the exact fraction in lowest terms, or <see langword="null"/> if unavailable.
    /// </summary>
    /// <remarks>
    /// Available for finite values marked <see cref="CalculationExactness.Exact"/>,
    /// regardless of storage type. The denominator is positive; whole numbers have
    /// denominator one. For a caller-marked Exact Double, the fraction represents its
    /// stored binary value.
    /// </remarks>
    public (BigInteger Numerator, BigInteger Denominator)? ExactFraction => Value.ExactFraction;

    /// <summary>
    /// Returns the exact fraction in lowest terms, or throws if unavailable.
    /// </summary>
    /// <returns>The numerator and positive denominator. Whole numbers have denominator
    /// one.</returns>
    /// <remarks>Use <see cref="ExactFraction"/> if an unavailable fraction should return
    /// null instead.</remarks>
    /// <exception cref="InvalidOperationException">The value is not finite or is not
    /// marked <see cref="CalculationExactness.Exact"/>.</exception>
    public (BigInteger Numerator, BigInteger Denominator) GetExactFraction() => Value.GetExactFraction();

    /// <summary>
    /// Gets the stored integer without converting other storage types.
    /// </summary>
    /// <remarks>Does not convert other storage types. Cast to <see cref="BigInteger"/>
    /// to convert a whole number instead.</remarks>
    /// <exception cref="InvalidOperationException">The number is not stored as Integer.
    /// </exception>
    public BigInteger IntegerValue => Value.IntegerValue;

    /// <summary>
    /// Gets the stored decimal without converting other storage types.
    /// </summary>
    /// <remarks>Does not convert other storage types. Cast to <see cref="decimal"/> to
    /// convert instead.</remarks>
    /// <exception cref="InvalidOperationException">The number is not stored as Decimal.
    /// </exception>
    public decimal DecimalValue => Value.DecimalValue;

    /// <summary>
    /// Gets the stored double without converting other storage types.
    /// </summary>
    /// <remarks>Does not convert other storage types. Use <see cref="AsDouble"/> to
    /// convert instead.</remarks>
    /// <exception cref="InvalidOperationException">The number is not stored as Double.
    /// </exception>
    public double DoubleValue => Value.DoubleValue;

    /// <summary>
    /// Gets whether the value is neither NaN nor positive or negative infinity.
    /// </summary>
    public bool IsFinite => Value.IsFinite;

    /// <summary>
    /// Gets whether the value is NaN (Not-a-Number).
    /// </summary>
    public bool IsNaN => Value.IsNaN;

    /// <summary>
    /// Gets whether the value is zero, including negative zero.
    /// </summary>
    public bool IsZero => Value.IsZero;

    /// <summary>
    /// Gets whether an exact fraction is available for this result.
    /// </summary>
    /// <remarks>
    /// Requires a finite value marked <see cref="CalculationExactness.Exact"/>. Use
    /// <see cref="ExactFraction"/> to read it. Storage type and display rounding do not
    /// determine availability. For example, 1/3 enables an exact-fraction toggle even
    /// when its decimal display rounds; ln(1) does not, despite displaying 0.
    /// </remarks>
    public bool HasExactFraction => Value.TryGetExactFraction(out _, out _);

    /// <summary>
    /// Gets the known or caller-asserted exactness, for UI guidance and understanding
    /// whether a calculation path retained established exactness.
    /// </summary>
    /// <remarks>
    /// For example, 1/3 is Exact despite a rounded decimal display; ln(1) is Unverified
    /// despite displaying 0 without rounding. Use this status to label an unverified
    /// answer separately from a rounded display of an exact answer. Unverified does not
    /// mean wrong, and this status is not an error estimate or a step-by-step history.
    /// Unverified inputs propagate conservatively; caller-supplied Exact is an assertion.
    /// </remarks>
    public CalculationExactness Exactness => Value.Exactness;

    /// <summary>
    /// Converts the result to a double. Precision may be lost.
    /// </summary>
    /// <returns>The converted value, which may underflow to zero or overflow to infinity.
    /// </returns>
    /// <remarks>Does not change the stored value or its exactness status.</remarks>
    public double AsDouble() => Value.AsDouble();

    /// <summary>
    /// Extracts the calculated value without changing its storage type or exactness
    /// status.
    /// </summary>
    /// <param name="result">The result whose value to extract.</param>
    /// <returns>The result's <see cref="Value"/>.</returns>
    /// <example><code>MathValue value = Calculator.Evaluate("2 + 2");</code></example>
    public static implicit operator MathValue(EvaluationResult result) => result.Value;

    /// <summary>
    /// Compares the result's number with another value, ignoring other result details.
    /// </summary>
    /// <param name="other">The value to compare.</param>
    /// <returns>Whether the numbers are equal, including two NaNs or positive and
    /// negative zero.</returns>
    /// <remarks>Unlike comparisons in evaluated expressions, this method treats NaN as
    /// equal to NaN.</remarks>
    public bool Equals(MathValue other) => Value.Equals(other);

    // Operator overloads.

    /// <summary>
    /// Adds two results, applying calculator percentage rules where appropriate.
    /// </summary>
    /// <param name="left">The result to add to.</param>
    /// <param name="right">The result to add.</param>
    /// <returns>A new result containing the sum or percentage-adjusted value.</returns>
    /// <remarks>
    /// If the right expression is a percentage and the left is not, the percentage is
    /// applied
    /// as an increase. For example,
    /// <c>Calculator.Evaluate("100") + Calculator.Evaluate("10%")</c> gives 110, not
    /// 100.1. Unary signs around a percentage do not change this rule.
    /// </remarks>
    /// <exception cref="CalculationLimitException">A calculation or expression limit is
    /// exceeded.</exception>
    public static EvaluationResult operator +(EvaluationResult left, EvaluationResult right) =>
        Combine(left, right, BinaryOperator.Add);

    /// <summary>
    /// Subtracts two results, applying calculator percentage rules where appropriate.
    /// </summary>
    /// <param name="left">The result to subtract from.</param>
    /// <param name="right">The result to subtract.</param>
    /// <returns>A new result containing the difference or percentage-adjusted value.
    /// </returns>
    /// <remarks>
    /// If the right expression is a percentage and the left is not, the percentage is
    /// applied
    /// as a discount. For example,
    /// <c>Calculator.Evaluate("100") - Calculator.Evaluate("10%")</c> gives 90, not 99.9.
    /// Unary signs around a percentage do not change this rule.
    /// </remarks>
    /// <exception cref="CalculationLimitException">A calculation or expression limit is
    /// exceeded.</exception>
    public static EvaluationResult operator -(EvaluationResult left, EvaluationResult right) =>
        Combine(left, right, BinaryOperator.Subtract);

    /// <summary>
    /// Multiplies two results.
    /// </summary>
    /// <param name="left">The left factor.</param>
    /// <param name="right">The right factor.</param>
    /// <returns>A new result containing the product.</returns>
    /// <exception cref="CalculationLimitException">A calculation or expression limit is
    /// exceeded.</exception>
    public static EvaluationResult operator *(EvaluationResult left, EvaluationResult right) =>
        Combine(left, right, BinaryOperator.Multiply);

    /// <summary>
    /// Divides one result by another.
    /// </summary>
    /// <param name="left">The dividend.</param>
    /// <param name="right">The divisor.</param>
    /// <returns>A new result containing the quotient.</returns>
    /// <exception cref="DivideByZeroException">The divisor is zero.</exception>
    /// <exception cref="CalculationLimitException">A calculation or expression limit is
    /// exceeded.</exception>
    public static EvaluationResult operator /(EvaluationResult left, EvaluationResult right) =>
        Combine(left, right, BinaryOperator.Divide);

    /// <summary>
    /// Returns the remainder after dividing one result by another.
    /// </summary>
    /// <param name="left">The dividend.</param>
    /// <param name="right">The divisor.</param>
    /// <returns>A new result containing the remainder.</returns>
    /// <remarks>The quotient is truncated toward zero. A nonzero finite remainder has the
    /// dividend's sign.</remarks>
    /// <exception cref="DivideByZeroException">The divisor is zero.</exception>
    /// <exception cref="CalculationLimitException">A calculation or expression limit is
    /// exceeded.</exception>
    public static EvaluationResult operator %(EvaluationResult left, EvaluationResult right) =>
        Combine(left, right, BinaryOperator.Modulo);

    /// <summary>
    /// Adds a number to a result, without applying percentage rules.
    /// </summary>
    /// <param name="left">The result to add to.</param>
    /// <param name="right">The number to add.</param>
    /// <returns>A new result containing the sum.</returns>
    /// <exception cref="CalculationLimitException">A calculation or expression limit is
    /// exceeded.</exception>
    public static EvaluationResult operator +(EvaluationResult left, MathValue right) =>
        Combine(left, right, BinaryOperator.Add);

    /// <summary>
    /// Adds a result to a number, applying calculator percentage rules where appropriate.
    /// </summary>
    /// <param name="left">The number to add to.</param>
    /// <param name="right">The result to add.</param>
    /// <returns>A new result containing the sum or percentage-adjusted value.</returns>
    /// <remarks>
    /// If the result's expression is a percentage, including one wrapped in unary signs,
    /// it is applied as an increase: <c>100 + Calculator.Evaluate("10%")</c> gives 110.
    /// </remarks>
    /// <exception cref="CalculationLimitException">A calculation or expression limit is
    /// exceeded.</exception>
    public static EvaluationResult operator +(MathValue left, EvaluationResult right) =>
        Combine(left, right, BinaryOperator.Add);

    /// <summary>
    /// Subtracts a number from a result, without applying percentage rules.
    /// </summary>
    /// <param name="left">The result to subtract from.</param>
    /// <param name="right">The number to subtract.</param>
    /// <returns>A new result containing the difference.</returns>
    /// <exception cref="CalculationLimitException">A calculation or expression limit is
    /// exceeded.</exception>
    public static EvaluationResult operator -(EvaluationResult left, MathValue right) =>
        Combine(left, right, BinaryOperator.Subtract);

    /// <summary>
    /// Subtracts a result from a number, applying calculator percentage rules where
    /// appropriate.
    /// </summary>
    /// <param name="left">The number to subtract from.</param>
    /// <param name="right">The result to subtract.</param>
    /// <returns>A new result containing the difference or percentage-adjusted value.
    /// </returns>
    /// <remarks>
    /// If the result's expression is a percentage, including one wrapped in unary signs,
    /// it is applied as a discount: <c>100 - Calculator.Evaluate("10%")</c> gives 90.
    /// </remarks>
    /// <exception cref="CalculationLimitException">A calculation or expression limit is
    /// exceeded.</exception>
    public static EvaluationResult operator -(MathValue left, EvaluationResult right) =>
        Combine(left, right, BinaryOperator.Subtract);

    /// <summary>
    /// Multiplies a result by a number.
    /// </summary>
    /// <param name="left">The result to multiply.</param>
    /// <param name="right">The multiplier.</param>
    /// <returns>A new result containing the product.</returns>
    /// <exception cref="CalculationLimitException">A calculation or expression limit is
    /// exceeded.</exception>
    public static EvaluationResult operator *(EvaluationResult left, MathValue right) =>
        Combine(left, right, BinaryOperator.Multiply);

    /// <summary>
    /// Multiplies a number by a result.
    /// </summary>
    /// <param name="left">The number to multiply.</param>
    /// <param name="right">The multiplier.</param>
    /// <returns>A new result containing the product.</returns>
    /// <exception cref="CalculationLimitException">A calculation or expression limit is
    /// exceeded.</exception>
    public static EvaluationResult operator *(MathValue left, EvaluationResult right) =>
        Combine(left, right, BinaryOperator.Multiply);

    /// <summary>
    /// Divides a result by a number.
    /// </summary>
    /// <param name="left">The dividend.</param>
    /// <param name="right">The divisor.</param>
    /// <returns>A new result containing the quotient.</returns>
    /// <exception cref="DivideByZeroException">The divisor is zero.</exception>
    /// <exception cref="CalculationLimitException">A calculation or expression limit is
    /// exceeded.</exception>
    public static EvaluationResult operator /(EvaluationResult left, MathValue right) =>
        Combine(left, right, BinaryOperator.Divide);

    /// <summary>
    /// Divides a number by a result.
    /// </summary>
    /// <param name="left">The dividend.</param>
    /// <param name="right">The divisor.</param>
    /// <returns>A new result containing the quotient.</returns>
    /// <exception cref="DivideByZeroException">The divisor is zero.</exception>
    /// <exception cref="CalculationLimitException">A calculation or expression limit is
    /// exceeded.</exception>
    public static EvaluationResult operator /(MathValue left, EvaluationResult right) =>
        Combine(left, right, BinaryOperator.Divide);

    /// <summary>
    /// Returns the remainder after dividing a result by a number.
    /// </summary>
    /// <param name="left">The dividend.</param>
    /// <param name="right">The divisor.</param>
    /// <returns>A new result containing the remainder.</returns>
    /// <remarks>The quotient is truncated toward zero. A nonzero finite remainder has the
    /// dividend's sign.</remarks>
    /// <exception cref="DivideByZeroException">The divisor is zero.</exception>
    /// <exception cref="CalculationLimitException">A calculation or expression limit is
    /// exceeded.</exception>
    public static EvaluationResult operator %(EvaluationResult left, MathValue right) =>
        Combine(left, right, BinaryOperator.Modulo);

    /// <summary>
    /// Returns the remainder after dividing a number by a result.
    /// </summary>
    /// <param name="left">The dividend.</param>
    /// <param name="right">The divisor.</param>
    /// <returns>A new result containing the remainder.</returns>
    /// <remarks>The quotient is truncated toward zero. A nonzero finite remainder has the
    /// dividend's sign.</remarks>
    /// <exception cref="DivideByZeroException">The divisor is zero.</exception>
    /// <exception cref="CalculationLimitException">A calculation or expression limit is
    /// exceeded.</exception>
    public static EvaluationResult operator %(MathValue left, EvaluationResult right) =>
        Combine(left, right, BinaryOperator.Modulo);

    /// <summary>
    /// Negates the result's value.
    /// </summary>
    /// <param name="operand">The result to negate.</param>
    /// <returns>A new result with the opposite sign and the same exactness status.
    /// </returns>
    /// <exception cref="CalculationLimitException">A calculation or expression limit is
    /// exceeded.</exception>
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
    /// Returns a new result with the same value, recording a unary plus in its expression.
    /// </summary>
    /// <param name="operand">The result to retain.</param>
    /// <returns>A new result with the unchanged value and exactness status. Negative
    /// values remain negative.</returns>
    /// <exception cref="CalculationLimitException">A calculation or expression limit is
    /// exceeded.</exception>
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

    /// <summary>
    /// Tests whether a result and a number have equal values.
    /// </summary>
    /// <param name="left">The result to compare.</param>
    /// <param name="right">The number to compare.</param>
    /// <returns>Whether the values are equal.</returns>
    /// <remarks>Ignores other result details. Two NaNs compare equal, as do positive and
    /// negative zero.</remarks>
    public static bool operator ==(EvaluationResult left, MathValue right) => left.Value.Equals(right);

    /// <summary>
    /// Tests whether a number and a result have equal values.
    /// </summary>
    /// <param name="left">The number to compare.</param>
    /// <param name="right">The result to compare.</param>
    /// <returns>Whether the values are equal.</returns>
    /// <remarks>Ignores other result details. Two NaNs compare equal, as do positive and
    /// negative zero.</remarks>
    public static bool operator ==(MathValue left, EvaluationResult right) => left.Equals(right.Value);

    /// <summary>
    /// Tests whether a result and a number have different values.
    /// </summary>
    /// <param name="left">The result to compare.</param>
    /// <param name="right">The number to compare.</param>
    /// <returns>Whether the values differ.</returns>
    /// <remarks>Ignores other result details. Two NaNs compare equal, as do positive and
    /// negative zero.</remarks>
    public static bool operator !=(EvaluationResult left, MathValue right) => !left.Value.Equals(right);

    /// <summary>
    /// Tests whether a number and a result have different values.
    /// </summary>
    /// <param name="left">The number to compare.</param>
    /// <param name="right">The result to compare.</param>
    /// <returns>Whether the values differ.</returns>
    /// <remarks>Ignores other result details. Two NaNs compare equal, as do positive and
    /// negative zero.</remarks>
    public static bool operator !=(MathValue left, EvaluationResult right) => !left.Equals(right.Value);

    /// <summary>
    /// Tests whether a result's value is greater than a number.
    /// </summary>
    /// <param name="left">The result to compare.</param>
    /// <param name="right">The number to compare.</param>
    /// <returns>Whether the left value is greater.</returns>
    /// <remarks>NaN sorts before every other value.</remarks>
    public static bool operator >(EvaluationResult left, MathValue right) => left.Value > right;

    /// <summary>
    /// Tests whether a number is greater than a result's value.
    /// </summary>
    /// <param name="left">The number to compare.</param>
    /// <param name="right">The result to compare.</param>
    /// <returns>Whether the left value is greater.</returns>
    /// <remarks>NaN sorts before every other value.</remarks>
    public static bool operator >(MathValue left, EvaluationResult right) => left > right.Value;

    /// <summary>
    /// Tests whether a result's value is less than a number.
    /// </summary>
    /// <param name="left">The result to compare.</param>
    /// <param name="right">The number to compare.</param>
    /// <returns>Whether the left value is less.</returns>
    /// <remarks>NaN sorts before every other value.</remarks>
    public static bool operator <(EvaluationResult left, MathValue right) => left.Value < right;

    /// <summary>
    /// Tests whether a number is less than a result's value.
    /// </summary>
    /// <param name="left">The number to compare.</param>
    /// <param name="right">The result to compare.</param>
    /// <returns>Whether the left value is less.</returns>
    /// <remarks>NaN sorts before every other value.</remarks>
    public static bool operator <(MathValue left, EvaluationResult right) => left < right.Value;

    /// <summary>
    /// Tests whether a result's value is greater than or equal to a number.
    /// </summary>
    /// <param name="left">The result to compare.</param>
    /// <param name="right">The number to compare.</param>
    /// <returns>Whether the left value is greater or equal.</returns>
    /// <remarks>NaN sorts before every other value; two NaNs compare equal.</remarks>
    public static bool operator >=(EvaluationResult left, MathValue right) => left.Value >= right;

    /// <summary>
    /// Tests whether a number is greater than or equal to a result's value.
    /// </summary>
    /// <param name="left">The number to compare.</param>
    /// <param name="right">The result to compare.</param>
    /// <returns>Whether the left value is greater or equal.</returns>
    /// <remarks>NaN sorts before every other value; two NaNs compare equal.</remarks>
    public static bool operator >=(MathValue left, EvaluationResult right) => left >= right.Value;

    /// <summary>
    /// Tests whether a result's value is less than or equal to a number.
    /// </summary>
    /// <param name="left">The result to compare.</param>
    /// <param name="right">The number to compare.</param>
    /// <returns>Whether the left value is less or equal.</returns>
    /// <remarks>NaN sorts before every other value; two NaNs compare equal.</remarks>
    public static bool operator <=(EvaluationResult left, MathValue right) => left.Value <= right;

    /// <summary>
    /// Tests whether a number is less than or equal to a result's value.
    /// </summary>
    /// <param name="left">The number to compare.</param>
    /// <param name="right">The result to compare.</param>
    /// <returns>Whether the left value is less or equal.</returns>
    /// <remarks>NaN sorts before every other value; two NaNs compare equal.</remarks>
    public static bool operator <=(MathValue left, EvaluationResult right) => left <= right.Value;

    // Explicit conversions.

    /// <summary>
    /// Converts the result to a decimal, rounding if necessary.
    /// </summary>
    /// <param name="result">The result to convert.</param>
    /// <returns>The decimal value.</returns>
    /// <remarks>Fractions use midpoint-to-even rounding. Doubles use the standard .NET
    /// decimal conversion.</remarks>
    /// <exception cref="OverflowException">The value is not finite or is outside the
    /// decimal range.</exception>
    public static explicit operator decimal(EvaluationResult result) => result.Value.ToDecimal();

    /// <summary>
    /// Converts a whole-number result to a <see cref="BigInteger"/> without rounding.
    /// </summary>
    /// <param name="result">The result to convert.</param>
    /// <returns>The integer value.</returns>
    /// <exception cref="InvalidCastException">The value is not finite or has a fractional
    /// part.</exception>
    public static explicit operator BigInteger(EvaluationResult result) => GetInteger(result.Value);

    /// <summary>
    /// Converts a whole-number result to a 32-bit signed integer without rounding.
    /// </summary>
    /// <param name="result">The result to convert.</param>
    /// <returns>The integer value.</returns>
    /// <exception cref="InvalidCastException">The value is not finite or has a fractional
    /// part.</exception>
    /// <exception cref="OverflowException">The value is outside the <see cref="int"/>
    /// range.</exception>
    public static explicit operator int(EvaluationResult result) => (int)GetInteger(result.Value);

    /// <summary>
    /// Converts a whole-number result to a 64-bit signed integer without rounding.
    /// </summary>
    /// <param name="result">The result to convert.</param>
    /// <returns>The integer value.</returns>
    /// <exception cref="InvalidCastException">The value is not finite or has a fractional
    /// part.</exception>
    /// <exception cref="OverflowException">The value is outside the <see cref="long"/>
    /// range.</exception>
    public static explicit operator long(EvaluationResult result) => (long)GetInteger(result.Value);

    /// <summary>
    /// Converts the result to a double, as <see cref="AsDouble"/> does. Precision may be
    /// lost.
    /// </summary>
    /// <param name="result">The result to convert.</param>
    /// <returns>The converted value, which may underflow to zero or overflow to infinity.
    /// </returns>
    public static explicit operator double(EvaluationResult result) => result.Value.AsDouble();

    // Radix representations.

    /// <summary>
    /// Returns a hexadecimal string for a whole number, or <see langword="null"/>
    /// otherwise.
    /// </summary>
    /// <param name="prefix">Whether to include <c>0x</c>. Defaults to
    /// <see langword="true"/>.</param>
    /// <returns>The hexadecimal text, or null if the value is not finite or has a
    /// fractional part.</returns>
    /// <remarks>Negative values use a leading minus sign, for example <c>-0xFF</c>, not
    /// two's complement.</remarks>
    public string? ToHexString(bool prefix = true) => Value.ToHexString(prefix);

    /// <summary>
    /// Returns a binary string for a whole number, or <see langword="null"/> otherwise.
    /// </summary>
    /// <param name="prefix">Whether to include <c>0b</c>. Defaults to
    /// <see langword="true"/>.</param>
    /// <returns>The binary text, or null if the value is not finite or has a fractional
    /// part.</returns>
    /// <remarks>Negative values use a leading minus sign, for example <c>-0b101</c>, not
    /// two's complement.</remarks>
    public string? ToBinaryString(bool prefix = true) => Value.ToBinaryString(prefix);

    /// <summary>
    /// Returns an octal string for a whole number, or <see langword="null"/> otherwise.
    /// </summary>
    /// <param name="prefix">Whether to include <c>0o</c>. Defaults to
    /// <see langword="true"/>.</param>
    /// <returns>The octal text, or null if the value is not finite or has a fractional
    /// part.</returns>
    /// <remarks>Negative values use a leading minus sign, for example <c>-0o17</c>.
    /// </remarks>
    public string? ToOctalString(bool prefix = true) => Value.ToOctalString(prefix);

    // Presentation and formatting helpers.

    /// <summary>
    /// Formats the result for display, including available fraction and radix
    /// alternatives.
    /// </summary>
    /// <param name="options">Display options, or <see langword="null"/> to use the
    /// result's options, falling back to <see cref="ResultFormatOptions.Default"/>.
    /// </param>
    /// <param name="culture">Display culture, or <see langword="null"/> to use the
    /// result's culture, falling back to current culture.</param>
    /// <param name="prefixRadixOutput">Whether to include <c>0x</c>, <c>0b</c>, and
    /// <c>0o</c> prefixes. Defaults to <see langword="true"/>.</param>
    /// <returns>Display text and alternatives, with rounding information and any assigned
    /// variables.</returns>
    /// <remarks>
    /// All applicable representations are prepared before returning, without a character
    /// limit. Reading the snapshot's properties does not perform more formatting.
    /// Display precision does not limit the length of exact or radix alternatives.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">A formatting option is outside its
    /// supported range.</exception>
    /// <exception cref="CalculationLimitException">Formatting arithmetic exceeds a
    /// numerical safety bound.</exception>
    public FormattedCalculation FormatResult(
        ResultFormatOptions? options = null, CultureInfo? culture = null, bool prefixRadixOutput = true) =>
        ResultFormatter.Format(this, options, culture, prefixRadixOutput);

    /// <summary>
    /// Gets the number as display text, using the result's formatting options and
    /// culture.
    /// </summary>
    /// <remarks>
    /// Uses library display defaults and current culture if none were supplied. Does
    /// not format alternatives; use
    /// <see cref="FormatResult(ResultFormatOptions?, CultureInfo?, bool)"/> for those and
    /// display-rounding information. Display text alone does not establish exactness.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">A formatting option is outside its
    /// supported range.</exception>
    /// <exception cref="CalculationLimitException">Formatting arithmetic exceeds a
    /// numerical safety bound.</exception>
    public string PrimaryDisplay => FormatDisplay(FormatOptions, Culture);

    /// <summary>
    /// Gets culture-invariant text that preserves the stored finite value without display
    /// rounding.
    /// </summary>
    /// <remarks>
    /// Does not apply the result's display settings. Decimal scale is
    /// kept where representable; Doubles use fractions of their stored binary values. NaN
    /// and infinity are diagnostic text, not expressions that can be evaluated again.
    /// Evaluating this text again need not preserve the storage type, exactness status,
    /// or all display details. Use <see cref="MathValue.Serialize"/> to preserve those
    /// too.
    /// </remarks>
    public string MachineReadable => Value.ToInvariantExpression();

    /// <summary>
    /// Gets a text summary of variables assigned by this evaluation, or
    /// <see langword="null"/> if none.
    /// </summary>
    /// <remarks>Includes only <see cref="AssignedVariables"/>, not every variable in the
    /// context.</remarks>
    public string? AssignmentSummary => HasAssignments ? AssignedVariables.ToString() : null;

    // Formatting interfaces.

    /// <summary>
    /// Returns the result as display text, using its formatting options and culture.
    /// </summary>
    /// <returns>The same text as <see cref="PrimaryDisplay"/>.</returns>
    /// <remarks>Uses library display defaults and current culture if none were
    /// supplied.</remarks>
    /// <exception cref="ArgumentOutOfRangeException">A formatting option is outside its
    /// supported range.</exception>
    /// <exception cref="CalculationLimitException">Formatting arithmetic exceeds a
    /// numerical safety bound.</exception>
    public override string ToString() => FormatDisplay(FormatOptions, Culture);

    /// <summary>
    /// Returns the result as display text, optionally using another culture or number
    /// format.
    /// </summary>
    /// <param name="formatProvider">A culture or numeric format provider. Null uses the
    /// result's culture, or current culture if absent.</param>
    /// <returns>The numeric display text, retaining the result's display options.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">A formatting option is outside its
    /// supported range.</exception>
    /// <exception cref="CalculationLimitException">The display or a formatting
    /// calculation exceeds a limit.</exception>
    public string ToString(IFormatProvider? formatProvider) => ToString(null, formatProvider);

    /// <summary>
    /// Formats the result using its display options or an explicit numeric format.
    /// </summary>
    /// <param name="format">A numeric format supported by the stored type, or null or
    /// empty to use the result's display options.</param>
    /// <param name="formatProvider">A culture or numeric format provider. Null uses the
    /// result's culture, or current culture if absent, with or without an explicit
    /// format.</param>
    /// <returns>The formatted number.</returns>
    /// <remarks>Explicit formats bypass the result's display options and use
    /// <see cref="MathValue"/> formatting. For example, <c>R</c> returns a stored
    /// Rational as a slash fraction.</remarks>
    /// <exception cref="FormatException">The format is invalid or unsupported by the
    /// stored type.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A formatting option or precision is
    /// outside its supported range.</exception>
    /// <exception cref="CalculationLimitException">A formatting calculation exceeds a
    /// limit.</exception>
    public string ToString(string? format, IFormatProvider? formatProvider) =>
        string.IsNullOrEmpty(format)
            ? FormatDisplay(FormatOptions, formatProvider)
            : Value.ToString(format, formatProvider ?? Culture ?? CultureInfo.CurrentCulture);

    private string FormatDisplay(ResultFormatOptions? options, IFormatProvider? provider)
    {
        var (text, _) = ResultValueFormatter.Format(
            Value,
            options ?? FormatOptions ?? ResultFormatOptions.Default,
            provider ?? Culture ?? CultureInfo.CurrentCulture);
        return text;
    }

    /// <summary>
    /// Writes the formatted number to a character buffer, if it fits.
    /// </summary>
    /// <param name="destination">The buffer to write to.</param>
    /// <param name="charsWritten">The number of UTF-16 characters written, or zero if the
    /// buffer is too small.</param>
    /// <param name="format">An optional numeric format. Empty uses the result's display
    /// options.</param>
    /// <param name="provider">An optional culture or numeric format provider.</param>
    /// <returns>True if the complete text fits; false if the buffer is too small.
    /// </returns>
    /// <remarks>Uses the format and culture rules of
    /// <see cref="ToString(string?, IFormatProvider?)"/>.
    /// May allocate temporary text. Invalid formats and limit violations throw rather
    /// than returning false.</remarks>
    /// <exception cref="FormatException">The format is invalid or unsupported by the
    /// stored type.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A formatting option or precision is
    /// outside its supported range.</exception>
    /// <exception cref="CalculationLimitException">A formatting calculation exceeds a
    /// limit.</exception>
    public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
    {
        if (!format.IsEmpty)
        {
            return Value.TryFormat(
                destination,
                out charsWritten,
                format,
                provider ?? Culture ?? CultureInfo.CurrentCulture);
        }

        string text = ToString(null, provider);
        bool success = text.AsSpan().TryCopyTo(destination);
        charsWritten = success ? text.Length : 0;
        return success;
    }

    /// <summary>
    /// Writes the formatted number to a UTF-8 buffer, if it fits.
    /// </summary>
    /// <param name="utf8Destination">The buffer to write to.</param>
    /// <param name="bytesWritten">The number of UTF-8 bytes written, or zero if the
    /// buffer is too small.</param>
    /// <param name="format">An optional numeric format. Empty uses the result's display
    /// options.</param>
    /// <param name="provider">An optional culture or numeric format provider.</param>
    /// <returns>True if the complete text fits; false if the buffer is too small.
    /// </returns>
    /// <remarks>Uses the format and culture rules of
    /// <see cref="ToString(string?, IFormatProvider?)"/>.
    /// May allocate temporary text. Invalid formats and limit violations throw rather
    /// than returning false.</remarks>
    /// <exception cref="FormatException">The format is invalid or unsupported by the
    /// stored type.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A formatting option or precision is
    /// outside its supported range.</exception>
    /// <exception cref="CalculationLimitException">A formatting calculation exceeds a
    /// limit.</exception>
    public bool TryFormat(
        Span<byte> utf8Destination,
        out int bytesWritten,
        ReadOnlySpan<char> format = default,
        IFormatProvider? provider = null)
    {
        if (!format.IsEmpty)
        {
            return Value.TryFormat(
                utf8Destination,
                out bytesWritten,
                format,
                provider ?? Culture ?? CultureInfo.CurrentCulture);
        }

        bool success = Encoding.UTF8.TryGetBytes(ToString(null, provider), utf8Destination, out bytesWritten);
        if (!success)
        {
            bytesWritten = 0;
        }

        return success;
    }

    /// <summary>
    /// Returns a hash code based on the numeric value.
    /// </summary>
    /// <returns>The value's hash code.</returns>
    /// <remarks>Other result details are not included. Different results can have the
    /// same hash code.</remarks>
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

    // The AST supplies percentage shape, not operands to reevaluate. Use captured
    // values so changed variables, random calls, or custom callbacks cannot alter
    // history.
    private static MathValue CalculateBinaryValue(
        MathValue left, MathValue right, BinaryNode node, CalculatorOptions options)
    {
        left.ValidateLimits(options.MaxIntegerBits);
        right.ValidateLimits(options.MaxIntegerBits);

        MathValue resultValue;
        if (PercentRules.IsMarkup(node))
        {
            var one = new MathValue(1);
            resultValue = node.Op is BinaryOperator.Add ?
                left * (one + right) :
                left * (one - right);
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
