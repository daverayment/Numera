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
/// An evaluation failure with a category and a message. Position is a zero-based UTF-16
/// offset; length counts UTF-16 code units. A null position means no source location is
/// available, and a zero length can identify the end of input.
/// </summary>
/// <remarks>
/// Consumers should use Code for programmatic decisions or localization rather than
/// matching message text, which is intended for human-readable diagnostics.
/// </remarks>
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
