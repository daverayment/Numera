namespace Numera;

/// <summary>
/// Hard safety ceilings used by the bounded numeric implementation.
/// </summary>
public static class CalculationLimits
{
    // Bound work on user input before allocating large integers or recursing deeply.
    public const int MaxInputLength = 4096;
    public const int MaxNestingDepth = 128;
    public const int MaxIntegerBits = 16384;
    public const int MaxFactorialArgument = 1000;
    public const int MaxExponentMagnitude = 10000;
}
