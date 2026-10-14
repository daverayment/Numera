namespace Numera;

/// <summary>
/// Per-evaluation limits that can tighten, but not raise, the engine's safety ceilings.
/// </summary>
public sealed record EvaluationOptions
{
    /// <summary>
    /// Maximum input length in UTF-16 code units, including whitespace.
    /// </summary>
    public int MaxInputLength { get; init; } = CalculationLimits.MaxInputLength;

    /// <summary>
    /// Maximum active unary-parser depth, including recursion through grouping,
    /// function arguments, unary signs, and powers; not just bracket nesting.
    /// </summary>
    public int MaxNestingDepth { get; init; } = CalculationLimits.MaxNestingDepth;

    /// <summary>
    /// The angular measurement unit applied to trigonometric functions.
    /// Defaults to <see cref="AngleUnit.Radians"/>.
    /// </summary>
    public AngleUnit AngleUnit { get; init; } = AngleUnit.Radians;

    internal void Validate()
    {
        if (!Enum.IsDefined(AngleUnit))
        {
            throw new ArgumentOutOfRangeException(nameof(AngleUnit));
        }

        // Options may reduce work for a caller, but cannot bypass the engine's ceilings.
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
