namespace Numera;

/// <summary>
/// Stable error categories for consumers to map to their own localized messages.
/// </summary>
public enum EvaluationError
{
    InvalidSyntax,
    UnknownFunction,
    UnknownConstant,
    InvalidArgumentCount,
    DivisionByZero,
    LimitExceeded,
    NumericOverflow,
}

/// <summary>
/// An evaluation failure. Position and length are UTF-16 offsets. null position means no
/// source location is available.
/// </summary>
public sealed record EvaluationDiagnostic(EvaluationError Code, string Message, int? Position = null, int Length = 0);

/// <summary>
/// A malformed expression with a structured, nonlocalized diagnostic.
/// </summary>
public sealed class ExpressionException : FormatException
{
    public EvaluationDiagnostic Diagnostic { get; }

    public ExpressionException(EvaluationDiagnostic diagnostic)
        : base((diagnostic ?? throw new ArgumentNullException(nameof(diagnostic))).Message)
    {
        Diagnostic = diagnostic;
    }
}
