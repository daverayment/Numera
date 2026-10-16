using System.Numerics;

namespace Numera;

/// <summary>
/// A calculator value stored as an integer, decimal, or double. Operations select their
/// result representation eagerly, preserving integer arithmetic where supported and
/// falling back to double when decimal arithmetic overflows.
/// </summary>
/// <remarks>
/// Only the property selected by Type carries the value; the other value properties are
/// zero. The default instance is integer zero. Representation alone does not establish
/// the accuracy of a calculation: decimal arithmetic can round, and double operations
/// can approximate. Integer results exceeding the safety ceiling throw rather than
/// falling back to double.
/// </remarks>
public readonly struct NumberValue : IFormattable
{
    /// <summary>
    /// Identifies the underlying active representation stored in a
    /// <see cref="NumberValue"/>.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1720:Identifier contains type name", Justification = "The enum names are descriptive of their usage, which does not conflict with type names.")]
    public enum DataType
    {
        /// <summary>
        /// Exact integer, bounded by the calculator's integer-size ceiling.
        /// </summary>
        Integer,

        /// <summary>
        /// A base-10 value stored with a 96-bit coefficient and a scale from 0 to 28.
        /// The stored value is exact, but parsing and arithmetic may round to fit it.
        /// </summary>
        Decimal,

        /// <summary>
        /// Approximate double-precision floating-point value. 64-bit binary
        /// representation with roughly 15-17 significant decimal digits. Also permits
        /// NaN and infinities; used for Math-based functions and decimal overflow fallback.
        /// </summary>
        Double,
    }

    /// <summary>
    /// Identifies which value property is active; it is not a numerical accuracy guarantee.
    /// </summary>
    public DataType Type { get; }

    /// <summary>
    /// Gets the stored <see cref="BigInteger"/> value when <see cref="Type"/> is
    /// <see cref="DataType.Integer"/>;
    /// otherwise default.
    /// </summary>
    public BigInteger IntVal { get; }

    /// <summary>
    /// Gets the stored <see cref="decimal"/> value when <see cref="Type"/> is
    /// <see cref="DataType.Decimal"/>;
    /// otherwise default.
    /// </summary>
    public decimal DecVal { get; }

    /// <summary>
    /// Gets the stored <see cref="double"/> value when <see cref="Type"/> is
    /// <see cref="DataType.Double"/>;
    /// otherwise default.
    /// </summary>
    public double DblVal { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="NumberValue"/> holding an exact
    /// arbitrary-precision integer.
    /// </summary>
    /// <param name="value">The integer value to store.</param>
    /// <exception cref="CalculationLimitException">Thrown if the magnitude exceeds the
    /// configured bit ceiling.</exception>
    public NumberValue(BigInteger value)
    {
        if (BigInteger.Abs(value).GetBitLength() > CalculationLimits.MaxIntegerBits)
        {
            throw new CalculationLimitException();
        }

        Type = DataType.Integer;
        IntVal = value;
        DecVal = default;
        DblVal = default;
    }

    /// <summary>
    /// Initializes a new instance of <see cref="NumberValue"/> holding a fixed-precision
    /// decimal value.
    /// </summary>
    /// <param name="value">The decimal fraction or integer to store.</param>
    public NumberValue(decimal value)
    {
        Type = DataType.Decimal;
        IntVal = default;
        DecVal = value;
        DblVal = default;
    }

    /// <summary>
    /// Initializes a new instance of <see cref="NumberValue"/> holding a double-precision
    /// floating-point value.
    /// </summary>
    /// <param name="value">The 64-bit binary floating-point value to store.</param>
    public NumberValue(double value)
    {
        Type = DataType.Double;
        IntVal = default;
        DecVal = default;
        DblVal = value;
    }

    /// <summary>
    /// Forces the value into a standard 64-bit floating-point representation. This may
    /// result in loss of precision or magnitude.
    /// </summary>
    /// <returns>The value as a 64-bit floating-point number.</returns>
    public double AsDouble() => Type switch
    {
        DataType.Integer => (double)IntVal,
        DataType.Decimal => (double)DecVal,
        _ => DblVal,
    };

    /// <summary>
    /// Returns <c>true</c> if the value is a finite number. Returns <c>false</c> for NaN
    /// or Infinity.
    /// </summary>
    public bool IsFinite => Type switch
    {
        DataType.Double => double.IsFinite(DblVal),
        _ => true,
    };

    /// <summary>
    /// Returns <c>true</c> if the value is mathematically undefined (Not-a-Number);
    /// otherwise <c>false</c>.
    /// </summary>
    public bool IsNaN => Type switch
    {
        DataType.Double => double.IsNaN(DblVal),
        _ => false,
    };

    /// <summary>
    /// Formats the value using the current thread culture.
    /// </summary>
    public override string ToString() => ToString(null, null);

    /// <summary>
    /// Formats the value using the specified format string and current thread culture.
    /// </summary>
    public string ToString(string? format) => ToString(format, null);

    /// <summary>
    /// Formats the value using the specified format provider and no format string.
    /// </summary>
    /// <param name="formatProvider">The format provider to use.</param>
    /// <returns>The formatted string representation of the value.</returns>
    public string ToString(IFormatProvider? formatProvider) => ToString(null, formatProvider);

    /// <summary>
    /// Formats the value using the specified format and format provider. If the active
    /// representation is <see cref="DataType.Double"/> and no format is supplied,
    /// defaults to "G17" to preserve floating-point round-trip precision.
    /// </summary>
    public string ToString(string? format, IFormatProvider? formatProvider) => Type switch
    {
        DataType.Integer => IntVal.ToString(format, formatProvider),
        DataType.Decimal => DecVal.ToString(format, formatProvider),
        DataType.Double => DblVal.ToString(format ?? "G17", formatProvider),
        _ => throw new InvalidOperationException($"Unexpected data type: {Type}"),
    };

    internal bool IsZero => Type switch
    {
        DataType.Integer => IntVal.IsZero,
        DataType.Decimal => DecVal == 0m,
        _ => DblVal == 0.0,
    };

    /// <summary>
    /// Extracts an integer when the stored value is finite and has no fractional part.
    /// Success does not recover digits lost in an earlier approximate calculation.
    /// </summary>
    public bool TryGetInteger(out BigInteger value)
    {
        switch (Type)
        {
            case DataType.Integer:
                value = IntVal;
                return true;
            case DataType.Decimal when decimal.Truncate(DecVal) == DecVal:
                value = new BigInteger(DecVal);
                return true;
            case DataType.Double when double.IsFinite(DblVal) && Math.Truncate(DblVal) == DblVal:
                value = new BigInteger(DblVal);
                return true;
            default:
                value = default;
                return false;
        }
    }

    /// <summary>
    /// Negates the value, preserving the active numerical representation.
    /// </summary>
    /// <param name="value">The value to negate.</param>
    /// <returns>A new <see cref="NumberValue"/> with the sign inverted.</returns>
    public static NumberValue operator -(NumberValue value) => value.Type switch
    {
        DataType.Integer => new NumberValue(-value.IntVal),
        DataType.Decimal => new NumberValue(-value.DecVal),
        _ => new NumberValue(-value.DblVal),
    };

    /// <summary>
    /// Orders integer and decimal values without precision loss. If either value is a
    /// double, both are converted and ordered using Double.CompareTo, including its NaN
    /// semantics; distinct exact values may compare equal after conversion.
    /// </summary>
    public static int Compare(NumberValue a, NumberValue b)
    {
        // If both values are integers, compare them as big integers to avoid precision
        // and magnitude loss from double conversion.
        if (a.Type is DataType.Integer && b.Type is DataType.Integer)
        {
            return a.IntVal.CompareTo(b.IntVal);
        }

        if (a.Type is not DataType.Double && b.Type is not DataType.Double)
        {
            if (a.Type is DataType.Integer)
            {
                return CompareIntegerAndDecimal(a.IntVal, b.DecVal);
            }

            if (b.Type is DataType.Integer)
            {
                return -CompareIntegerAndDecimal(b.IntVal, a.DecVal);
            }

            return a.DecVal.CompareTo(b.DecVal);
        }

        // Handle mixed Double and exact Integer/Decimal without overflow-to-infinity
        // false matches.
        if (a.Type is DataType.Double && b.Type is not DataType.Double)
        {
            return -CompareDoubleAndExact(b, a.DblVal);
        }

        if (b.Type is DataType.Double && a.Type is not DataType.Double)
        {
            return CompareDoubleAndExact(a, b.DblVal);
        }

        var dblA = a.AsDouble();
        var dblB = b.AsDouble();

        // In IEEE 754 arithmetic, +0.0 == -0.0 is true. Double.CompareTo introduces
        // a total order where -0.0 < +0.0, which causes 0.0 > -0.0 to evaluate to true.
        // We ensure standard equality before falling back to CompareTo (for NaN ordering).
        if (dblA == dblB)
        {
            return 0;
        }

        return dblA.CompareTo(dblB);
    }

    // Logical results use decimal zero/one. Only zero is false, so NaN is truthy here.
    // These helpers receive both values; short-circuiting belongs to the parser.
    /// <summary>
    /// Performs a logical AND on two values, treating zero as false and all other
    /// values (including NaN) as true.
    /// Returns 1m for true or 0m for false.
    /// </summary>
    public static NumberValue LogicalAnd(NumberValue a, NumberValue b) =>
        (!a.IsZero && !b.IsZero) ? new NumberValue(1m) : new NumberValue(0m);

    /// <summary>
    /// Performs a logical OR on two values, treating zero as false and all other
    /// values (including NaN) as true.
    /// Returns 1m for true or 0m for false.
    /// </summary>
    public static NumberValue LogicalOr(NumberValue a, NumberValue b) =>
        (!a.IsZero || !b.IsZero) ? new NumberValue(1m) : new NumberValue(0m);

    /// <summary>
    /// Performs a logical XOR on two values, treating zero as false and all other
    /// values (including NaN) as true.
    /// Returns 1m if exactly one operand is true, or 0m otherwise.
    /// </summary>
    public static NumberValue LogicalXor(NumberValue a, NumberValue b) =>
        (!a.IsZero ^ !b.IsZero) ? new NumberValue(1m) : new NumberValue(0m);

    /// <summary>
    /// Performs a logical NOT on a value, treating zero as false and all other
    /// values (including NaN) as true.
    /// Returns 1m if the operand is zero (false), or 0m otherwise.
    /// </summary>
    public static NumberValue LogicalNot(NumberValue a) =>
        a.IsZero ? new NumberValue(1m) : new NumberValue(0m);

    /// <summary>
    /// Performs a bitwise shift-left on integer operands, enforcing the maximum bit
    /// ceiling.
    /// </summary>
    /// <exception cref="ExpressionException">Thrown if an operand is not an integer or
    /// the shift count is negative.</exception>
    /// <exception cref="CalculationLimitException">Thrown if the shift amount exceeds the
    /// configured bit ceiling.</exception>
    public static NumberValue ShiftLeft(NumberValue a, NumberValue b)
    {
        if (!a.TryGetInteger(out var val) || !b.TryGetInteger(out var count))
        {
            throw new ExpressionException(new EvaluationDiagnostic(
                EvaluationError.IntegerRequired, "Bitwise shift requires integer operands."));
        }

        if (count < BigInteger.Zero)
        {
            throw new ExpressionException(new EvaluationDiagnostic(
                EvaluationError.ArgumentOutOfRange, "Shift count cannot be negative."));
        }

        if (count > CalculationLimits.MaxIntegerBits)
        {
            throw new CalculationLimitException();
        }

        var shift = (int)count;
        if (!val.IsZero)
        {
            var bitLength = BigInteger.Abs(val).GetBitLength();
            if (bitLength + shift > CalculationLimits.MaxIntegerBits)
            {
                throw new CalculationLimitException();
            }
        }

        return new NumberValue(val << shift);
    }

    /// <summary>
    /// Performs an arithmetic bitwise shift-right on integer operands.
    /// </summary>
    /// <exception cref="ExpressionException">Thrown if an operand is not an integer or the shift count is negative.</exception>
    public static NumberValue ShiftRight(NumberValue a, NumberValue b)
    {
        if (!a.TryGetInteger(out var val) || !b.TryGetInteger(out var count))
        {
            throw new ExpressionException(new EvaluationDiagnostic(
                EvaluationError.IntegerRequired, "Bitwise shift requires integer operands."));
        }

        if (count < BigInteger.Zero)
        {
            throw new ExpressionException(new EvaluationDiagnostic(
                EvaluationError.ArgumentOutOfRange, "Shift count cannot be negative."));
        }

        var shift = count > CalculationLimits.MaxIntegerBits ? CalculationLimits.MaxIntegerBits : (int)count;
        return new NumberValue(val >> shift);
    }

    /// <summary>
    /// Performs a bitwise AND on integer operands.
    /// </summary>
    /// <exception cref="ExpressionException">Thrown if either operand is not an integer.</exception>
    public static NumberValue BitwiseAnd(NumberValue a, NumberValue b)
    {
        if (!a.TryGetInteger(out var intA) || !b.TryGetInteger(out var intB))
        {
            throw new ExpressionException(new EvaluationDiagnostic(
                EvaluationError.IntegerRequired, "Bitwise AND requires integer operands."));
        }

        return new NumberValue(intA & intB);
    }

    /// <summary>
    /// Performs a bitwise OR on integer operands.
    /// </summary>
    /// <exception cref="ExpressionException">Thrown if either operand is not an integer.</exception>
    public static NumberValue BitwiseOr(NumberValue a, NumberValue b)
    {
        if (!a.TryGetInteger(out var intA) || !b.TryGetInteger(out var intB))
        {
            throw new ExpressionException(new EvaluationDiagnostic(
                EvaluationError.IntegerRequired, "Bitwise OR requires integer operands."));
        }

        return new NumberValue(intA | intB);
    }

    /// <summary>
    /// Performs a bitwise XOR on integer operands.
    /// </summary>
    /// <exception cref="ExpressionException">Thrown if either operand is not an integer.</exception>
    public static NumberValue BitwiseXor(NumberValue a, NumberValue b)
    {
        if (!a.TryGetInteger(out var intA) || !b.TryGetInteger(out var intB))
        {
            throw new ExpressionException(new EvaluationDiagnostic(
                EvaluationError.IntegerRequired, "Bitwise XOR requires integer operands."));
        }

        return new NumberValue(intA ^ intB);
    }

    /// <summary>
    /// Performs a bitwise one's complement (NOT) on an integer operand.
    /// </summary>
    /// <exception cref="ExpressionException">Thrown if the operand is not an integer.</exception>
    public static NumberValue BitwiseNot(NumberValue a)
    {
        if (!a.TryGetInteger(out var val))
        {
            throw new ExpressionException(new EvaluationDiagnostic(
                EvaluationError.IntegerRequired, "Bitwise NOT requires an integer operand."));
        }

        return new NumberValue(~val);
    }

    /// <summary>
    /// Compares two values for equality using <see cref="Compare(NumberValue, NumberValue)"/>.
    /// Returns 1m if equal, or 0m if unequal.
    /// </summary>
    public static NumberValue AreEqual(NumberValue a, NumberValue b) =>
        Compare(a, b) == 0 ? new NumberValue(1m) : new NumberValue(0m);

    /// <summary>
    /// Compares two values for inequality using <see cref="Compare(NumberValue, NumberValue)"/>.
    /// Returns 1m if unequal, or 0m if equal.
    /// </summary>
    public static NumberValue AreNotEqual(NumberValue a, NumberValue b) =>
        Compare(a, b) != 0 ? new NumberValue(1m) : new NumberValue(0m);

    /// <summary>
    /// Compares if the left operand is strictly less than the right operand using
    /// <see cref="Compare(NumberValue, NumberValue)"/>.
    /// Returns 1m if less, or 0m otherwise.
    /// </summary>
    public static NumberValue LessThan(NumberValue a, NumberValue b) =>
        Compare(a, b) < 0 ? new NumberValue(1m) : new NumberValue(0m);

    /// <summary>
    /// Compares if the left operand is less than or equal to the right operand using
    /// <see cref="Compare(NumberValue, NumberValue)"/>.
    /// Returns 1m if less than or equal, or 0m otherwise.
    /// </summary>
    public static NumberValue LessThanOrEqual(NumberValue a, NumberValue b) =>
        Compare(a, b) <= 0 ? new NumberValue(1m) : new NumberValue(0m);

    /// <summary>
    /// Compares if the left operand is strictly greater than the right operand using
    /// <see cref="Compare(NumberValue, NumberValue)"/>.
    /// Returns 1m if greater, or 0m otherwise.
    /// </summary>
    public static NumberValue GreaterThan(NumberValue a, NumberValue b) =>
        Compare(a, b) > 0 ? new NumberValue(1m) : new NumberValue(0m);

    /// <summary>
    /// Compares if the left operand is greater than or equal to the right operand using
    /// <see cref="Compare(NumberValue, NumberValue)"/>.
    /// Returns 1m if greater than or equal, or 0m otherwise.
    /// </summary>
    public static NumberValue GreaterThanOrEqual(NumberValue a, NumberValue b) =>
        Compare(a, b) >= 0 ? new NumberValue(1m) : new NumberValue(0m);

    /// <summary>
    /// Adds two values, using exact integer arithmetic when both operands are integers,
    /// decimal arithmetic for fixed-precision fractions, or promoting to double when
    /// either operand is floating-point or on decimal overflow.
    /// </summary>
    public static NumberValue operator +(NumberValue a, NumberValue b)
    {
        // If either value is a double, the entire operation is promoted to double.
        if (a.Type is DataType.Double || b.Type is DataType.Double)
        {
            return new NumberValue(a.AsDouble() + b.AsDouble());
        }

        // If both values are integers, perform exact integer addition.
        if (a.Type is DataType.Integer && b.Type is DataType.Integer)
        {
            return new NumberValue(a.IntVal + b.IntVal);
        }

        // Prefer decimal for mixed integer/decimal or pure decimal arithmetic. This can
        // round to fit decimal precision; conversion or arithmetic overflow uses double.
        try
        {
            var decA = a.Type is DataType.Integer ? (decimal)a.IntVal : a.DecVal;
            var decB = b.Type is DataType.Integer ? (decimal)b.IntVal : b.DecVal;
            return new NumberValue(decA + decB);
        }
        catch (OverflowException)
        {
            return new NumberValue(a.AsDouble() + b.AsDouble());
        }
    }

    /// <summary>
    /// Subtracts the right operand from the left operand, maintaining exact integer
    /// arithmetic or decimal fractions where possible, and falling back to double if
    /// either operand is floating-point or on decimal overflow.
    /// </summary>
    public static NumberValue operator -(NumberValue a, NumberValue b)
    {
        if (a.Type is DataType.Double || b.Type is DataType.Double)
        {
            return new NumberValue(a.AsDouble() - b.AsDouble());
        }

        if (a.Type is DataType.Integer && b.Type is DataType.Integer)
        {
            return new NumberValue(a.IntVal - b.IntVal);
        }

        try
        {
            var decA = a.Type is DataType.Integer ? (decimal)a.IntVal : a.DecVal;
            var decB = b.Type is DataType.Integer ? (decimal)b.IntVal : b.DecVal;
            return new NumberValue(decA - decB);
        }
        catch (OverflowException)
        {
            return new NumberValue(a.AsDouble() - b.AsDouble());
        }
    }

    /// <summary>
    /// Multiplies two values, maintaining exact integer arithmetic or decimal fractions
    /// where possible, and falling back to double if either operand is floating-point or
    /// on decimal overflow.
    /// </summary>
    public static NumberValue operator *(NumberValue a, NumberValue b)
    {
        if (a.Type is DataType.Double || b.Type is DataType.Double)
        {
            return new NumberValue(a.AsDouble() * b.AsDouble());
        }

        if (a.Type is DataType.Integer && b.Type is DataType.Integer)
        {
            return new NumberValue(a.IntVal * b.IntVal);
        }

        try
        {
            var decA = a.Type is DataType.Integer ? (decimal)a.IntVal : a.DecVal;
            var decB = b.Type is DataType.Integer ? (decimal)b.IntVal : b.DecVal;
            return new NumberValue(decA * decB);
        }
        catch (OverflowException)
        {
            return new NumberValue(a.AsDouble() * b.AsDouble());
        }
    }

    /// <summary>
    /// Divides two values. Preserves exact integers when the quotient divides evenly,
    /// uses decimal arithmetic for representable fractional quotients, and falls back to
    /// double on decimal overflow or floating-point operands.
    /// </summary>
    /// <exception cref="DivideByZeroException">Thrown when the denominator evaluates to
    /// zero.</exception>
    public static NumberValue operator /(NumberValue a, NumberValue b)
    {
        if (b.IsZero)
        {
            throw new DivideByZeroException();
        }

        if (a.Type is DataType.Double || b.Type is DataType.Double)
        {
            return new NumberValue(DivideAsDouble(a, b));
        }

        if (a.Type is DataType.Integer &&
            b.Type is DataType.Integer &&
            BigInteger.Remainder(a.IntVal, b.IntVal) == BigInteger.Zero)
        {
            // Preserve an exact integer quotient; fractional quotients use the decimal
            // path below, which can round even when it does not overflow.
            return new NumberValue(a.IntVal / b.IntVal);
        }

        try
        {
            var decA = a.Type is DataType.Integer ? (decimal)a.IntVal : a.DecVal;
            var decB = b.Type is DataType.Integer ? (decimal)b.IntVal : b.DecVal;
            return new NumberValue(decA / decB);
        }
        catch (OverflowException)
        {
            return new NumberValue(DivideAsDouble(a, b));
        }
    }

    /// <summary>
    /// Computes the remainder of dividing two values, preserving exact integer remainder
    /// when both operands are integers, decimal remainder where representable, or double
    /// remainder otherwise.
    /// </summary>
    /// <exception cref="DivideByZeroException">Thrown when the denominator evaluates to
    /// zero.</exception>
    public static NumberValue operator %(NumberValue a, NumberValue b)
    {
        if (b.IsZero)
        {
            throw new DivideByZeroException();
        }

        if (a.Type is DataType.Double || b.Type is DataType.Double)
        {
            return new NumberValue(a.AsDouble() % b.AsDouble());
        }

        if (a.Type is DataType.Integer && b.Type is DataType.Integer)
        {
            return new NumberValue(a.IntVal % b.IntVal);
        }

        try
        {
            var decA = a.Type is DataType.Integer ? (decimal)a.IntVal : a.DecVal;
            var decB = b.Type is DataType.Integer ? (decimal)b.IntVal : b.DecVal;
            return new NumberValue(decA % decB);
        }
        catch (OverflowException)
        {
            return new NumberValue(a.AsDouble() % b.AsDouble());
        }
    }

    /// <summary>
    /// Uses bounded integer exponentiation for an integer base and a nonnegative whole
    /// exponent. For decimal bases with nonnegative whole exponents, uses binary
    /// exponentiation (exponentiation by squaring) preserving exact decimal precision,
    /// falling back to double on OverflowException. Other cases use Math.Pow, including
    /// negative integer exponents.
    /// </summary>
    /// <exception cref="DivideByZeroException">Thrown if zero is raised to a negative
    /// power.</exception>
    /// <exception cref="ExpressionException">Thrown if a negative base is raised to a
    /// fractional power.</exception>
    public static NumberValue Pow(NumberValue baseVal, NumberValue expVal)
    {
        var hasIntExp = expVal.TryGetInteger(out var exponent);
        if (hasIntExp)
        {
            if (BigInteger.Abs(exponent) > CalculationLimits.MaxExponentMagnitude)
            {
                throw new CalculationLimitException();
            }

            if (baseVal.Type is DataType.Integer && exponent >= BigInteger.Zero)
            {
                var magnitude = BigInteger.Abs(baseVal.IntVal);

                // This lower bound rejects powers that must exceed the bit ceiling
                // before allocating them. The result constructor enforces the final size.
                if (magnitude > BigInteger.One &&
                    ((magnitude.GetBitLength() - 1) * (int)exponent) + 1 > CalculationLimits.MaxIntegerBits)
                {
                    throw new CalculationLimitException();
                }

                return new NumberValue(BigInteger.Pow(baseVal.IntVal, (int)exponent));
            }

            if (baseVal.Type is DataType.Decimal && exponent >= BigInteger.Zero)
            {
                try
                {
                    return new NumberValue(DecimalPow(baseVal.DecVal, (int)exponent));
                }
                catch (OverflowException)
                {
                    // Fall through to double calculation on decimal overflow.
                }
            }
        }

        var b = baseVal.AsDouble();
        var e = expVal.AsDouble();

        // 0 raised to a negative power represents division by zero: 0^-n = 1 / 0^n.
        if (baseVal.IsZero && e < 0.0)
        {
            throw new DivideByZeroException("Cannot raise zero to a negative power.");
        }

        // Negative base raised to a non-integer power yields a complex result without a
        // real value.
        if (b < 0.0 && !hasIntExp)
        {
            throw new ExpressionException(new EvaluationDiagnostic(
                EvaluationError.UndefinedMathResult, "Negative base with a fractional exponent has no real solution."));
        }

        return new NumberValue(Math.Pow(b, e));
    }

    private static decimal DecimalPow(decimal baseVal, int exponent)
    {
        // Exponentiation by squaring to preserve decimal precision without precision
        // loss.
        var result = 1m;
        var b = baseVal;
        var e = exponent;

        while (e > 0)
        {
            if ((e & 1) != 0)
            {
                result = checked(result * b);
            }

            e >>= 1;
            if (e > 0)
            {
                b = checked(b * b);
            }
        }

        return result;
    }

    private static int CompareIntegerAndDecimal(BigInteger integer, decimal value)
    {
        // Scale the integer rather than narrowing it, so comparison remains exact even
        // when the integer cannot be represented by a decimal.
        var bits = decimal.GetBits(value);
        var coefficient = (BigInteger)(uint)bits[0]
            + ((BigInteger)(uint)bits[1] << 32)
            + ((BigInteger)(uint)bits[2] << 64);
        if (bits[3] < 0)
        {
            coefficient = -coefficient;
        }

        var scale = (bits[3] >> 16) & 0xff;

        // A decimal is coefficient / 10^scale. Multiplying the integer by 10^scale
        // puts both sides on the same exact integer scale without division or rounding.
        return (integer * BigInteger.Pow(10, scale)).CompareTo(coefficient);
    }

    private static int CompareDoubleAndExact(NumberValue exact, double dbl)
    {
        if (double.IsNaN(dbl))
        {
            // Consistent with double.CompareTo: non-NaN is greater than NaN.
            return 1;
        }

        if (double.IsPositiveInfinity(dbl))
        {
            // Any finite integer or decimal is strictly less than +Infinity.
            return -1;
        }

        if (double.IsNegativeInfinity(dbl))
        {
            // Any finite integer or decimal is strictly greater than -Infinity.
            return 1;
        }

        if (exact.Type is DataType.Integer)
        {
            var integer = exact.IntVal;
            var bitLength = integer.GetBitLength();

            // Double.MaxValue has an exponent of 1023, so any integer with 1025+ bits
            // exceeds Double.MaxValue in magnitude and cannot be compared via (double)int.
            if (bitLength > 1024)
            {
                return integer.Sign > 0 ? 1 : -1;
            }

            var dblExact = (double)integer;
            if (dblExact == dbl)
            {
                return 0;
            }

            return dblExact.CompareTo(dbl);
        }

        // For Decimal vs finite double
        var dblDec = (double)exact.DecVal;
        if (dblDec == dbl)
        {
            return 0;
        }

        return dblDec.CompareTo(dbl);
    }

    private static double DivideAsDouble(NumberValue a, NumberValue b)
    {
        // Convert bounded mantissas instead of entire integers to avoid infinity/infinity
        // when a very large numerator and denominator have a finite quotient.
        var numerator = a.AsDoubleMantissa(out var numeratorExponent);
        var denominator = b.AsDoubleMantissa(out var denominatorExponent);
        return Math.ScaleB(numerator / denominator, numeratorExponent - denominatorExponent);
    }

    private double AsDoubleMantissa(out int exponent)
    {
        exponent = 0;
        if (Type is not DataType.Integer)
        {
            return AsDouble();
        }

        var magnitude = BigInteger.Abs(IntVal);

        // Keep at most 53 leading bits, the double significand's capacity, and restore
        // their scale separately. Discarded low bits are truncated, so this is
        // approximate.
        exponent = (int)Math.Max(0, magnitude.GetBitLength() - 53);
        var mantissa = (double)(magnitude >> exponent);
        return IntVal.Sign < 0 ? -mantissa : mantissa;
    }
}
