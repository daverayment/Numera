using System.Numerics;

namespace Numera;

/// <summary>
/// A calculator value stored as an integer, decimal, or double. Operations select their
/// result representation eagerly, preserving integer arithmetic where supported and
/// falling back to double when decimal arithmetic overflows.
/// </summary>
/// <remarks>
/// Only the property selected by <see cref="Kind"/> carries the value; the other value
/// properties are zero. The default instance is integer zero. Representation alone does
/// not establish the accuracy of a calculation: decimal arithmetic can round, and double
/// operations can approximate. Integer results exceeding the safety ceiling throw
/// <see cref="CalculationLimitException"/> rather than falling back to double.
/// <para>
/// Division and modulo by zero throw <see cref="DivideByZeroException"/> rather than
/// producing infinity or NaN. Comparison and logical operations return decimal <c>1m</c>
/// (true) or <c>0m</c> (false). Converting an integer beyond the double range yields
/// +/-infinity. Radix formatting (<see cref="ToHexString(bool)"/>) is sign-magnitude,
/// whereas bitwise operators use two's-complement semantics for negative operands.
/// </para>
/// <para>
/// Object equality (<see cref="Equals(MathValue)"/>, <see cref="operator =="/>) treats
/// <c>NaN</c> as equal to <c>NaN</c> and <c>+0.0</c> as equal to <c>-0.0</c> to maintain
/// reflexive hashing and dictionary lookup semantics. This contrasts with expression
/// evaluation (<see cref="AreEqual(MathValue, MathValue)"/>), which follows IEEE 754
/// relational semantics where comparisons involving <c>NaN</c> always evaluate to false.
/// </para>
/// </remarks>
public readonly struct MathValue :
    IEquatable<MathValue>,
    IComparable<MathValue>,
    IComparable,
    IFormattable,
    ISpanFormattable,
    IUtf8SpanFormattable
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
    /// Gets which value property is active; this is not a numerical accuracy guarantee.
    /// </summary>
    public DataType Kind { get; }

    /// <summary>
    /// Gets the stored <see cref="BigInteger"/> value when <see cref="Kind"/> is
    /// <see cref="DataType.Integer"/>; otherwise zero.
    /// </summary>
    public BigInteger IntegerValue { get; }

    /// <summary>
    /// Gets the stored <see cref="decimal"/> value when <see cref="Kind"/> is
    /// <see cref="DataType.Decimal"/>; otherwise zero.
    /// </summary>
    public decimal DecimalValue { get; }

    /// <summary>
    /// Gets the stored <see cref="double"/> value when <see cref="Kind"/> is
    /// <see cref="DataType.Double"/>; otherwise zero.
    /// </summary>
    public double DoubleValue { get; }

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

        Kind = DataType.Integer;
        IntegerValue = value;
        DecimalValue = default;
        DoubleValue = default;
    }

    /// <summary>
    /// Initializes a new instance of <see cref="MathValue"/> holding an exact integer.
    /// </summary>
    /// <param name="value">The integer value to store.</param>
    public MathValue(int value)
    {
        Kind = DataType.Integer;
        IntegerValue = new BigInteger(value);
        DecimalValue = default;
        DoubleValue = default;
    }

    /// <summary>
    /// Initializes a new instance of <see cref="MathValue"/> holding an exact unsigned integer.
    /// </summary>
    /// <param name="value">The unsigned integer value to store.</param>
    public MathValue(uint value)
    {
        Kind = DataType.Integer;
        IntegerValue = new BigInteger(value);
        DecimalValue = default;
        DoubleValue = default;
    }

    /// <summary>
    /// Initializes a new instance of <see cref="MathValue"/> holding an exact 64-bit integer.
    /// </summary>
    /// <param name="value">The 64-bit integer value to store.</param>
    public MathValue(long value)
    {
        Kind = DataType.Integer;
        IntegerValue = new BigInteger(value);
        DecimalValue = default;
        DoubleValue = default;
    }

    /// <summary>
    /// Initializes a new instance of <see cref="MathValue"/> holding an exact unsigned 64-bit integer.
    /// </summary>
    /// <param name="value">The unsigned 64-bit integer value to store.</param>
    public MathValue(ulong value)
    {
        Kind = DataType.Integer;
        IntegerValue = new BigInteger(value);
        DecimalValue = default;
        DoubleValue = default;
    }

    /// <summary>
    /// Initializes a new instance of <see cref="MathValue"/> holding a fixed-precision
    /// decimal value.
    /// </summary>
    /// <param name="value">The decimal fraction or integer to store.</param>
    public MathValue(decimal value)
    {
        Kind = DataType.Decimal;
        IntegerValue = default;
        DecimalValue = value;
        DoubleValue = default;
    }

    /// <summary>
    /// Initializes a new instance of <see cref="MathValue"/> holding a double-precision
    /// floating-point value.
    /// </summary>
    /// <param name="value">The 64-bit binary floating-point value to store.</param>
    public MathValue(double value)
    {
        Kind = DataType.Double;
        IntegerValue = default;
        DecimalValue = default;
        DoubleValue = value;
    }

    /// <summary>
    /// Converts the value to a 64-bit floating-point number. Integers and decimals may
    /// lose precision, and integers beyond the double range become +/-infinity.
    /// </summary>
    /// <returns>The value as a <see cref="double"/>.</returns>
    public double AsDouble() => Kind switch
    {
        DataType.Integer => (double)IntegerValue,
        DataType.Decimal => (double)DecimalValue,
        _ => DoubleValue,
    };

    /// <summary>
    /// Gets a value indicating whether the value is finite. Integer and decimal values
    /// are always finite; a double is not finite if it is NaN or +/-infinity.
    /// </summary>
    public bool IsFinite => Kind switch
    {
        DataType.Double => double.IsFinite(DoubleValue),
        _ => true,
    };

    /// <summary>
    /// Gets a value indicating whether the value is Not-a-Number. Only a double can be a
    /// NaN.
    /// </summary>
    public bool IsNaN => Kind switch
    {
        DataType.Double => double.IsNaN(DoubleValue),
        _ => false,
    };

    /// <summary>
    /// Formats the value using the current culture and the default format.
    /// </summary>
    /// <returns>The formatted string representation of the value.</returns>
    public override string ToString() => ToString(null, null);

    /// <summary>
    /// Formats the value using the specified format string and the current culture.
    /// </summary>
    /// <param name="format">A format string valid for the active representation, or
    /// <see langword="null"/> for the default.</param>
    /// <returns>The formatted string representation of the value.</returns>
    public string ToString(string? format) => ToString(format, null);

    /// <summary>
    /// Formats the value using the specified format provider and no format string.
    /// </summary>
    /// <param name="formatProvider">The format provider to use.</param>
    /// <returns>The formatted string representation of the value.</returns>
    public string ToString(IFormatProvider? formatProvider) => ToString(null, formatProvider);

    /// <summary>
    /// Formats the value using the specified format and format provider. A double with no
    /// format uses the shortest string that round-trips.
    /// </summary>
    /// <param name="format">A format string valid for the active representation, or
    /// <see langword="null"/> for the default.</param>
    /// <param name="formatProvider">The format provider to use, or <see langword="null"/>
    /// for the current culture.</param>
    /// <returns>The formatted string representation of the value.</returns>
    public string ToString(string? format, IFormatProvider? formatProvider) => Kind switch
    {
        DataType.Integer => IntegerValue.ToString(format, formatProvider),
        DataType.Decimal => DecimalValue.ToString(format, formatProvider),
        DataType.Double => DoubleValue.ToString(format, formatProvider),
        _ => throw new InvalidOperationException($"Unexpected data type: {Kind}"),
    };

    /// <summary>
    /// Tries to format the value into a character span.
    /// </summary>
    /// <param name="destination">The span to write the formatted value into.</param>
    /// <param name="charsWritten">When this method returns, the number of characters
    /// written.</param>
    /// <param name="format">A format string valid for the active representation, or
    /// empty for the default.</param>
    /// <param name="provider">The format provider, or <see langword="null"/> for the
    /// current culture.</param>
    /// <returns><see langword="true"/> if the value was formatted;
    /// <see langword="false"/> if <paramref name="destination"/> was too small.</returns>
    public bool TryFormat(
        Span<char> destination,
        out int charsWritten,
        ReadOnlySpan<char> format = default,
        IFormatProvider? provider = null)
    {
        return Kind switch
        {
            DataType.Integer => IntegerValue.TryFormat(destination, out charsWritten, format, provider),
            DataType.Decimal => DecimalValue.TryFormat(destination, out charsWritten, format, provider),
            DataType.Double => DoubleValue.TryFormat(destination, out charsWritten, format, provider),
            _ => throw new InvalidOperationException($"Unexpected data type: {Kind}"),
        };
    }

    /// <summary>
    /// Tries to format the value into a UTF-8 byte span.
    /// </summary>
    /// <param name="utf8Destination">The span to write to.</param>
    /// <param name="bytesWritten">When this method returns, the number of bytes written.
    /// </param>
    /// <param name="format">A format string valid for the active representation, or
    /// empty for the default.</param>
    /// <param name="provider">The format provider, or <see langword="null"/> for the
    /// current culture.</param>
    /// <returns><see langword="true"/> if the value was formatted;
    /// <see langword="false"/> if <paramref name="utf8Destination"/> was too small.
    /// </returns>
    public bool TryFormat(
        Span<byte> utf8Destination,
        out int bytesWritten,
        ReadOnlySpan<char> format = default,
        IFormatProvider? provider = null)
    {
        switch (Kind)
        {
            case DataType.Integer:
            {
                // BigInteger implements ISpanFormattable (chars). Format to a stack buffer and encode to UTF-8.
                Span<char> charBuffer = stackalloc char[256];
                if (IntegerValue.TryFormat(charBuffer, out int charsWritten, format, provider))
                {
                    return System.Text.Encoding.UTF8.TryGetBytes(charBuffer[..charsWritten], utf8Destination, out bytesWritten);
                }

                // If the integer exceeds 256 characters, allocate a temporary string
                string formatted = IntegerValue.ToString(format.IsEmpty ? null : format.ToString(), provider);
                return System.Text.Encoding.UTF8.TryGetBytes(formatted, utf8Destination, out bytesWritten);
            }
            case DataType.Decimal:
                return DecimalValue.TryFormat(utf8Destination, out bytesWritten, format, provider);
            case DataType.Double:
                return DoubleValue.TryFormat(
                    utf8Destination,
                    out bytesWritten,
                    format,
                    provider);
            default:
                throw new InvalidOperationException($"Unexpected data type: {Kind}");
        }
    }

    /// <summary>
    /// Returns the hexadecimal representation of the value if it is integer-valued;
    /// otherwise, <see langword="null"/>.
    /// </summary>
    /// <param name="prefix">Whether to prefix the result with "0x" (or "-0x" if
    /// negative).</param>
    /// <returns>The hexadecimal string, or <see langword="null"/> if
    /// <see cref="TryGetInteger(out BigInteger)"/> fails. Negative values are written as
    /// minus sign followed by the magnitude, not in two's-complement.</returns>
    public string? ToHexString(bool prefix = true)
    {
        if (!TryGetInteger(out var intValue))
        {
            return null;
        }

        if (intValue.IsZero)
        {
            return prefix ? "0x0" : "0";
        }

        bool isNegative = intValue < BigInteger.Zero;
        BigInteger absVal = BigInteger.Abs(intValue);

        // BigInteger's "X" format pads with a leading zero when the top bit is set so the
        // value cannot be misread as negative; that padding is noise here because the
        // sign is added explicitly.
        string hex = absVal.ToString("X").TrimStart('0');
        if (hex.Length == 0)
        {
            hex = "0";
        }

        if (!prefix)
        {
            return isNegative ? "-" + hex : hex;
        }

        return isNegative ? "-0x" + hex : "0x" + hex;
    }

    /// <summary>
    /// Returns the binary representation of the value if it is integer-valued;
    /// otherwise, <see langword="null"/>.
    /// </summary>
    /// <param name="prefix">Whether to prefix the result with "0b" (or "-0b" if
    /// negative).</param>
    /// <returns>The binary string, or <see langword="null"/> if
    /// <see cref="TryGetInteger(out BigInteger)"/> fails. Negative values are written as
    /// minus sign followed by the magnitude, not in two's-complement.</returns>
    public string? ToBinaryString(bool prefix = true)
    {
        if (!TryGetInteger(out var intValue))
        {
            return null;
        }

        if (intValue.IsZero)
        {
            return prefix ? "0b0" : "0";
        }

        bool isNegative = intValue < BigInteger.Zero;
        BigInteger absVal = BigInteger.Abs(intValue);
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
    /// Returns the octal representation of the value if it is integer-valued; otherwise
    /// <see langword="null"/>.
    /// </summary>
    /// <param name="prefix">Whether to prefix the result with "0o" (or "-0o" if
    /// negative).</param>
    /// <returns>The octal string, or <see langword="null"/> if
    /// <see cref="TryGetInteger(out BigInteger)"/> fails. Negative values are written as
    /// minus sign followed by the magnitude, not in two's-complement.</returns>
    public string? ToOctalString(bool prefix = true)
    {
        if (!TryGetInteger(out var intValue))
        {
            return null;
        }

        if (intValue.IsZero)
        {
            return prefix ? "0o0" : "0";
        }

        bool isNegative = intValue < BigInteger.Zero;
        BigInteger absVal = BigInteger.Abs(intValue);

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

    /// <summary>
    /// Gets a value indicating whether the current <see cref="MathValue"/> is zero.
    /// </summary>
    public bool IsZero => Kind switch
    {
        DataType.Integer => IntegerValue.IsZero,
        DataType.Decimal => DecimalValue == 0m,
        _ => DoubleValue == 0.0,
    };

    /// <summary>
    /// Extracts an integer when the stored value is finite and has no fractional part.
    /// Success does not recover digits lost in an earlier approximate calculation.
    /// </summary>
    /// <param name="value">When this method returns <see langword="true"/>, the integer
    /// value; otherwise zero.</param>
    /// <returns><see langword="true"/> if the value is integer-valued; otherwise
    /// <see langword="false"/>.</returns>
    public bool TryGetInteger(out BigInteger value)
    {
        switch (Kind)
        {
            case DataType.Integer:
                value = IntegerValue;
                return true;
            case DataType.Decimal when decimal.Truncate(DecimalValue) == DecimalValue:
                value = new BigInteger(DecimalValue);
                return true;
            case DataType.Double when double.IsFinite(DoubleValue) && Math.Truncate(DoubleValue) == DoubleValue:
                value = new BigInteger(DoubleValue);
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
    public static MathValue operator -(MathValue value) => value.Kind switch
    {
        DataType.Integer => new MathValue(-value.IntegerValue),
        DataType.Decimal => new MathValue(-value.DecimalValue),
        _ => new MathValue(-value.DoubleValue),
    };

    /// <summary>
    /// Orders two values. Integer and decimal operands, and any mix of them with a double
    /// are compared exactly without conversion. Two doubles are ordered with
    /// <see cref="double.CompareTo(double)"/>, except that +0.0 and -0.0 compare equal.
    /// NaN orders below every other value, including -Infinity.
    /// </summary>
    /// <param name="a">The left operand.</param>
    /// <param name="b">The right operand.</param>
    /// <returns>A negative value, zero, or a positive value when <paramref name="a"/> is
    /// less than, equal to, or greater than <paramref name="b"/>.</returns>
    public static int Compare(MathValue a, MathValue b)
    {
        // If both values are integers, compare them as big integers to avoid precision
        // and magnitude loss from double conversion.
        if (a.Kind is DataType.Integer && b.Kind is DataType.Integer)
        {
            return a.IntegerValue.CompareTo(b.IntegerValue);
        }

        if (a.Kind is not DataType.Double && b.Kind is not DataType.Double)
        {
            if (a.Kind is DataType.Integer)
            {
                return CompareIntegerAndDecimal(a.IntegerValue, b.DecimalValue);
            }

            if (b.Kind is DataType.Integer)
            {
                return -CompareIntegerAndDecimal(b.IntegerValue, a.DecimalValue);
            }

            return a.DecimalValue.CompareTo(b.DecimalValue);
        }

        // Handle mixed Double and exact Integer/Decimal without overflow-to-infinity
        // false matches.
        if (a.Kind is DataType.Double && b.Kind is not DataType.Double)
        {
            return -CompareDoubleAndExact(b, a.DoubleValue);
        }

        if (b.Kind is DataType.Double && a.Kind is not DataType.Double)
        {
            return CompareDoubleAndExact(a, b.DoubleValue);
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

    /// <inheritdoc />
    public int CompareTo(MathValue other) => Compare(this, other);

    /// <inheritdoc />
    int IComparable.CompareTo(object? obj)
    {
        if (obj is null)
        {
            return 1;
        }

        if (obj is MathValue other)
        {
            return Compare(this, other);
        }

        throw new ArgumentException($"Object must be of type {nameof(MathValue)}.", nameof(obj));
    }

    /// <summary>
    /// Indicates whether the current value is equal to another <see cref="MathValue"/>,
    /// comparing numerical values across representations. Unlike IEEE 754 relational
    /// operations, two NaN values compare equal here so that hashing and dictionary lookups
    /// remain reflexive.
    /// </summary>
    /// <param name="other">The value to compare with this instance.</param>
    /// <returns><see langword="true"/> if the current value is numerically equal to
    /// <paramref name="other"/>; otherwise <see langword="false"/>.</returns>
    public bool Equals(MathValue other)
    {
        // NaN == NaN for object/IEquatable identity.
        if (IsNaN && other.IsNaN)
        {
            return true;
        }

        if (IsNaN || other.IsNaN)
        {
            return false;
        }

        return Compare(this, other) == 0;
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is MathValue other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        // Consistent hash code across representations for equal values.
        if (IsNaN)
        {
            return typeof(MathValue).GetHashCode();
        }

        if (TryGetInteger(out var intVal))
        {
            return intVal.GetHashCode();
        }

        // For non-integer values, reduce to canonical rational representation (num / den)
        // so that mathematically equal values in Decimal and Double (e.g., 0.5m and 0.5)
        // produce identical hash codes.
        if (Kind is DataType.Decimal)
        {
            var (num, den) = ToReducedFraction(DecimalValue);
            return HashCode.Combine(num, den);
        }

        if (Kind is DataType.Double && double.IsFinite(DoubleValue))
        {
            var (num, den) = ToReducedFraction(DoubleValue);
            return HashCode.Combine(num, den);
        }

        return DoubleValue.GetHashCode();
    }

    private static (BigInteger numerator, BigInteger denominator) ToReducedFraction(decimal d)
    {
        Span<int> decBits = stackalloc int[4];
        decimal.GetBits(d, decBits);

        var c = (BigInteger)(uint)decBits[0]
            | ((BigInteger)(uint)decBits[1] << 32)
            | ((BigInteger)(uint)decBits[2] << 64);

        if (decBits[3] < 0)
        {
            c = -c;
        }

        int s = (decBits[3] >> 16) & 0x7F;
        BigInteger den = BigInteger.Pow(10, s);
        BigInteger gcd = BigInteger.GreatestCommonDivisor(BigInteger.Abs(c), den);
        return (c / gcd, den / gcd);
    }

    private static (BigInteger numerator, BigInteger denominator) ToReducedFraction(double d)
    {
        long dblBits = BitConverter.DoubleToInt64Bits(d);
        bool isNegative = dblBits < 0;
        int exp = (int)((dblBits >> 52) & 0x7FFL);
        long mantissa = dblBits & 0x000F_FFFF_FFFF_FFFFL;

        BigInteger m;
        int shift;

        if (exp == 0)
        {
            m = mantissa;
            shift = -1074;
        }
        else
        {
            m = mantissa | (1L << 52);
            shift = exp - 1075;
        }

        if (isNegative)
        {
            m = -m;
        }

        BigInteger num;
        BigInteger den;

        if (shift >= 0)
        {
            num = m << shift;
            den = BigInteger.One;
        }
        else
        {
            num = m;
            den = BigInteger.One << (-shift);
            BigInteger gcd = BigInteger.GreatestCommonDivisor(BigInteger.Abs(num), den);
            num /= gcd;
            den /= gcd;
        }

        return (num, den);
    }

    /// <summary>
    /// Determines whether two <see cref="MathValue"/> instances are equal.
    /// </summary>
    public static bool operator ==(MathValue left, MathValue right) => left.Equals(right);

    /// <summary>
    /// Determines whether two <see cref="MathValue"/> instances are not equal.
    /// </summary>
    public static bool operator !=(MathValue left, MathValue right) => !left.Equals(right);

    // Logical results use decimal zero/one. Only zero is false, so NaN is truthy here.
    // These helpers receive both values; short-circuiting belongs to the parser.
    /// <summary>
    /// Performs a logical AND on two values, treating zero as false and all other values
    /// (including NaN) as true.
    /// </summary>
    /// <param name="a">The left operand.</param>
    /// <param name="b">The right operand.</param>
    /// <returns><c>1m</c> if the condition holds; otherwise <c>0m</c>.</returns>
    public static MathValue LogicalAnd(MathValue a, MathValue b) =>
        (!a.IsZero && !b.IsZero) ? new MathValue(1m) : new MathValue(0m);

    /// <summary>
    /// Performs a logical OR on two values, treating zero as false and all other values
    /// (including NaN) as true.
    /// </summary>
    /// <param name="a">The left operand.</param>
    /// <param name="b">The right operand.</param>
    /// <returns><c>1m</c> if the condition holds; otherwise <c>0m</c>.</returns>
    public static MathValue LogicalOr(MathValue a, MathValue b) =>
        (!a.IsZero || !b.IsZero) ? new MathValue(1m) : new MathValue(0m);

    /// <summary>
    /// Performs a logical XOR on two values, treating zero as false and all other values
    /// (including NaN) as true.
    /// </summary>
    /// <param name="a">The left operand.</param>
    /// <param name="b">The right operand.</param>
    /// <returns><c>1m</c> if the condition holds; otherwise <c>0m</c>.</returns>
    public static MathValue LogicalXor(MathValue a, MathValue b) =>
        (!a.IsZero ^ !b.IsZero) ? new MathValue(1m) : new MathValue(0m);

    /// <summary>
    /// Performs a logical NOT on a value, treating zero as false and all other values
    /// (including NaN) as true.
    /// </summary>
    /// <param name="a">The operand.</param>
    /// <returns><c>1m</c> if <paramref name="a"/> is zero; otherwise <c>0m</c>.</returns>
    public static MathValue LogicalNot(MathValue a) =>
        a.IsZero ? new MathValue(1m) : new MathValue(0m);

    /// <summary>
    /// Performs a bitwise shift-left on integer operands, enforcing the maximum bit
    /// ceiling.
    /// </summary>
    /// <param name="a">The value to shift.</param>
    /// <param name="b">The shift count.</param>
    /// <returns>The result of the shift operation.</returns>
    /// <exception cref="ExpressionException">Thrown if an operand is not an integer or
    /// the shift count is negative.</exception>
    /// <exception cref="CalculationLimitException">Thrown if the shift count, or the
    /// shifted result, would exceed the configured bit ceiling. A zero value shifted
    /// by any count is always zero and does not throw.</exception>
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

        if (val.IsZero)
        {
            return new MathValue(BigInteger.Zero);
        }

        if (count > CalculationLimits.MaxIntegerBits)
        {
            throw new CalculationLimitException();
        }

        int shift = (int)count;
        if (BigInteger.Abs(val).GetBitLength() + shift > CalculationLimits.MaxIntegerBits)
        {
            throw new CalculationLimitException();
        }

        return new MathValue(val << shift);
    }

    /// <summary>
    /// Performs an arithmetic bitwise shift-right on integer operands.
    /// </summary>
    /// <param name="a">The value to shift.</param>
    /// <param name="b">The shift count.</param>
    /// <returns>The result of the shift operation.</returns>
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

        // Clamp the shift count to the bit length of the value.
        long bitLength = val.GetBitLength();
        int shift = count > bitLength ? (int)bitLength : (int)count;

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
    /// Compares two values for equality using
    /// <see cref="Compare(MathValue, MathValue)"/>.
    /// </summary>
    /// <param name="a">The left operand.</param>
    /// <param name="b">The right operand.</param>
    /// <returns><c>1m</c> if the operands are equal; otherwise <c>0m</c>. If either
    /// operand is NaN, returns <c>0m</c>, as NaN is not equal to anything else.</returns>
    public static MathValue AreEqual(MathValue a, MathValue b)
    {
        // IEEE 754: if either value is NaN, the comparison is false.
        if (a.IsNaN || b.IsNaN)
        {
            return new MathValue(0m);
        }

        return Compare(a, b) == 0 ? new MathValue(1m) : new MathValue(0m);
    }

    /// <summary>
    /// Compares two values for inequality using
    /// <see cref="Compare(MathValue, MathValue)"/>.
    /// </summary>
    /// <param name="a">The left operand.</param>
    /// <param name="b">The right operand.</param>
    /// <returns><c>1m</c> if the operands are not equal; otherwise <c>0m</c>. If either
    /// operand is NaN, returns <c>1m</c>, as NaN is not equal to anything else.</returns>
    public static MathValue AreNotEqual(MathValue a, MathValue b)
    {
        // IEEE 754: if either value is NaN, the comparison is true.
        if (a.IsNaN || b.IsNaN)
        {
            return new MathValue(1m);
        }

        return Compare(a, b) != 0 ? new MathValue(1m) : new MathValue(0m);
    }

    /// <summary>
    /// Compares if the left operand is strictly less than the right operand using
    /// <see cref="Compare(MathValue, MathValue)"/>.
    /// </summary>
    /// <param name="a">The left operand.</param>
    /// <param name="b">The right operand.</param>
    /// <returns><c>1m</c> if the comparison holds; otherwise <c>0m</c>.</returns>
    public static MathValue LessThan(MathValue a, MathValue b)
    {
        if (a.IsNaN || b.IsNaN)
        {
            return new MathValue(0m);
        }

        return Compare(a, b) < 0 ? new MathValue(1m) : new MathValue(0m);
    }

    /// <summary>
    /// Compares if the left operand is less than or equal to the right operand using
    /// <see cref="Compare(MathValue, MathValue)"/>.
    /// </summary>
    /// <param name="a">The left operand.</param>
    /// <param name="b">The right operand.</param>
    /// <returns><c>1m</c> if the comparison holds; otherwise <c>0m</c>.</returns>
    public static MathValue LessThanOrEqual(MathValue a, MathValue b)
    {
        if (a.IsNaN || b.IsNaN)
        {
            return new MathValue(0m);
        }

        return Compare(a, b) <= 0 ? new MathValue(1m) : new MathValue(0m);
    }

    /// <summary>
    /// Compares if the left operand is strictly greater than the right operand using
    /// <see cref="Compare(MathValue, MathValue)"/>.
    /// </summary>
    /// <param name="a">The left operand.</param>
    /// <param name="b">The right operand.</param>
    /// <returns><c>1m</c> if the comparison holds; otherwise <c>0m</c>.</returns>
    public static MathValue GreaterThan(MathValue a, MathValue b)
    {
        if (a.IsNaN || b.IsNaN)
        {
            return new MathValue(0m);
        }

        return Compare(a, b) > 0 ? new MathValue(1m) : new MathValue(0m);
    }

    /// <summary>
    /// Compares if the left operand is greater than or equal to the right operand using
    /// <see cref="Compare(MathValue, MathValue)"/>.
    /// </summary>
    /// <param name="a">The left operand.</param>
    /// <param name="b">The right operand.</param>
    /// <returns><c>1m</c> if the comparison holds; otherwise <c>0m</c>.</returns>
    public static MathValue GreaterThanOrEqual(MathValue a, MathValue b)
    {
        if (a.IsNaN || b.IsNaN)
        {
            return new MathValue(0m);
        }

        return Compare(a, b) >= 0 ? new MathValue(1m) : new MathValue(0m);
    }

    /// <summary>
    /// Adds two values. Integers add exactly. If either operand is a double, the result
    /// is a double. Otherwise, decimal arithmetic is used, which can round; on decimal
    /// overflow, whole-number operands are added exactly as integers and anything else
    /// falls back to double.
    /// </summary>
    /// <param name="a">The left operand.</param>
    /// <param name="b">The right operand.</param>
    /// <returns>The sum.</returns>
    /// <exception cref="CalculationLimitException">Thrown if an integer result exceeds
    /// the configured bit ceiling.</exception>
    public static MathValue operator +(MathValue a, MathValue b)
    {
        // If either value is a double, the entire operation is promoted to double.
        if (a.Kind is DataType.Double || b.Kind is DataType.Double)
        {
            return new MathValue(a.AsDouble() + b.AsDouble());
        }

        // If both values are integers, perform exact integer addition.
        if (a.Kind is DataType.Integer && b.Kind is DataType.Integer)
        {
            return new MathValue(a.IntegerValue + b.IntegerValue);
        }

        // Prefer decimal for mixed integer/decimal or pure decimal arithmetic. This can
        // round to fit decimal precision; conversion or arithmetic overflow uses double.
        // However, if the decimal operand represents a whole number, promote to exact
        // BigInteger rather than losing precision in double.
        try
        {
            decimal decA = a.Kind is DataType.Integer ? (decimal)a.IntegerValue : a.DecimalValue;
            decimal decB = b.Kind is DataType.Integer ? (decimal)b.IntegerValue : b.DecimalValue;
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
    /// Subtracts the right operand from the left. If either operand is a double, the
    /// result is a double. Otherwise, decimal arithmetic is used, which can round; on
    /// decimal overflow, whole-number operands are subtracted exactly as integers and
    /// anything else falls back to double.
    /// </summary>
    /// <param name="a">The left operand.</param>
    /// <param name="b">The right operand.</param>
    /// <returns>The difference.</returns>
    /// <exception cref="CalculationLimitException">Thrown if an integer result exceeds
    /// the configured bit ceiling.</exception>
    public static MathValue operator -(MathValue a, MathValue b)
    {
        if (a.Kind is DataType.Double || b.Kind is DataType.Double)
        {
            return new MathValue(a.AsDouble() - b.AsDouble());
        }

        if (a.Kind is DataType.Integer && b.Kind is DataType.Integer)
        {
            return new MathValue(a.IntegerValue - b.IntegerValue);
        }

        try
        {
            decimal decA = a.Kind is DataType.Integer ? (decimal)a.IntegerValue : a.DecimalValue;
            decimal decB = b.Kind is DataType.Integer ? (decimal)b.IntegerValue : b.DecimalValue;
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
    /// Multiplies two values. Integers multiply exactly. If either operand is a double,
    /// the result is a double. Otherwise, decimal arithmetic is used, which can round; on
    /// decimal overflow, whole-number operands are multiplied exactly as integers and
    /// anything else falls back to double.
    /// </summary>
    /// <param name="a">The left operand.</param>
    /// <param name="b">The right operand.</param>
    /// <returns>The product.</returns>
    /// <exception cref="CalculationLimitException">Thrown if an integer result exceeds
    /// the configured bit ceiling.</exception>
    public static MathValue operator *(MathValue a, MathValue b)
    {
        if (a.Kind is DataType.Double || b.Kind is DataType.Double)
        {
            return new MathValue(a.AsDouble() * b.AsDouble());
        }

        if (a.Kind is DataType.Integer && b.Kind is DataType.Integer)
        {
            return new MathValue(a.IntegerValue * b.IntegerValue);
        }

        try
        {
            decimal decA = a.Kind is DataType.Integer ? (decimal)a.IntegerValue : a.DecimalValue;
            decimal decB = b.Kind is DataType.Integer ? (decimal)b.IntegerValue : b.DecimalValue;
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
    /// Divides two values. Integers divide exactly when the quotient is whole. Otherwise,
    /// decimal arithmetic is used, which can round. On decimal overflow, whole-number
    /// operands with a whole quotient give an exact integer and anything else falls back
    /// to double.
    /// </summary>
    /// <param name="a">The dividend.</param>
    /// <param name="b">The divisor.</param>
    /// <returns>The quotient.</returns>
    /// <exception cref="DivideByZeroException">Thrown when <paramref name="b"/> is zero.
    /// </exception>
    /// <exception cref="CalculationLimitException">Thrown if an integer result exceeds
    /// the configured bit ceiling.</exception>
    public static MathValue operator /(MathValue a, MathValue b)
    {
        if (b.IsZero)
        {
            throw new DivideByZeroException();
        }

        if (a.Kind is DataType.Double || b.Kind is DataType.Double)
        {
            return new MathValue(DivideAsDouble(a, b));
        }

        if (a.Kind is DataType.Integer &&
            b.Kind is DataType.Integer &&
            BigInteger.Remainder(a.IntegerValue, b.IntegerValue) == BigInteger.Zero)
        {
            // Preserve an exact integer quotient; fractional quotients use the decimal
            // path below, which can round even when it does not overflow.
            return new MathValue(a.IntegerValue / b.IntegerValue);
        }

        try
        {
            decimal decA = a.Kind is DataType.Integer ? (decimal)a.IntegerValue : a.DecimalValue;
            decimal decB = b.Kind is DataType.Integer ? (decimal)b.IntegerValue : b.DecimalValue;
            return new MathValue(decA / decB);
        }
        catch (OverflowException)
        {
            // If both operands represent whole numbers and divide evenly, promote to
            // exact BigInteger rather than losing precision in double. If there is a
            // fractional remainder that cannot fit in decimal, fall back to double to
            // avoid truncating integer division.
            if (a.TryGetInteger(out var intA) &&
                b.TryGetInteger(out var intB) &&
                BigInteger.Remainder(intA, intB).IsZero)
            {
                return new MathValue(intA / intB);
            }

            return new MathValue(DivideAsDouble(a, b));
        }
    }

    /// <summary>
    /// Computes the remainder of dividing two values. Integer remainders are exact;
    /// otherwise decimal arithmetic is used, falling back to an exact integer remainder
    /// for whole numbers on decimal overflow, and double for all other cases. If either
    /// operand is a double, the result is a double.
    /// </summary>
    /// <param name="a">The dividend.</param>
    /// <param name="b">The divisor.</param>
    /// <returns>The remainder, with the sign of <paramref name="a"/>.</returns>
    /// <exception cref="DivideByZeroException">Thrown when <paramref name="b"/> is zero.
    /// </exception>
    public static MathValue operator %(MathValue a, MathValue b)
    {
        if (b.IsZero)
        {
            throw new DivideByZeroException();
        }

        if (a.Kind is DataType.Double || b.Kind is DataType.Double)
        {
            return new MathValue(a.AsDouble() % b.AsDouble());
        }

        if (a.Kind is DataType.Integer && b.Kind is DataType.Integer)
        {
            return new MathValue(a.IntegerValue % b.IntegerValue);
        }

        try
        {
            decimal decA = a.Kind is DataType.Integer ? (decimal)a.IntegerValue : a.DecimalValue;
            decimal decB = b.Kind is DataType.Integer ? (decimal)b.IntegerValue : b.DecimalValue;
            return new MathValue(decA % decB);
        }
        catch (OverflowException)
        {
            // If both operands represent whole numbers, promote to exact BigInteger remainder
            // rather than losing precision in double.
            if (a.TryGetInteger(out var intA) && b.TryGetInteger(out var intB))
            {
                return new MathValue(intA % intB);
            }

            return new MathValue(a.AsDouble() % b.AsDouble());
        }
    }

    /// <summary>
    /// Implicitly converts a <see cref="decimal"/> to a <see cref="DataType.Decimal"/>
    /// <see cref="MathValue"/>.
    /// </summary>
    /// <param name="value">The decimal fraction or integer to store.</param>
    /// <returns>A <see cref="MathValue"/> holding <paramref name="value"/>.</returns>
    public static implicit operator MathValue(decimal value) => new(value);

    /// <summary>
    /// Implicitly converts a <see cref="BigInteger"/> to a <see cref="DataType.Integer"/>
    /// <see cref="MathValue"/>.
    /// </summary>
    /// <param name="value">The integer value to store.</param>
    /// <returns>A <see cref="MathValue"/> holding <paramref name="value"/> exactly.</returns>
    /// <exception cref="CalculationLimitException">Thrown if the magnitude exceeds the
    /// configured bit ceiling.</exception>
    public static implicit operator MathValue(BigInteger value) => new(value);

    /// <summary>
    /// Implicitly converts a <see cref="double"/> to a <see cref="DataType.Double"/>
    /// <see cref="MathValue"/>. No normalisation or promotion is applied.
    /// </summary>
    /// <param name="value">The 64-bit binary floating-point value to store.</param>
    /// <returns>A <see cref="MathValue"/> holding <paramref name="value"/>.</returns>
    public static implicit operator MathValue(double value) => new(value);

    /// <summary>
    /// Implicitly converts an <see cref="int"/> to a <see cref="DataType.Integer"/>
    /// <see cref="MathValue"/>.
    /// </summary>
    /// <param name="value">The integer value to store.</param>
    /// <returns>A <see cref="MathValue"/> holding <paramref name="value"/> exactly.</returns>
    public static implicit operator MathValue(int value) => new(value);

    /// <summary>
    /// Implicitly converts a <see cref="uint"/> to a <see cref="DataType.Integer"/>
    /// <see cref="MathValue"/>.
    /// </summary>
    /// <param name="value">The integer value to store.</param>
    /// <returns>A <see cref="MathValue"/> holding <paramref name="value"/> exactly.</returns>
    public static implicit operator MathValue(uint value) => new(value);

    /// <summary>
    /// Implicitly converts a <see cref="long"/> to a <see cref="DataType.Integer"/>
    /// <see cref="MathValue"/>.
    /// </summary>
    /// <param name="value">The integer value to store.</param>
    /// <returns>A <see cref="MathValue"/> holding <paramref name="value"/> exactly.</returns>
    public static implicit operator MathValue(long value) => new(value);

    /// <summary>
    /// Implicitly converts a <see cref="ulong"/> to a <see cref="DataType.Integer"/>
    /// <see cref="MathValue"/>.
    /// </summary>
    /// <param name="value">The integer value to store.</param>
    /// <returns>A <see cref="MathValue"/> holding <paramref name="value"/> exactly.</returns>
    public static implicit operator MathValue(ulong value) => new(value);

    /// <summary>
    /// Raises a value to a power. An integer base with a nonnegative integer-valued
    /// exponent is computed exactly. A decimal base with such an exponent uses
    /// exponentiation by squaring in decimal, which can round; on overflow, a whole-
    /// number base is promoted to an exact integer calculation; otherwise, the
    /// calculation falls back to double. Negative integer-valued exponents compute the
    /// reciprocal of the positive power (<c>1 / a^n</c>) via division, falling back to
    /// double when decimal precision would lose significant digits or when the positive
    /// power exceeds calculation limits. All other cases, including fractional exponents,
    /// use <see cref="Math.Pow(double, double)"/>.
    /// </summary>
    /// <param name="baseVal">The base value.</param>
    /// <param name="expVal">The exponent value.</param>
    /// <returns>The base raised to the exponent.</returns>
    /// <exception cref="DivideByZeroException">Thrown if zero is raised to a finite 
    /// negative power.</exception>
    /// <exception cref="ExpressionException">Thrown if a negative base is raised to a
    /// fractional power.</exception>
    /// <exception cref="CalculationLimitException">Thrown if an integer-valued exponent
    /// exceeds the configured magnitude limit, whatever the base type, or if an exact
    /// positive integer result would exceed the bit ceiling.</exception>
    public static MathValue Pow(MathValue baseVal, MathValue expVal)
    {
        var hasIntExp = expVal.TryGetInteger(out var exponent);
        if (hasIntExp)
        {
            if (BigInteger.Abs(exponent) > CalculationLimits.MaxExponentMagnitude)
            {
                throw new CalculationLimitException();
            }

            // Zero raised to a finite negative power is a division by zero.
            if (baseVal.IsZero && exponent < BigInteger.Zero)
            {
                throw new DivideByZeroException("Cannot raise zero to a negative power.");
            }

            if (exponent < BigInteger.Zero)
            {
                // The reciprocal rule is for exact kinds only; doubles keep Math.Pow semantics
                // (overflow to Infinity, underflow to zero) rather than throwing on 1 / 0.
                if (baseVal.Kind is DataType.Double)
                {
                    return new MathValue(Math.Pow(baseVal.AsDouble(), expVal.AsDouble()));
                }

                // If the base is decimal, has absolute value less than 1, and has an exact
                // decimal reciprocal (e.g., 0.5m -> 2m, 0.25m -> 4m, 0.1m -> 10m), invert the
                // base first and raise that to the positive power. Guarding with |base| < 1 ensures
                // we only invert when it turns a shrinking fractional power into an expanding
                // whole-number power that can fit in decimal or promote to BigInteger.
                if (baseVal.Kind is DataType.Decimal && !baseVal.IsZero && Math.Abs(baseVal.DecimalValue) < 1m)
                {
                    try
                    {
                        decimal inverse = 1m / baseVal.DecimalValue;
                        if (AreExactReciprocals(inverse, baseVal.DecimalValue))
                        {
                            return Pow(new MathValue(inverse), new MathValue(-exponent));
                        }
                    }
                    catch (OverflowException)
                    {
                        // Infinitesimal decimal bases (e.g., 1e-28m) whose inverse overflows decimal.
                    }
                }

                // Negative integer exponent: defined as reciprocal 1 / a^n.
                MathValue denominator;
                try
                {
                    denominator = Pow(baseVal, new MathValue(-exponent));
                }
                catch (CalculationLimitException)
                {
                    // The positive power exceeded the bit ceiling (e.g. 10^-5000), but the
                    // reciprocal is a tiny number representable in double.
                    return new MathValue(Math.Pow(baseVal.AsDouble(), expVal.AsDouble()));
                }

                // If the denominator represents an integer power of 2 beyond decimal's 28-digit
                // capacity (k > 28), decimal arithmetic cannot represent 1 / 2^k without rounding,
                // but IEEE 754 binary double represents it with zero roundoff error down to 2^-1074.
                // Whole-number decimals (e.g. from 2.0m^30) are checked as well so they do not slip past.
                BigInteger? intDenominator = denominator.Kind switch
                {
                    DataType.Integer => denominator.IntegerValue,
                    DataType.Decimal when decimal.Truncate(denominator.DecimalValue) == denominator.DecimalValue
                        => (BigInteger)denominator.DecimalValue,
                    _ => null,
                };

                if (intDenominator.HasValue)
                {
                    BigInteger magnitude = BigInteger.Abs(intDenominator.Value);
                    if (BigInteger.IsPow2(magnitude))
                    {
                        long k = magnitude.GetBitLength() - 1;
                        if (k > 28 && k <= 1074)
                        {
                            return new MathValue(intDenominator.Value.Sign * Math.ScaleB(1.0, (int)-k));
                        }
                    }
                }

                // A rounded decimal power has lost digits (or become zero) once it is small, so
                // reciprocating it would amplify the error. With exponent -1 the denominator is
                // the base itself and is exact, so it needs no guard.
                if (exponent < -BigInteger.One &&
                    denominator.Kind is DataType.Decimal &&
                    Math.Abs(denominator.DecimalValue) < 1e-11m)
                {
                    return new MathValue(Math.Pow(baseVal.AsDouble(), expVal.AsDouble()));
                }

                var quotient = new MathValue(BigInteger.One) / denominator;

                // Decimal has a fixed scale (at most 28 fractional digits). Guard against
                // losing significant digits on small quotients: require at least ~17 significant
                // digits (>= 1e-11m) or an exact decimal fraction (quotient * denominator == 1m).
                if (quotient.Kind is not DataType.Decimal || IsDecimalQuotientSignificant(quotient.DecimalValue, denominator))
                {
                    return quotient;
                }

                return new MathValue(Math.Pow(baseVal.AsDouble(), expVal.AsDouble()));
            }

            if (baseVal.Kind is DataType.Integer && exponent >= BigInteger.Zero)
            {
                var magnitude = BigInteger.Abs(baseVal.IntegerValue);

                // This lower bound rejects powers that must exceed the bit ceiling
                // before allocating them. The result constructor enforces the final size.
                if (magnitude > BigInteger.One &&
                    ((magnitude.GetBitLength() - 1) * (int)exponent) + 1 > CalculationLimits.MaxIntegerBits)
                {
                    throw new CalculationLimitException();
                }

                return new MathValue(BigInteger.Pow(baseVal.IntegerValue, (int)exponent));
            }

            if (baseVal.Kind is DataType.Decimal && exponent >= BigInteger.Zero)
            {
                try
                {
                    return new MathValue(DecimalPow(baseVal.DecimalValue, (int)exponent));
                }
                catch (OverflowException)
                {
                    // If the decimal base has no fractional part, promote to BigInteger
                    // rather than losing precision in double.
                    if (decimal.Truncate(baseVal.DecimalValue) == baseVal.DecimalValue)
                    {
                        var intBase = new BigInteger(baseVal.DecimalValue);
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

        // 0 raised to a finite negative power represents division by zero:
        // 0^-n = 1 / 0^n. A -Infinity exponent is a defined IEEE 754 limit (+Infinity),
        // so it falls through to Math.Pow.
        if (baseVal.IsZero && double.IsFinite(e) && e < 0.0)
        {
            throw new DivideByZeroException("Cannot raise zero to a negative power.");
        }

        // Negative base raised to a non-integer power yields a complex result without a
        // real value.
        if (b < 0.0 && double.IsFinite(e) && !hasIntExp)
        {
            throw new ExpressionException(new EvaluationDiagnostic(
                EvaluationError.UndefinedMathResult, "Negative base with a fractional exponent has no real solution."));
        }

        return new MathValue(Math.Pow(b, e));
    }

    private static bool IsDecimalQuotientSignificant(decimal quotient, MathValue denominator)
    {
        if (quotient == 0m)
        {
            return false;
        }

        if (Math.Abs(quotient) >= 1e-11m)
        {
            return true;
        }

        // Check whether the division was exact using integer arithmetic so decimal
        // multiplication rounding does not produce false positives (e.g. 1/0.3).
        if (denominator.Kind is DataType.Decimal)
        {
            return AreExactReciprocals(quotient, denominator.DecimalValue);
        }

        if (denominator.Kind is DataType.Integer && denominator.IntegerValue <= (BigInteger)decimal.MaxValue)
        {
            return AreExactReciprocals(quotient, (decimal)denominator.IntegerValue);
        }

        return false;
    }

    /// <summary>
    /// Decomposes a <see cref="decimal"/> into its signed integer coefficient and scale.
    /// </summary>
    private static (BigInteger Coefficient, int Scale) DecomposeDecimal(decimal value)
    {
        Span<int> bits = stackalloc int[4];
        decimal.GetBits(value, bits);
        var coefficient = (BigInteger)(uint)bits[0]
            + ((BigInteger)(uint)bits[1] << 32)
            + ((BigInteger)(uint)bits[2] << 64);
        if (bits[3] < 0)
        {
            coefficient = -coefficient;
        }

        int scale = (bits[3] >> 16) & 0x7f;
        return (coefficient, scale);
    }

    /// <summary>
    /// Checks whether two decimal values multiply to exactly 1 without rounding,
    /// evaluated in exact integer arithmetic: c_a * c_b == 10^(scale_a + scale_b).
    /// </summary>
    private static bool AreExactReciprocals(decimal a, decimal b)
    {
        var (ca, sa) = DecomposeDecimal(a);
        var (cb, sb) = DecomposeDecimal(b);
        return ca * cb == BigInteger.Pow(10, sa + sb);
    }

    private static decimal DecimalPow(decimal baseVal, int exponent)
    {
        // Exponentiation by squaring. checked() surfaces overflow, but intermediate
        // products can still round to decimal's 28-29 significant digits.
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
        // Handle non-finite doubles first.
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

        // Handle exact BigInteger vs finite double.
        if (exact.Kind is DataType.Integer)
        {
            var integer = exact.IntegerValue;

            // Double.MaxValue has a 1024-bit binary exponent (~1.79e308). Any integer
            // with 1025+ bits strictly exceeds Double.MaxValue in magnitude and cannot
            // be equal.
            if (integer.GetBitLength() > 1024)
            {
                return integer.Sign > 0 ? 1 : -1;
            }

            // Every finite double without a fractional part converts to a BigInteger
            // without precision loss, so we can convert and compare without narrowing.
            if (double.IsInteger(dbl))
            {
                var dblAsBigInt = new BigInteger(dbl);
                return integer.CompareTo(dblAsBigInt);
            }

            // An exact integer can never equal a floating-point value with a fractional
            // part. A non-integer double strictly sits in the open interval
            // (floor, floor + 1). Because floor(dbl) is an integer, it converts
            // exactly to a BigInteger.
            var floor = new BigInteger(Math.Floor(dbl));
            return (integer <= floor) ? -1 : 1;
        }

        // Exact decimal vs finite double.
        // If the double exceeds the decimal range, the comparison is trivial.
        if (Math.Abs(dbl) > (double)decimal.MaxValue)
        {
            return dbl > 0.0 ? -1 : 1;
        }

        // Whole doubles within decimal range convert to BigInteger and can be compared
        // via scaled integer arithmetic without division or rounding.
        if (double.IsInteger(dbl))
        {
            var dblAsBigInt = new BigInteger(dbl);
            return -CompareIntegerAndDecimal(dblAsBigInt, exact.DecimalValue);
        }

        // The double has a fractional part. Every finite double is an exact dyadic
        // rational (m * 2^e), and every decimal is an exact base-10 fraction (c * 10^-s).
        // Since double.IsInteger was false, the exponent is guaranteed to be negative
        // (k = -e > 0):
        //     exact  = c * 10^-s (or c / 10^s)
        //     double = m / 2^k
        // Cross-multiplying by positive denominators yields an exact integer comparison:
        //     exact <=> dbl === (c * 2^k) <=> (m * 10^s)
        Span<int> decBits = stackalloc int[4];
        decimal.GetBits(exact.DecimalValue, decBits);

        var c = (BigInteger)(uint)decBits[0]
            | ((BigInteger)(uint)decBits[1] << 32)
            | ((BigInteger)(uint)decBits[2] << 64);

        if (decBits[3] < 0)
        {
            c = -c;
        }

        int s = (decBits[3] >> 16) & 0x7F;

        // Deconstruct double bits into signed mantissa and negative power-of-two divisor
        // (2^k).
        long dblBits = BitConverter.DoubleToInt64Bits(dbl);
        bool isNegative = dblBits < 0;
        int exp = (int)((dblBits >> 52) & 0x7FFL);
        long mantissa = dblBits & 0x000F_FFFF_FFFF_FFFFL;

        BigInteger m;
        int k;

        if (exp == 0)
        {
            // Subnormal double: magnitude is mantissa * 2^-1074. The mantissa is nonzero,
            // and the exponent is -1022 - 52 = -1074.
            m = mantissa;
            k = 1074;
        }
        else
        {
            // Normal double: magnitude is (2^52 + mantissa) * 2^(exp - 1023 - 52) = (2^52 + mantissa) * 2^(exp - 1075).
            m = mantissa | (1L << 52);
            k = 1075 - exp;
        }

        if (isNegative)
        {
            m = -m;
        }

        // c * 2^k is a fast binary bit-shift; m * 10^s is a small BigInteger
        // multiplication (s <= 28). The result is exact.
        BigInteger decScaled = c << k;
        BigInteger dblScaled = m * BigInteger.Pow(10, s);

        return decScaled.CompareTo(dblScaled);
    }

    private static double DivideAsDouble(MathValue a, MathValue b)
    {
        // When both operands are exact integers, compute correctly rounded (round-half-even)
        // quotient via exact integer arithmetic rather than truncating mantissas.
        if (a.Kind is DataType.Integer && b.Kind is DataType.Integer)
        {
            return DivideIntegersAsDouble(a.IntegerValue, b.IntegerValue);
        }

        // Convert bounded mantissas instead of entire integers to avoid infinity/infinity
        // when a very large numerator and denominator have a finite quotient.
        double numerator = a.AsDoubleMantissa(out int numeratorExponent);
        double denominator = b.AsDoubleMantissa(out int denominatorExponent);
        return Math.ScaleB(numerator / denominator, numeratorExponent - denominatorExponent);
    }

    /// <summary>
    /// Correctly rounded (round-half-even) <paramref name="numerator"/> / <paramref name="denominator"/>
    /// for nonzero denominator, using exact integer arithmetic with a single rounding step.
    /// </summary>
    internal static double DivideIntegersAsDouble(BigInteger numerator, BigInteger denominator)
    {
        bool negative = (numerator.Sign < 0) != (denominator.Sign < 0);
        BigInteger n = BigInteger.Abs(numerator);
        BigInteger d = BigInteger.Abs(denominator);

        if (n.IsZero)
        {
            return negative ? -0.0 : 0.0;
        }

        // Aim for a quotient of 55-56 bits: 53 to keep, plus round and guard bits.
        long shift = 55 - ((long)n.GetBitLength() - (long)d.GetBitLength());
        if (shift >= 0)
        {
            n <<= (int)shift;
        }
        else
        {
            d <<= (int)-shift;
        }

        BigInteger q = BigInteger.DivRem(n, d, out BigInteger remainder);

        int extra = (int)q.GetBitLength() - 53; // 2 or 3 bits to drop
        BigInteger dropped = q & ((BigInteger.One << extra) - 1);
        q >>= extra;

        BigInteger half = BigInteger.One << (extra - 1);
        bool roundUp = dropped > half
            || (dropped == half && (!remainder.IsZero || !q.IsEven));
        if (roundUp)
        {
            q += 1; // may become 2^53, still exact in double
        }

        double result = Math.ScaleB((double)q, (int)(extra - shift));
        return negative ? -result : result;
    }

    private double AsDoubleMantissa(out int exponent)
    {
        exponent = 0;
        if (Kind is not DataType.Integer)
        {
            return AsDouble();
        }

        var magnitude = BigInteger.Abs(IntegerValue);

        // Keep at most 53 leading bits, the double significand's capacity, and restore
        // their scale separately. Discarded low bits are truncated, so this is
        // approximate.
        exponent = (int)Math.Max(0, magnitude.GetBitLength() - 53);
        double mantissa = (double)(magnitude >> exponent);
        return IntegerValue.Sign < 0 ? -mantissa : mantissa;
    }

    private static bool TryEvaluateMixedIntegerAndWholeDecimal(
        MathValue a,
        MathValue b,
        Func<BigInteger, BigInteger, BigInteger> intOperation,
        out MathValue result)
    {
        if (a.Kind is DataType.Integer && b.Kind is DataType.Decimal)
        {
            if (decimal.Truncate(b.DecimalValue) == b.DecimalValue)
            {
                result = new MathValue(intOperation(a.IntegerValue, new BigInteger(b.DecimalValue)));
                return true;
            }
        }
        else if (a.Kind is DataType.Decimal && b.Kind is DataType.Integer)
        {
            if (decimal.Truncate(a.DecimalValue) == a.DecimalValue)
            {
                result = new MathValue(intOperation(new BigInteger(a.DecimalValue), b.IntegerValue));
                return true;
            }
        }
        else if (a.Kind is DataType.Decimal && b.Kind is DataType.Decimal)
        {
            if (decimal.Truncate(a.DecimalValue) == a.DecimalValue && decimal.Truncate(b.DecimalValue) == b.DecimalValue)
            {
                result = new MathValue(intOperation(new BigInteger(a.DecimalValue), new BigInteger(b.DecimalValue)));
                return true;
            }
        }

        result = default;
        return false;
    }
}
