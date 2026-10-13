namespace Numera;

/// <summary>
/// Indicates that evaluation would exceed a computational safety ceiling.
/// </summary>
public sealed class CalculationLimitException : InvalidOperationException
{
    /// <summary>
    /// Initializes a new instance of <see cref="CalculationLimitException"/> with the
    /// default safety limit message.
    /// </summary>
    public CalculationLimitException()
        : base("The expression exceeds the calculator's computation limits.")
    {
    }
}
