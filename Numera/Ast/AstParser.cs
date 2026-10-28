using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace Numera.Ast;

/// <summary>
/// Parses mathematical expressions into an abstract syntax tree (<see
/// cref="ExpressionNode"/>) using recursive descent grammar matching Numera's canonical 
/// precedence.
/// </summary>
internal sealed class AstParser
{
    private readonly string _input;
    private readonly CalculatorOptions _options;
    private readonly EvaluationContext _context;
    private readonly SourceMap? _sourceMap;
    private int _pos;
    private int _depth;

    // Outer extents (parentheses included) of nodes that were written inside a group.
    // Keyed by reference: nodes are records, so default equality would be structural.
    private Dictionary<ExpressionNode, TextSpan>? _groupExtents;

    private TextSpan Extent(ExpressionNode node) =>
        _groupExtents is not null && _groupExtents.TryGetValue(node, out var extent) ? extent : node.Span;

    private TextSpan Combine(ExpressionNode left, ExpressionNode right) =>
        TextSpan.Combine(Extent(left), Extent(right));

    // Positions are tracked in the normalized text, but spans and diagnostics must point
    // at what the caller typed. Mapping happens once, where a span or position is first
    // built from _pos; spans that come from Combine/Extent/_groupExtents are already in
    // original coordinates and are not remapped.
    private TextSpan Span(int start, int length) => _sourceMap is null ?
        new TextSpan(start, length) :
        _sourceMap.MapSpan(new TextSpan(start, length));

    private EvaluationDiagnostic Diagnostic(
        EvaluationError code, string message, int start, int length)
    {
        var span = Span(start, length);
        return new EvaluationDiagnostic(code, message, span.Start, span.Length);
    }

    // The depth count alone is not a stack bound: frames per level vary and the host may
    // already be deep in its own stack, so also check the real remaining stack.
    private bool NestingLimitReached() =>
        _depth >= _options.MaxNestingDepth ||
        !RuntimeHelpers.TryEnsureSufficientExecutionStack();

    public AstParser(
        string input,
        CalculatorOptions options,
        EvaluationContext? context = null,
        SourceMap? sourceMap = null)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(options);
        _context = context ?? EvaluationContext.Empty;
        _sourceMap = sourceMap;
        if (input.Length > options.MaxInputLength)
        {
            throw new CalculationLimitException();
        }

        _input = input;
        _options = options;
    }

    public ExpressionNode Parse()
    {
        var result = ParseStatement();
        SkipWhitespace();

        // Report unparsed trailing text before validating: until the whole input is
        // consumed the tree is only a prefix that is never returned, and a validation
        // error from it would mask the real syntax error with a misleading message and
        // position.
        if (_pos < _input.Length)
        {
            char trailing = _input[_pos];
            if (trailing is '[' or ']' or ';')
            {
                throw SyntaxError(
                    $"'{trailing}' is reserved for future matrix and array syntax.");
            }

            throw SyntaxError($"Unexpected character '{trailing}'.");
        }

        AstValidator.Validate(result, _options);

        return result;
    }

    private ExpressionNode ParseStatement()
    {
        SkipWhitespace();
        int savePos = _pos;

        // Check if input begins with: <Identifier> '=' (and not "==").
        if (IsIdentifierStart(Peek()))
        {
            var identifierSpan = ParseIdentifier();
            string identifier = identifierSpan.ToString();
            SkipWhitespace();

            if (Peek() == '=' && Peek(1) != '=')
            {
                ValidateAssignableIdentifier(identifier, savePos);
                Consume(); // '='

                int valueStart = _pos;

                // Right-associative recursion: "a = b = 10" parses as "a = (b = 10)".
                // Guarded like the other recursive paths so long assignment chains cannot
                // recurse deeper during parsing than the validator would later allow.
                if (NestingLimitReached())
                {
                    throw new CalculationLimitException();
                }

                _depth++;
                var value = ParseStatement();
                _depth--;

                // If the right-hand side was another assignment, reuse its already-
                // extracted definition without allocating again.
                string? definition = value is AssignmentNode inner ?
                    inner.Definition :
                    _input[valueStart.._pos].Trim();

                return new AssignmentNode(
                    identifier, value, definition, Span(savePos, _pos - savePos));
            }

            // Not an assignment; reset position to parse as a logical expression.
            _pos = savePos;
        }

        return ParseLogical();
    }

    private void ValidateAssignableIdentifier(string name, int pos)
    {
        if (KeywordCatalog.IsReserved(name))
        {
            throw new ExpressionException(Diagnostic(
                EvaluationError.InvalidSyntax,
                $"Cannot assign to reserved keyword '{name}'.",
                pos,
                name.Length));
        }

        if (ConstantCatalog.TryResolve(name, out _, out _))
        {
            throw new ExpressionException(Diagnostic(
                EvaluationError.InvalidSyntax,
                $"Cannot assign to reserved constant '{name}'.",
                pos,
                name.Length));
        }

        if (FunctionCatalog.TryGetInfo(name, out _))
        {
            throw new ExpressionException(Diagnostic(
                EvaluationError.InvalidSyntax,
                $"Cannot assign to function name '{name}'.",
                pos,
                name.Length));
        }
    }

    // Operator precedence (lowest to highest):
    // 1.  Logical OR (||, or)
    // 2.  Logical XOR (xor)
    // 3.  Logical AND (&&, and)
    // 4.  Logical NOT (!, not)
    //     Note on 'not' and '!': in Numera (matching Python convention), logical negation has lower
    //     precedence than comparison chains, so "not x == y" parses as "not (x == y)".
    // 5.  Comparison (==, !=, ~=, <, <=, >, >=)
    // 6.  Bitwise Shift (<<, >>)
    // 7.  Additive (+, -)
    // 8.  Multiplicative (*, /, %)
    // 9.  Implicit Multiplication (juxtaposition, e.g. 2x, 2(3+4); binds tighter than explicit *, /)
    // 10. Unary (+, -, ~; binds tighter than juxtaposition, so -2x is (-2)*x)
    // 11. Exponentiation (^)
    // 12. Postfix Factorial (!; binds tighter than power, so 2^3! is 2^(3!) and -3! is -(3!))
    // 13. Primary (literals, identifiers, function calls, grouped expressions)

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
                left = new BinaryNode(BinaryOperator.LogicalOr, left, right, Combine(left, right));
            }
            else if (MatchKeyword("or"))
            {
                var right = ParseLogicalXor();
                left = new BinaryNode(BinaryOperator.LogicalOr, left, right, Combine(left, right));
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
                left = new BinaryNode(BinaryOperator.LogicalXor, left, right, Combine(left, right));
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
        var left = ParseLogicalNot();
        while (true)
        {
            SkipWhitespace();
            if (Peek() == '&' && Peek(1) == '&')
            {
                Consume();
                Consume();
                var right = ParseLogicalNot();
                left = new BinaryNode(BinaryOperator.LogicalAnd, left, right, Combine(left, right));
            }
            else if (MatchKeyword("and"))
            {
                var right = ParseLogicalNot();
                left = new BinaryNode(BinaryOperator.LogicalAnd, left, right, Combine(left, right));
            }
            else
            {
                break;
            }
        }

        return left;
    }

    private ExpressionNode ParseLogicalNot()
    {
        SkipWhitespace();
        int opStart = _pos;
        int opLen = 0;
        if (Peek() == '!')
        {
            Consume();
            opLen = 1;
        }
        else if (MatchKeyword("not"))
        {
            opLen = 3;
        }

        if (opLen > 0)
        {
            if (NestingLimitReached())
            {
                throw new CalculationLimitException();
            }

            _depth++;
            var operand = ParseLogicalNot();
            _depth--;

            if (_options.RequireExplicitLogicalNegation && operand is ComparisonChainNode && !IsFullyParenthesized(operand))
            {
                throw new ExpressionException(Diagnostic(
                    EvaluationError.AmbiguousLogicalNegation,
                    "Explicit parentheses are required when logical negation is applied to a comparison expression.",
                    opStart,
                    opLen));
            }

            return new UnaryNode(UnaryOperator.LogicalNot, operand,
                TextSpan.Combine(Span(opStart, opLen), Extent(operand)));
        }

        return ParseComparison();
    }

    private bool IsFullyParenthesized(ExpressionNode operand) =>
        _groupExtents?.ContainsKey(operand) == true;

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

        return new ComparisonChainNode(operands, operators!, Combine(operands[0], operands[^1]));
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
        var left = ParseAdditive();
        while (true)
        {
            SkipWhitespace();
            if (Peek() == '<' && Peek(1) == '<')
            {
                Consume();
                Consume();
                var right = ParseAdditive();
                left = new BinaryNode(BinaryOperator.ShiftLeft, left, right, Combine(left, right));
            }
            else if (Peek() == '>' && Peek(1) == '>')
            {
                Consume();
                Consume();
                var right = ParseAdditive();
                left = new BinaryNode(BinaryOperator.ShiftRight, left, right, Combine(left, right));
            }
            else
            {
                break;
            }
        }

        return left;
    }

    /// <summary>
    /// Parses additive expressions (+ and -). This represents the additive precedence level,
    /// situated between shift expressions and multiplicative terms.
    /// </summary>
    private ExpressionNode ParseAdditive()
    {
        var left = ParseTerm();
        while (true)
        {
            SkipWhitespace();
            if (Peek() == '+')
            {
                Consume();
                var right = ParseTerm();
                left = new BinaryNode(BinaryOperator.Add, left, right, Combine(left, right));
            }
            else if (Peek() == '-')
            {
                Consume();
                var right = ParseTerm();
                left = new BinaryNode(BinaryOperator.Subtract, left, right, Combine(left, right));
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
        var left = ParseImplicitProduct();
        while (true)
        {
            SkipWhitespace();
            if (Peek() == '*')
            {
                Consume();
                var right = ParseImplicitProduct();
                left = new BinaryNode(BinaryOperator.Multiply, left, right, Combine(left, right));
            }
            else if (Peek() == '/')
            {
                Consume();
                var right = ParseImplicitProduct();
                left = new BinaryNode(BinaryOperator.Divide, left, right, Combine(left, right));
            }
            else if (Peek() == '%' && PercentIsModulo())
            {
                Consume();
                var right = ParseImplicitProduct();
                left = new BinaryNode(BinaryOperator.Modulo, left, right, Combine(left, right));
            }
            else
            {
                break;
            }
        }

        return left;
    }

    private ExpressionNode ParseImplicitProduct()
    {
        var left = ParseUnary();
        while (StartsImplicitOperand())
        {
            var right = ParseUnary();
            left = new BinaryNode(BinaryOperator.Multiply, left, right, Combine(left, right), IsImplicit: true);
        }

        SkipWhitespace();
        if (IsKeywordAt("of"))
        {
            if (!PercentRules.IsPercentShaped(left))
            {
                throw SyntaxError("Only a percentage can appear before 'of', e.g. 10% of 200.");
            }

            Consume();
            Consume();
            if (NestingLimitReached())
            {
                throw new CalculationLimitException();
            }

            // Right-associative so "10% of 20% of 1000" reads as 10% of (20% of 1000).
            _depth++;
            var ofRight = ParseImplicitProduct();
            _depth--;
            return new BinaryNode(BinaryOperator.Of, left, ofRight, Combine(left, ofRight));
        }

        return left;
    }

    /// <summary>
    /// Decides whether the '%' at the current position is modulo. It is only modulo when an operand follows,
    /// so "10% + 3" and "10% of 5" are percentages but "10 % 3" and "10 %(3)" are modulo.
    /// </summary>
    private bool PercentIsModulo()
    {
        int save = _pos;
        Consume(); // '%'
        SkipWhitespace();
        char c = Peek();

        bool modulo = char.IsDigit(c) || c == '.' || c == '(';
        if (!modulo)
        {
            int len = GetIdentifierLengthAt(_pos);

            // An identifier follows: it is modulo if it is not a keyword that would start
            // an implicit operand, e.g. "10% of 5" is a percentage but "10% x" is modulo.
            modulo = len > 0 &&
                !KeywordCatalog.IsOperatorKeyword(_input.AsSpan(_pos, len));
        }

        _pos = save;
        return modulo;
    }

    private int GetIdentifierLengthAt(int pos)
    {
        if (pos >= _input.Length || !IsIdentifierStart(_input[pos]))
        {
            return 0;
        }

        int length = 1;
        while (pos + length < _input.Length && IsIdentifierPart(_input[pos + length]))
        {
            length++;
        }

        return length;
    }

    private bool StartsImplicitOperand()
    {
        if (!_options.AllowImplicitMultiplication) return false;
        SkipWhitespace();
        char c = Peek();
        if (c == '(') return true;

        // Deliberately not digits or '.': "1 000" would otherwise silently become 1 * 0.
        // Reject boolean literals and keywords from starting an implicit product, e.g.
        // allow "2x" but not "2true" or "2and(3,4)".
        int len = GetIdentifierLengthAt(_pos);
        if (len == 0) return false;

        return !KeywordCatalog.PreventsImplicitMultiplication(_input.AsSpan(_pos, len));
    }

    private ExpressionNode ParseUnary()
    {
        if (NestingLimitReached())
        {
            throw new CalculationLimitException();
        }

        _depth++;
        SkipWhitespace();
        int opStart = _pos;
        ExpressionNode result;
        if (Peek() == '-')
        {
            Consume();
            var operand = ParseUnary();
            result = new UnaryNode(UnaryOperator.Negate, operand,
                TextSpan.Combine(Span(opStart, 1), Extent(operand)));
        }
        else if (Peek() == '+')
        {
            Consume();
            var operand = ParseUnary();
            result = new UnaryNode(UnaryOperator.Positive, operand,
                TextSpan.Combine(Span(opStart, 1), Extent(operand)));
        }
        else if (Peek() == '~')
        {
            Consume();
            var operand = ParseUnary();
            result = new UnaryNode(UnaryOperator.BitwiseNot, operand,
                TextSpan.Combine(Span(opStart, 1), Extent(operand)));
        }
        else
        {
            result = ParsePower();
        }

        _depth--;
        return result;
    }

    private ExpressionNode ParsePower()
    {
        var left = ParsePostfix();
        SkipWhitespace();
        if (Peek() == '^')
        {
            Consume();
            var right = ParseUnary();
            return new BinaryNode(BinaryOperator.Power, left, right, Combine(left, right));
        }

        return left;
    }

    private ExpressionNode ParsePostfix()
    {
        var operand = ParsePrimary();
        SkipWhitespace();
        if (Peek() == '%' && !PercentIsModulo())
        {
            int pctStart = _pos;
            Consume();
            return new UnaryNode(UnaryOperator.Percent, operand,
                TextSpan.Combine(Extent(operand), Span(pctStart, 1)));
        }

        if (!_options.AllowPostfixFactorial)
        {
            return operand;
        }

        // '!=' is comparison, but '!==' is factorial followed by '=='.
        if (Peek() != '!' || (Peek(1) == '=' && Peek(2) != '='))
        {
            return operand;
        }

        int opStart = _pos;
        Consume();

        if (Peek() == '!' && Peek(1) != '=')
        {
            throw SyntaxError("Repeated '!' is ambiguous (double factorial); write (n!)! instead.");
        }

        return new UnaryNode(UnaryOperator.Factorial, operand,
            TextSpan.Combine(Extent(operand), Span(opStart, 1)));
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
            int groupStart = _pos;
            Consume();
            var value = ParseLogical();
            SkipWhitespace();

            if (Peek() != ')')
            {
                throw SyntaxError("Expected ')'.");
            }

            Consume();
            _groupExtents ??= new Dictionary<ExpressionNode, TextSpan>(ReferenceEqualityComparer.Instance);
            _groupExtents[value] = Span(groupStart, _pos - groupStart);
            return value;
        }

        if (c == '!' && Peek(1) != '=')
        {
            throw SyntaxError("Logical negation is only allowed at the start of a logical operand; wrap the negated expression in parentheses.");
        }

        if (IsIdentifierStart(c))
        {
            int kwPos = _pos;
            if (MatchKeyword("not"))
            {
                _pos = kwPos;
                throw new ExpressionException(Diagnostic(
                    EvaluationError.InvalidSyntax,
                    "Logical negation is only allowed at the start of a logical operand; wrap the negated expression in parentheses.",
                    kwPos,
                    3));
            }

            return ParseIdentifierOrCall();
        }

        if (char.IsAsciiDigit(c) || c == '.')
        {
            int numStart = _pos;
            var num = ParseNumber();
            return new NumberNode(num, Span(numStart, _pos - numStart));
        }

        throw SyntaxError("Expected a number, constant, or '('.");
    }

    private ExpressionNode ParseIdentifierOrCall()
    {
        int identifierStart = _pos;
        var identifierSpan = ParseIdentifier();
        string identifier = identifierSpan.ToString();
        SkipWhitespace();

        if (Peek() == '(' && FunctionArity.TryGet(identifier, out var signature))
        {
            var args = ParseArgumentList();

            if (args.Count < signature.MinimumArguments || (signature.MaximumArguments is int maximum && args.Count > maximum))
            {
                throw new ExpressionException(Diagnostic(
                    EvaluationError.InvalidArgumentCount, $"Invalid argument count for '{identifier}'.", identifierStart, identifier.Length));
            }

            return new FunctionCallNode(identifier, args, Span(identifierStart, _pos - identifierStart));
        }

        if (Peek() == '(' && !_options.AllowImplicitMultiplication)
        {
            throw new ExpressionException(Diagnostic(
                EvaluationError.UnknownFunction, $"Unknown function '{identifier}'.", identifierStart, identifier.Length));
        }

        if (Peek() is '[' or ']' or ';')
        {
            char reservedChar = Peek();
            throw SyntaxError($"'{reservedChar}' is reserved for future matrix and array syntax.");
        }

        if (ConstantCatalog.TryResolve(identifier, out var constantKind, out _))
        {
            return new IdentifierNode(identifier, constantKind, Span(identifierStart, identifier.Length));
        }

        if (Peek() != '(' && FunctionArity.TryGet(identifier, out _))
        {
            throw new ExpressionException(Diagnostic(
                EvaluationError.InvalidSyntax, $"Function '{identifier}' requires parentheses.", identifierStart, identifier.Length));
        }

        if (_context.TryResolveVariable(identifier, out _))
        {
            return new IdentifierNode(identifier, null, Span(identifierStart, identifier.Length));
        }

        if (Peek() == '(')
        {
            throw new ExpressionException(Diagnostic(
                EvaluationError.UnknownFunction, $"Unknown function '{identifier}'.", identifierStart, identifier.Length));
        }

        throw new ExpressionException(Diagnostic(
            EvaluationError.UnknownConstant, $"Unknown constant or variable '{identifier}'.", identifierStart, identifier.Length));
    }

    private List<ExpressionNode> ParseArgumentList()
    {
        Consume(); // '('
        SkipWhitespace();
        var args = new List<ExpressionNode>();
        if (Peek() != ')')
        {
            while (true)
            {
                if (NestingLimitReached())
                {
                    throw new CalculationLimitException();
                }

                _depth++;
                args.Add(ParseLogical());
                _depth--;
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

        return args;
    }

    private delegate BigInteger RadixDigitsParser(ReadOnlySpan<char> digits);

    /// <summary>
    /// Everything that differs between the radix-prefixed literal forms (0x, 0b, 0o). Keeping it in one
    /// place means the prefix check and the per-radix behaviour cannot drift apart.
    /// </summary>
    private readonly record struct RadixLiteral(
        char Prefix,
        string Name,
        int BitsPerDigit,
        Func<char, bool> IsDigit,
        RadixDigitsParser Parse);

    private static bool TryGetRadix(char prefixChar, out RadixLiteral radix)
    {
        switch (char.ToLowerInvariant(prefixChar))
        {
            case 'x':
                radix = new RadixLiteral('x', "hexadecimal", 4, static c => char.IsAsciiHexDigit(c), ParseHexBigInteger);
                return true;
            case 'b':
                radix = new RadixLiteral('b', "binary", 1, static c => c is '0' or '1', ParseBinaryBigInteger);
                return true;
            case 'o':
                radix = new RadixLiteral('o', "octal", 3, static c => c is >= '0' and <= '7', ParseOctalBigInteger);
                return true;
            default:
                radix = default;
                return false;
        }
    }

    private MathValue ParseNumber()
    {
        int start = _pos;

        // Check for radix-prefixed integer literals: 0x, 0b, 0o.
        if (Peek() == '0' && TryGetRadix(Peek(1), out var radix))
        {
            Consume(); // '0'
            Consume(); // radix prefix letter

            int digitsStart = _pos;
            while (_pos < _input.Length && radix.IsDigit(Peek()))
            {
                Consume();
            }

            if (_pos == digitsStart)
            {
                throw SyntaxError(
                    $"Expected {radix.Name} digits after '0{radix.Prefix}'.");
            }

            // Guard against bit lengths that exceed the maximum integer ceiling before
            // allocating BigInteger.
            int digitCount = _pos - digitsStart;
            if ((long)digitCount * radix.BitsPerDigit > CalculationLimits.MaxIntegerBits)
            {
                throw new CalculationLimitException();
            }

            return new MathValue(radix.Parse(_input.AsSpan(digitsStart, digitCount)));
        }

        bool hasDecimal = false;
        bool hasExponent = false;

        while (_pos < _input.Length)
        {
            char ch = Peek();
            if (char.IsAsciiDigit(ch))
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

                if (!char.IsAsciiDigit(Peek(nextPos)))
                {
                    break;
                }

                hasExponent = true;
                Consume();
                if (Peek() is '+' or '-')
                {
                    Consume();
                }

                while (_pos < _input.Length && char.IsAsciiDigit(Peek()))
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

        if (!hasDecimal && !hasExponent)
        {
            // L decimal digits require at least (L - 1) * log2(10) bits (~3.3219).
            // Reject excessively large plain integer literals before attempting BigInteger parsing.
            if ((numberSpan.Length - 1) * 3.3219 > CalculationLimits.MaxIntegerBits)
            {
                throw new CalculationLimitException();
            }

            if (BigInteger.TryParse(numberSpan, NumberStyles.Integer, CultureInfo.InvariantCulture, out var intValue))
            {
                if (intValue.GetBitLength() > CalculationLimits.MaxIntegerBits)
                {
                    throw new CalculationLimitException();
                }

                return new MathValue(intValue);
            }
        }
        else if (decimal.TryParse(
            numberSpan, NumberStyles.Float, CultureInfo.InvariantCulture, out var decValue))
        {
            if (decValue != 0m || IsZeroMantissa(numberSpan))
            {
                return new MathValue(decValue);
            }
        }

        if (double.TryParse(
            numberSpan, NumberStyles.Float, CultureInfo.InvariantCulture, out double doubleValue))
        {
            if (!double.IsFinite(doubleValue))
            {
                // If a large floating-point literal represents an exact integer (e.g.
                // '1e309' or '2.5e400') and fits within the configured BigInteger bit
                // ceiling, promote it to an exact BigInteger.
                if (TryPromoteScientificToBigInteger(numberSpan, out var bigIntValue))
                {
                    return new MathValue(bigIntValue);
                }

                throw new ExpressionException(
                    Diagnostic(EvaluationError.NumericOverflow, "Literal overflowed floating-point range.", start, Math.Max(1, _pos - start)));
            }

            if (doubleValue == 0.0 && !IsZeroMantissa(numberSpan))
            {
                throw new ExpressionException(
                    Diagnostic(EvaluationError.NumericOverflow, "Literal underflowed floating-point range.", start, Math.Max(1, _pos - start)));
            }

            return new MathValue(doubleValue);
        }

        // Check if a literal that could not parse as double can still be represented as
        // an exact BigInteger.
        if (TryPromoteScientificToBigInteger(numberSpan, out var bigIntFallback))
        {
            return new MathValue(bigIntFallback);
        }

        throw new ExpressionException(
            Diagnostic(EvaluationError.InvalidSyntax, "Expected a number.", start, Math.Max(1, _pos - start)));
    }

    private static bool TryPromoteScientificToBigInteger(ReadOnlySpan<char> span,
        out BigInteger result)
    {
        result = BigInteger.Zero;
        int expIndex = span.IndexOfAny('e', 'E');
        if (expIndex < 0)
        {
            return false;
        }

        var mantissaSpan = span[..expIndex];
        var expSpan = span[(expIndex + 1)..];

        if (expSpan.StartsWith("+"))
        {
            expSpan = expSpan[1..];
        }

        if (expSpan.StartsWith("-"))
        {
            // Negative exponent cannot be promoted to a large integer.
            return false;
        }

        if (!int.TryParse(expSpan, NumberStyles.None, CultureInfo.InvariantCulture, out int exponent))
        {
            return false;
        }

        int dotIndex = mantissaSpan.IndexOf('.');
        int fractionDigits = 0;
        BigInteger mantissaInt;

        if (dotIndex >= 0)
        {
            var integerPart = mantissaSpan[..dotIndex];
            var fractionalPart = mantissaSpan[(dotIndex + 1)..];
            fractionDigits = fractionalPart.Length;

            if (exponent < fractionDigits)
            {
                // Has remaining fractional digits, so it cannot be an exact integer.
                return false;
            }

            // Reject mantissas whose digit counts alone exceed the integer bit ceiling
            // to guard against pathological allocations or huge BigInteger parsing.
            if ((long)(integerPart.Length + fractionalPart.Length) * 4 > CalculationLimits.MaxIntegerBits)
            {
                return false;
            }

            // Avoid heap allocation by using stackalloc for the mantissa buffer.
            Span<char> buffer = stackalloc char[integerPart.Length + fractionalPart.Length];
            integerPart.CopyTo(buffer);
            fractionalPart.CopyTo(buffer[integerPart.Length..]);
            if (!BigInteger.TryParse(buffer, NumberStyles.Integer, CultureInfo.InvariantCulture, out mantissaInt))
            {
                return false;
            }
        }
        else
        {
            if ((long)mantissaSpan.Length * 4 > CalculationLimits.MaxIntegerBits)
            {
                return false;
            }

            if (!BigInteger.TryParse(mantissaSpan, NumberStyles.Integer, CultureInfo.InvariantCulture, out mantissaInt))
            {
                return false;
            }
        }

        long effectiveExponent = (long)exponent - fractionDigits;
        // Upper bound check: 10^k takes about k * log2(10) bits. This is only a cheap guard
        // against pathological Pow calls, so it must not be stricter than the authoritative
        // bit-length check below. Use 64-bit maths to avoid overflow with large exponents.
        if (effectiveExponent > 0)
        {
            if (effectiveExponent > CalculationLimits.MaxIntegerBits ||
                effectiveExponent * 3.3219280948873623 > CalculationLimits.MaxIntegerBits)
            {
                return false;
            }
        }

        try
        {
            result = mantissaInt * BigInteger.Pow(10, (int)effectiveExponent);
            if (BigInteger.Abs(result).GetBitLength() > CalculationLimits.MaxIntegerBits)
            {
                return false;
            }

            return true;
        }
        catch (ArithmeticException)
        {
            return false;
        }
    }

    private static bool IsZeroMantissa(ReadOnlySpan<char> span)
    {
        int expIndex = span.IndexOfAny('e', 'E');
        var mantissa = expIndex >= 0 ? span[..expIndex] : span;
        return mantissa.IndexOfAnyInRange('1', '9') < 0;
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

    private static bool IsIdentifierStart(char c) =>
        char.IsLetter(c) || c == '_';

    private static bool IsIdentifierPart(char c) =>
        (char.IsLetter(c) || char.IsAsciiDigit(c) || c == '_') && !ConstantCatalog.IsConstantLetter(c);

    private ReadOnlySpan<char> ParseIdentifier()
    {
        var start = _pos;
        if (ConstantCatalog.IsConstantLetter(Peek()))
        {
            Consume();
            return _input.AsSpan(start, 1);
        }

        while (_pos < _input.Length)
        {
            char ch = Peek();
            if (!IsIdentifierPart(ch))
            {
                break;
            }

            Consume();
        }

        return _input.AsSpan(start, _pos - start);
    }

    private ExpressionException SyntaxError(string message) =>
        new(Diagnostic(EvaluationError.InvalidSyntax, message, _pos, _pos < _input.Length ? 1 : 0));

    private char Peek(int ahead = 0) => _pos + ahead < _input.Length ? _input[_pos + ahead] : '\0';

    private void Consume() => _pos++;

    private void SkipWhitespace()
    {
        while (_pos < _input.Length && char.IsWhiteSpace(_input[_pos]))
        {
            _pos++;
        }
    }

    private bool IsKeywordAt(string keyword) =>
        _pos + keyword.Length <= _input.Length &&
        _input.AsSpan(_pos, keyword.Length).Equals(keyword, Identifier.Comparison) &&
        (_pos + keyword.Length == _input.Length || !IsIdentifierPart(_input[_pos + keyword.Length]));

    private bool MatchKeyword(string keyword)
    {
        if (!IsKeywordAt(keyword)) return false;
        _pos += keyword.Length;
        return true;
    }
}
