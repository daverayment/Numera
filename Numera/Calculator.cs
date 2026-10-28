using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Numera.Ast;

namespace Numera;

/// <summary>
/// Static convenience facade for quick scripts, one-liners and tests. It delegates to a shared
/// <see cref="CalculatorEngine"/> built from <see cref="CalculatorOptions.Default"/>: invariant culture,
/// radians, and human-input normalization (launcher equals, Unicode operators, division colons)
/// enabled. Applications that need another culture, angle unit or limits, or that want results
/// formatted in the user's culture, should create their own <see cref="CalculatorEngine"/>.
/// </summary>
/// <remarks>
/// Input is always read with invariant-culture rules here, so <c>1.5</c> is one and a half and a comma
/// is an argument separator. <see cref="FormatResult"/>, however, defaults to the current culture for
/// display, so a displayed result may not be re-parseable by this facade. A <see cref="CalculatorEngine"/>
/// avoids that by using one culture in both directions. Session state (variables and <c>ans</c>) is
/// supplied per call through <see cref="EvaluationContext"/>; the facade itself holds none and is thread-safe.
/// <code>
/// MathValue four = Calculator.Evaluate("2 + 2");
/// var context = new EvaluationContext { PreviousResult = four };
/// MathValue eight = Calculator.Evaluate("ans * 2", context);
/// </code>
/// </remarks>
public static class Calculator
{
    private static readonly CalculatorEngine DefaultEngine = new(CalculatorOptions.Default);

    /// <summary>
    /// Evaluates a mathematical expression and returns a polymorphic <see cref="MathValue"/>.
    /// </summary>
    /// <param name="expression">The expression to evaluate, e.g. <c>2 + 2 * sqrt(16)</c>.</param>
    /// <param name="context">Optional variables and previous result.</param>
    /// <returns>The result of the evaluation as a <see cref="MathValue"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="expression"/> is null.</exception>
    /// <exception cref="NumeraException">Thrown if the expression cannot be normalized or parsed, or
    /// evaluation fails. Check <see cref="NumeraException.Diagnostic"/> for structured details.</exception>
    public static MathValue Evaluate(string expression, EvaluationContext? context = null) =>
        DefaultEngine.Evaluate(expression, context);

    /// <summary>
    /// Evaluates an already parsed expression tree, so a caller that both formats and evaluates an
    /// expression only parses it once. The tree is validated against the default limits first.
    /// </summary>
    /// <param name="node">The tree to evaluate, typically from <see cref="Parse(string, EvaluationContext?)"/>.</param>
    /// <param name="context">Optional variables and previous result used to resolve identifiers.</param>
    /// <returns>The result of the evaluation as a <see cref="MathValue"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="node"/> is null.</exception>
    /// <exception cref="NumeraException">Thrown if the tree breaches limits or evaluation fails; check
    /// <see cref="NumeraException.Diagnostic"/>.</exception>
    public static MathValue Evaluate(ExpressionNode node, EvaluationContext? context = null) =>
        DefaultEngine.Evaluate(node, context);

    /// <summary>
    /// Attempts to evaluate an expression without throwing for expected failures. Invalid API
    /// arguments (such as a null expression) still throw.
    /// </summary>
    /// <param name="expression">The expression to evaluate.</param>
    /// <param name="result">On success, the result; otherwise <c>default</c> and not meaningful.</param>
    /// <param name="diagnostic">On failure, the structured error (code, message and, where known, source
    /// position); otherwise <c>null</c>. Positions refer to the normalized text.</param>
    /// <param name="context">Optional variables and previous result.</param>
    /// <returns><c>true</c> if evaluation succeeded; <c>false</c> if normalization, parsing or evaluation failed.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="expression"/> is null.</exception>
    public static bool TryEvaluate(
        string expression,
        out EvaluationResult result,
        [NotNullWhen(false)] out EvaluationDiagnostic? diagnostic,
        EvaluationContext? context = null) =>
        DefaultEngine.TryEvaluate(expression, out result, out diagnostic, context);

    /// <summary>
    /// Attempts to evaluate an already parsed expression tree without throwing for
    /// expected failures.
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
    public static bool TryEvaluate(
        ExpressionNode node,
        out EvaluationResult result,
        [NotNullWhen(false)] out EvaluationDiagnostic? diagnostic,
        EvaluationContext? context = null) =>
        DefaultEngine.TryEvaluate(node, out result, out diagnostic, context);

    /// <summary>
    /// Parses an expression into an abstract syntax tree without evaluating it.
    /// Normalization runs first, so node spans refer to the normalized text rather than
    /// the original input.
    /// </summary>
    /// <param name="expression">The expression to parse.</param>
    /// <param name="context">Optional variables; supplying them lets their names parse as
    /// identifiers (for example, enabling implicit multiplication such as <c>2x</c>).
    /// </param>
    /// <returns>The root node of the syntax tree.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="expression"/> is
    /// null.</exception>
    /// <exception cref="NumeraException">Thrown if normalization or parsing fails, or
    /// limits are exceeded.</exception>
    public static ExpressionNode Parse(string expression, EvaluationContext? context = null) =>
        DefaultEngine.Parse(expression, context);

    /// <summary>
    /// Attempts to parse an expression without throwing for syntax or normalization
    /// errors.
    /// </summary>
    /// <param name="expression">The expression to parse.</param>
    /// <param name="node">On success, the root of the syntax tree; otherwise <c>null</c>.
    /// </param>
    /// <param name="diagnostic">On failure, the structured error; otherwise <c>null</c>.
    /// </param>
    /// <param name="context">Optional variables so their names parse as identifiers.
    /// </param>
    /// <returns><c>true</c> if parsing succeeded; otherwise <c>false</c>.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="expression"/> is
    /// null.</exception>
    public static bool TryParse(
        string expression,
        [NotNullWhen(true)] out ExpressionNode? node,
        [NotNullWhen(false)] out EvaluationDiagnostic? diagnostic,
        EvaluationContext? context = null) =>
        DefaultEngine.TryParse(expression, out node, out diagnostic, context);

    /// <summary>
    /// Parses and re-renders an expression, for example to show a tidied or typeset
    /// version of what the user typed. The expression is not evaluated.
    /// </summary>
    /// <param name="expression">The expression to format.</param>
    /// <param name="options">Rendering style; defaults to
    /// <see cref="ExpressionFormatOptions.Typographic"/>.</param>
    /// <returns>The rendered expression text.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="expression"/> is
    /// null.</exception>
    /// <exception cref="NumeraException">Thrown if the expression cannot be normalized or
    /// parsed.</exception>
    public static string FormatExpression(string expression, ExpressionFormatOptions? options = null) =>
        DefaultEngine.FormatExpression(expression, options);

    /// <summary>
    /// Renders an existing expression tree. Unknown function names are tolerated so
    /// partially understood input can still be displayed.
    /// </summary>
    /// <param name="node">The tree to render.</param>
    /// <param name="options">Rendering style; defaults to
    /// <see cref="ExpressionFormatOptions.Typographic"/>.</param>
    /// <returns>The rendered expression text.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="node"/> is null.
    /// </exception>
    /// <exception cref="NumeraException">Thrown if the tree breaches nesting or length
    /// limits.</exception>
    public static string FormatExpression(ExpressionNode node, ExpressionFormatOptions? options = null) =>
        DefaultEngine.FormatExpression(node, options);

    /// <summary>
    /// Formats a calculated <see cref="MathValue"/> into a structured
    /// <see cref="FormattedCalculation"/>, providing primary user-facing localized text,
    /// invariant machine-readable text, and radix representations (hexadecimal, binary,
    /// octal) for integer results.
    /// </summary>
    /// <param name="value">The evaluated calculation result.</param>
    /// <param name="culture">The culture for the primary display; defaults to
    /// <see cref="CultureInfo.CurrentCulture"/>. A <see cref="CalculatorEngine"/> formats
    /// with its own culture instead, which keeps display and input symmetrical.</param>
    /// <returns>A <see cref="FormattedCalculation"/> holding the original value, the
    /// culture-specific display text, an invariant-culture string safe for machine use,
    /// and prefixed hex, binary and octal forms.</returns>
    public static FormattedCalculation FormatResult(MathValue value, CultureInfo? culture = null)
    {
        culture ??= CultureInfo.CurrentCulture;
        string primaryDisplay = value.ToString(culture);
        string machineReadable = value.ToString(CultureInfo.InvariantCulture);

        return new FormattedCalculation(
            Value: value,
            PrimaryDisplay: primaryDisplay,
            MachineReadable: machineReadable,
            HexRepresentation: value.ToHexString(prefix: true),
            BinaryRepresentation: value.ToBinaryString(prefix: true),
            OctalRepresentation: value.ToOctalString(prefix: true));
    }
}
