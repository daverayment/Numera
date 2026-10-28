namespace Numera;

/// <summary>
/// Per-evaluation state: caller variables and the previous result (<c>ans</c>). It is
/// separate from <see cref="CalculatorOptions"/> so a long-lived, shared engine never
/// carries session state.
/// </summary>
/// <remarks>
/// <para>
/// Instances are immutable and safe to share across threads and calls. Immutability is
/// enforced by the type system: variables are held in an immutable <see cref="VariableCollection"/>,
/// so even a caller who casts <see cref="Variables"/> back to a mutable interface cannot
/// modify them.
/// </para>
/// <para>
/// Variable names are case-insensitive (ordinal). A variable named <c>ans</c> takes
/// precedence over <see cref="PreviousResult"/>.
/// </para>
/// <para>
/// Equality is by value: two contexts are equal if they define the same variables (names
/// compared case-insensitively) and the same previous result.
/// </para>
/// <code>
/// var context = new EvaluationContext(
///     new Dictionary&lt;string, MathValue&gt; { ["x"] = new(3m) },
///     new MathValue(10m));
/// MathValue result = Calculator.Evaluate("x * ans", context);   // 30
/// </code>
/// </remarks>
public sealed record EvaluationContext
{
    /// <summary>
    /// The string comparer used for variable names, forwarding to <see cref="Identifier.Comparer"/>.
    /// </summary>
    public static StringComparer NameComparer => Identifier.Comparer;

    /// <summary>
    /// The name of the standard variable that refers to the previous calculation result.
    /// </summary>
    public const string PreviousResultVariableName = "ans";

    /// <summary>
    /// A shared context with no variables and no previous result; the same instance is
    /// returned every time, which is safe because contexts are immutable.
    /// </summary>
    public static EvaluationContext Empty { get; } = new(null, null);

    /// <summary>
    /// The variables currently assigned to values. Lookups are case-insensitive. The
    /// returned collection is read-only.
    /// </summary>
    public VariableCollection Variables { get; init; } = VariableCollection.Empty;

    /// <summary>
    /// The value of the standard <c>ans</c> variable: the previous calculation result.
    /// <c>null</c> means <c>ans</c> is undefined, so using it reports an unknown-constant
    /// error rather than silently evaluating as zero.
    /// </summary>
    public MathValue? PreviousResult { get; init; }

    /// <summary>
    /// Initializes a new instance of the <see cref="EvaluationContext"/> class.
    /// </summary>
    public EvaluationContext() { }

    /// <summary>
    /// Initializes a new instance of the <see cref="EvaluationContext"/> class with the
    /// given variables and previous result.
    /// </summary>
    /// <param name="variables">The variables to include in the context.</param>
    /// <param name="previousResult">The previous result to include in the context.
    /// </param>
    public EvaluationContext(VariableCollection? variables, MathValue? previousResult)
    {
        Variables = variables ?? VariableCollection.Empty;
        PreviousResult = previousResult;
    }

    /// <summary>
    /// Initializes a new evaluation context from a sequence of deconstructed variables
    /// and an optional previous result.
    /// </summary>
    /// <remarks>The variable sequence is copied into a <see cref="VariableCollection"/>
    /// before initialization.</remarks>
    /// <param name="variables">Named values available during evaluation. If
    /// <see langword="null"/>, no variables are provided.</param>
    /// <param name="previousResult">Result from a previous evaluation that can be reused
    /// by subsequent expressions.</param>
    public EvaluationContext(IEnumerable<KeyValuePair<string, MathValue>>? variables,
        MathValue? previousResult = null)
        : this(variables is null ? null : new VariableCollection(variables), previousResult)
    {
    }

    /// <summary>
    /// Resolves an identifier to a value. Custom variables take precedence over
    /// <c>ans</c>.
    /// </summary>
    /// <param name="name">The identifier to resolve (case-insensitive).</param>
    /// <param name="value">The resolved value, or <c>default</c> if not found.</param>
    /// <returns><c>true</c> if the name resolved to a variable or <c>ans</c>.</returns>
    internal bool TryResolveVariable(string name, out MathValue value)
    {
        if (Variables.TryGetValue(name, out value))
        {
            return true;
        }

        if (PreviousResult.HasValue &&
            NameComparer.Equals(name, PreviousResultVariableName))
        {
            value = PreviousResult.Value;
            return true;
        }

        value = default;
        return false;
    }

    internal EvaluationContext Advance(
        IReadOnlyList<KeyValuePair<string, MathValue>>? assignments,
        MathValue result)
    {
        // If there are no assignments, we can reuse the existing variables dictionary.
        if (assignments is null || assignments.Count == 0)
        {
            return this with { PreviousResult = result };
        }

        // Add/update the assigned variables, and return a new context with the updated
        // variables and previous result.
        return new EvaluationContext(Variables.SetItems(assignments), result);
    }

    /// <summary>
    /// Value equality. The compiler-generated record equality would compare the variables
    /// dictionary by reference, making two contexts with identical variables unequal, so
    /// it is replaced with a comparison of the contents.
    /// </summary>
    /// <param name="other">The context to compare with.</param>
    /// <returns><c>true</c> if both contexts have equal previous results and the same
    /// variables with equal values.</returns>
    public bool Equals(EvaluationContext? other)
    {
        if (ReferenceEquals(this, other))
        {
            return true;
        }

        if (other is null ||
            !EqualityComparer<MathValue?>.Default.Equals(PreviousResult, other.PreviousResult))
        {
            return false;
        }

        // Variable collections are immutable, so sharing one instance means equal variables.
        if (ReferenceEquals(Variables, other.Variables))
        {
            return true;
        }

        // A null and an empty variable set both mean "no variables".
        int count = Variables?.Count ?? 0;
        if (count != (other.Variables?.Count ?? 0))
        {
            return false;
        }

        if (count == 0)
        {
            return true;
        }

        foreach (var kvp in Variables!)
        {
            if (!other.Variables!.TryGetValue(kvp.Key, out MathValue otherValue) ||
                !EqualityComparer<MathValue>.Default.Equals(kvp.Value, otherValue))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Hash code consistent with <see cref="Equals(EvaluationContext?)"/>. Variable
    /// entries are combined with an order-independent sum, because equal contexts may
    /// enumerate their variables in different orders.
    /// </summary>
    /// <returns>A hash code for this context.</returns>
    public override int GetHashCode()
    {
        int variablesHash = 0;
        if (Variables is not null)
        {
            foreach (var kvp in Variables)
            {
                variablesHash += HashCode.Combine(NameComparer.GetHashCode(kvp.Key), kvp.Value);
            }
        }

        return HashCode.Combine(PreviousResult, variablesHash);
    }
}
