namespace Numera;

/// <summary>
/// Hard safety ceilings for expression parsing and bounded numeric operations.
/// </summary>
public static class CalculationLimits
{
    // Bound work on user input before allocating large integers or recursing deeply.
    /// <summary>
    /// The maximum permitted length of an expression string in UTF-16 code units.
    /// </summary>
    public const int MaxInputLength = 4096;

    /// <summary>
    /// The maximum recursion depth permitted during unary and nested expression parsing.
    /// </summary>
    public const int MaxNestingDepth = 128;

    /// <summary>
    /// The maximum bit length allowed for integer operations before rejecting as
    /// exceeding limits.
    /// </summary>
    public const int MaxIntegerBits = 16384;

    /// <summary>
    /// The maximum integer argument permitted for the factorial function.
    /// </summary>
    public const int MaxFactorialArgument = 1000;

    /// <summary>
    /// The maximum absolute magnitude permitted for integer exponents.
    /// </summary>
    public const int MaxExponentMagnitude = 10000;

    /// <summary>
    /// The maximum bit length permitted for integer factorization.
    /// </summary>
    public const int MaxFactorizationBits = 128;
}
