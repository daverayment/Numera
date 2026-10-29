using System.Diagnostics.CodeAnalysis;

namespace Numera;

/// <summary>
/// Shares function argument-count rules between parser validation and public metadata,
/// backed directly by the central <see cref="FunctionCatalog"/> and configured custom functions
/// so definitions never drift.
/// </summary>
internal static class FunctionArity
{
    internal static bool TryGet(string name, [NotNullWhen(true)] out FunctionSignature? signature) =>
        TryGet(name, customFunctions: null, out signature);

    internal static bool TryGet(string name, CustomFunctionCollection? customFunctions, [NotNullWhen(true)] out FunctionSignature? signature)
    {
        if (FunctionCatalog.TryGetInfo(name, out var info))
        {
            signature = info.Signature;
            return true;
        }

        if (customFunctions is not null && customFunctions.TryGet(name, out var custom))
        {
            signature = custom.Signature;
            return true;
        }

        signature = null;
        return false;
    }

    internal static bool TryGet(ReadOnlySpan<char> name, [NotNullWhen(true)] out FunctionSignature? signature) =>
        TryGet(name, customFunctions: null, out signature);

    internal static bool TryGet(ReadOnlySpan<char> name, CustomFunctionCollection? customFunctions, [NotNullWhen(true)] out FunctionSignature? signature)
    {
        if (FunctionCatalog.TryGetInfo(name, out var info))
        {
            signature = info.Signature;
            return true;
        }

        if (customFunctions is not null && customFunctions.TryGet(name, out var custom))
        {
            signature = custom.Signature;
            return true;
        }

        signature = null;
        return false;
    }
}
