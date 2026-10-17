using System.Diagnostics.CodeAnalysis;

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
}
