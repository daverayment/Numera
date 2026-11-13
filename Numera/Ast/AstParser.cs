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
    private readonly bool _allowUnboundVariables;
    private int _pos;
    private int _depth;
    private HashSet<string>? _declaredVariables;

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
        SourceMap? sourceMap = null,
        bool allowUnboundVariables = false)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(options);
        _context = context ?? EvaluationContext.Empty;
        _sourceMap = sourceMap;
        _allowUnboundVariables = allowUnboundVariables;
        if (input.Length > options.MaxInputLength)
        {
            throw new CalculationLimitException();
        }

        _input = input;
        _options = options;
    }

    private void ThrowSyntaxErrorBracketsOrSemicolon(char c) =>
        throw SyntaxError(
            c switch
            {
                '[' or ']' => "Square brackets '[]' are reserved for future vector and matrix syntax. Use round parentheses '()' for grouping.",
                ';' => "Semicolons ';' are reserved for future matrix row separation. Use commas ',' to separate function arguments.",
                _ => throw new ArgumentException($"Parser error. '{c}' should not be passed to this method.")
            });

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
                ThrowSyntaxErrorBracketsOrSemicolon(trailing);
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
                int assignmentPosition = _pos;
                Consume(); // '='

                int valueStart = _pos;
                SkipWhitespace();
                if (_pos >= _input.Length || Peek() is ')' or ',' or ';')
                {
                    throw new ExpressionException(Diagnostic(
                        EvaluationError.InvalidSyntax,
                        "An assignment requires a value after '='.",
                        assignmentPosition,
                        1));
                }

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

                (_declaredVariables ??= new HashSet<string>(Identifier.Comparer)).Add(identifier);
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
                $"Cannot assign to '{name}' because it is a reserved language keyword.",
                pos,
                name.Length));
        }

        if (ConstantCatalog.TryResolve(name, out var constantKind, out _))
        {
            string detail = constantKind switch
            {
                ConstantKind.E => "Euler's number (~ 2.71828)",
                ConstantKind.Pi => "Archimedes' constant π (~ 3.14159)",
                ConstantKind.Tau => "the circle constant τ (~ 6.28318)",
                ConstantKind.Phi => "the golden ratio φ (~ 1.61803)",
                ConstantKind.True or ConstantKind.False => "a boolean literal",
                _ => "a built-in mathematical constant"
            };

            throw new ExpressionException(Diagnostic(
                EvaluationError.InvalidSyntax,
                $"Cannot assign to '{name}' because it is reserved for {detail}. Choose a different variable name.",
                pos,
                name.Length));
        }

        if (FunctionCatalog.TryGetInfo(name, out var fnInfo))
        {
            string aliasNote = fnInfo.IsAlias ? $" (alias for '{fnInfo.CanonicalName}')" : "";
            throw new ExpressionException(Diagnostic(
                EvaluationError.InvalidSyntax,
                $"Cannot assign to name '{name}' because it is a built-in function{aliasNote}.",
                pos,
                name.Length));
        }

        if (_options.CustomFunctions.TryGetFunction(name, out _))
        {
            throw new ExpressionException(Diagnostic(
                EvaluationError.InvalidSyntax,
                $"Cannot assign to name '{name}' because it is registered as a custom function.",
                pos,
                name.Length));
        }
    }

    // Operator precedence (lowest to highest):
    // 1.  Logical OR (||, or)
    // 2.  Logical XOR (xor)
    // 3.  Logical AND (&&, and)
    // 4.  Logical NOT (!, not)
    //     Note on 'not' and '!': in Numera (matching Python convention), logical negation
    //     has lower precedence than comparison chains, so "not x == y" parses as
    //     "not (x == y)".
    // 5.  Comparison (==, !=, ~=, <, <=, >, >=)
    // 6.  Bitwise Shift (<<, >>)
    // 7.  Additive (+, -)
    // 8.  Multiplicative (*, /, %)
    // 9.  Implicit Multiplication (juxtaposition, e.g. 2x, 2(3+4); binds tighter than
    //     explicit *, /)
    // 10. Unary (+, -, ~; binds tighter than juxtaposition, so -2x is (-2)*x)
    // 11. Exponentiation (^)
    // 12. Postfix Factorial (!; binds tighter than power, so 2^3! is 2^(3!) and -3! is
    //     -(3!))
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
        while (StartsImplicitOperand(left))
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

    private bool StartsImplicitOperand(ExpressionNode left)
    {
        if (!_options.AllowImplicitMultiplication)
        {
            return false;
        }

        SkipWhitespace();
        char c = Peek();
        if (c == '(')
        {
            return true;
        }

        // Find the rightmost term of the preceding expression, but stop unwrapping if
        // the node itself is a parenthesized group (e.g. do not unwrap inside (3 + 4).
        ExpressionNode rightmost = left;
        while (_groupExtents is null || !_groupExtents.ContainsKey(rightmost))
        {
            if (rightmost is BinaryNode binary)
            {
                rightmost = binary.Right;
            }
            else
            {
                break;
            }
        }

        // Allow adjacent numeric literals after function calls or parenthesized
        // expressions, e.g. "triple(3)4", "(2)3", or "(1+2)(3+4)2", but never after bare
        // numbers ("1 000", "2 3").
        if ((char.IsAsciiDigit(c) || c == '.') &&
            (rightmost is FunctionCallNode || (_groupExtents is not null && _groupExtents.ContainsKey(rightmost))))
        {
            return true;
        }

        // Reject boolean literals and keywords from starting an implicit product, e.g.
        // allow "2x" but not "2true" or "2and(3,4)".
        int len = GetIdentifierLengthAt(_pos);
        if (len == 0)
        {
            return false;
        }

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

            // Re-enter unary parsing to allow 2^-3 and make powers right-associative:
            // 2^3^4 means 2^(3^4), while the enclosing unary parser keeps -2^2 as -(2^2).
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
            ThrowSyntaxErrorBracketsOrSemicolon(c);
        }

        if (c == '(')
        {
            int groupStart = _pos;
            Consume();
            var value = ParseStatement();
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

        // A known function owns its parentheses; a bound variable followed by a group
        // can instead be an implicit product such as x(2+3).
        if (Peek() == '(' && FunctionArity.TryGet(identifier, _options.CustomFunctions, out var signature))
        {
            var args = ParseArgumentList();

            if (args.Count < signature.MinimumArguments || (signature.MaximumArguments is int maximum && args.Count > maximum))
            {
                throw new ExpressionException(Diagnostic(
                    EvaluationError.InvalidArgumentCount,
                    AstValidator.FormatArityErrorMessage(identifier, signature, args.Count),
                    identifierStart,
                    identifier.Length));
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
            ThrowSyntaxErrorBracketsOrSemicolon(Peek());
        }
            
        if (ConstantCatalog.TryResolve(identifier, out var constantKind, out _))
        {
            return new IdentifierNode(identifier, constantKind, Span(identifierStart, identifier.Length));
        }

        if (Peek() != '(' && FunctionArity.TryGet(identifier, _options.CustomFunctions, out _))
        {
            throw new ExpressionException(Diagnostic(
                EvaluationError.InvalidSyntax, $"Function '{identifier}' requires parentheses.", identifierStart, identifier.Length));
        }

        if (_context.TryResolveVariable(identifier, out _) ||
            _declaredVariables?.Contains(identifier) == true)
        {
            return new IdentifierNode(identifier, null, Span(identifierStart, identifier.Length));
        }

        if (_allowUnboundVariables)
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
            var digits = _input.AsSpan(digitsStart, digitCount).TrimStart('0');
            int bitLimit = Math.Min(_options.MaxIntegerBits, CalculationLimits.MaxIntermediateBits);
            if ((long)Math.Max(0, digits.Length - 1) * radix.BitsPerDigit > bitLimit)
            {
                throw LiteralLimit(start);
            }

            var result = new MathValue(radix.Parse(digits));
            if (result.IntegerValue.GetBitLength() > bitLimit)
            {
                throw LiteralLimit(start);
            }
            return result;
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

                // Without exponent digits, leave 'e' for identifier parsing (2e is
                // multiplication by Euler's constant, not a malformed exponent).
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

        int exponentIndex = numberSpan.IndexOfAny('e', 'E');
        var mantissa = exponentIndex < 0 ? numberSpan : numberSpan[..exponentIndex];
        if (mantissa.IndexOfAnyInRange('0', '9') < 0)
        {
            throw new ExpressionException(Diagnostic(
                EvaluationError.InvalidSyntax, "Expected a number.", start, Math.Max(1, _pos - start)));
        }

        int dot = mantissa.IndexOf('.');
        int fractionalDigits = dot < 0 ? 0 : mantissa.Length - dot - 1;
        // Zero is exact even with an exponent too large to represent or allocate.
        if (mantissa.IndexOfAnyInRange('1', '9') < 0)
        {
            long zeroScale = fractionalDigits;
            if (exponentIndex >= 0)
            {
                var exponentText = numberSpan[(exponentIndex + 1)..];
                zeroScale = GetZeroDisplayScale(exponentText, fractionalDigits);
            }

            bool decimalZero = hasDecimal || (exponentIndex >= 0 && numberSpan[exponentIndex + 1] == '-');
            return MathValue.FromExactFraction(BigInteger.Zero, BigInteger.One, decimalZero)
                .WithDecimalDisplayScale(zeroScale);
        }

        long exponent = 0;
        if (exponentIndex >= 0 &&
            (!long.TryParse(numberSpan[(exponentIndex + 1)..], NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture, out exponent) ||
             exponent < -_options.MaxExponentMagnitude ||
             exponent > _options.MaxExponentMagnitude))
        {
            throw LiteralLimit(start);
        }

        string coefficientText = dot < 0 ? mantissa.ToString() :
            string.Concat(mantissa[..dot], mantissa[(dot + 1)..]);
        var coefficientDigits = coefficientText.AsSpan().TrimStart('0');

        // Move trailing coefficient zeros into the power of ten before allocating
        // either component, preserving the value with smaller integer intermediates.
        int trailingZeros = coefficientDigits.Length - coefficientDigits.TrimEnd('0').Length;
        coefficientDigits = coefficientDigits[..(coefficientDigits.Length - trailingZeros)];
        long power = exponent - fractionalDigits + trailingZeros;

        // log2(10) turns decimal length into a pre-allocation size estimate.
        // Exact bit-length checks below still enforce the boundary after construction.
        if ((coefficientDigits.Length - 1L) * 3.3219280948873623 > CalculationLimits.MaxIntermediateBits ||
            Math.Abs(power) * 3.3219280948873623 > CalculationLimits.MaxIntermediateBits)
        {
            throw LiteralLimit(start);
        }

        var coefficient = BigInteger.Parse(coefficientDigits, NumberStyles.None, CultureInfo.InvariantCulture);
        var scale = BigInteger.Pow(10, checked((int)Math.Abs(power)));
        if (coefficient.GetBitLength() > CalculationLimits.MaxIntermediateBits ||
            scale.GetBitLength() > CalculationLimits.MaxIntermediateBits ||
            (power > 0 && coefficient.GetBitLength() + scale.GetBitLength() - 1 > CalculationLimits.MaxIntermediateBits))
        {
            throw LiteralLimit(start);
        }

        try
        {
            bool decimalDomain = hasDecimal || exponent < 0;
            var result = power >= 0
                ? MathValue.FromExactFraction(coefficient * scale, BigInteger.One, decimalDomain)
                : MathValue.FromExactFraction(coefficient, scale, decimalDomain);
            result.ValidateLimits(_options.MaxIntegerBits);
            return result.WithDecimalDisplayScale(Math.Max(0, fractionalDigits - exponent));
        }
        catch (CalculationLimitException)
        {
            throw LiteralLimit(start);
        }
    }

    private static long GetZeroDisplayScale(ReadOnlySpan<char> exponentText, int fractionalDigits)
    {
        if (!long.TryParse(exponentText, NumberStyles.AllowLeadingSign,
            CultureInfo.InvariantCulture, out var exponent))
        {
            if (exponentText.StartsWith("-"))
            {
                return MathValue.MaxDecimalDisplayScale;
            }

            return 0;
        }

        if (exponent >= fractionalDigits)
        {
            return 0;
        }

        if (exponent < -MathValue.MaxDecimalDisplayScale)
        {
            return MathValue.MaxDecimalDisplayScale;
        }

        return fractionalDigits - exponent;
    }

    private CalculationLimitException LiteralLimit(int start) => new(Diagnostic(
        EvaluationError.LimitExceeded, "Number literal exceeds the configured computation limits.",
        start, Math.Max(1, _pos - start)));

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
        if (!IsKeywordAt(keyword))
        {
            return false;
        }

        _pos += keyword.Length;
        return true;
    }
}
