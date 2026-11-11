using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;

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
    False
}

/// <summary>
/// Central registry and resolution for recognized mathematical constants.
/// Values use Decimal storage and carry the same exactness for discovery and evaluation.
/// </summary>
internal static class ConstantCatalog
{
    // High-precision decimal constant definitions (~28-29 significant digits).
    private const decimal EValue = 2.7182818284590452353602874714m;
    private const decimal PiValue = 3.1415926535897932384626433833m;
    private const decimal TauValue = 6.2831853071795864769252867666m;
    private const decimal PhiValue = 1.6180339887498948482045868344m;

    private static readonly FrozenDictionary<string, ConstantInfo> InfoByExactName;
    private static readonly FrozenDictionary<string, ConstantInfo> InfoByName;

    internal static IReadOnlyCollection<string> AllNames { get; }
    internal static IReadOnlyCollection<string> CanonicalNames { get; }
    internal static IReadOnlyList<ConstantInfo> All { get; }
    internal static IReadOnlyList<ConstantInfo> Canonical { get; }

    static ConstantCatalog()
    {
        ConstantInfo[] definitions =
        [
            CreateCanonical("e", ConstantKind.E, EValue, "Euler's number, the base of natural logarithms."),
            CreateCanonical("pi", ConstantKind.Pi, PiValue, "The ratio of a circle's circumference to its diameter.", "\u03C0"),
            CreateCanonical("tau", ConstantKind.Tau, TauValue, "The circle constant, equal to twice pi.", "\u03C4"),
            CreateCanonical("phi", ConstantKind.Phi, PhiValue, "The golden ratio.", "\u03C6", "\u03D5"),
            CreateCanonical("true", ConstantKind.True, 1m, "The Boolean true literal, with numeric value one."),
            CreateCanonical("false", ConstantKind.False, 0m, "The Boolean false literal, with numeric value zero.")
        ];
        Canonical = Array.AsReadOnly(definitions.OrderBy(info => info.Name, Identifier.Comparer).ToArray());
        CanonicalNames = Array.AsReadOnly(Canonical.Select(info => info.Name).ToArray());

        var all = new List<ConstantInfo>();
        foreach (var canonical in Canonical)
        {
            all.Add(canonical);
            foreach (var alias in canonical.Aliases)
            {
                all.Add(canonical with { Name = alias, IsAlias = true });
            }
        }

        All = Array.AsReadOnly(all.OrderBy(info => info.Name, Identifier.Comparer).ToArray());
        AllNames = Array.AsReadOnly(All.Select(info => info.Name).ToArray());

        // Distinct phi glyphs compare equal ignoring case; retain exact-spelling metadata.
        InfoByExactName = All.ToFrozenDictionary(info => info.Name, StringComparer.Ordinal);
        var byName = new Dictionary<string, ConstantInfo>(Identifier.Comparer);
        foreach (var info in All)
        {
            byName.TryAdd(info.Name, info);
        }

        InfoByName = byName.ToFrozenDictionary(Identifier.Comparer);
    }

    private static ConstantInfo CreateCanonical(
        string name, ConstantKind kind, decimal value, string description, params string[] aliases)
    {
        var exactness = CalculationExactness.Unverified;
        if (kind is ConstantKind.True or ConstantKind.False)
        {
            exactness = CalculationExactness.Exact;
        }

        return new ConstantInfo(name, name, kind, new MathValue(value).WithExactness(exactness),
            description, Array.AsReadOnly(aliases));
    }

    /// <summary>
    /// Determines whether the specified character is a recognized non-ASCII mathematical constant symbol (e.g. Greek letters).
    /// </summary>
    internal static bool IsConstantLetter(char c) =>
        c is '\u03C0' or '\u03A0' or           // π, Π
             '\u03C4' or '\u03A4' or           // τ, Τ
             '\u03D5' or '\u03C6' or '\u03A6'; // ϕ (symbol), φ (small), Φ (capital)

    internal static bool TryGetInfo(ReadOnlySpan<char> name, [NotNullWhen(true)] out ConstantInfo? info)
    {
        // Case folding also matches unsupported Greek variants; preserve the parser's symbol set.
        if (name.Length == 1 && name[0] > '\u007F' && !IsConstantLetter(name[0]))
        {
            info = null;
            return false;
        }

        return InfoByExactName.GetAlternateLookup<ReadOnlySpan<char>>().TryGetValue(name, out info) ||
            InfoByName.GetAlternateLookup<ReadOnlySpan<char>>().TryGetValue(name, out info);
    }

    internal static bool TryResolve(ReadOnlySpan<char> name, out ConstantKind kind, out decimal value)
    {
        if (TryGetInfo(name, out var info))
        {
            kind = info.Kind;
            value = info.Value.DecimalValue;
            return true;
        }

        kind = default;
        value = 0m;
        return false;
    }
}
