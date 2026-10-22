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

    /// <summary>
    /// When enabled, requires explicit parentheses when logical NOT (<c>!</c> or <c>not</c>) is applied to a
    /// comparison expression (e.g. <c>!(a == b)</c> or <c>not (a &lt; b)</c> instead of <c>not a == b</c>).
    /// Defaults to <c>false</c>.
    /// </summary>
    public bool RequireExplicitLogicalNegation { get; init; }

    /// <summary>
    /// When enabled, permits postfix factorial notation (<c>5!</c>).
    /// Defaults to <c>true</c>.
    /// </summary>
    public bool AllowPostfixFactorial { get; init; } = true;

    /// <summary>
    /// When enabled, permits implicit juxtaposition multiplication (<c>2pi</c>, <c>2(3)</c>).
    /// Defaults to <c>true</c>.
    /// </summary>
    public bool AllowImplicitMultiplication { get; init; } = true;

    /// <summary>
    /// Caller-supplied variables available for lookup during expression parsing and evaluation.
    /// Variable names are looked up case-insensitively.
    /// </summary>
    public IReadOnlyDictionary<string, MathValue>? Variables { get; init; }

    /// <summary>
    /// Gets or initializes the value of the standard <c>ans</c> variable representing the previous calculation result.
    /// When set, populates or merges <c>ans</c> into <see cref="Variables"/>.
    /// </summary>
    public MathValue? PreviousResult
    {
        get => Variables != null && Variables.TryGetValue("ans", out var val) ? val : null;
        init
        {
            if (value.HasValue)
            {
                var dict = new Dictionary<string, MathValue>(StringComparer.OrdinalIgnoreCase);
                if (Variables != null)
                {
                    foreach (var (k, v) in Variables)
                    {
                        dict[k] = v;
                    }
                }

                dict["ans"] = value.Value;
                Variables = dict;
            }
        }
    }

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
