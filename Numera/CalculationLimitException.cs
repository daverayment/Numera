namespace Numera;

/// <summary>
/// Indicates that evaluation would exceed a computational safety ceiling.
/// </summary>
public sealed class CalculationLimitException : InvalidOperationException
{
    public CalculationLimitException()
        : base("The expression exceeds the calculator's computation limits.")
    {
    }
}
