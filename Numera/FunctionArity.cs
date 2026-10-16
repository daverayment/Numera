namespace Numera;

/// <summary>
/// Shares function argument-count rules between parser validation and public metadata,
/// so adapters do not need a separate list of supported function signatures.
/// </summary>
internal static class FunctionArity
{
    internal static bool TryGet(string name, out FunctionSignature signature) =>
        TryGet(name.AsSpan(), out signature);

    internal static bool TryGet(ReadOnlySpan<char> name, out FunctionSignature signature)
    {
        // Stack-allocated span conversion avoids heap allocations while ensuring case
        // insensitivity. Longest supported function name is 9 chars (factorial), capped
        // at 16 for now.
        if (name.Length <= 16)
        {
            Span<char> lower = stackalloc char[name.Length];
            name.ToLowerInvariant(lower);

            // Pattern match against string literals: C# compiles this into a zero-
            // allocation jump table.
            (int Min, int? Max)? range = lower switch
            {
                "ceil" or "floor" or "exp" or "expm1" or "abs" or "log" or "log2" or "log10" or "ln" or "log1p"
                    or "sqrt" or "factorial" or "sign" or "sgn" or "randi"
                    or "sin" or "cos" or "tan" or "arcsin" or "arccos" or "arctan"
                    or "asin" or "acos" or "atan"
                    or "cot" or "sec" or "csc" or "arccot" or "arcsec" or "arccsc"
                    or "sinh" or "cosh" or "tanh" or "arsinh" or "arcosh" or "artanh"
                    or "asinh" or "acosh" or "atanh"
                    or "coth" or "sech" or "csch" or "arcoth" or "arsech" or "arcsch"
                    or "rad" or "deg" or "grad" or "deg2rad" or "rad2deg" or "grad2rad"
                    or "rad2grad" or "deg2grad" or "grad2deg" or "trunc" or "frac"
                    or "cbrt" or "bnot"
                    or "erf" or "erfc" or "ncdf" or "sinc" => (1, 1),

                "round" => (1, 2),

                "pow" or "root" or "logn" or "atan2" or "hypot" or "mod"
                    or "shl" or "shr" or "band" or "bor" or "bxor" => (2, 2),

                "clamp" or "lerp" => (3, 3),

                "max" or "min" or "avg" or "sum" or "mul" => (1, null),

                "rand" => (0, 0),

                _ => null,
            };

            if (range is { } r)
            {
                signature = new FunctionSignature(r.Min, r.Max);
                return true;
            }
        }

        signature = default;
        return false;
    }
}
