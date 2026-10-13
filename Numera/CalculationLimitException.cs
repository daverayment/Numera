namespace Numera;

/// <summary>
/// Indicates that evaluation would exceed a computational safety ceiling.
/// </summary>
/// <param name="diagnostic">The optional diagnostic describing the ceiling failure.</param>
public sealed class CalculationLimitException(EvaluationDiagnostic? diagnostic = null)
    : NumeraException(diagnostic ?? new EvaluationDiagnostic(
        EvaluationError.LimitExceeded,
        "The expression exceeds the calculator's computation limits."))
{
    /// <summary>
    /// Initializes a new instance of <see cref="CalculationLimitException"/> with the
    /// default safety limit message.
    /// </summary>
    public CalculationLimitException()
        : this(null)
    {
    }
}
