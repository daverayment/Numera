namespace Numera;

/// <summary>
/// Shares function argument-count rules between parser validation and public metadata,
/// so adapters do not need a separate list of supported function signatures.
/// </summary>
internal static class FunctionArity
{
    // A null maximum allows variadic calls while the minimum still rejects empty calls.
    private static readonly Dictionary<string, (int Minimum, int? Maximum)> ArgumentRanges = new(StringComparer.OrdinalIgnoreCase)
    {
        { "ceil", (1, 1) },
        { "floor", (1, 1) },
        { "exp", (1, 1) },
        { "max", (1, null) },
        { "min", (1, null) },
        { "abs", (1, 1) },
        { "log", (1, 1) },
        { "log2", (1, 1) },
        { "log10", (1, 1) },
        { "logn", (2, 2) },
        { "ln", (1, 1) },
        { "sqrt", (1, 1) },
        { "pow", (2, 2) },
        { "root", (2, 2) },
        { "factorial", (1, 1) },
        { "sign", (1, 1) },
        { "sgn", (1, 1) },
        { "round", (1, 1) },
        { "rand", (0, 0) },
        { "randi", (1, 1) },
        { "sin", (1, 1) },
        { "cos", (1, 1) },
        { "tan", (1, 1) },
        { "arcsin", (1, 1) },
        { "arccos", (1, 1) },
        { "arctan", (1, 1) },
        { "cot", (1, 1) },
        { "sec", (1, 1) },
        { "csc", (1, 1) },
        { "arccot", (1, 1) },
        { "arcsec", (1, 1) },
        { "arccsc", (1, 1) },
        { "sinh", (1, 1) },
        { "cosh", (1, 1) },
        { "tanh", (1, 1) },
        { "arsinh", (1, 1) },
        { "arcosh", (1, 1) },
        { "artanh", (1, 1) },
        { "coth", (1, 1) },
        { "sech", (1, 1) },
        { "csch", (1, 1) },
        { "arcoth", (1, 1) },
        { "arsech", (1, 1) },
        { "arcsch", (1, 1) },
        { "rad", (1, 1) },
        { "deg", (1, 1) },
        { "grad", (1, 1) },
        { "deg2rad", (1, 1) },
        { "rad2deg", (1, 1) },
        { "grad2rad", (1, 1) },
        { "rad2grad", (1, 1) },
        { "deg2grad", (1, 1) },
        { "grad2deg", (1, 1) },
        { "atan2", (2, 2) },
        { "hypot", (2, 2) },
        { "clamp", (3, 3) },
        { "avg", (1, null) },
        { "trunc", (1, 1) },
        { "frac", (1, 1) },
        { "cbrt", (1, 1) },
        { "lerp", (3, 3) },
        { "shl", (2, 2) },
        { "shr", (2, 2) },
        { "band", (2, 2) },
        { "bor", (2, 2) },
        { "bxor", (2, 2) },
        { "bnot", (1, 1) },
    };

    internal static bool TryGet(string name, out FunctionSignature signature)
    {
        if (ArgumentRanges.TryGetValue(name, out var range))
        {
            signature = new FunctionSignature(range.Minimum, range.Maximum);
            return true;
        }

        signature = default;
        return false;
    }

    internal static bool TryGet(ReadOnlySpan<char> name, out FunctionSignature signature)
    {
        if (ArgumentRanges.GetAlternateLookup<ReadOnlySpan<char>>().TryGetValue(name, out var range))
        {
            signature = new FunctionSignature(range.Minimum, range.Maximum);
            return true;
        }

        signature = default;
        return false;
    }
}
