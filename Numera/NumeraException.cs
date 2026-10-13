namespace Numera;

/// <summary>
/// Base exception for all expression evaluation errors produced by Numera.
/// </summary>
/// <remarks>
/// Guarantees that every evaluation error carries a non-null <see cref="Diagnostic"/>
/// with category, message, and optional source location.
/// </remarks>
public abstract class NumeraException(EvaluationDiagnostic diagnostic, Exception? innerException = null)
    : Exception((diagnostic ?? throw new ArgumentNullException(nameof(diagnostic))).Message, innerException)
{
    /// <summary>
    /// Gets the structured diagnostic describing the failure.
    /// </summary>
    public EvaluationDiagnostic Diagnostic { get; } = diagnostic;
}
