namespace Numera;

/// <summary>
/// Inclusive argument-count bounds for a supported function. A null maximum denotes
/// a variadic function; the minimum still applies.
/// </summary>
public readonly record struct FunctionSignature(int MinimumArguments, int? MaximumArguments)
{
    /// <summary>
    /// Whether the signature permits more than one argument, not whether it requires them.
    /// </summary>
    public bool AllowsMultipleArguments => MaximumArguments is null or > 1;
}

/// <summary>
/// Read-only function arity metadata. Adapters can use it when interpreting argument
/// separators; this API does not itself normalize expression syntax.
/// </summary>
public static class Functions
{
    /// <summary>
    /// Looks up a supported function name using ordinal, case-insensitive matching.
    /// Returns false for an unknown name; a null name is an invalid API argument.
    /// </summary>
    public static bool TryGetSignature(string name, out FunctionSignature signature)
    {
        ArgumentNullException.ThrowIfNull(name);
        return FunctionArity.TryGet(name, out signature);
    }
}
