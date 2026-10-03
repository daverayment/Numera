namespace Numera;

/// <summary>
/// Supported function arity. A null maximum denotes a variadic function.
/// </summary>
public readonly record struct FunctionSignature(int MinimumArguments, int? MaximumArguments)
{
    public bool AllowsMultipleArguments => MaximumArguments is null or > 1;
}

/// <summary>
/// Read-only metadata for adapters that need to recognize function argument separators.
/// </summary>
public static class Functions
{
    public static bool TryGetSignature(string name, out FunctionSignature signature)
    {
        ArgumentNullException.ThrowIfNull(name);
        return FunctionArity.TryGet(name, out signature);
    }
}
