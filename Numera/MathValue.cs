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
public readonly struct MathValue : IFormattable, ISpanFormattable, IUtf8SpanFormattable
{
    /// <summary>
    /// Identifies the underlying active representation stored in a
    /// <see cref="MathValue"/>.
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
    /// Initializes a new instance of <see cref="MathValue"/> holding an exact
    /// arbitrary-precision integer.
    /// </summary>
    /// <param name="value">The integer value to store.</param>
    /// <exception cref="CalculationLimitException">Thrown if the magnitude exceeds the
    /// configured bit ceiling.</exception>
    public MathValue(BigInteger value)
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
    /// Initializes a new instance of <see cref="MathValue"/> holding a fixed-precision
    /// decimal value.
    /// </summary>
    /// <param name="value">The decimal fraction or integer to store.</param>
    public MathValue(decimal value)
    {
        Type = DataType.Decimal;
        IntVal = default;
        DecVal = value;
        DblVal = default;
    }

    /// <summary>
    /// Initializes a new instance of <see cref="MathValue"/> holding a double-precision
    /// floating-point value.
    /// </summary>
    /// <param name="value">The 64-bit binary floating-point value to store.</param>
    public MathValue(double value)
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

    /// <summary>
    /// Formats the value into a character span.
    /// </summary>
    public bool TryFormat(
        Span<char> destination,
        out int charsWritten,
        ReadOnlySpan<char> format = default,
        IFormatProvider? provider = null)
    {
        switch (Type)
        {
            case DataType.Integer:
                return IntVal.TryFormat(destination, out charsWritten, format, provider);
            case DataType.Decimal:
                return DecVal.TryFormat(destination, out charsWritten, format, provider);
            case DataType.Double:
                return DblVal.TryFormat(
                    destination,
                    out charsWritten,
                    format.IsEmpty ? "G17" : format,
                    provider);
            default:
                throw new InvalidOperationException($"Unexpected data type: {Type}");
        }
    }

    /// <summary>
    /// Formats the value into a UTF-8 byte span.
    /// </summary>
    public bool TryFormat(
        Span<byte> utf8Destination,
        out int bytesWritten,
        ReadOnlySpan<char> format = default,
        IFormatProvider? provider = null)
    {
        switch (Type)
        {
            case DataType.Integer:
            {
                // BigInteger implements ISpanFormattable (chars). Format to a stack buffer and encode to UTF-8.
                Span<char> charBuffer = stackalloc char[256];
                if (IntVal.TryFormat(charBuffer, out int charsWritten, format, provider))
                {
                    return System.Text.Encoding.UTF8.TryGetBytes(charBuffer[..charsWritten], utf8Destination, out bytesWritten);
                }

                // If the integer exceeds 256 characters, allocate a temporary string
                string formatted = IntVal.ToString(format.IsEmpty ? null : format.ToString(), provider);
                return System.Text.Encoding.UTF8.TryGetBytes(formatted, utf8Destination, out bytesWritten);
            }
            case DataType.Decimal:
                return DecVal.TryFormat(utf8Destination, out bytesWritten, format, provider);
            case DataType.Double:
                return DblVal.TryFormat(
                    utf8Destination,
                    out bytesWritten,
                    format.IsEmpty ? "G17" : format,
                    provider);
            default:
                throw new InvalidOperationException($"Unexpected data type: {Type}");
        }
    }

    /// <summary>
    /// Returns the hexadecimal representation of the value if it is an integer; otherwise, <c>null</c>.
    /// </summary>
    /// <param name="prefix">Whether to prefix the result with "0x" (or "-0x" if negative).</param>
    public string? ToHexString(bool prefix = true)
    {
        if (Type != DataType.Integer)
        {
            return null;
        }

        if (IntVal.IsZero)
        {
            return prefix ? "0x0" : "0";
        }

        bool isNegative = IntVal < BigInteger.Zero;
        BigInteger absVal = BigInteger.Abs(IntVal);
        string hex = absVal.ToString("X");

        if (!prefix)
        {
            return isNegative ? "-" + hex : hex;
        }

        return isNegative ? "-0x" + hex : "0x" + hex;
    }

    /// <summary>
    /// Returns the binary representation of the value if it is an integer; otherwise, <c>null</c>.
    /// </summary>
    /// <param name="prefix">Whether to prefix the result with "0b" (or "-0b" if negative).</param>
    public string? ToBinaryString(bool prefix = true)
    {
        if (Type != DataType.Integer)
        {
            return null;
        }

        if (IntVal.IsZero)
        {
            return prefix ? "0b0" : "0";
        }

        bool isNegative = IntVal < BigInteger.Zero;
        BigInteger absVal = BigInteger.Abs(IntVal);
        string binary = absVal.ToString("B").TrimStart('0');
        if (binary.Length == 0)
        {
            binary = "0";
        }

        if (!prefix)
        {
            return isNegative ? "-" + binary : binary;
        }

        return isNegative ? "-0b" + binary : "0b" + binary;
    }

    /// <summary>
    /// Returns the octal representation of the value if it is an integer; otherwise, <c>null</c>.
    /// </summary>
    /// <param name="prefix">Whether to prefix the result with "0o" (or "-0o" if negative).</param>
    public string? ToOctalString(bool prefix = true)
    {
        if (Type != DataType.Integer)
        {
            return null;
        }

        if (IntVal.IsZero)
        {
            return prefix ? "0o0" : "0";
        }

        bool isNegative = IntVal < BigInteger.Zero;
        BigInteger absVal = BigInteger.Abs(IntVal);

        // Convert absVal to octal by converting binary representation in 3-bit groups
        string binary = absVal.ToString("B").TrimStart('0');
        if (binary.Length == 0)
        {
            binary = "0";
        }

        int rem = binary.Length % 3;
        int pad = rem == 0 ? 0 : 3 - rem;
        int totalLen = binary.Length + pad;

        var sb = new System.Text.StringBuilder(totalLen / 3);
        int i = 0;
        if (pad > 0)
        {
            int firstOctal = 0;
            for (int k = 0; k < rem; k++)
            {
                firstOctal = (firstOctal << 1) | (binary[k] - '0');
            }

            sb.Append((char)('0' + firstOctal));
            i = rem;
        }

        while (i < binary.Length)
        {
            int val = ((binary[i] - '0') << 2) | ((binary[i + 1] - '0') << 1) | (binary[i + 2] - '0');
            sb.Append((char)('0' + val));
            i += 3;
        }

        string octal = sb.ToString();
        if (!prefix)
        {
            return isNegative ? "-" + octal : octal;
        }

        return isNegative ? "-0o" + octal : "0o" + octal;
    }

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
    /// <returns>A new <see cref="MathValue"/> with the sign inverted.</returns>
    public static MathValue operator -(MathValue value) => value.Type switch
    {
        DataType.Integer => new MathValue(-value.IntVal),
        DataType.Decimal => new MathValue(-value.DecVal),
        _ => new MathValue(-value.DblVal),
    };

    /// <summary>
    /// Orders integer and decimal values without precision loss. If either value is a
    /// double, both are converted and ordered using Double.CompareTo, including its NaN
    /// semantics; distinct exact values may compare equal after conversion.
    /// </summary>
    public static int Compare(MathValue a, MathValue b)
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

        double dblA = a.AsDouble();
        double dblB = b.AsDouble();

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
    public static MathValue LogicalAnd(MathValue a, MathValue b) =>
        (!a.IsZero && !b.IsZero) ? new MathValue(1m) : new MathValue(0m);

    /// <summary>
    /// Performs a logical OR on two values, treating zero as false and all other
    /// values (including NaN) as true.
    /// Returns 1m for true or 0m for false.
    /// </summary>
    public static MathValue LogicalOr(MathValue a, MathValue b) =>
        (!a.IsZero || !b.IsZero) ? new MathValue(1m) : new MathValue(0m);

    /// <summary>
    /// Performs a logical XOR on two values, treating zero as false and all other
    /// values (including NaN) as true.
    /// Returns 1m if exactly one operand is true, or 0m otherwise.
    /// </summary>
    public static MathValue LogicalXor(MathValue a, MathValue b) =>
        (!a.IsZero ^ !b.IsZero) ? new MathValue(1m) : new MathValue(0m);

    /// <summary>
    /// Performs a logical NOT on a value, treating zero as false and all other
    /// values (including NaN) as true.
    /// Returns 1m if the operand is zero (false), or 0m otherwise.
    /// </summary>
    public static MathValue LogicalNot(MathValue a) =>
        a.IsZero ? new MathValue(1m) : new MathValue(0m);

    /// <summary>
    /// Performs a bitwise shift-left on integer operands, enforcing the maximum bit
    /// ceiling.
    /// </summary>
    /// <exception cref="ExpressionException">Thrown if an operand is not an integer or
    /// the shift count is negative.</exception>
    /// <exception cref="CalculationLimitException">Thrown if the shift amount exceeds the
    /// configured bit ceiling.</exception>
    public static MathValue ShiftLeft(MathValue a, MathValue b)
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

        int shift = (int)count;
        if (!val.IsZero)
        {
            long bitLength = BigInteger.Abs(val).GetBitLength();
            if (bitLength + shift > CalculationLimits.MaxIntegerBits)
            {
                throw new CalculationLimitException();
            }
        }

        return new MathValue(val << shift);
    }

    /// <summary>
    /// Performs an arithmetic bitwise shift-right on integer operands.
    /// </summary>
    /// <exception cref="ExpressionException">Thrown if an operand is not an integer or
    /// the shift count is negative.</exception>
    public static MathValue ShiftRight(MathValue a, MathValue b)
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

        int shift = count > CalculationLimits.MaxIntegerBits ? CalculationLimits.MaxIntegerBits : (int)count;
        return new MathValue(val >> shift);
    }

    /// <summary>
    /// Performs a bitwise AND on integer operands.
    /// </summary>
    /// <exception cref="ExpressionException">Thrown if either operand is not an integer.
    /// </exception>
    public static MathValue BitwiseAnd(MathValue a, MathValue b)
    {
        if (!a.TryGetInteger(out var intA) || !b.TryGetInteger(out var intB))
        {
            throw new ExpressionException(new EvaluationDiagnostic(
                EvaluationError.IntegerRequired, "Bitwise AND requires integer operands."));
        }

        return new MathValue(intA & intB);
    }

    /// <summary>
    /// Performs a bitwise OR on integer operands.
    /// </summary>
    /// <exception cref="ExpressionException">Thrown if either operand is not an integer.
    /// </exception>
    public static MathValue BitwiseOr(MathValue a, MathValue b)
    {
        if (!a.TryGetInteger(out var intA) || !b.TryGetInteger(out var intB))
        {
            throw new ExpressionException(new EvaluationDiagnostic(
                EvaluationError.IntegerRequired, "Bitwise OR requires integer operands."));
        }

        return new MathValue(intA | intB);
    }

    /// <summary>
    /// Performs a bitwise XOR on integer operands.
    /// </summary>
    /// <exception cref="ExpressionException">Thrown if either operand is not an integer.
    /// </exception>
    public static MathValue BitwiseXor(MathValue a, MathValue b)
    {
        if (!a.TryGetInteger(out var intA) || !b.TryGetInteger(out var intB))
        {
            throw new ExpressionException(new EvaluationDiagnostic(
                EvaluationError.IntegerRequired, "Bitwise XOR requires integer operands."));
        }

        return new MathValue(intA ^ intB);
    }

    /// <summary>
    /// Performs a bitwise one's complement (NOT) on an integer operand.
    /// </summary>
    /// <exception cref="ExpressionException">Thrown if the operand is not an integer.
    /// </exception>
    public static MathValue BitwiseNot(MathValue a)
    {
        if (!a.TryGetInteger(out var val))
        {
            throw new ExpressionException(new EvaluationDiagnostic(
                EvaluationError.IntegerRequired, "Bitwise NOT requires an integer operand."));
        }

        return new MathValue(~val);
    }

    /// <summary>
    /// Compares two values for equality using <see cref="Compare(MathValue, MathValue)"/>.
    /// Returns 1m if equal, or 0m if unequal.
    /// </summary>
    public static MathValue AreEqual(MathValue a, MathValue b) =>
        Compare(a, b) == 0 ? new MathValue(1m) : new MathValue(0m);

    /// <summary>
    /// Compares two values for inequality using <see cref="Compare(MathValue, MathValue)"/>.
    /// Returns 1m if unequal, or 0m if equal.
    /// </summary>
    public static MathValue AreNotEqual(MathValue a, MathValue b) =>
        Compare(a, b) != 0 ? new MathValue(1m) : new MathValue(0m);

    /// <summary>
    /// Compares if the left operand is strictly less than the right operand using
    /// <see cref="Compare(MathValue, MathValue)"/>.
    /// Returns 1m if less, or 0m otherwise.
    /// </summary>
    public static MathValue LessThan(MathValue a, MathValue b) =>
        Compare(a, b) < 0 ? new MathValue(1m) : new MathValue(0m);

    /// <summary>
    /// Compares if the left operand is less than or equal to the right operand using
    /// <see cref="Compare(MathValue, MathValue)"/>.
    /// Returns 1m if less than or equal, or 0m otherwise.
    /// </summary>
    public static MathValue LessThanOrEqual(MathValue a, MathValue b) =>
        Compare(a, b) <= 0 ? new MathValue(1m) : new MathValue(0m);

    /// <summary>
    /// Compares if the left operand is strictly greater than the right operand using
    /// <see cref="Compare(MathValue, MathValue)"/>.
    /// Returns 1m if greater, or 0m otherwise.
    /// </summary>
    public static MathValue GreaterThan(MathValue a, MathValue b) =>
        Compare(a, b) > 0 ? new MathValue(1m) : new MathValue(0m);

    /// <summary>
    /// Compares if the left operand is greater than or equal to the right operand using
    /// <see cref="Compare(MathValue, MathValue)"/>.
    /// Returns 1m if greater than or equal, or 0m otherwise.
    /// </summary>
    public static MathValue GreaterThanOrEqual(MathValue a, MathValue b) =>
        Compare(a, b) >= 0 ? new MathValue(1m) : new MathValue(0m);

    /// <summary>
    /// Adds two values, using exact integer arithmetic when both operands are integers,
    /// decimal arithmetic for fixed-precision fractions, or promoting to double when
    /// either operand is floating-point or on decimal overflow.
    /// </summary>
    public static MathValue operator +(MathValue a, MathValue b)
    {
        // If either value is a double, the entire operation is promoted to double.
        if (a.Type is DataType.Double || b.Type is DataType.Double)
        {
            return new MathValue(a.AsDouble() + b.AsDouble());
        }

        // If both values are integers, perform exact integer addition.
        if (a.Type is DataType.Integer && b.Type is DataType.Integer)
        {
            return new MathValue(a.IntVal + b.IntVal);
        }

        // Prefer decimal for mixed integer/decimal or pure decimal arithmetic. This can
        // round to fit decimal precision; conversion or arithmetic overflow uses double.
        // However, if the decimal operand represents a whole number, promote to exact
        // BigInteger rather than losing precision in double.
        try
        {
            decimal decA = a.Type is DataType.Integer ? (decimal)a.IntVal : a.DecVal;
            decimal decB = b.Type is DataType.Integer ? (decimal)b.IntVal : b.DecVal;
            return new MathValue(decA + decB);
        }
        catch (OverflowException)
        {
            if (TryEvaluateMixedIntegerAndWholeDecimal(a, b, static (x, y) => x + y, out var intResult))
            {
                return intResult;
            }

            return new MathValue(a.AsDouble() + b.AsDouble());
        }
    }

    /// <summary>
    /// Subtracts the right operand from the left operand, maintaining exact integer
    /// arithmetic or decimal fractions where possible, and falling back to double if
    /// either operand is floating-point or on decimal overflow.
    /// </summary>
    public static MathValue operator -(MathValue a, MathValue b)
    {
        if (a.Type is DataType.Double || b.Type is DataType.Double)
        {
            return new MathValue(a.AsDouble() - b.AsDouble());
        }

        if (a.Type is DataType.Integer && b.Type is DataType.Integer)
        {
            return new MathValue(a.IntVal - b.IntVal);
        }

        try
        {
            decimal decA = a.Type is DataType.Integer ? (decimal)a.IntVal : a.DecVal;
            decimal decB = b.Type is DataType.Integer ? (decimal)b.IntVal : b.DecVal;
            return new MathValue(decA - decB);
        }
        catch (OverflowException)
        {
            if (TryEvaluateMixedIntegerAndWholeDecimal(a, b, static (x, y) => x - y, out var intResult))
            {
                return intResult;
            }

            return new MathValue(a.AsDouble() - b.AsDouble());
        }
    }

    /// <summary>
    /// Multiplies two values, maintaining exact integer arithmetic or decimal fractions
    /// where possible, and falling back to double if either operand is floating-point or
    /// on decimal overflow.
    /// </summary>
    public static MathValue operator *(MathValue a, MathValue b)
    {
        if (a.Type is DataType.Double || b.Type is DataType.Double)
        {
            return new MathValue(a.AsDouble() * b.AsDouble());
        }

        if (a.Type is DataType.Integer && b.Type is DataType.Integer)
        {
            return new MathValue(a.IntVal * b.IntVal);
        }

        try
        {
            decimal decA = a.Type is DataType.Integer ? (decimal)a.IntVal : a.DecVal;
            decimal decB = b.Type is DataType.Integer ? (decimal)b.IntVal : b.DecVal;
            return new MathValue(decA * decB);
        }
        catch (OverflowException)
        {
            if (TryEvaluateMixedIntegerAndWholeDecimal(a, b, static (x, y) => x * y, out var intResult))
            {
                return intResult;
            }

            return new MathValue(a.AsDouble() * b.AsDouble());
        }
    }

    /// <summary>
    /// Divides two values. Preserves exact integers when the quotient divides evenly,
    /// uses decimal arithmetic for representable fractional quotients, and falls back to
    /// double on decimal overflow or floating-point operands.
    /// </summary>
    /// <exception cref="DivideByZeroException">Thrown when the denominator evaluates to
    /// zero.</exception>
    public static MathValue operator /(MathValue a, MathValue b)
    {
        if (b.IsZero)
        {
            throw new DivideByZeroException();
        }

        if (a.Type is DataType.Double || b.Type is DataType.Double)
        {
            return new MathValue(DivideAsDouble(a, b));
        }

        if (a.Type is DataType.Integer &&
            b.Type is DataType.Integer &&
            BigInteger.Remainder(a.IntVal, b.IntVal) == BigInteger.Zero)
        {
            // Preserve an exact integer quotient; fractional quotients use the decimal
            // path below, which can round even when it does not overflow.
            return new MathValue(a.IntVal / b.IntVal);
        }

        try
        {
            decimal decA = a.Type is DataType.Integer ? (decimal)a.IntVal : a.DecVal;
            decimal decB = b.Type is DataType.Integer ? (decimal)b.IntVal : b.DecVal;
            return new MathValue(decA / decB);
        }
        catch (OverflowException)
        {
            return new MathValue(DivideAsDouble(a, b));
        }
    }

    /// <summary>
    /// Computes the remainder of dividing two values, preserving exact integer remainder
    /// when both operands are integers, decimal remainder where representable, or double
    /// remainder otherwise.
    /// </summary>
    /// <exception cref="DivideByZeroException">Thrown when the denominator evaluates to
    /// zero.</exception>
    public static MathValue operator %(MathValue a, MathValue b)
    {
        if (b.IsZero)
        {
            throw new DivideByZeroException();
        }

        if (a.Type is DataType.Double || b.Type is DataType.Double)
        {
            return new MathValue(a.AsDouble() % b.AsDouble());
        }

        if (a.Type is DataType.Integer && b.Type is DataType.Integer)
        {
            return new MathValue(a.IntVal % b.IntVal);
        }

        try
        {
            decimal decA = a.Type is DataType.Integer ? (decimal)a.IntVal : a.DecVal;
            decimal decB = b.Type is DataType.Integer ? (decimal)b.IntVal : b.DecVal;
            return new MathValue(decA % decB);
        }
        catch (OverflowException)
        {
            return new MathValue(a.AsDouble() % b.AsDouble());
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
    public static MathValue Pow(MathValue baseVal, MathValue expVal)
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

                return new MathValue(BigInteger.Pow(baseVal.IntVal, (int)exponent));
            }

            if (baseVal.Type is DataType.Decimal && exponent >= BigInteger.Zero)
            {
                try
                {
                    return new MathValue(DecimalPow(baseVal.DecVal, (int)exponent));
                }
                catch (OverflowException)
                {
                    // If the decimal base has no fractional part, promote to BigInteger
                    // rather than losing precision in double.
                    if (decimal.Truncate(baseVal.DecVal) == baseVal.DecVal)
                    {
                        var intBase = new BigInteger(baseVal.DecVal);
                        var magnitude = BigInteger.Abs(intBase);
                        if (magnitude > BigInteger.One &&
                            ((magnitude.GetBitLength() - 1) * (int)exponent) + 1 > CalculationLimits.MaxIntegerBits)
                        {
                            throw new CalculationLimitException();
                        }

                        return new MathValue(BigInteger.Pow(intBase, (int)exponent));
                    }

                    // Fall through to double calculation on decimal overflow.
                }
            }
        }

        double b = baseVal.AsDouble();
        double e = expVal.AsDouble();

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

        return new MathValue(Math.Pow(b, e));
    }

    private static decimal DecimalPow(decimal baseVal, int exponent)
    {
        // Exponentiation by squaring to preserve decimal precision without precision
        // loss.
        decimal result = 1m;
        decimal b = baseVal;
        int e = exponent;

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

        int scale = (bits[3] >> 16) & 0xff;

        // A decimal is coefficient / 10^scale. Multiplying the integer by 10^scale
        // puts both sides on the same exact integer scale without division or rounding.
        return (integer * BigInteger.Pow(10, scale)).CompareTo(coefficient);
    }

    private static int CompareDoubleAndExact(MathValue exact, double dbl)
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
            long bitLength = integer.GetBitLength();

            // Double.MaxValue has an exponent of 1023, so any integer with 1025+ bits
            // exceeds Double.MaxValue in magnitude and cannot be compared via (double)int.
            if (bitLength > 1024)
            {
                return integer.Sign > 0 ? 1 : -1;
            }

            double dblExact = (double)integer;
            if (dblExact == dbl)
            {
                return 0;
            }

            return dblExact.CompareTo(dbl);
        }

        // For Decimal vs finite double.
        double dblDec = (double)exact.DecVal;
        if (dblDec == dbl)
        {
            return 0;
        }

        return dblDec.CompareTo(dbl);
    }

    private static double DivideAsDouble(MathValue a, MathValue b)
    {
        // Convert bounded mantissas instead of entire integers to avoid infinity/infinity
        // when a very large numerator and denominator have a finite quotient.
        double numerator = a.AsDoubleMantissa(out int numeratorExponent);
        double denominator = b.AsDoubleMantissa(out int denominatorExponent);
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
        double mantissa = (double)(magnitude >> exponent);
        return IntVal.Sign < 0 ? -mantissa : mantissa;
    }

    private static bool TryEvaluateMixedIntegerAndWholeDecimal(
        MathValue a,
        MathValue b,
        Func<BigInteger, BigInteger, BigInteger> intOperation,
        out MathValue result)
    {
        if (a.Type is DataType.Integer && b.Type is DataType.Decimal)
        {
            if (decimal.Truncate(b.DecVal) == b.DecVal)
            {
                result = new MathValue(intOperation(a.IntVal, new BigInteger(b.DecVal)));
                return true;
            }
        }
        else if (a.Type is DataType.Decimal && b.Type is DataType.Integer)
        {
            if (decimal.Truncate(a.DecVal) == a.DecVal)
            {
                result = new MathValue(intOperation(new BigInteger(a.DecVal), b.IntVal));
                return true;
            }
        }
        else if (a.Type is DataType.Decimal && b.Type is DataType.Decimal)
        {
            if (decimal.Truncate(a.DecVal) == a.DecVal && decimal.Truncate(b.DecVal) == b.DecVal)
            {
                result = new MathValue(intOperation(new BigInteger(a.DecVal), new BigInteger(b.DecVal)));
                return true;
            }
        }

        result = default;
        return false;
    }
}
