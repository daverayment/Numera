using System.Globalization;

namespace Numera;

/// <summary>
/// Long-lived, immutable configuration for a <see cref="CalculatorEngine"/>. It deliberately holds no
/// per-evaluation state (variables, previous result): that lives in <see cref="EvaluationContext"/>, so
/// one engine can be shared across threads and sessions.
/// </summary>
public sealed record CalculatorOptions
{
    /// <summary>
    /// The culture used both to interpret typed input (decimal and grouping separators,
    /// argument delimiters) and to format results. Sharing one culture for both
    /// directions is what keeps a displayed result re-parseable. Defaults to
    /// <see cref="CultureInfo.InvariantCulture"/>.
    /// </summary>
    public CultureInfo Culture { get; init; } = CultureInfo.InvariantCulture;

    /// <summary>
    /// Configuration for human-input normalization (launcher equals, Unicode operators,
    /// division colons, culture-specific number formatting). When <c>null</c>, input is
    /// passed to the parser untouched, which suits serialized or machine-generated
    /// expressions. Normalization reads numbers using <see cref="Culture"/>, so a non-
    /// invariant culture changes how <c>1.234,5</c> is interpreted. Defaults to
    /// <see cref="NormalizationOptions.Default"/>.
    /// </summary>
    public NormalizationOptions? Normalization { get; init; } = NormalizationOptions.Default;

    /// <summary>
    /// Maximum input length in UTF-16 code units, including whitespace. Must be between 1
    /// and <see cref="CalculationLimits.MaxInputLength"/>; callers can lower the ceiling
    /// but not raise it. Out-of-range values cause <see cref="CalculatorEngine"/>
    /// construction to throw.
    /// </summary>
    public int MaxInputLength { get; init; } = CalculationLimits.MaxInputLength;

    /// <summary>
    /// Maximum active unary-parser depth, including recursion through grouping, function
    /// arguments, unary signs, and powers; not just bracket nesting. Must be between 1
    /// and <see cref="CalculationLimits.MaxNestingDepth"/>; out-of-range values cause
    /// <see cref="CalculatorEngine"/> construction to throw.
    /// </summary>
    public int MaxNestingDepth { get; init; } = CalculationLimits.MaxNestingDepth;

    /// <summary>
    /// The angular measurement unit applied to trigonometric functions. Defaults to
    /// <see cref="AngleUnit.Radians"/> and is never changed implicitly, because pasted
    /// formulae assume radians.
    /// </summary>
    public AngleUnit AngleUnit { get; init; } = AngleUnit.Radians;

    /// <summary>
    /// When enabled, requires explicit parentheses when logical NOT (<c>!</c> or
    /// <c>not</c>) is applied to a comparison expression (e.g. <c>!(a == b)</c> or
    /// <c>not (a &lt; b)</c> instead of <c>not a == b</c>). Defaults to <c>false</c>.
    /// </summary>
    public bool RequireExplicitLogicalNegation { get; init; }

    /// <summary>
    /// When enabled, permits postfix factorial notation (<c>5!</c>).
    /// Defaults to <c>true</c>.
    /// </summary>
    public bool AllowPostfixFactorial { get; init; } = true;

    /// <summary>
    /// When enabled, permits implicit juxtaposition multiplication (<c>2pi</c>,
    /// <c>2(3)</c>). Defaults to <c>true</c>.
    /// </summary>
    public bool AllowImplicitMultiplication { get; init; } = true;

    /// <summary>
    /// Custom functions available to expressions evaluated with this options instance.
    /// Defaults to an empty collection.
    /// </summary>
    public CustomFunctionCollection CustomFunctions { get; init; } = CustomFunctionCollection.Empty;

    /// <summary>
    /// Default options: culture-invariant, radians, with standard human-input
    /// normalization enabled. The instance is immutable and shared.
    /// </summary>
    public static CalculatorOptions Default { get; } = new();

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

    // Called once by CalculatorEngine's constructor so evaluation calls need not re-check
    // immutable options.
    internal void Validate()
    {
        if (!Enum.IsDefined(AngleUnit))
        {
            throw new ArgumentOutOfRangeException(nameof(AngleUnit));
        }

        ArgumentNullException.ThrowIfNull(Culture);

        // Options may reduce work for a caller, but cannot bypass the engine's ceilings.
        if (MaxInputLength is < 1 or > CalculationLimits.MaxInputLength)
        {
            throw new ArgumentOutOfRangeException(nameof(MaxInputLength));
        }

        if (MaxNestingDepth is < 1 or > CalculationLimits.MaxNestingDepth)
        {
            throw new ArgumentOutOfRangeException(nameof(MaxNestingDepth));
        }

        ArgumentNullException.ThrowIfNull(CustomFunctions);

        foreach (var func in CustomFunctions)
        {
            if (string.IsNullOrWhiteSpace(func.Name) || !Identifier.IsValidIdentifier(func.Name))
            {
                throw new ArgumentException(
                    $"Custom function name '{func.Name}' is not a valid identifier.", nameof(CustomFunctions));
            }

            // Protect "ans" from being shadowed by a custom function name.
            if (func.Name.Equals(EvaluationContext.PreviousResultVariableName, Identifier.Comparison))
            {
                throw new ArgumentException(
                    $"Custom function name '{func.Name}' cannot shadow the '{EvaluationContext.PreviousResultVariableName}' variable.",
                    nameof(CustomFunctions));
            }

            if (KeywordCatalog.IsReserved(func.Name))
            {
                throw new ArgumentException(
                    $"Custom function name '{func.Name}' cannot shadow reserved keyword.", nameof(CustomFunctions));
            }

            if (ConstantCatalog.TryResolve(func.Name, out _, out _))
            {
                throw new ArgumentException(
                    $"Custom function name '{func.Name}' cannot shadow reserved constant.", nameof(CustomFunctions));
            }

            if (FunctionCatalog.TryGetInfo(func.Name, out _))
            {
                throw new ArgumentException(
                    $"Custom function name '{func.Name}' cannot shadow built-in function.", nameof(CustomFunctions));
            }
        }
    }
}
