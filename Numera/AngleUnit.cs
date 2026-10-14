namespace Numera;

/// <summary>
/// Specifies the angular measurement unit used when evaluating trigonometric functions.
/// </summary>
public enum AngleUnit
{
    /// <summary>
    /// Angles are measured in radians. This is the default mathematical standard.
    /// </summary>
    Radians,

    /// <summary>
    /// Angles are measured in degrees (360 degrees in a full turn).
    /// Exact cardinal angles (such as 0°, 90°, 180°, 270°) are computed accurately
    /// using trigonometric pi-scaled primitives.
    /// </summary>
    Degrees,

    /// <summary>
    /// Angles are measured in gradians (400 gradians in a full turn).
    /// </summary>
    Gradians,
}
