namespace Numera;

/// <summary>
/// Controls numeric result notation.
/// </summary>
public enum NumericNotation
{
    /// <summary>
    /// Uses fixed notation within the configured rounded exponent interval, for example
    /// "123.456" or "0.00123". Uses scientific notation outside that interval, for
    /// example "1.23456e+5" or "1.23e-6".
    /// </summary>
    Auto,

    /// <summary>
    /// Uses fixed decimal notation, for example "123.456" or "0.00123". Does not use
    /// scientific notation.
    /// </summary>
    /// <remarks>
    /// Be aware that fixed notation can produce very long strings for very large or very
    /// small numbers, which may be truncated by the output medium. Use <see cref="Auto"/>
    /// to automatically switch to scientific notation for large or small numbers.
    /// </remarks>
    Fixed,

    /// <summary>
    /// Uses a significand and a power of ten, for example "1.23456e+5" or "1.23e-6".
    /// </summary>
    Scientific,
}
