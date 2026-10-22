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
    /// Caller-supplied variables available for lookup during expression parsing and
    /// evaluation. Variable names are looked up case-insensitively.
    /// </summary>
    public IReadOnlyDictionary<string, MathValue>? Variables { get; init; }

    /// <summary>
    /// Gets or initializes the value of the standard <c>ans</c> variable, representing
    /// the previous calculation result.
    /// </summary>
    public MathValue? PreviousResult { get; init; }

    /// <summary>
    /// Checks whether a variable with the given name is defined in the current evaluation
    /// context, or is the special <c>ans</c> variable with a defined previous result.
    /// </summary>
    /// <param name="name">The name of the variable to check.</param>
    /// <returns><c>true</c> if the variable is defined; otherwise, <c>false</c>.</returns>
    internal bool IsVariableDefined(string name)
    {
        if (name.Equals("ans", StringComparison.OrdinalIgnoreCase) && PreviousResult.HasValue)
        {
            return true;
        }

        return Variables != null && Variables.ContainsKey(name);
    }

    /// <summary>
    /// Attempts to resolve an identifier to its corresponding <see cref="MathValue"/> in
    /// the current evaluation context. Custom variables take precedence over the special
    /// <c>ans</c> variable.
    /// </summary>
    /// <param name="name">The name of the variable to resolve.</param>
    /// <param name="value">When this method returns, contains the <see cref="MathValue"/>
    /// associated with the specified name, if the name is found; otherwise, the default
    /// value for <see cref="MathValue"/>.</param>
    /// <returns><c>true</c> if the variable was successfully resolved; otherwise,
    /// <c>false</c>.</returns>
    internal bool TryResolveVariable(string name, out MathValue value)
    {
        if (Variables != null && Variables.TryGetValue(name, out value))
        {
            return true;
        }

        if (name.Equals("ans", StringComparison.OrdinalIgnoreCase) &&
            PreviousResult.HasValue)
        {
            value = PreviousResult.Value;
            return true;
        }

        value = default;
        return false;
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
