namespace Numera;

/// <summary>
/// Describes the trusted calculation provenance, independently of
/// representation.
/// </summary>
public enum CalculationAccuracy
{
    /// <summary>The calculation's accuracy has not been established.</summary>
    Unverified = 0,

    /// <summary>The value is known to be the precise result of the calculation.</summary>
    Exact,
}
