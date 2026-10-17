using System.Diagnostics.CodeAnalysis;

namespace Numera;

/// <summary>
/// Shares function argument-count rules between parser validation and public metadata,
/// backed directly by the central <see cref="FunctionCatalog"/> so definitions never
/// drift.
/// </summary>
internal static class FunctionArity
{
    internal static bool TryGet(string name, [NotNullWhen(true)] out FunctionSignature? signature)
    {
        if (FunctionCatalog.TryGetInfo(name, out var info))
        {
            signature = info.Signature;
            return true;
        }

        signature = null;
        return false;
    }

    internal static bool TryGet(ReadOnlySpan<char> name, [NotNullWhen(true)] out FunctionSignature? signature)
    {
        if (FunctionCatalog.TryGetInfo(name, out var info))
        {
            signature = info.Signature;
            return true;
        }

        signature = null;
        return false;
    }
}
