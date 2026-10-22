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

    /// <summary>
    /// An operation requires integer operands, but received a non-integer or fractional
    /// value.
    /// </summary>
    IntegerRequired,

    /// <summary>
    /// An argument value is outside the permitted numerical range for the operation.
    /// </summary>
    ArgumentOutOfRange,

    /// <summary>
    /// A mathematical function has no defined real-valued result for the given argument.
    /// </summary>
    UndefinedMathResult,

    /// <summary>
    /// A logical negation was applied directly to a comparison expression without explicit parentheses,
    /// violating the configured requirement for unambiguous parenthesization.
    /// </summary>
    AmbiguousLogicalNegation,
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
/// A malformed expression or evaluation error with a structured diagnostic.
/// </summary>
/// <param name="diagnostic">The structured diagnostic describing the failure.</param>
/// <param name="innerException">The optional underlying exception that caused the error.</param>
public sealed class ExpressionException(EvaluationDiagnostic diagnostic, Exception? innerException = null)
    : NumeraException(diagnostic, innerException);
