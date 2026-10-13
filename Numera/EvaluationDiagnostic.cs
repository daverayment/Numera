namespace Numera;

/// <summary>
/// Stable error categories for consumers to map to their own localized messages.
/// </summary>
public enum EvaluationError
{
    /// <summary>
    /// The expression violates grammar rules, such as unbalanced brackets or invalid
    /// characters.
    /// </summary>
    InvalidSyntax,

    /// <summary>
    /// An identifier was called as a function but is not recognized by the evaluator.
    /// </summary>
    UnknownFunction,

    /// <summary>
    /// An identifier was referenced as a constant but is not recognized by the evaluator.
    /// </summary>
    UnknownConstant,

    /// <summary>
    /// A function was invoked with fewer or more arguments than permitted by its arity
    /// rules.
    /// </summary>
    InvalidArgumentCount,

    /// <summary>
    /// The expression attempts a division or remainder operation by zero.
    /// </summary>
    DivisionByZero,

    /// <summary>
    /// Evaluation exceeded a configured safety ceiling, such as max length, nesting
    /// depth, or bit size.
    /// </summary>
    LimitExceeded,

    /// <summary>
    /// A numerical operation exceeded the representable bounds of the arithmetic type.
    /// </summary>
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
    /// <summary>
    /// Structured diagnostic details identifying the error category, human-readable
    /// message, and source location.
    /// </summary>
    public EvaluationDiagnostic Diagnostic { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="ExpressionException"/> wrapping the given
    /// evaluation diagnostic.
    /// </summary>
    /// <param name="diagnostic">The structured diagnostic describing the failure.</param>
    public ExpressionException(EvaluationDiagnostic diagnostic)
        : base((diagnostic ?? throw new ArgumentNullException(nameof(diagnostic))).Message)
    {
        Diagnostic = diagnostic;
    }
}
