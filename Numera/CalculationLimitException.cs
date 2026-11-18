namespace Numera;

/// <summary>
/// Identifies a computational safety ceiling independent of diagnostic message text.
/// </summary>
public enum CalculationLimitKind
{
    /// <summary>
    /// Expression length in UTF-16 code units.
    /// </summary>
    InputLength,

    /// <summary>
    /// Parser or expression-tree nesting depth.
    /// </summary>
    NestingDepth,

    /// <summary>
    /// Number of expression-tree node visits, including shared subtrees.
    /// </summary>
    AstNodeCount,

    /// <summary>
    /// Available execution stack; no numeric ceiling is exposed.
    /// </summary>
    ExecutionStack,

    /// <summary>
    /// Absolute-magnitude bits per integer or reduced fraction component.
    /// </summary>
    IntegerBits,

    /// <summary>
    /// Absolute-magnitude bits per intermediate arithmetic value.
    /// </summary>
    IntermediateBits,

    /// <summary>
    /// Argument to the factorial function.
    /// </summary>
    FactorialArgument,

    /// <summary>
    /// Number of terms processed by combinations or permutations.
    /// </summary>
    CombinatoricsTerms,

    /// <summary>
    /// Absolute exponent magnitude.
    /// </summary>
    ExponentMagnitude,

    /// <summary>
    /// Input magnitude in bits for primality testing.
    /// </summary>
    PrimalityBits,

    /// <summary>
    /// Input magnitude in bits for factorization.
    /// </summary>
    FactorizationBits,

    /// <summary>
    /// Number of bounded factorization search attempts.
    /// </summary>
    FactorizationAttempts,

    /// <summary>
    /// Requested digits in an explicit numeric format.
    /// </summary>
    FormattingDigits,

    /// <summary>
    /// Serialized value length in UTF-16 code units.
    /// </summary>
    SerializedValueCharacters,

    /// <summary>
    /// Serialized value length in UTF-8 bytes.
    /// </summary>
    SerializedValueBytes
}

/// <summary>
/// Indicates that evaluation would exceed a computational safety ceiling.
/// </summary>
/// <param name="diagnostic">The optional diagnostic describing the ceiling failure.
/// </param>
public sealed class CalculationLimitException(EvaluationDiagnostic? diagnostic = null)
    : NumeraException(diagnostic ?? new EvaluationDiagnostic(
        EvaluationError.LimitExceeded,
        "The expression exceeds the calculator's computation limits."))
{
    /// <summary>
    /// Initializes a new instance of <see cref="CalculationLimitException"/> with the
    /// default safety limit message.
    /// </summary>
    public CalculationLimitException()
        : this(null)
    {
    }

    /// <summary>
    /// Initializes a limit failure with a descriptive message and structured metadata.
    /// </summary>
    /// <param name="limitKind">The safety ceiling that rejected the operation.</param>
    /// <param name="limitValue">The effective ceiling, not the rejected value's size.
    /// Omit when there is no numeric ceiling, such as available execution stack.</param>
    public CalculationLimitException(CalculationLimitKind limitKind, long? limitValue = null)
        : this(new EvaluationDiagnostic(EvaluationError.LimitExceeded, CreateMessage(limitKind, limitValue))
        {
            LimitKind = limitKind,
            LimitValue = limitValue
        })
    {
    }

    private static string CreateMessage(CalculationLimitKind limitKind, long? value)
    {
        var (description, unit) = limitKind switch
        {
            CalculationLimitKind.InputLength => ("input length", "UTF-16 code units"),
            CalculationLimitKind.NestingDepth => ("nesting depth", "levels"),
            CalculationLimitKind.AstNodeCount => ("expression-tree node count", "node visits"),
            CalculationLimitKind.ExecutionStack => ("available execution stack", ""),
            CalculationLimitKind.IntegerBits => ("integer or reduced fraction component size", "bits"),
            CalculationLimitKind.IntermediateBits => ("arithmetic intermediate size", "bits"),
            CalculationLimitKind.FactorialArgument => ("factorial argument", ""),
            CalculationLimitKind.CombinatoricsTerms => ("combinations or permutations work", "terms"),
            CalculationLimitKind.ExponentMagnitude => ("exponent magnitude", ""),
            CalculationLimitKind.PrimalityBits => ("primality input size", "bits"),
            CalculationLimitKind.FactorizationBits => ("factorization input size", "bits"),
            CalculationLimitKind.FactorizationAttempts => ("factorization search", "attempts"),
            CalculationLimitKind.FormattingDigits => ("formatting precision", "digits"),
            CalculationLimitKind.SerializedValueCharacters => ("serialized value length", "UTF-16 code units"),
            CalculationLimitKind.SerializedValueBytes => ("serialized value length", "UTF-8 bytes"),
            _ => throw new ArgumentOutOfRangeException(nameof(limitKind), limitKind, "Unknown calculation limit kind.")
        };

        if (value is long ceiling)
        {
            if (unit.Length == 0)
            {
                return $"The {description} limit was exceeded ({ceiling}).";
            }
            return $"The {description} limit was exceeded ({ceiling} {unit}).";
        }

        return $"The {description} limit was exceeded.";
    }
}
