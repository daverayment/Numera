using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Numera.Ast;

namespace Numera;

/// <summary>
/// Static convenience facade for quick scripts, one-liners and tests. Each call uses
/// the current culture, radians, and standard human-input normalization. Create a
/// <see cref="CalculatorEngine"/> to reuse a stable culture or customize other settings.
/// </summary>
/// <remarks>
/// Results retain the culture used to read input, even if the current culture later
/// changes. Use <see cref="EvaluationResult.PrimaryDisplay"/> for display text or
/// <see cref="EvaluationResult.FormatResult(ResultFormatOptions?, CultureInfo?, bool)"/>
/// for a formatted result with alternatives.
/// Session state (variables and <c>ans</c>) is supplied per call through
/// <see cref="EvaluationContext"/>; the facade itself holds none and is thread-safe.
/// <code>
/// MathValue four = Calculator.Evaluate("2 + 2");
/// var context = new EvaluationContext { PreviousResult = four };
/// MathValue eight = Calculator.Evaluate("ans * 2", context);
/// </code>
/// </remarks>
public static class Calculator
{
    /// <summary>
    /// Evaluates a mathematical expression and returns a result containing a polymorphic
    /// <see cref="MathValue"/>.
    /// </summary>
    /// <param name="expression">The expression to evaluate, e.g. <c>2 + 2 * sqrt(16)</c>.
    /// </param>
    /// <param name="context">Optional variables and previous result.</param>
    /// <returns>The result of the evaluation and the updated exectuion context as an
    /// <see cref="EvaluationResult"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="expression"/>
    /// is null.</exception>
    /// <exception cref="NumeraException">Thrown if the expression cannot be normalized or
    /// parsed, or evaluation fails. Check <see cref="NumeraException.Diagnostic"/> for
    /// structured details.</exception>
    public static EvaluationResult Evaluate(string expression, EvaluationContext? context = null) =>
        CalculatorEngine.Default.Evaluate(expression, context);

    /// <summary>
    /// Evaluates an already parsed expression tree, so a caller that both formats and
    /// evaluates an expression only parses it once. The tree is validated against the
    /// default limits first.
    /// </summary>
    /// <param name="node">The tree to evaluate, typically from
    /// <see cref="Parse(string, EvaluationContext?, bool)"/>.</param>
    /// <param name="context">Optional variables and previous result used to resolve
    /// identifiers.</param>
    /// <returns>The result of the evaluation and the updated execution context as an 
    /// <see cref="EvaluationResult"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="node"/> is
    /// null.</exception>
    /// <exception cref="NumeraException">Thrown if the tree breaches limits or evaluation
    /// fails; check <see cref="NumeraException.Diagnostic"/>.</exception>
    public static EvaluationResult Evaluate(ExpressionNode node, EvaluationContext? context = null) =>
        CalculatorEngine.Default.Evaluate(node, context);

    /// <summary>
    /// Attempts to evaluate an expression without throwing for expected failures. Invalid
    /// API arguments (such as a null expression) still throw.
    /// </summary>
    /// <param name="expression">The expression to evaluate.</param>
    /// <param name="result">On success, the result; otherwise <c>default</c> and not
    /// meaningful.</param>
    /// <param name="diagnostic">On failure, the structured error (code, message and,
    /// where known, source position); otherwise <c>null</c>. Positions refer to the
    /// original input.</param>
    /// <param name="context">Optional variables and previous result.</param>
    /// <returns><c>true</c> if evaluation succeeded; <c>false</c> if normalization,
    /// parsing or evaluation failed.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="expression"/> is
    /// null.</exception>
    public static bool TryEvaluate(
        string expression,
        out EvaluationResult result,
        [NotNullWhen(false)] out EvaluationDiagnostic? diagnostic,
        EvaluationContext? context = null) =>
        CalculatorEngine.Default.TryEvaluate(expression, out result, out diagnostic, context);

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
        CalculatorEngine.Default.TryEvaluate(node, out result, out diagnostic, context);

    /// <summary>
    /// Parses an expression into an abstract syntax tree without evaluating it.
    /// Node spans refer to the original input, including when normalization is enabled.
    /// </summary>
    /// <param name="expression">The expression to parse.</param>
    /// <param name="context">Optional variables; supplying them lets their names parse as
    /// identifiers (for example, enabling implicit multiplication such as <c>2x</c>).
    /// </param>
    /// <param name="allowUnboundVariables">If true, variable references in the expression
    /// which do not have corresponding entries in <paramref name="context"/> do not cause
    /// an error. Use for formatting or REPL use.</param>
    /// <returns>The root node of the syntax tree.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="expression"/> is
    /// null.</exception>
    /// <exception cref="NumeraException">Thrown if normalization or parsing fails, or
    /// limits are exceeded.</exception>
    public static ExpressionNode Parse(
        string expression,
        EvaluationContext? context = null,
        bool allowUnboundVariables = false) =>
        CalculatorEngine.Default.Parse(expression, context, allowUnboundVariables);

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
    /// <param name="allowUnboundVariables">If true, identifiers not present in context are accepted as variable nodes.</param>
    /// <returns><c>true</c> if parsing succeeded; otherwise <c>false</c>.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="expression"/> is
    /// null.</exception>
    public static bool TryParse(
        string expression,
        [NotNullWhen(true)] out ExpressionNode? node,
        [NotNullWhen(false)] out EvaluationDiagnostic? diagnostic,
        EvaluationContext? context = null,
        bool allowUnboundVariables = false) =>
        CalculatorEngine.Default.TryParse(expression, out node, out diagnostic, context, allowUnboundVariables);

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
    /// <exception cref="ArgumentOutOfRangeException">Thrown if the rendering options are invalid.</exception>
    /// <exception cref="NumeraException">Thrown if the expression cannot be normalized or
    /// parsed.</exception>
    public static string FormatExpression(string expression, ExpressionFormatOptions? options = null) =>
        CalculatorEngine.Default.FormatExpression(expression, options);

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
    /// <exception cref="ArgumentOutOfRangeException">Thrown if the rendering options are invalid.</exception>
    /// <exception cref="NumeraException">Thrown if the tree breaches nesting or length
    /// limits.</exception>
    public static string FormatExpression(ExpressionNode node, ExpressionFormatOptions? options = null) =>
        CalculatorEngine.Default.FormatExpression(node, options);
}
