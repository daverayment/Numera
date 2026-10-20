using System.Globalization;
using System.Numerics;

namespace Numera.Ast;

/// <summary>
/// Parses mathematical expressions into an abstract syntax tree (<see
/// cref="ExpressionNode"/>) using recursive descent grammar matching Numera's canonical 
/// precedence.
/// </summary>
internal sealed class AstParser
{
    private readonly string _input;
    private readonly EvaluationOptions _options;
    private int _pos;
    private int _depth;

    public AstParser(string input, EvaluationOptions options)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (input.Length > options.MaxInputLength)
        {
            throw new CalculationLimitException();
        }

        _input = input;
        _options = options;
    }

    public ExpressionNode Parse()
    {
        var result = ParseLogical();
        SkipWhitespace();

        if (_pos < _input.Length)
        {
            char trailing = _input[_pos];
            if (trailing is '[' or ']' or ';')
            {
                throw SyntaxError($"'{trailing}' is reserved for future matrix and array syntax.");
            }

            throw SyntaxError($"Unexpected character '{trailing}'.");
        }

        return result;
    }

    private ExpressionNode ParseLogical()
    {
        var left = ParseLogicalXor();
        while (true)
        {
            SkipWhitespace();
            if (Peek() == '|' && Peek(1) == '|')
            {
                Consume();
                Consume();
                var right = ParseLogicalXor();
                left = new BinaryNode(BinaryOperator.LogicalOr, left, right);
            }
            else if (MatchKeyword("or"))
            {
                var right = ParseLogicalXor();
                left = new BinaryNode(BinaryOperator.LogicalOr, left, right);
            }
            else
            {
                break;
            }
        }

        return left;
    }

    private ExpressionNode ParseLogicalXor()
    {
        var left = ParseLogicalAnd();
        while (true)
        {
            SkipWhitespace();
            if (MatchKeyword("xor"))
            {
                var right = ParseLogicalAnd();
                left = new BinaryNode(BinaryOperator.LogicalXor, left, right);
            }
            else
            {
                break;
            }
        }

        return left;
    }

    private ExpressionNode ParseLogicalAnd()
    {
        var left = ParseComparison();
        while (true)
        {
            SkipWhitespace();
            if (Peek() == '&' && Peek(1) == '&')
            {
                Consume();
                Consume();
                var right = ParseComparison();
                left = new BinaryNode(BinaryOperator.LogicalAnd, left, right);
            }
            else if (MatchKeyword("and"))
            {
                var right = ParseComparison();
                left = new BinaryNode(BinaryOperator.LogicalAnd, left, right);
            }
            else
            {
                break;
            }
        }

        return left;
    }

    private ExpressionNode ParseComparison()
    {
        var first = ParseShift();
        List<ExpressionNode>? operands = null;
        List<ComparisonOperator>? operators = null;

        while (true)
        {
            SkipWhitespace();
            var op = MatchComparisonOperator();
            if (op == null)
            {
                break;
            }

            operands ??= [first];
            operators ??= [];

            operators.Add(op.Value);
            var next = ParseShift();
            operands.Add(next);
        }

        if (operands is null)
        {
            return first;
        }

        return new ComparisonChainNode(operands, operators!);
    }

    private ComparisonOperator? MatchComparisonOperator()
    {
        ComparisonOperator? op = Peek() switch
        {
            '=' when Peek(1) == '=' => ComparisonOperator.Equal,
            '!' when Peek(1) == '=' => ComparisonOperator.NotEqual,
            '~' when Peek(1) == '=' => ComparisonOperator.NotEqual,
            '<' when Peek(1) == '=' => ComparisonOperator.LessThanOrEqual,
            '>' when Peek(1) == '=' => ComparisonOperator.GreaterThanOrEqual,
            '<' => ComparisonOperator.LessThan,
            '>' => ComparisonOperator.GreaterThan,
            _ => null,
        };

        if (op == null)
        {
            return null;
        }

        Consume();
        if (op is ComparisonOperator.Equal or ComparisonOperator.NotEqual or
            ComparisonOperator.LessThanOrEqual or ComparisonOperator.GreaterThanOrEqual)
        {
            Consume();
        }

        return op;
    }

    private ExpressionNode ParseShift()
    {
        var left = ParseExpression();
        while (true)
        {
            SkipWhitespace();
            if (Peek() == '<' && Peek(1) == '<')
            {
                Consume();
                Consume();
                var right = ParseExpression();
                left = new BinaryNode(BinaryOperator.ShiftLeft, left, right);
            }
            else if (Peek() == '>' && Peek(1) == '>')
            {
                Consume();
                Consume();
                var right = ParseExpression();
                left = new BinaryNode(BinaryOperator.ShiftRight, left, right);
            }
            else
            {
                break;
            }
        }

        return left;
    }

    private ExpressionNode ParseExpression()
    {
        var left = ParseTerm();
        while (true)
        {
            SkipWhitespace();
            if (Peek() == '+')
            {
                Consume();
                var right = ParseTerm();
                left = new BinaryNode(BinaryOperator.Add, left, right);
            }
            else if (Peek() == '-')
            {
                Consume();
                var right = ParseTerm();
                left = new BinaryNode(BinaryOperator.Subtract, left, right);
            }
            else
            {
                break;
            }
        }

        return left;
    }

    private ExpressionNode ParseTerm()
    {
        var left = ParseUnary();
        while (true)
        {
            SkipWhitespace();
            if (Peek() == '*')
            {
                Consume();
                var right = ParseUnary();
                left = new BinaryNode(BinaryOperator.Multiply, left, right);
            }
            else if (Peek() == '/')
            {
                Consume();
                var right = ParseUnary();
                left = new BinaryNode(BinaryOperator.Divide, left, right);
            }
            else if (Peek() == '%')
            {
                Consume();
                var right = ParseUnary();
                left = new BinaryNode(BinaryOperator.Modulo, left, right);
            }
            else
            {
                break;
            }
        }

        return left;
    }

    private ExpressionNode ParseUnary()
    {
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
                var operand = ParseUnary();
                return new UnaryNode(UnaryOperator.Negate, operand);
            }
            else if (Peek() == '+')
            {
                Consume();
                var operand = ParseUnary();
                return new UnaryNode(UnaryOperator.Positive, operand);
            }
            else if (Peek() == '!')
            {
                Consume();
                var operand = ParseUnary();
                return new UnaryNode(UnaryOperator.LogicalNot, operand);
            }
            else if (Peek() == '~')
            {
                Consume();
                var operand = ParseUnary();
                return new UnaryNode(UnaryOperator.BitwiseNot, operand);
            }
            else if (MatchKeyword("not"))
            {
                var operand = ParseUnary();
                return new UnaryNode(UnaryOperator.LogicalNot, operand);
            }

            return ParsePower();
        }
        finally
        {
            _depth--;
        }
    }

    private ExpressionNode ParsePower()
    {
        var left = ParsePrimary();
        SkipWhitespace();
        if (Peek() == '^')
        {
            Consume();
            var right = ParseUnary();
            return new BinaryNode(BinaryOperator.Power, left, right);
        }

        return left;
    }

    private ExpressionNode ParsePrimary()
    {
        SkipWhitespace();
        if (_pos >= _input.Length)
        {
            throw SyntaxError("Unexpected end of input.");
        }

        var c = Peek();

        if (c is '[' or ']' or ';')
        {
            throw SyntaxError($"'{c}' is reserved for future matrix and array syntax.");
        }

        if (c == '(')
        {
            Consume();
            var value = ParseLogical();
            SkipWhitespace();

            if (Peek() != ')')
            {
                throw SyntaxError("Expected ')'.");
            }

            Consume();
            return value;
        }

        if (char.IsLetter(c) || c == '_' || c == 'π' || c == 'τ')
        {
            int identifierStart = _pos;
            var identifierSpan = ParseIdentifier();
            string identifier = identifierSpan.ToString();
            SkipWhitespace();

            if (Peek() == '(')
            {
                Consume();
                SkipWhitespace();
                var args = new List<ExpressionNode>();
                if (Peek() != ')')
                {
                    while (true)
                    {
                        args.Add(ParseLogical());
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

                return new FunctionCallNode(identifier, args);
            }

            if (Peek() is '[' or ']' or ';')
            {
                char reservedChar = Peek();
                throw SyntaxError($"'{reservedChar}' is reserved for future matrix and array syntax.");
            }

            // Verify valid constant identifier
            if (!IsRecognizedConstant(identifier))
            {
                throw new ExpressionException(new EvaluationDiagnostic(
                    EvaluationError.UnknownConstant, $"Unknown constant '{identifier}'.", identifierStart, identifier.Length));
            }

            return new IdentifierNode(identifier);
        }

        return new NumberNode(ParseNumber());
    }

    private static bool IsRecognizedConstant(string name) =>
        name.Equals("pi", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("π", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("Π", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("tau", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("τ", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("Τ", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("e", StringComparison.OrdinalIgnoreCase);

    private MathValue ParseNumber()
    {
        int start = _pos;

        // Check for radix-prefixed integer literals: 0x, 0b, 0o
        if (Peek() == '0' && _pos + 1 < _input.Length)
        {
            char prefix = char.ToLowerInvariant(Peek(1));
            if (prefix is 'x' or 'b' or 'o')
            {
                Consume(); // '0'
                Consume(); // 'x', 'b', or 'o'

                int digitsStart = _pos;
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

                return new MathValue(value);
            }
        }

        bool hasDecimal = false;
        bool hasExponent = false;

        while (_pos < _input.Length)
        {
            char ch = Peek();
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

                int nextPos = 1;
                if (Peek(nextPos) is '+' or '-')
                {
                    nextPos++;
                }

                if (!char.IsDigit(Peek(nextPos)))
                {
                    break;
                }

                hasExponent = true;
                Consume();
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

        if (!hasDecimal && !hasExponent && BigInteger.TryParse(
            numberSpan, NumberStyles.Integer, CultureInfo.InvariantCulture, out var intValue))
        {
            return new MathValue(intValue);
        }
        else if (decimal.TryParse(
            numberSpan, NumberStyles.Float, CultureInfo.InvariantCulture, out var decValue))
        {
            if (decValue != 0m || numberSpan.IndexOfAnyInRange('1', '9') < 0)
            {
                return new MathValue(decValue);
            }
        }

        if (double.TryParse(
            numberSpan, NumberStyles.Float, CultureInfo.InvariantCulture, out double doubleValue))
        {
            return new MathValue(doubleValue);
        }

        throw new ExpressionException(
            new EvaluationDiagnostic(EvaluationError.InvalidSyntax, "Expected a number.", start, Math.Max(1, _pos - start)));
    }

    private static BigInteger ParseHexBigInteger(ReadOnlySpan<char> span)
    {
        var result = BigInteger.Zero;
        foreach (var ch in span)
        {
            int digit = ch switch
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
        foreach (char ch in span)
        {
            result = (result << 1) | (uint)(ch - '0');
        }

        return result;
    }

    private static BigInteger ParseOctalBigInteger(ReadOnlySpan<char> span)
    {
        var result = BigInteger.Zero;
        foreach (char ch in span)
        {
            result = (result << 3) | (uint)(ch - '0');
        }

        return result;
    }

    private ReadOnlySpan<char> ParseIdentifier()
    {
        var start = _pos;
        while (_pos < _input.Length && (char.IsLetterOrDigit(Peek()) || Peek() == '_' || Peek() == 'π' || Peek() == 'Π' || Peek() == 'τ' || Peek() == 'Τ'))
        {
            Consume();
        }

        return _input.AsSpan(start, _pos - start);
    }

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

    private bool MatchKeyword(string keyword)
    {
        int len = keyword.Length;
        if (_pos + len > _input.Length)
        {
            return false;
        }

        if (!_input.AsSpan(_pos, len).Equals(keyword, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (_pos + len < _input.Length)
        {
            char next = _input[_pos + len];
            if (char.IsLetterOrDigit(next) || next == '_')
            {
                return false;
            }
        }

        _pos += len;
        return true;
    }
}
