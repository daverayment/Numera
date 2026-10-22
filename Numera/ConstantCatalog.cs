namespace Numera;

/// <summary>
/// Identifies recognized mathematical constants supported by Numera parsers, evaluators, and formatters.
/// </summary>
public enum ConstantKind
{
    /// <summary>Euler's number e (approx. 2.718281828459045).</summary>
    E,

    /// <summary>Archimedes' constant pi (approx. 3.141592653589793).</summary>
    Pi,

    /// <summary>The circle constant tau (2*pi, approx. 6.283185307179586).</summary>
    Tau,

    /// <summary>The golden ratio phi (approx. 1.618033988749895).</summary>
    Phi,

    /// <summary>Boolean true literal (1m).</summary>
    True,

    /// <summary>Boolean false literal (0m).</summary>
    False,
}

/// <summary>
/// Central registry and resolution for recognized mathematical constants.
/// Constants are resolved as <see cref="decimal"/> to provide up to 28-29 digits of precision
/// for direct inspection and linear/decimal arithmetic; callers handling transcendental or
/// trigonometry functions convert them down to <see cref="double"/> as required.
/// </summary>
internal static class ConstantCatalog
{
    // High-precision decimal constant definitions (~28-29 significant digits).
    private const decimal EValue = 2.7182818284590452353602874714m;
    private const decimal PiValue = 3.1415926535897932384626433833m;
    private const decimal TauValue = 6.2831853071795864769252867666m;
    private const decimal PhiValue = 1.6180339887498948482045868344m;

    /// <summary>
    /// Determines whether the specified character is a recognized non-ASCII mathematical constant symbol (e.g. Greek letters).
    /// </summary>
    internal static bool IsConstantLetter(char c) =>
        c is '\u03C0' or '\u03A0' or           // π, Π
             '\u03C4' or '\u03A4' or           // τ, Τ
             '\u03D5' or '\u03C6' or '\u03A6'; // ϕ (symbol), φ (small), Φ (capital)

    internal static bool TryResolve(ReadOnlySpan<char> name, out ConstantKind kind, out decimal value)
    {
        switch (name.Length)
        {
            case 1:
                switch (name[0])
                {
                    case 'e' or 'E':
                        kind = ConstantKind.E;
                        value = EValue;
                        return true;
                    case '\u03C0' or '\u03A0': // π, Π
                        kind = ConstantKind.Pi;
                        value = PiValue;
                        return true;
                    case '\u03C4' or '\u03A4': // τ, Τ
                        kind = ConstantKind.Tau;
                        value = TauValue;
                        return true;
                    case '\u03D5' or '\u03C6' or '\u03A6': // ϕ (symbol), φ (small), Φ (capital)
                        kind = ConstantKind.Phi;
                        value = PhiValue;
                        return true;
                }
                break;

            case 2 when name.Equals("pi", StringComparison.OrdinalIgnoreCase):
                kind = ConstantKind.Pi;
                value = PiValue;
                return true;

            case 3 when name.Equals("tau", StringComparison.OrdinalIgnoreCase):
                kind = ConstantKind.Tau;
                value = TauValue;
                return true;

            case 3 when name.Equals("phi", StringComparison.OrdinalIgnoreCase):
                kind = ConstantKind.Phi;
                value = PhiValue;
                return true;

            case 4 when name.Equals("true", StringComparison.OrdinalIgnoreCase):
                kind = ConstantKind.True;
                value = 1m;
                return true;

            case 5 when name.Equals("false", StringComparison.OrdinalIgnoreCase):
                kind = ConstantKind.False;
                value = 0m;
                return true;
        }

        kind = default;
        value = 0m;
        return false;
    }
}
