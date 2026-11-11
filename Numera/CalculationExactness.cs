namespace Numera;

/// <summary>
/// Describes whether a value is known or asserted to be exact, independently of storage
/// and display rounding.
/// </summary>
/// <remarks>
/// Use this status for UI guidance and to recognize calculations that encountered an
/// unverified input or operation. It is not an error estimate or a record of each step.
/// For example, 1/3 is Exact even when its decimal display rounds; ln(1) is Unverified
/// even though its floating-point result displays as 0 without rounding.
/// </remarks>
public enum CalculationExactness
{
    /// <summary>
    /// Exactness has not been established. This does not imply the value is wrong:
    /// an ordinary Double containing 0.5 is Unverified despite representing 1/2 exactly.
    /// NaN and infinities also have this status.
    /// </summary>
    Unverified = 0,

    /// <summary>
    /// The finite value is known or asserted by the caller to be exact relative to its
    /// supplied inputs. It can still need a rounded display, as with 1/3.
    /// </summary>
    Exact
}