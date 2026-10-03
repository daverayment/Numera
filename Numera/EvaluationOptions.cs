namespace Numera;

/// <summary>
/// Per-evaluation limits that can tighten, but not raise, the engine's safety ceilings.
/// </summary>
public sealed record EvaluationOptions
{
    public int MaxInputLength { get; init; } = CalculationLimits.MaxInputLength;

    public int MaxNestingDepth { get; init; } = CalculationLimits.MaxNestingDepth;

    internal void Validate()
    {
        if (MaxInputLength is < 1 or > CalculationLimits.MaxInputLength)
        {
            throw new ArgumentOutOfRangeException(nameof(MaxInputLength));
        }

        if (MaxNestingDepth is < 1 or > CalculationLimits.MaxNestingDepth)
        {
            throw new ArgumentOutOfRangeException(nameof(MaxNestingDepth));
        }
    }
}
