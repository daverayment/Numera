using System.Diagnostics.CodeAnalysis;

namespace Numera;

/// <summary>
/// Evaluates invariant calculator expressions without formatting or narrowing results.
/// </summary>
public static class Calculator
{
    /// <summary>
    /// Evaluates an expression. Syntax errors carry diagnostics; arithmetic and limit
    /// failures throw their specific exceptions.
    /// </summary>
    public static NumberValue Evaluate(string expression, EvaluationOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(expression);
        options ??= new EvaluationOptions();
        options.Validate();
        return new ExpressionParser(expression, options).Evaluate();
    }

    /// <summary>
    /// Evaluates an expression without throwing for expected expression failures. Invalid
    /// API arguments still throw exceptions.
    /// </summary>
    public static bool TryEvaluate(string expression, out NumberValue value, [NotNullWhen(false)] out EvaluationDiagnostic? diagnostic, EvaluationOptions? options = null)
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
            diagnostic = new EvaluationDiagnostic(EvaluationError.NumericOverflow, exception.Message);
        }

        value = default;
        return false;
    }
}
