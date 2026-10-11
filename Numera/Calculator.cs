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
    /// cref="NumberValue"/>.
    /// </summary>
    /// <param name="expression">The expression to evaluate, e.g. "2 + 2 * sqrt(16)".
    /// </param>
    /// <param name="options">Optional constraints for evaluation to prevent memory/CPU
    /// exhaustion on untrusted inputs.</param>
    /// <returns>The result of the evaluation as a <see cref="NumberValue"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="expression"/> is
    /// null.</exception>
    /// <exception cref="ExpressionException">Thrown if the expression contains syntax
    /// errors, unknown functions, or invalid arguments.</exception>
    /// <exception cref="CalculationLimitException">Thrown if the expression exceeds
    /// computational safety limits.</exception>
    /// <exception cref="DivideByZeroException">Thrown if the expression attempts to
    /// divide by zero.</exception>
    /// <exception cref="OverflowException">Thrown if a numeric overflow occurs during
    /// evaluation.</exception>
    /// <remarks>
    /// To evaluate an expression without throwing exceptions for expected
    /// failures, use <see cref="TryEvaluate"/>.
    /// </remarks>
    public static NumberValue Evaluate(string expression, EvaluationOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(expression);
        options ??= new EvaluationOptions();
        options.Validate();
        return new ExpressionParser(expression, options).Evaluate();
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
        out NumberValue value,
        [NotNullWhen(false)] out EvaluationDiagnostic? diagnostic,
        EvaluationOptions? options = null)
    {
        try
        {
            value = Evaluate(expression, options);
            diagnostic = null;
            return true;
        }
        catch (ExpressionException exception)
        {
            diagnostic = exception.Diagnostic;
        }
        catch (CalculationLimitException exception)
        {
            diagnostic = new EvaluationDiagnostic(EvaluationError.LimitExceeded, exception.Message);
        }
        catch (DivideByZeroException exception)
        {
            diagnostic = new EvaluationDiagnostic(EvaluationError.DivisionByZero, exception.Message);
        }
        catch (OverflowException exception)
        {
            // Arithmetic operators handle their own decimal-to-double fallback. Any
            // overflow that still escapes evaluation is reported as a failure here.
            diagnostic = new EvaluationDiagnostic(EvaluationError.NumericOverflow, exception.Message);
        }

        value = default;
        return false;
    }
}
