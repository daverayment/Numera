using System.Diagnostics.CodeAnalysis;
using Numera.Ast;

namespace Numera;

/// <summary>
/// Evaluates mathematical expressions using a polymorphic number system based on
/// BigInteger, Decimal, and Double. Stateless and zero-dependency.
/// </summary>
public static class Calculator
{
    /// <summary>
    /// Evaluates a mathematical expression and returns a polymorphic <see
    /// cref="MathValue"/>.
    /// </summary>
    /// <param name="expression">The expression to evaluate, e.g. "2 + 2 * sqrt(16)".
    /// </param>
    /// <param name="options">Optional constraints for evaluation to prevent memory/CPU
    /// exhaustion on untrusted inputs.</param>
    /// <returns>The result of the evaluation as a <see cref="MathValue"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="expression"/> is
    /// null.</exception>
    /// <exception cref="NumeraException">Thrown if the expression contains syntax errors,
    /// unknown functions, invalid arguments, division by zero, or exceeds computational limits.
    /// Check <see cref="NumeraException.Diagnostic"/> for structured failure details.</exception>
    /// <remarks>
    /// To evaluate an expression without throwing exceptions for expected
    /// failures, use <see cref="TryEvaluate"/>.
    /// </remarks>
    public static MathValue Evaluate(string expression, EvaluationOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(expression);
        options ??= new EvaluationOptions();
        options.Validate();

        try
        {
            return new ExpressionParser(expression, options).Evaluate();
        }
        catch (DivideByZeroException exception)
        {
            throw new ExpressionException(
                new EvaluationDiagnostic(EvaluationError.DivisionByZero, exception.Message), exception);
        }
        catch (OverflowException exception)
        {
            // Arithmetic operators handle their own decimal-to-double fallback. Any
            // overflow that still escapes evaluation is reported as a failure here.
            throw new ExpressionException(
                new EvaluationDiagnostic(EvaluationError.NumericOverflow, exception.Message), exception);
        }
    }

    /// <summary>
    /// Safely attempts to evaluate an expression without throwing exceptions for standard
    /// mathematical or syntax errors. Invalid API arguments still throw exceptions.
    /// </summary>
    /// <param name="expression">The mathematical expression to evaluate.</param>
    /// <param name="value">The resulting value if evaluation succeeds; otherwise, the
    /// default value.</param>
    /// <param name="diagnostic">Contains detailed error information if the evaluation
    /// failed; otherwise, null.</param>
    /// <param name="options">Optional constraints for evaluation to prevent memory/CPU
    /// exhaustion on untrusted input.</param>
    /// <returns><c>true</c> if the expression was successfully evaluated; otherwise,
    /// <c>false</c>.</returns>
    /// <remarks>
    /// Invalid API arguments (e.g., null expression) will still throw standard
    /// exceptions.
    /// </remarks>
    public static bool TryEvaluate(
        string expression,
        out MathValue value,
        [NotNullWhen(false)] out EvaluationDiagnostic? diagnostic,
        EvaluationOptions? options = null)
    {
        try
        {
            value = Evaluate(expression, options);
            diagnostic = null;
            return true;
        }
        catch (NumeraException exception)
        {
            diagnostic = exception.Diagnostic;
        }

        value = default;
        return false;
    }

    /// <summary>
    /// Formats a calculated <see cref="MathValue"/> into a structured <see cref="FormattedCalculation"/>,
    /// providing primary user-facing localized text, invariant machine-readable text, and optional
    /// radix representations (hexadecimal, binary, octal) for integer results.
    /// </summary>
    /// <param name="value">The evaluated calculation result.</param>
    /// <param name="culture">The culture to use for primary display formatting; defaults to <see cref="System.Globalization.CultureInfo.CurrentCulture"/>.</param>
    /// <returns>A structured record containing the formatted values.</returns>
    public static FormattedCalculation FormatResult(MathValue value, System.Globalization.CultureInfo? culture = null)
    {
        culture ??= System.Globalization.CultureInfo.CurrentCulture;
        string primaryDisplay = value.ToString(culture);
        string machineReadable = value.ToString(System.Globalization.CultureInfo.InvariantCulture);

        return new FormattedCalculation(
            Value: value,
            PrimaryDisplay: primaryDisplay,
            MachineReadable: machineReadable,
            HexRepresentation: value.ToHexString(prefix: true),
            BinaryRepresentation: value.ToBinaryString(prefix: true),
            OctalRepresentation: value.ToOctalString(prefix: true));
    }

    /// <summary>
    /// Parses a mathematical expression into an abstract syntax tree (<see cref="ExpressionNode"/>)
    /// on demand, without evaluating it.
    /// </summary>
    /// <param name="expression">The mathematical expression to parse.</param>
    /// <param name="options">Optional evaluation constraints and limits.</param>
    /// <returns>The root <see cref="ExpressionNode"/> of the parsed expression tree.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="expression"/> is null.</exception>
    /// <exception cref="NumeraException">Thrown if the expression contains syntax errors or invalid function names.</exception>
    public static ExpressionNode Parse(string expression, EvaluationOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(expression);
        options ??= new EvaluationOptions();
        options.Validate();

        return new AstParser(expression, options).Parse();
    }

    /// <summary>
    /// Safely attempts to parse a mathematical expression into an abstract syntax tree (<see cref="ExpressionNode"/>)
    /// without throwing exceptions for syntax errors.
    /// </summary>
    /// <param name="expression">The mathematical expression to parse.</param>
    /// <param name="node">The resulting <see cref="ExpressionNode"/> if parsing succeeds; otherwise, null.</param>
    /// <param name="diagnostic">Contains detailed error information if parsing failed; otherwise, null.</param>
    /// <param name="options">Optional evaluation constraints and limits.</param>
    /// <returns><c>true</c> if parsing succeeded; otherwise, <c>false</c>.</returns>
    public static bool TryParse(
        string expression,
        [NotNullWhen(true)] out ExpressionNode? node,
        [NotNullWhen(false)] out EvaluationDiagnostic? diagnostic,
        EvaluationOptions? options = null)
    {
        try
        {
            node = Parse(expression, options);
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
    /// Formats a mathematical expression string into a rendered representation using the specified format options.
    /// </summary>
    /// <param name="expression">The mathematical expression string to format.</param>
    /// <param name="options">Formatting style and typographical options; defaults to <see cref="ExpressionFormatOptions.Typographic"/>.</param>
    /// <returns>The formatted expression string.</returns>
    public static string FormatExpression(string expression, ExpressionFormatOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(expression);
        var node = Parse(expression);
        return FormatExpression(node, options);
    }

    /// <summary>
    /// Formats an <see cref="ExpressionNode"/> abstract syntax tree into a rendered representation using the specified format options.
    /// </summary>
    /// <param name="node">The syntax tree node to format.</param>
    /// <param name="options">Formatting style and typographical options; defaults to <see cref="ExpressionFormatOptions.Typographic"/>.</param>
    /// <returns>The formatted expression string.</returns>
    public static string FormatExpression(ExpressionNode node, ExpressionFormatOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(node);
        options ??= ExpressionFormatOptions.Typographic;

        IExpressionVisitor<string> visitor = options.Style switch
        {
            ExpressionFormatStyle.MathJax => new MathJaxVisitor(options),
            _ => new TypographicVisitor(options),
        };

        return node.Accept(visitor);
    }
}
