using System.Globalization;

namespace Numera;

/// <summary>
/// Long-lived, immutable configuration for a <see cref="CalculatorEngine"/>. It
/// deliberately holds no per-evaluation state (variables, previous result): that lives in
/// <see cref="EvaluationContext"/>, so one engine can be shared across threads and
/// sessions.
/// </summary>
public sealed record CalculatorOptions
{
    /// <summary>
    /// Creates default options capturing the current culture, with radians and standard
    /// human-input normalization enabled. Each access returns a new immutable instance.
    /// </summary>
    public static CalculatorOptions Default => new();

    // Culture and Environment.

    /// <summary>
    /// The culture used both to interpret typed input (decimal and grouping separators,
    /// argument delimiters) and to format results. Sharing one culture for both
    /// directions avoids culture-related changes when reading displayed text. Captures
    /// <see cref="CultureInfo.CurrentCulture"/> when the options are created. Set
    /// <see cref="CultureInfo.InvariantCulture"/> explicitly for machine-authored input.
    /// </summary>
    public CultureInfo Culture { get; init; } = CultureInfo.CurrentCulture;

    /// <summary>
    /// The angular measurement unit applied to trigonometric functions. Defaults to
    /// <see cref="AngleUnit.Radians"/> and is never changed implicitly, because pasted
    /// formulae assume radians.
    /// </summary>
    public AngleUnit AngleUnit { get; init; } = AngleUnit.Radians;

    /// <summary>
    /// Configuration for human-input normalization (launcher equals, Unicode operators,
    /// division colons, culture-specific number formatting). When <c>null</c>, input is
    /// passed to the parser untouched, which suits serialized or machine-generated
    /// expressions. Normalization reads numbers using <see cref="Culture"/>, so a non-
    /// invariant culture changes how <c>1.234,5</c> is interpreted. Defaults to
    /// <see cref="NormalizationOptions.Default"/>.
    /// </summary>
    public NormalizationOptions? Normalization { get; init; } = NormalizationOptions.Default;

    // Language and grammar switches.

    /// <summary>
    /// When enabled, permits implicit juxtaposition multiplication (<c>2pi</c>,
    /// <c>2(3)</c>). Defaults to <c>true</c>.
    /// </summary>
    public bool AllowImplicitMultiplication { get; init; } = true;

    /// <summary>
    /// When enabled, permits postfix factorial notation (<c>5!</c>).
    /// Defaults to <c>true</c>.
    /// </summary>
    public bool AllowPostfixFactorial { get; init; } = true;

    /// <summary>
    /// When enabled, requires explicit parentheses when logical NOT (<c>!</c> or
    /// <c>not</c>) is applied to a comparison expression (e.g. <c>!(a == b)</c> or
    /// <c>not (a &lt; b)</c> instead of <c>not a == b</c>). Defaults to <c>false</c>.
    /// </summary>
    public bool RequireExplicitLogicalNegation { get; init; }

    /// <summary>
    /// Custom functions available to expressions evaluated with this options instance.
    /// Defaults to an empty collection.
    /// </summary>
    public CustomFunctionCollection CustomFunctions { get; init; } = CustomFunctionCollection.Empty;

    // Tunable safety limits.

    /// <summary>
    /// Maximum input length in UTF-16 code units, including whitespace. Must be between 1
    /// and <see cref="CalculationLimits.MaxInputLength"/>; callers can lower the ceiling
    /// but not raise it.
    /// </summary>
    public int MaxInputLength { get; init; } = CalculationLimits.MaxInputLength;

    /// <summary>
    /// Maximum active unary-parser depth, including recursion through grouping, function
    /// arguments, unary signs, and powers; not just bracket nesting. Must be between 1
    /// and <see cref="CalculationLimits.MaxNestingDepth"/>.
    /// </summary>
    public int MaxNestingDepth { get; init; } = CalculationLimits.MaxNestingDepth;

    /// <summary>
    /// Maximum stored integer magnitude in bits, including sign. Must be between 1 and
    /// <see cref="CalculationLimits.MaxIntegerBits"/>.
    /// </summary>
    public int MaxIntegerBits { get; init; } = CalculationLimits.MaxIntegerBits;

    /// <summary>
    /// Maximum absolute integer exponent, including sign. Must be between 1 and
    /// <see cref="CalculationLimits.MaxExponentMagnitude"/>.
    /// </summary>
    public int MaxExponentMagnitude { get; init; } = CalculationLimits.MaxExponentMagnitude;

    // Formatting defaults for the engine.

    /// <summary>
    /// Default formatting options applied when formatting results with this engine.
    /// Defaults to <see cref="ResultFormatOptions.Default"/>.
    /// </summary>
    public ResultFormatOptions ResultFormat { get; init; } = ResultFormatOptions.Default;

    /// <summary>
    /// Validates the options against hard safety ceilings and enum ranges.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown if <see cref="Culture"/> or
    /// <see cref="CustomFunctions"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if any limit or setting is
    /// outside its valid range.</exception>
    public void Validate()
    {
        ArgumentNullException.ThrowIfNull(Culture);
        ArgumentNullException.ThrowIfNull(CustomFunctions);

        if (!Enum.IsDefined(AngleUnit))
        {
            throw new ArgumentOutOfRangeException(nameof(AngleUnit), "Invalid angle unit.");
        }

        if (MaxInputLength < 1 || MaxInputLength > CalculationLimits.MaxInputLength)
        {
            throw new ArgumentOutOfRangeException(
                nameof(MaxInputLength),
                $"Maximum input length must be between 1 and {CalculationLimits.MaxInputLength}.");
        }

        if (MaxNestingDepth < 1 || MaxNestingDepth > CalculationLimits.MaxNestingDepth)
        {
            throw new ArgumentOutOfRangeException(
                nameof(MaxNestingDepth),
                $"Maximum nesting depth must be between 1 and {CalculationLimits.MaxNestingDepth}.");
        }

        if (MaxIntegerBits < 1 || MaxIntegerBits > CalculationLimits.MaxIntegerBits)
        {
            throw new ArgumentOutOfRangeException(
                nameof(MaxIntegerBits),
                $"Maximum integer bits must be between 1 and {CalculationLimits.MaxIntegerBits}.");
        }

        if (MaxExponentMagnitude < 1 || MaxExponentMagnitude > CalculationLimits.MaxExponentMagnitude)
        {
            throw new ArgumentOutOfRangeException(
                nameof(MaxExponentMagnitude),
                $"Maximum exponent magnitude must be between 1 and {CalculationLimits.MaxExponentMagnitude}.");
        }

        ResultFormat?.Validate();
    }

    /// <summary>
    /// Creates default options for the specified culture, so numbers typed and displayed
    /// follow that culture's conventions. Use <c>with</c> to adjust other settings
    /// afterwards.
    /// </summary>
    /// <param name="culture">The culture used to read input and format results.</param>
    /// <returns>Options identical to <see cref="Default"/> except for
    /// <see cref="Culture"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="culture"/> is
    /// null.</exception>
    public static CalculatorOptions ForCulture(CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(culture);
        return Default with { Culture = culture };
    }

    /// <summary>
    /// Creates default options for the culture of the calling thread at the moment of the
    /// call. The culture is captured, not tracked: if the user changes culture later,
    /// build new options (and a new <see cref="CalculatorEngine"/>) rather than expecting
    /// existing ones to follow.
    /// </summary>
    /// <returns>Options identical to <see cref="Default"/> except for
    /// <see cref="Culture"/>.</returns>
    public static CalculatorOptions ForCurrentCulture() =>
        ForCulture(CultureInfo.CurrentCulture);
}
