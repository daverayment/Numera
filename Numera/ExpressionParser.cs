using System.Globalization;
using System.Numerics;

namespace Numera;

/// <summary>
/// Evaluates one mathematical expression using recursive descent, without retaining an
/// abstract syntax tree. Each instance owns the parsing state for a single evaluation.
/// </summary>
/// <remarks>
/// Precedence, from lowest to highest, is OR, XOR, AND, equality, relational,
/// addition/subtraction, multiplication/division/remainder, unary signs/negation,
/// and exponentiation. Grouping and function arguments restart at the lowest precedence.
/// </remarks>
internal sealed class ExpressionParser
{
    private readonly string _input;
    private readonly EvaluationOptions _options;
    // Cursor positions also identify diagnostic locations in the original input.
    private readonly struct AngleArgument(double value, AngleUnit unit)
    {
        public double Value { get; } = value;
        public AngleUnit Unit { get; } = unit;
    }

    private AngleArgument? _currentTrigAngle;

    private int _pos;
    private int _depth;

    public ExpressionParser(string input, EvaluationOptions options)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (input.Length > options.MaxInputLength)
        {
            throw new CalculationLimitException();
        }

        _input = input;
        _options = options;
    }

    public NumberValue Evaluate()
    {
        var result = ParseLogical();
        SkipWhitespace();

        // A valid prefix is not enough: the entire expression must be consumed.
        if (_pos < _input.Length)
        {
            throw SyntaxError($"Unexpected character '{_input[_pos]}'.");
        }

        return result;
    }

    // With evaluate=false, parsing and validation still run, but operators and function
    // execution are skipped. Returned values on that path are not evaluation results.
    private NumberValue ParseLogical(bool evaluate = true)
    {
        // Recursive descent starts at the loosest-binding level (lowest precedence)
        // and descends toward tighter-binding levels:
        // ParseLogical (OR) -> ParseLogicalXor (XOR) -> ParseLogicalAnd (AND) -> ParseEquality -> ...
        // A method delegates to the next level for its operands, so the deeper levels
        // (AND) are evaluated before the outer levels (OR).
        var left = ParseLogicalXor(evaluate);
        while (true)
        {
            SkipWhitespace();
            if (Peek() == '|' && Peek(1) == '|')
            {
                Consume();
                Consume();
                var right = ParseLogicalXor(evaluate && left.IsZero);
                if (evaluate)
                {
                    left = NumberValue.LogicalOr(left, right);
                }
            }
            else if (MatchKeyword("or"))
            {
                var right = ParseLogicalXor(evaluate && left.IsZero);
                if (evaluate)
                {
                    left = NumberValue.LogicalOr(left, right);
                }
            }
            else
            {
                break;
            }
        }

        return left;
    }

    private NumberValue ParseLogicalXor(bool evaluate)
    {
        var left = ParseLogicalAnd(evaluate);
        while (true)
        {
            SkipWhitespace();
            if (MatchKeyword("xor"))
            {
                var right = ParseLogicalAnd(evaluate);
                if (evaluate)
                {
                    left = NumberValue.LogicalXor(left, right);
                }
            }
            else
            {
                break;
            }
        }

        return left;
    }

    private NumberValue ParseLogicalAnd(bool evaluate)
    {
        var left = ParseEquality(evaluate);
        while (true)
        {
            SkipWhitespace();
            if (Peek() == '&' && Peek(1) == '&')
            {
                Consume();
                Consume();
                var right = ParseEquality(evaluate && !left.IsZero);
                if (evaluate)
                {
                    left = NumberValue.LogicalAnd(left, right);
                }
            }
            else if (MatchKeyword("and"))
            {
                var right = ParseEquality(evaluate && !left.IsZero);
                if (evaluate)
                {
                    left = NumberValue.LogicalAnd(left, right);
                }
            }
            else
            {
                break;
            }
        }

        return left;
    }

    private NumberValue ParseEquality(bool evaluate)
    {
        // Equality chains fold left, comparing each Boolean result with the next operand;
        // they are not interpreted as a conjunction of pairwise comparisons.
        var left = ParseRelational(evaluate);
        while (true)
        {
            SkipWhitespace();
            if (Peek() == '=' && Peek(1) == '=')
            {
                Consume();
                Consume();
                var right = ParseRelational(evaluate);
                if (evaluate)
                {
                    left = NumberValue.AreEqual(left, right);
                }
            }
            else if ((Peek() == '~' || Peek() == '!') && Peek(1) == '=')
            {
                Consume();
                Consume();
                var right = ParseRelational(evaluate);
                if (evaluate)
                {
                    left = NumberValue.AreNotEqual(left, right);
                }
            }
            else
            {
                break;
            }
        }

        return left;
    }

    private NumberValue ParseRelational(bool evaluate)
    {
        var left = ParseExpression(evaluate);
        while (true)
        {
            SkipWhitespace();
            if (Peek() == '<' && Peek(1) == '=')
            {
                Consume();
                Consume();
                var right = ParseExpression(evaluate);
                if (evaluate)
                {
                    left = NumberValue.LessThanOrEqual(left, right);
                }
            }
            else if (Peek() == '>' && Peek(1) == '=')
            {
                Consume();
                Consume();
                var right = ParseExpression(evaluate);
                if (evaluate)
                {
                    left = NumberValue.GreaterThanOrEqual(left, right);
                }
            }
            else if (Peek() == '<')
            {
                Consume();
                var right = ParseExpression(evaluate);
                if (evaluate)
                {
                    left = NumberValue.LessThan(left, right);
                }
            }
            else if (Peek() == '>')
            {
                Consume();
                var right = ParseExpression(evaluate);
                if (evaluate)
                {
                    left = NumberValue.GreaterThan(left, right);
                }
            }
            else
            {
                break;
            }
        }

        return left;
    }

    private NumberValue ParseExpression(bool evaluate)
    {
        var left = ParseTerm(evaluate);
        while (true)
        {
            SkipWhitespace();
            if (Peek() == '+')
            {
                Consume();
                var right = ParseTerm(evaluate);
                if (evaluate)
                {
                    left += right;
                }
            }
            else if (Peek() == '-')
            {
                Consume();
                var right = ParseTerm(evaluate);
                if (evaluate)
                {
                    left -= right;
                }
            }
            else
            {
                break;
            }
        }

        return left;
    }

    private NumberValue ParseTerm(bool evaluate)
    {
        var left = ParseUnary(evaluate);
        while (true)
        {
            SkipWhitespace();
            if (Peek() == '*')
            {
                Consume();
                var right = ParseUnary(evaluate);
                if (evaluate)
                {
                    left *= right;
                }
            }
            else if (Peek() == '/')
            {
                Consume();
                var right = ParseUnary(evaluate);
                if (evaluate)
                {
                    left /= right;
                }
            }
            else if (Peek() == '%')
            {
                Consume();
                var right = ParseUnary(evaluate);
                if (evaluate)
                {
                    left %= right;
                }
            }
            else
            {
                break;
            }
        }

        return left;
    }

    private NumberValue ParseUnary(bool evaluate)
    {
        // Recursive grammar paths return through here, so one counter bounds grouping,
        // function arguments, unary chains, and right-associative powers alike.
        if (_depth >= _options.MaxNestingDepth)
        {
            throw new CalculationLimitException();
        }

        _depth++;
        try
        {
            SkipWhitespace();
            if (Peek() == '-')
            {
                Consume();
                var value = ParseUnary(evaluate);
                return evaluate ? -value : default;
            }
            else if (Peek() == '+')
            {
                Consume();
                return ParseUnary(evaluate);
            }
            else if (Peek() == '!')
            {
                Consume();
                var value = ParseUnary(evaluate);
                return evaluate ? NumberValue.LogicalNot(value) : default;
            }
            else if (MatchKeyword("not"))
            {
                var value = ParseUnary(evaluate);
                return evaluate ? NumberValue.LogicalNot(value) : default;
            }

            // Powers bind more tightly than a leading sign: -2^2 means -(2^2).
            return ParsePower(evaluate);
        }
        finally
        {
            _depth--;
        }
    }

    private NumberValue ParsePower(bool evaluate)
    {
        var left = ParsePrimary(evaluate);
        SkipWhitespace();
        if (Peek() == '^')
        {
            Consume();

            // Recursing through unary permits signed exponents and makes powers
            // right-associative: 2^-3 is valid, and 2^3^2 means 2^(3^2).
            var right = ParseUnary(evaluate);
            if (evaluate)
            {
                left = NumberValue.Pow(left, right);
            }
        }

        return left;
    }

    private NumberValue ParsePrimary(bool evaluate)
    {
        SkipWhitespace();
        if (_pos >= _input.Length)
        {
            throw SyntaxError("Unexpected end of input.");
        }

        var c = Peek();

        // Handle both () and [] as standard grouping brackets.
        if (c is '(' or '[')
        {
            Consume();
            var value = ParseLogical(evaluate);
            SkipWhitespace();

            var expectedClosing = c == '(' ? ')' : ']';

            if (Peek() != expectedClosing)
            {
                throw SyntaxError($"Expected '{expectedClosing}'.");
            }

            Consume();
            return value;
        }

        if (char.IsLetter(c) || c == 'π')
        {
            var identifierStart = _pos;
            var identifier = ParseIdentifier();
            SkipWhitespace();
            if (Peek() == '(')
            {
                Consume();
                SkipWhitespace();
                var args = new List<NumberValue>();
                if (Peek() != ')')
                {
                    while (true)
                    {
                        args.Add(ParseLogical(evaluate));
                        SkipWhitespace();
                        if (Peek() == ',')
                        {
                            Consume();
                        }
                        else
                        {
                            break;
                        }
                    }
                }

                if (Peek() == ')')
                {
                    Consume();
                }
                else
                {
                    throw SyntaxError("Expected ')'.");
                }

                // Short-circuiting suppresses execution, not errors in function names
                // or argument counts. Validation also protects the dispatch below.
                if (!FunctionArity.TryGet(identifier, out var signature))
                {
                    throw new ExpressionException(new EvaluationDiagnostic(
                        EvaluationError.UnknownFunction, $"Unknown function '{identifier}'.", identifierStart, identifier.Length));
                }

                if (args.Count < signature.MinimumArguments || (signature.MaximumArguments is int maximum && args.Count > maximum))
                {
                    throw new ExpressionException(new EvaluationDiagnostic(
                        EvaluationError.InvalidArgumentCount, $"Invalid argument count for '{identifier}'.", identifierStart, identifier.Length));
                }

                if (evaluate)
                {
                    var result = EvaluateFunction(identifier, args);
                    if (!identifier.Equals("deg", StringComparison.OrdinalIgnoreCase) &&
                        !identifier.Equals("rad", StringComparison.OrdinalIgnoreCase) &&
                        !identifier.Equals("grad", StringComparison.OrdinalIgnoreCase) &&
                        !identifier.Equals("deg2rad", StringComparison.OrdinalIgnoreCase) &&
                        !identifier.Equals("grad2rad", StringComparison.OrdinalIgnoreCase))
                    {
                        _currentTrigAngle = null;
                    }

                    return result;
                }

                return default;
            }

            // Constants and literals are still resolved in skipped operands, so invalid
            // names and malformed numbers cannot hide behind short-circuiting.
            return EvaluateConstant(identifier);
        }

        return ParseNumber();
    }

    private NumberValue ParseNumber()
    {
        var start = _pos;

        // Check for radix-prefixed integer literals: 0x (hex), 0b (binary), 0o (octal).
        if (Peek() == '0' && _pos + 1 < _input.Length)
        {
            var prefix = char.ToLowerInvariant(Peek(1));
            if (prefix is 'x' or 'b' or 'o')
            {
                Consume(); // '0'
                Consume(); // 'x', 'b', or 'o'

                var digitsStart = _pos;
                while (_pos < _input.Length && (prefix switch
                {
                    'x' => char.IsAsciiHexDigit(Peek()),
                    'b' => Peek() is '0' or '1',
                    'o' => Peek() is >= '0' and <= '7',
                    _ => false,
                }))
                {
                    Consume();
                }

                if (_pos == digitsStart)
                {
                    throw SyntaxError($"Expected {prefix switch { 'x' => "hexadecimal", 'b' => "binary", 'o' => "octal", _ => "radix" }} digits after '0{prefix}'.");
                }

                var digitsSpan = _input.AsSpan(digitsStart, _pos - digitsStart);
                var value = prefix switch
                {
                    'x' => ParseHexBigInteger(digitsSpan),
                    'b' => ParseBinaryBigInteger(digitsSpan),
                    'o' => ParseOctalBigInteger(digitsSpan),
                    _ => throw SyntaxError("Unsupported number prefix."),
                };

                return new NumberValue(value);
            }
        }

        var hasDecimal = false;
        var hasExponent = false;

        // Signs belong to ParseUnary. Literal syntax allows digits, an optional decimal point,
        // and an optional scientific exponent (e/E optionally followed by +/- and digits).
        while (_pos < _input.Length)
        {
            var ch = Peek();
            if (char.IsDigit(ch))
            {
                Consume();
            }
            else if (ch == '.')
            {
                if (hasDecimal || hasExponent)
                {
                    break;
                }

                hasDecimal = true;
                Consume();
            }
            else if (ch is 'e' or 'E')
            {
                if (hasExponent)
                {
                    break;
                }

                // Check ahead: exponent indicator must be followed by optional sign and at least one digit.
                var nextPos = 1;
                if (Peek(nextPos) is '+' or '-')
                {
                    nextPos++;
                }

                if (!char.IsDigit(Peek(nextPos)))
                {
                    break;
                }

                hasExponent = true;
                Consume(); // 'e' or 'E'
                if (Peek() is '+' or '-')
                {
                    Consume();
                }

                while (_pos < _input.Length && char.IsDigit(Peek()))
                {
                    Consume();
                }
            }
            else
            {
                break;
            }
        }

        var numberSpan = _input.AsSpan(start, _pos - start);

        // Keep whole-number literals without exponents as arbitrary-precision integers. For literals with
        // a decimal point or scientific exponent, prefer decimal to binary floating point; parsing may still
        // round excess digits. Double is a fallback for overflow or underflow to zero.
        if (!hasDecimal && !hasExponent && BigInteger.TryParse(
            numberSpan, NumberStyles.Integer, CultureInfo.InvariantCulture, out var intValue))
        {
            return new NumberValue(intValue);
        }
        else if (decimal.TryParse(
            numberSpan, NumberStyles.Float, CultureInfo.InvariantCulture, out var decValue))
        {
            // If decimal parsed to zero but the literal contains non-zero digits, the literal
            // underflowed decimal's 28-29 digit scale limit. Fall through to double so the
            // non-zero magnitude is preserved and expressions like 1 / 0.000...001 do not divide by zero.
            if (decValue != 0m || numberSpan.IndexOfAnyInRange('1', '9') < 0)
            {
                return new NumberValue(decValue);
            }
        }

        if (double.TryParse(
            numberSpan, NumberStyles.Float, CultureInfo.InvariantCulture, out var doubleValue))
        {
            return new NumberValue(doubleValue);
        }

        throw new ExpressionException(
            new EvaluationDiagnostic(EvaluationError.InvalidSyntax, "Expected a number.", start, Math.Max(1, _pos - start)));
    }

    private static BigInteger ParseHexBigInteger(ReadOnlySpan<char> span)
    {
        var result = BigInteger.Zero;
        foreach (var ch in span)
        {
            var digit = ch switch
            {
                >= '0' and <= '9' => ch - '0',
                >= 'a' and <= 'f' => ch - 'a' + 10,
                >= 'A' and <= 'F' => ch - 'A' + 10,
                _ => 0,
            };
            result = (result << 4) | (uint)digit;
        }

        return result;
    }

    private static BigInteger ParseBinaryBigInteger(ReadOnlySpan<char> span)
    {
        var result = BigInteger.Zero;
        foreach (var ch in span)
        {
            result = (result << 1) | (uint)(ch - '0');
        }

        return result;
    }

    private static BigInteger ParseOctalBigInteger(ReadOnlySpan<char> span)
    {
        var result = BigInteger.Zero;
        foreach (var ch in span)
        {
            result = (result << 3) | (uint)(ch - '0');
        }

        return result;
    }

    private ReadOnlySpan<char> ParseIdentifier()
    {
        var start = _pos;
        while (_pos < _input.Length && (char.IsLetterOrDigit(Peek()) || Peek() == 'π'))
        {
            Consume();
        }

        return _input.AsSpan(start, _pos - start);
    }

    private NumberValue EvaluateConstant(ReadOnlySpan<char> name)
    {
        if (name.Equals("pi", StringComparison.OrdinalIgnoreCase) || name.SequenceEqual("π"))
        {
            return new NumberValue(Math.PI);
        }

        if (name.Equals("e", StringComparison.OrdinalIgnoreCase))
        {
            return new NumberValue(Math.E);
        }

        throw new ExpressionException(
            new EvaluationDiagnostic(EvaluationError.UnknownConstant, $"Unknown constant '{name}'.", _pos - name.Length, name.Length));
    }

    private NumberValue EvaluateFunction(ReadOnlySpan<char> name, List<NumberValue> args)
    {
        Span<char> lower = stackalloc char[name.Length];
        name.ToLowerInvariant(lower);

        // ParsePrimary has already validated the canonical name and argument count.
        if (lower is "min" or "max")
        {
            return EvaluateMinMax(args, lower is "min");
        }

        if (lower is "avg")
        {
            return EvaluateAvg(args);
        }

        if (args.Count == 0)
        {
            // rand is the only registered zero-argument function.
            return new NumberValue(Random.Shared.NextDouble());
        }

        if (args.Count == 1)
        {
            // Math-based functions use double approximations. Branches that can operate
            // on integer or decimal values use the original argument instead.
            var v = args[0].AsDouble();
            switch (lower)
            {
                case "ceil":
                    return args[0].Type switch
                    {
                        NumberValue.DataType.Integer => args[0],
                        NumberValue.DataType.Decimal => new NumberValue(Math.Ceiling(args[0].DecVal)),
                        _ => new NumberValue(Math.Ceiling(v)),
                    };

                case "floor":
                    return args[0].Type switch
                    {
                        NumberValue.DataType.Integer => args[0],
                        NumberValue.DataType.Decimal => new NumberValue(Math.Floor(args[0].DecVal)),
                        _ => new NumberValue(Math.Floor(v)),
                    };

                case "trunc":
                    return args[0].Type switch
                    {
                        NumberValue.DataType.Integer => args[0],
                        NumberValue.DataType.Decimal => new NumberValue(decimal.Truncate(args[0].DecVal)),
                        _ => new NumberValue(Math.Truncate(v)),
                    };

                case "frac":
                    return args[0].Type switch
                    {
                        NumberValue.DataType.Integer => new NumberValue(0m),
                        NumberValue.DataType.Decimal => new NumberValue(args[0].DecVal - decimal.Truncate(args[0].DecVal)),
                        _ => new NumberValue(v - Math.Truncate(v)),
                    };

                case "cbrt":
                    return new NumberValue(Math.Cbrt(v));

                case "exp":
                    return new NumberValue(Math.Exp(v));

                case "abs":
                    if (args[0].Type is NumberValue.DataType.Integer)
                    {
                        return new NumberValue(BigInteger.Abs(args[0].IntVal));
                    }

                    if (args[0].Type is NumberValue.DataType.Decimal)
                    {
                        return new NumberValue(Math.Abs(args[0].DecVal));
                    }

                    return new NumberValue(Math.Abs(v));

                case "log":
                    return new NumberValue(Math.Log10(v));

                case "log2":
                    return new NumberValue(Math.Log2(v));

                case "log10":
                    return new NumberValue(Math.Log10(v));

                case "ln":
                    return new NumberValue(Math.Log(v));

                case "sqrt":
                    return new NumberValue(Math.Sqrt(v));

                case "factorial":
                    // Domain errors yield NaN; valid but prohibitively expensive inputs
                    // fail with a calculation limit before the multiplication loop.
                    if (!args[0].TryGetInteger(out var factorialArgument) || factorialArgument < 0)
                    {
                        return new NumberValue(double.NaN);
                    }

                    if (factorialArgument > CalculationLimits.MaxFactorialArgument)
                    {
                        throw new CalculationLimitException();
                    }

                    BigInteger f = 1;
                    for (var i = 2; i <= (int)factorialArgument; i++)
                    {
                        f *= i;
                    }

                    return new NumberValue(f);

                case "sign":
                case "sgn":
                    if (args[0].Type is NumberValue.DataType.Integer)
                    {
                        return new NumberValue((BigInteger)args[0].IntVal.Sign);
                    }

                    if (args[0].Type is NumberValue.DataType.Decimal)
                    {
                        return new NumberValue((decimal)Math.Sign(args[0].DecVal));
                    }

                    if (double.IsNaN(v))
                    {
                        // The calculator defines sign(NaN) as zero (e.g. sign(sqrt(-1))),
                        // whereas Math.Sign would throw for NaN.
                        return new NumberValue(0m);
                    }

                    return new NumberValue((double)Math.Sign(v));

                case "round":
                    // Calculator midpoint rounding is away from zero, not Math.Round's
                    // default ties-to-even behavior.
                    if (args[0].Type is NumberValue.DataType.Integer)
                    {
                        return args[0];
                    }

                    if (args[0].Type is NumberValue.DataType.Decimal)
                    {
                        return new NumberValue(Math.Round(args[0].DecVal, MidpointRounding.AwayFromZero));
                    }

                    return new NumberValue(Math.Round(v, MidpointRounding.AwayFromZero));

                case "randi":
                    if (!args[0].TryGetInteger(out var randomUpperBound) || randomUpperBound <= 0 || randomUpperBound > long.MaxValue)
                    {
                        return new NumberValue(double.NaN);
                    }

                    return new NumberValue((BigInteger)Random.Shared.NextInt64((long)randomUpperBound));

                case "sin":
                    return new NumberValue(EvaluateSin(v));

                case "cos":
                    return new NumberValue(EvaluateCos(v));

                case "tan":
                    return new NumberValue(EvaluateTan(v));

                case "arcsin":
                    return new NumberValue(EvaluateArcSin(v));

                case "arccos":
                    return new NumberValue(EvaluateArcCos(v));

                case "arctan":
                    return new NumberValue(EvaluateArcTan(v));

                case "cot":
                    return new NumberValue(EvaluateCot(v));

                case "sec":
                    return new NumberValue(EvaluateSec(v));

                case "csc":
                    return new NumberValue(EvaluateCsc(v));

                case "arccot":
                    return new NumberValue(EvaluateArcCot(v));

                case "arcsec":
                    return new NumberValue(EvaluateArcSec(v));

                case "arccsc":
                    return new NumberValue(EvaluateArcCsc(v));

                case "sinh":
                    return new NumberValue(Math.Sinh(v));

                case "cosh":
                    return new NumberValue(Math.Cosh(v));

                case "tanh":
                    return new NumberValue(Math.Tanh(v));

                case "arsinh":
                    return new NumberValue(Math.Asinh(v));

                case "arcosh":
                    return new NumberValue(Math.Acosh(v));

                case "artanh":
                    return new NumberValue(Math.Atanh(v));

                case "coth":
                    return new NumberValue(1.0 / Math.Tanh(v));

                case "sech":
                    return new NumberValue(1.0 / Math.Cosh(v));

                case "csch":
                    return new NumberValue(1.0 / Math.Sinh(v));

                case "arcoth":
                    return new NumberValue(Math.Atanh(1.0 / v));

                case "arsech":
                    return new NumberValue(Math.Acosh(1.0 / v));

                case "arcsch":
                    return new NumberValue(Math.Asinh(1.0 / v));

                case "rad":
                case "deg2rad":
                    _currentTrigAngle = new AngleArgument(v, AngleUnit.Degrees);
                    return new NumberValue(v * (Math.PI / 180.0));

                case "deg":
                case "rad2deg":
                    return new NumberValue(v * (180.0 / Math.PI));

                case "grad":
                case "grad2rad":
                    _currentTrigAngle = new AngleArgument(v, AngleUnit.Gradians);
                    return new NumberValue(v * (Math.PI / 200.0));

                case "rad2grad":
                    return new NumberValue(v * (200.0 / Math.PI));

                case "deg2grad":
                    return new NumberValue(v * (200.0 / 180.0));

                case "grad2deg":
                    return new NumberValue(v * (180.0 / 200.0));

                default:
                    throw new FormatException($"Unknown function '{name}'.");
            }
        }

        if (args.Count == 2)
        {
            switch (lower)
            {
                case "pow":
                    return NumberValue.Pow(args[0], args[1]);

                case "logn":
                    return new NumberValue(Math.Log(args[0].AsDouble(), args[1].AsDouble()));

                case "atan2":
                    var y = args[0].AsDouble();
                    var x = args[1].AsDouble();
                    return new NumberValue(EvaluateArcTan2(y, x));

                case "hypot":
                    return new NumberValue(Math.Sqrt(args[0].AsDouble() * args[0].AsDouble() + args[1].AsDouble() * args[1].AsDouble()));

                case "root":
                    var baseVal = args[0].AsDouble();
                    var rootVal = args[1].AsDouble();
                    if (baseVal < 0 && Math.Abs(rootVal % 2) == 1)
                    {
                        // Math.Pow with a negative base and a fractional exponent yields
                        // NaN. Odd integer roots have a real branch: root(-8, 3) = -2.
                        return new NumberValue(-Math.Pow(-baseVal, 1.0 / rootVal));
                    }

                    return new NumberValue(Math.Pow(baseVal, 1.0 / rootVal));

                default:
                    throw new FormatException($"Unknown function '{name}'.");
            }
        }

        if (args.Count == 3)
        {
            switch (lower)
            {
                case "clamp":
                    return EvaluateClamp(args[0], args[1], args[2]);

                case "lerp":
                    // a + t * (b - a)
                    return args[0] + (args[2] * (args[1] - args[0]));

                default:
                    throw new FormatException($"Unknown function '{name}'.");
            }
        }

        throw new FormatException($"Too many arguments for function '{name}'.");
    }

    private static NumberValue EvaluateMinMax(List<NumberValue> args, bool findMinimum)
    {
        // Compare without converting every argument to double, and return the selected
        // operand unchanged so its numeric representation is preserved.
        var result = args[0];
        for (var i = 1; i < args.Count; i++)
        {
            var comparison = NumberValue.Compare(result, args[i]);
            if (findMinimum ? comparison > 0 : comparison < 0)
            {
                result = args[i];
            }
        }

        return result;
    }

    private static NumberValue EvaluateAvg(List<NumberValue> args)
    {
        var sum = args[0];
        for (var i = 1; i < args.Count; i++)
        {
            sum += args[i];
        }

        return sum / new NumberValue((BigInteger)args.Count);
    }

    private static NumberValue EvaluateClamp(NumberValue val, NumberValue min, NumberValue max)
    {
        if (NumberValue.Compare(min, max) > 0)
        {
            return new NumberValue(double.NaN);
        }

        if (NumberValue.Compare(val, min) < 0)
        {
            return min;
        }

        if (NumberValue.Compare(val, max) > 0)
        {
            return max;
        }

        return val;
    }

    private double EvaluateSin(double v) =>
        (_currentTrigAngle is { } angle ? angle.Unit : _options.AngleUnit) switch
        {
            AngleUnit.Degrees => double.SinPi((_currentTrigAngle is { } a ? a.Value : v) / 180.0),
            AngleUnit.Gradians => double.SinPi((_currentTrigAngle is { } a ? a.Value : v) / 200.0),
            _ => Math.Sin(v),
        };

    private double EvaluateCos(double v) =>
        (_currentTrigAngle is { } angle ? angle.Unit : _options.AngleUnit) switch
        {
            AngleUnit.Degrees => double.CosPi((_currentTrigAngle is { } a ? a.Value : v) / 180.0),
            AngleUnit.Gradians => double.CosPi((_currentTrigAngle is { } a ? a.Value : v) / 200.0),
            _ => Math.Cos(v),
        };

    private double EvaluateTan(double v)
    {
        var unit = _currentTrigAngle is { } angle ? angle.Unit : _options.AngleUnit;
        var value = _currentTrigAngle is { } a ? a.Value : v;

        if (unit == AngleUnit.Degrees && Math.Abs(value % 180.0) == 45.0)
        {
            return value > 0 ? 1.0 : -1.0;
        }

        if (unit == AngleUnit.Gradians && Math.Abs(value % 200.0) == 50.0)
        {
            return value > 0 ? 1.0 : -1.0;
        }

        return unit switch
        {
            AngleUnit.Degrees => double.TanPi(value / 180.0),
            AngleUnit.Gradians => double.TanPi(value / 200.0),
            _ => Math.Tan(v),
        };
    }

    private double EvaluateSec(double v) => 1.0 / EvaluateCos(v);

    private double EvaluateCsc(double v) => 1.0 / EvaluateSin(v);

    private double EvaluateCot(double v)
    {
        var unit = _currentTrigAngle is { } angle ? angle.Unit : _options.AngleUnit;
        var value = _currentTrigAngle is { } a ? a.Value : v;
        return unit switch
        {
            AngleUnit.Degrees => double.CosPi(value / 180.0) / double.SinPi(value / 180.0),
            AngleUnit.Gradians => double.CosPi(value / 200.0) / double.SinPi(value / 200.0),
            _ => 1.0 / Math.Tan(v),
        };
    }

    private double EvaluateArcSin(double v) =>
        _options.AngleUnit switch
        {
            AngleUnit.Degrees => double.AsinPi(v) * 180.0,
            AngleUnit.Gradians => double.AsinPi(v) * 200.0,
            _ => Math.Asin(v),
        };

    private double EvaluateArcCos(double v) =>
        _options.AngleUnit switch
        {
            AngleUnit.Degrees => double.AcosPi(v) * 180.0,
            AngleUnit.Gradians => double.AcosPi(v) * 200.0,
            _ => Math.Acos(v),
        };

    private double EvaluateArcTan(double v) =>
        _options.AngleUnit switch
        {
            AngleUnit.Degrees => double.AtanPi(v) * 180.0,
            AngleUnit.Gradians => double.AtanPi(v) * 200.0,
            _ => Math.Atan(v),
        };

    private double EvaluateArcTan2(double y, double x) =>
        _options.AngleUnit switch
        {
            AngleUnit.Degrees => double.Atan2Pi(y, x) * 180.0,
            AngleUnit.Gradians => double.Atan2Pi(y, x) * 200.0,
            _ => Math.Atan2(y, x),
        };

    private double EvaluateArcCot(double v) =>
        _options.AngleUnit switch
        {
            AngleUnit.Degrees => double.Atan2Pi(1.0, v) * 180.0,
            AngleUnit.Gradians => double.Atan2Pi(1.0, v) * 200.0,
            _ => Math.Atan2(1.0, v),
        };

    private double EvaluateArcSec(double v) =>
        _options.AngleUnit switch
        {
            AngleUnit.Degrees => double.AcosPi(1.0 / v) * 180.0,
            AngleUnit.Gradians => double.AcosPi(1.0 / v) * 200.0,
            _ => Math.Acos(1.0 / v),
        };

    private double EvaluateArcCsc(double v) =>
        _options.AngleUnit switch
        {
            AngleUnit.Degrees => double.AsinPi(1.0 / v) * 180.0,
            AngleUnit.Gradians => double.AsinPi(1.0 / v) * 200.0,
            _ => Math.Asin(1.0 / v),
        };

    private ExpressionException SyntaxError(string message) =>
        new(new EvaluationDiagnostic(EvaluationError.InvalidSyntax, message, _pos, _pos < _input.Length ? 1 : 0));

    private char Peek(int ahead = 0) => _pos + ahead < _input.Length ? _input[_pos + ahead] : '\0';

    private void Consume() => _pos++;

    private void SkipWhitespace()
    {
        while (_pos < _input.Length && char.IsWhiteSpace(_input[_pos]))
        {
            _pos++;
        }
    }

    /// <summary>
    /// Matches a whole-word keyword case-insensitively and advances the cursor past it if found.
    /// Ensures the match is delimited and not merely a prefix of a longer identifier.
    /// </summary>
    private bool MatchKeyword(string keyword)
    {
        var len = keyword.Length;
        if (_pos + len > _input.Length)
        {
            return false;
        }

        if (!_input.AsSpan(_pos, len).Equals(keyword, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // Must be delimited: next character cannot be letter, digit, or underscore.
        if (_pos + len < _input.Length)
        {
            var next = _input[_pos + len];
            if (char.IsLetterOrDigit(next) || next == '_')
            {
                return false;
            }
        }

        _pos += len;
        return true;
    }
}
