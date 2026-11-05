using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Numera.Ast;

namespace Numera;

/// <summary>
/// A long-lived, thread-safe calculator bound to one <see cref="CalculatorOptions"/>.
/// Options are validated once at construction, and the same
/// <see cref="CalculatorOptions.Culture"/> is used to interpret input and to format
/// results, so a displayed value can be typed back in.
/// </summary>
/// <remarks>
/// The engine holds no per-evaluation state: variables and <c>ans</c> travel in an
/// <see cref="EvaluationContext"/>, so one instance can serve many sessions concurrently.
/// An engine is bound to a single culture; applications serving users with different
/// cultures should keep one engine per culture. When
/// <see cref="CalculatorOptions.Normalization"/> is set, input is normalized before
/// parsing, and diagnostic positions and AST source spans are mapped back to the original
/// input.
/// <code>
/// var engine = new CalculatorEngine(CalculatorOptions.ForCulture(new CultureInfo("de-DE")));
/// MathValue value = engine.Evaluate("1.000,5 + 2");   // 1002.5
/// string shown = engine.FormatResult(value).PrimaryDisplay;   // "1002,5"
/// </code>
/// </remarks>
public sealed class CalculatorEngine
{
    /// <summary>
    /// Gets the shared, default calculator engine using
    /// <see cref="CalculatorOptions.Default"/>.
    /// </summary>
    public static CalculatorEngine Default { get; } = new();

    /// <summary>
    /// Creates a new engine configured for the specified culture, using default limits
    /// and normalization.
    /// </summary>
    /// <param name="culture">The culture to use for the engine.</param>
    /// <returns>A new <see cref="CalculatorEngine"/> configured for the specified
    /// culture.</returns>
    public static CalculatorEngine ForCulture(CultureInfo culture) =>
        new(CalculatorOptions.ForCulture(culture));

    /// <summary>
    /// Creates a new engine configured for the specified culture, with optional
    /// customizations.
    /// </summary>
    /// <param name="culture">The culture to use for the engine.</param>
    /// <param name="configure">A function that takes the current
    /// <see cref="CalculatorOptions"/> and returns a modified version.</param>
    /// <returns>A new <see cref="CalculatorEngine"/> configured for the specified culture
    /// and customizations.</returns>
    /// <remarks>
    /// For example:
    /// <code>
    /// var engine = CalculatorEngine.ForCulture(
    ///     new CultureInfo("fr-FR"),
    ///     options => options with { AngleUnit = AngleUnit.Degrees });
    /// </code>
    /// </remarks>
    public static CalculatorEngine ForCulture(CultureInfo culture, Func<CalculatorOptions, CalculatorOptions>? configure = null)
    {
        var baseOptions = CalculatorOptions.ForCulture(culture);
        return new(configure != null ? configure(baseOptions) : baseOptions);
    }

    /// <summary>
    /// Creates a new engine capturing the calling thread's current culture, using default
    /// limits and normalization.
    /// </summary>
    /// <returns>A new <see cref="CalculatorEngine"/> configured for the current culture.
    /// </returns>
    public static CalculatorEngine ForCurrentCulture() =>
        new(CalculatorOptions.ForCurrentCulture());

    /// <summary>
    /// Creates a new engine configured for the current culture, with optional
    /// customizations.
    /// </summary>
    /// <param name="configure">A function that takes the current
    /// <see cref="CalculatorOptions"/> and returns a modified version.</param>
    /// <returns>A new <see cref="CalculatorEngine"/> configured for the current culture
    /// and customizations.</returns>
    /// <remarks>
    /// For example:
    /// <code>
    /// var engine = CalculatorEngine.ForCurrentCulture(
    ///     options => options with { AngleUnit = AngleUnit.Degrees });
    /// </code>
    /// </remarks>
    public static CalculatorEngine ForCurrentCulture(Func<CalculatorOptions, CalculatorOptions>? configure)
    {
        var baseOptions = CalculatorOptions.ForCurrentCulture();
        return new(configure != null ? configure(baseOptions) : baseOptions);
    }

    /// <summary>
    /// The immutable options this engine was created with. They were validated at
    /// construction and cannot change afterwards, which is what makes the engine safe to
    /// share between threads.
    /// </summary>
    public CalculatorOptions Options { get; }

    /// <summary>
    /// Initializes a new <see cref="CalculatorEngine"/> via a transformation of the
    /// default options.
    /// </summary>
    /// <param name="configure">A function that takes the default
    /// <see cref="CalculatorOptions"/> and returns a modified version.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="configure"/> is
    /// <c>null</c>.</exception>
    /// <remarks>
    /// For example:
    /// <code>
    /// var engine = new CalculatorEngine(options => options with
    /// {
    ///     AngleUnit = AngleUnit.Degrees,
    ///     MaxInputLength = 500
    /// });
    /// </code>
    /// </remarks>
    public CalculatorEngine(Func<CalculatorOptions, CalculatorOptions> configure) :
        this((configure ?? throw new ArgumentNullException(nameof(configure)))(CalculatorOptions.Default))
    {
    }

    /// <summary>
    /// Creates an engine, validating the options once so evaluation calls do not repeat
    /// the checks.
    /// </summary>
    /// <param name="options">The options to use; <c>null</c> selects
    /// <see cref="CalculatorOptions.Default"/>
    /// (invariant culture, radians, normalization on).</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if the options contain
    /// out-of-range values, such as an undefined angle unit or limits outside the
    /// supported ranges.</exception>
    /// <exception cref="ArgumentNullException">Thrown if the options have a null culture.
    /// </exception>
    public CalculatorEngine(CalculatorOptions? options = null)
    {
        options ??= CalculatorOptions.Default;
        options.Validate();
        Options = options;
    }

    /// <summary>
    /// Returns a new <see cref="CalculatorEngine"/> with modified options derived from
    /// this engine's options. The original engine is unchanged.
    /// </summary>
    /// <param name="configure">A function that takes the current
    /// <see cref="CalculatorOptions"/> and returns a modified version.</param>
    /// <returns>A new <see cref="CalculatorEngine"/> with the modified options.</returns>
    public CalculatorEngine WithOptions(Func<CalculatorOptions, CalculatorOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        return new CalculatorEngine(configure(Options));
    }

    /// <summary>
    /// Returns a new <see cref="CalculatorEngine"/> using the specified options.
    /// </summary>
    /// <param name="options">The options to apply.</param>
    /// <returns>A new <see cref="CalculatorEngine"/> with the specified options.
    /// </returns>
    public static CalculatorEngine WithOptions(CalculatorOptions options) => new(options);

    /// <summary>
    /// Returns a new <see cref="CalculatorEngine"/> with the specified angle unit,
    /// keeping all other options unchanged.
    /// </summary>
    /// <param name="unit">The angle unit to apply.</param>
    /// <returns>A new <see cref="CalculatorEngine"/> with the specified angle unit.
    /// </returns>
    public CalculatorEngine WithAngleUnit(AngleUnit unit) =>
        WithOptions(o => o with { AngleUnit = unit });

    /// <summary>
    /// Returns a new <see cref="CalculatorEngine"/> with the specified culture, keeping
    /// all other options unchanged.
    /// </summary>
    /// <param name="culture">The new culture to apply.</param>
    /// <returns>A new <see cref="CalculatorEngine"/> with the same options as this engine
    /// and the specified culture.</returns>
    public CalculatorEngine WithCulture(CultureInfo culture) =>
        WithOptions(o => o with { Culture = culture });

    /// <summary>
    /// Evaluates an expression and returns a <see cref="EvaluationResult"/> containing
    /// the polymorphic <see cref="MathValue"/>.
    /// </summary>
    /// <param name="expression">The expression, e.g. <c>2 + 2 * sqrt(16)</c>.</param>
    /// <param name="context">Optional variables and previous result.</param>
    /// <returns>The result of the evaluation as a <see cref="EvaluationResult"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="expression"/> is null.</exception>
    /// <exception cref="NumeraException">Thrown if normalization, parsing or evaluation fails. Check
    /// <see cref="NumeraException.Diagnostic"/> for structured details.</exception>
    public EvaluationResult Evaluate(string expression, EvaluationContext? context = null)
    {
        ArgumentNullException.ThrowIfNull(expression);

        try
        {
            return Evaluate(Parse(expression, context), context);
        }
        catch (DivideByZeroException exception)
        {
            throw new ExpressionException(
                new EvaluationDiagnostic(EvaluationError.DivisionByZero, exception.Message), exception);
        }
        catch (OverflowException exception)
        {
            // Arithmetic operators handle their own decimal-to-double fallback. Any overflow that
            // still escapes evaluation is reported as a failure here.
            throw new ExpressionException(
                new EvaluationDiagnostic(EvaluationError.NumericOverflow, exception.Message), exception);
        }
    }

    /// <summary>
    /// Evaluates an already parsed tree, enabling single-parse workflows that both format
    /// and evaluate. The tree is validated against this engine's limits first.
    /// </summary>
    /// <param name="node">The tree to evaluate, typically from <see cref="Parse"/>.
    /// </param>
    /// <param name="context">Optional variables and previous result used to resolve
    /// identifiers.</param>
    /// <returns>The result of the evaluation as a <see cref="EvaluationResult"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="node"/> is null.
    /// </exception>
    /// <exception cref="NumeraException">Thrown if evaluation fails.</exception>
    public EvaluationResult Evaluate(ExpressionNode node, EvaluationContext? context = null)
    {
        ArgumentNullException.ThrowIfNull(node);

        AstValidator.Validate(node, Options);

        try
        {
            var evaluator = new AstEvaluator(Options, context);
            var mathValue = node.Accept(evaluator);

            // Advance the cumulative context for the next evaluation, merging any new
            // assignments into the previous variables, and recording the evaluated node
            // as the canonical AST for ans.
            var nextContext = context?.Advance(evaluator.Assignments, mathValue, node)
                ?? new EvaluationContext(evaluator.Assignments, mathValue, node);

            var delta = evaluator.Assignments != null ?
                evaluator.Assignments.ToVariableCollection() :
                VariableCollection.Empty;

            return new EvaluationResult(mathValue, nextContext, delta, this);
        }
        catch (DivideByZeroException exception)
        {
            throw new ExpressionException(new EvaluationDiagnostic(
                EvaluationError.DivisionByZero, exception.Message), exception);
        }
        catch (OverflowException exception)
        {
            throw new ExpressionException(new EvaluationDiagnostic(
                EvaluationError.NumericOverflow, exception.Message), exception);
        }
    }

    /// <summary>
    /// Attempts to evaluate an expression without throwing for expected failures. Invalid
    /// API arguments (such as a null expression) still throw.
    /// </summary>
    /// <param name="expression">The expression to evaluate.</param>
    /// <param name="result">On success, the result; otherwise <c>default</c> and not
    /// meaningful.</param>
    /// <param name="diagnostic">On failure, the structured error (code, message and,
    /// where known, source position in the original input); otherwise <c>null</c>.
    /// </param>
    /// <param name="context">Optional variables and previous result.</param>
    /// <returns><c>true</c> if evaluation succeeded; <c>false</c> if normalization,
    /// parsing or evaluation failed.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="expression"/> is
    /// null.</exception>
    public bool TryEvaluate(
        string expression,
        out EvaluationResult result,
        [NotNullWhen(false)] out EvaluationDiagnostic? diagnostic,
        EvaluationContext? context = null)
    {
        ArgumentNullException.ThrowIfNull(expression);

        try
        {
            result = Evaluate(expression, context);
            diagnostic = null;
            return true;
        }
        catch (NumeraException exception)
        {
            diagnostic = exception.Diagnostic;
        }

        result = default;
        return false;
    }

    /// <summary>
    /// Attempts to evaluate an already parsed tree without throwing for expected
    /// failures.
    /// </summary>
    /// <param name="node">The tree to evaluate.</param>
    /// <param name="result">On success, the result; otherwise <c>default</c> and not
    /// meaningful.</param>
    /// <param name="diagnostic">On failure, the structured error; otherwise <c>null</c>.
    /// </param>
    /// <param name="context">Optional variables and previous result.</param>
    /// <returns><c>true</c> if evaluation succeeded; otherwise <c>false</c>.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="node"/> is null.
    /// </exception>
    public bool TryEvaluate(
        ExpressionNode node,
        out EvaluationResult result,
        [NotNullWhen(false)] out EvaluationDiagnostic? diagnostic,
        EvaluationContext? context = null)
    {
        ArgumentNullException.ThrowIfNull(node);

        try
        {
            result = Evaluate(node, context);
            diagnostic = null;
            return true;
        }
        catch (NumeraException exception)
        {
            diagnostic = exception.Diagnostic;
        }

        result = default;
        return false;
    }

    /// <summary>
    /// Parses an expression into an abstract syntax tree without evaluating it. The
    /// context is needed so caller variables are recognized as identifiers.
    /// </summary>
    /// <param name="expression">The expression to parse. Its length is checked against
    /// <see cref="CalculatorOptions.MaxInputLength"/> before any normalization work.
    /// </param>
    /// <param name="context">Optional variables, so their names parse as identifiers.
    /// </param>
    /// <param name="allowUnboundVariables">If true, variable references in the expression
    /// which do not have corresponding entries in <paramref name="context"/> do not cause
    /// an error. Use for formatting or REPL use.</param>
    /// <returns>The root node of the syntax tree.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="expression"/> is
    /// null.</exception>
    /// <exception cref="NumeraException">Thrown if normalization or parsing fails, or
    /// limits are exceeded.</exception>
    public ExpressionNode Parse(
        string expression,
        EvaluationContext? context = null,
        bool allowUnboundVariables = false)
    {
        ArgumentNullException.ThrowIfNull(expression);

        // Checked before normalizing so oversized input is rejected without doing any
        // rewriting work.
        if (expression.Length > Options.MaxInputLength)
        {
            throw new CalculationLimitException();
        }

        string text = expression;
        var sourceMap = SourceMap.Identity(expression.Length);
        if (Options.Normalization is { } normalization &&
            !ExpressionNormalizer.TryNormalize(
                expression, Options.Culture, out text!, out var diagnostic, out sourceMap, normalization))
        {
            throw new ExpressionException(diagnostic);
        }

        return new AstParser(
            text, Options, context, sourceMap, allowUnboundVariables).Parse();
    }

    /// <summary>
    /// Attempts to parse an expression without throwing for syntax or normalization
    /// errors.
    /// </summary>
    /// <param name="expression">The expression to parse.</param>
    /// <param name="node">On success, the root of the syntax tree; otherwise <c>null</c>.
    /// </param>
    /// <param name="diagnostic">On failure, the structured error; otherwise <c>null</c>.
    /// </param>
    /// <param name="context">Optional variables, so their names parse as identifiers.
    /// </param>
    /// <param name="allowUnboundVariables">If true, identifiers not present in context
    /// are accepted as variable nodes.</param>
    /// <returns><c>true</c> if parsing succeeded; otherwise <c>false</c>.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="expression"/> is
    /// null.</exception>
    public bool TryParse(
        string expression,
        [NotNullWhen(true)] out ExpressionNode? node,
        [NotNullWhen(false)] out EvaluationDiagnostic? diagnostic,
        EvaluationContext? context = null,
        bool allowUnboundVariables = false)
    {
        ArgumentNullException.ThrowIfNull(expression);

        try
        {
            node = Parse(expression, context, allowUnboundVariables);
            diagnostic = null;
            return true;
        }
        catch (NumeraException exception)
        {
            diagnostic = exception.Diagnostic;
        }

        node = null;
        return false;
    }

    /// <summary>
    /// Parses and re-renders an expression, for example to show a tidied or typeset
    /// version of what the
    /// user typed. The expression is not evaluated.
    /// </summary>
    /// <param name="expression">The expression to format.</param>
    /// <param name="options">Rendering style; defaults to
    /// <see cref="ExpressionFormatOptions.Typographic"/>.</param>
    /// <param name="context">Optional variables, so variable names parse as identifiers.
    /// </param>
    /// <returns>The rendered expression text.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="expression"/> is
    /// null.</exception>
    /// <exception cref="NumeraException">Thrown if the expression cannot be normalized or
    /// parsed.</exception>
    public string FormatExpression(
        string expression,
        ExpressionFormatOptions? options = null,
        EvaluationContext? context = null) =>
        FormatExpression(Parse(expression, context, allowUnboundVariables: true), options);

    /// <summary>
    /// Renders an existing expression tree. Unknown function names are tolerated so
    /// partially understood input can still be displayed; nesting limits still apply.
    /// </summary>
    /// <param name="node">The tree to render.</param>
    /// <param name="options">Rendering style; defaults to
    /// <see cref="ExpressionFormatOptions.Typographic"/>.</param>
    /// <returns>The rendered expression text.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="node"/> is null.
    /// </exception>
    /// <exception cref="NumeraException">Thrown if the tree breaches this engine's
    /// limits.</exception>
    public string FormatExpression(ExpressionNode node, ExpressionFormatOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(node);
        options ??= ExpressionFormatOptions.Typographic;

        AstValidator.Validate(node, Options, allowUnknownFunctions: true);

        IExpressionVisitor<string> visitor = options.Style switch
        {
            ExpressionFormatStyle.MathJax => new MathJaxVisitor(options),
            _ => new TypographicVisitor(options),
        };

        return node.Accept(visitor);
    }

    /// <summary>
    /// Formats an evaluation result into a structured, presentation-ready card using the
    /// engine's culture, or the caller-specified culture.
    /// </summary>
    /// <param name="result">The evaluation result to format.</param>
    /// <param name="culture">The culture to use for formatting.</param>
    /// <param name="prefixRadixOutput">Whether to prefix hexadecimal, binary, and octal
    /// outputs with their respective radix indicators.</param>
    /// <returns>A <see cref="FormattedCalculation"/> holding the original value, the
    /// display text in the specified culture, an invariant-culture string for machine
    /// use, and hex, binary and octal forms.</returns>
    public FormattedCalculation FormatResult(
        EvaluationResult result, CultureInfo? culture = null, bool prefixRadixOutput = true) =>
        FormatCore(
            result.Value,
            culture ?? Options.Culture,
            result.HasAssignments ? result.AssignedVariables : null,
            prefixRadixOutput);

    /// <summary>
    /// Formats a calculation value into a structured presentation-ready card using the
    /// engine's culture, or the caller-specified culture.
    /// </summary>
    /// <param name="value">The calculation value to format.</param>
    /// <param name="culture">The culture to use for formatting.</param>
    /// <param name="prefixRadixOutput">Whether to prefix hexadecimal, binary, and octal
    /// outputs with their respective radix indicators.</param>
    /// <returns>A <see cref="FormattedCalculation"/> holding the original value, the
    /// display text in the specified culture, an invariant-culture string for machine
    /// use, and prefixed hex, binary and octal forms.</returns>
    public FormattedCalculation FormatResult(
        MathValue value, CultureInfo? culture = null, bool prefixRadixOutput = true) =>
        FormatCore(
            value,
            culture ?? Options.Culture,
            null,
            prefixRadixOutput);

    private static FormattedCalculation FormatCore(
        MathValue value,
        CultureInfo culture,
        VariableCollection? assignedVariables,
        bool prefixRadixOutput)
    {
        string primaryDisplay = value.Kind == MathValue.DataType.Decimal ?
            FormatDisplayDecimal(value.DecimalValue, culture) :
            value.ToString(culture);

        string machineReadable = value.ToString(CultureInfo.InvariantCulture);

        return new FormattedCalculation(
            Value: value,
            PrimaryDisplay: primaryDisplay,
            MachineReadable: machineReadable,
            HexRepresentation: value.ToHexString(prefixRadixOutput),
            BinaryRepresentation: value.ToBinaryString(prefixRadixOutput),
            OctalRepresentation: value.ToOctalString(prefixRadixOutput),
            AssignedVariables: assignedVariables);
    }

    private static string FormatDisplayDecimal(decimal decVal, CultureInfo culture)
    {
        // If it's a whole number with excessive trailing zeros (scale > 2 and
        // Truncate(d) == d), format as a clean integer, while leaving values such as
        // 100.0 or 194.40 alone if scale <= 2.
        int scale = (decimal.GetBits(decVal)[3] >> 16) & 0x7F;
        if (scale > 2 && decimal.Truncate(decVal) == decVal)
        {
            return decimal.Truncate(decVal).ToString("F0", culture);
        }

        // Trim long runs of trailing zeros for human display.
        if (scale > 4)
        {
            decimal normalized = decVal / 1.000000000000000000000000000000000m;
            return normalized.ToString(culture);
        }

        return decVal.ToString(culture);
    }

    /// <summary>
    /// Formats a result for display using this engine's culture, the same one used to
    /// read input, so the displayed text can be typed back in.
    /// </summary>
    /// <param name="result">The evaluated result to format.</param>
    /// <returns>A <see cref="FormattedCalculation"/> holding the original value, the
    /// display text in this engine's culture, an invariant-culture string for machine
    /// use, and prefixed hex, binary and octal forms.</returns>
    public FormattedCalculation FormatResult(EvaluationResult result)
    {
        var card = Calculator.FormatResult(result, Options.Culture);
        return card with { AssignedVariables = result.HasAssignments ? result.AssignedVariables : null };
    }

    /// <summary>
    /// Formats a calculation value using this engine's culture.
    /// </summary>
    /// <param name="value">Calculation value to format.</param>
    /// <returns>A <see cref="FormattedCalculation"/> holding the original value, the
    /// display text in this engine's culture, an invariant-culture string for machine
    /// use, and prefixed hex, binary and octal forms.</returns>
    public FormattedCalculation FormatResult(MathValue value) =>
        Calculator.FormatResult(value, Options.Culture);
}
