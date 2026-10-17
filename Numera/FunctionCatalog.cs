using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;

namespace Numera;

/// <summary>
/// Central static registry of supported mathematical functions, parameter bounds,
/// metadata, and discovery search for Numera.
/// </summary>
internal static class FunctionCatalog
{
    private static readonly FunctionInfo[] CanonicalArray;
    private static readonly FunctionInfo[] AllArray;
    private static readonly FrozenDictionary<string, FunctionInfo> InfoByName;
    private static readonly string[] AllNamesArray;
    private static readonly string[] CanonicalNamesArray;

    static FunctionCatalog()
    {
        var canonicalList = BuildCanonicalCatalog();
        CanonicalArray = [.. canonicalList.OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase)];
        CanonicalNamesArray = [.. CanonicalArray.Select(f => f.Name)];

        var allList = new List<FunctionInfo>(canonicalList.Count * 2);
        var map = new Dictionary<string, FunctionInfo>(StringComparer.OrdinalIgnoreCase);

        foreach (var canonical in CanonicalArray)
        {
            allList.Add(canonical);
            map[canonical.Name] = canonical;

            foreach (var alias in canonical.Aliases)
            {
                var aliasInfo = new FunctionInfo(
                    Name: alias,
                    CanonicalName: canonical.Name,
                    Category: canonical.Category,
                    Signature: canonical.Signature,
                    Description: canonical.Description,
                    Aliases: canonical.Aliases,
                    Keywords: canonical.Keywords,
                    IsAlias: true);

                allList.Add(aliasInfo);
                map[alias] = aliasInfo;
            }
        }

        AllArray = [.. allList.OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase)];
        AllNamesArray = [.. AllArray.Select(f => f.Name)];
        InfoByName = map.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
    }

    public static IReadOnlyCollection<string> AllNames => AllNamesArray;

    public static IReadOnlyCollection<string> CanonicalNames => CanonicalNamesArray;

    public static IReadOnlyList<FunctionInfo> All => AllArray;

    public static IReadOnlyList<FunctionInfo> Canonical => CanonicalArray;

    public static bool TryGetInfo(string name, [NotNullWhen(true)] out FunctionInfo? info) =>
        InfoByName.TryGetValue(name, out info);

    public static bool TryGetInfo(ReadOnlySpan<char> name, [NotNullWhen(true)] out FunctionInfo? info)
    {
        // FrozenDictionary supports ReadOnlySpan<char> lookups via AlternateLookup in
        // .NET 9+.
        var lookup = InfoByName.GetAlternateLookup<ReadOnlySpan<char>>();
        return lookup.TryGetValue(name, out info);
    }

    public static IEnumerable<FunctionInfo> Search(string query, bool includeAliases)
    {
        var trimmed = query.Trim();
        if (trimmed.Length == 0)
        {
            return includeAliases ? AllArray : CanonicalArray;
        }

        var results = new List<(FunctionInfo Info, int Tier)>();
        var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var source = includeAliases ? AllArray : CanonicalArray;

        foreach (var item in source)
        {
            var tier = ClassifyMatch(item, trimmed);
            if (tier is int t)
            {
                // When includeAliases is false, each canonical appears at most once.
                // When includeAliases is true, items appear under their respective Name.
                if (seenNames.Add(item.Name))
                {
                    results.Add((item, t));
                }
            }
        }

        return results
            .OrderBy(r => r.Tier)
            .ThenBy(r => r.Info.Name, StringComparer.OrdinalIgnoreCase)
            .Select(r => r.Info);
    }

    private static int? ClassifyMatch(FunctionInfo info, string query)
    {
        // Tier 1: Exact Name or Alias match.
        if (info.Name.Equals(query, StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }

        if (info.Aliases.Any(a => a.Equals(query, StringComparison.OrdinalIgnoreCase)))
        {
            // If aliases are included, the alias itself will match as Tier 1 by its Name.
            // The canonical entry matches at Tier 2 for an alias query.
            return 2;
        }

        // Tier 2: Prefix Name or Alias match.
        if (info.Name.StartsWith(query, StringComparison.OrdinalIgnoreCase) ||
            info.Aliases.Any(a => a.StartsWith(query, StringComparison.OrdinalIgnoreCase)))
        {
            return 3;
        }

        // Tier 3: Substring Name or Alias match.
        if (info.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
            info.Aliases.Any(a => a.Contains(query, StringComparison.OrdinalIgnoreCase)))
        {
            return 4;
        }

        // Tier 4: Category name or standard abbreviation match.
        if (MatchesCategory(info.Category, query))
        {
            return 5;
        }

        // Tier 5: Curated keyword match.
        if (info.Keywords.Any(k => k.Equals(query, StringComparison.OrdinalIgnoreCase) ||
                                   k.StartsWith(query, StringComparison.OrdinalIgnoreCase) ||
                                   (query.Length >= 3 && k.Contains(query, StringComparison.OrdinalIgnoreCase))))
        {
            return 6;
        }

        return null;
    }

    private static bool MatchesCategory(FunctionCategory category, string query)
    {
        var catName = category.ToString();
        if (catName.Contains(query, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return category switch
        {
            FunctionCategory.Trigonometry =>
                query.Equals("trig", StringComparison.OrdinalIgnoreCase) ||
                query.Equals("trigonometric", StringComparison.OrdinalIgnoreCase) ||
                query.Equals("polar", StringComparison.OrdinalIgnoreCase),

            FunctionCategory.Hyperbolic =>
                query.Equals("hyp", StringComparison.OrdinalIgnoreCase),

            FunctionCategory.AngleConversion =>
                query.Equals("angle", StringComparison.OrdinalIgnoreCase) ||
                query.Equals("angles", StringComparison.OrdinalIgnoreCase) ||
                query.Equals("deg", StringComparison.OrdinalIgnoreCase) ||
                query.Equals("rad", StringComparison.OrdinalIgnoreCase),

            FunctionCategory.ExponentialsAndLogarithms =>
                query.Equals("exp", StringComparison.OrdinalIgnoreCase) ||
                query.Equals("log", StringComparison.OrdinalIgnoreCase) ||
                query.Equals("power", StringComparison.OrdinalIgnoreCase) ||
                query.Equals("root", StringComparison.OrdinalIgnoreCase),

            FunctionCategory.ArithmeticAndRounding =>
                query.Equals("arith", StringComparison.OrdinalIgnoreCase) ||
                query.Equals("arithmetic", StringComparison.OrdinalIgnoreCase) ||
                query.Equals("round", StringComparison.OrdinalIgnoreCase),

            FunctionCategory.StatisticalAndAggregation =>
                query.Equals("stat", StringComparison.OrdinalIgnoreCase) ||
                query.Equals("stats", StringComparison.OrdinalIgnoreCase) ||
                query.Equals("statistics", StringComparison.OrdinalIgnoreCase) ||
                query.Equals("agg", StringComparison.OrdinalIgnoreCase) ||
                query.Equals("aggregate", StringComparison.OrdinalIgnoreCase),

            FunctionCategory.CombinatoricsAndNumberTheory =>
                query.Equals("comb", StringComparison.OrdinalIgnoreCase) ||
                query.Equals("discrete", StringComparison.OrdinalIgnoreCase) ||
                query.Equals("number theory", StringComparison.OrdinalIgnoreCase),

            FunctionCategory.Bitwise =>
                query.Equals("bit", StringComparison.OrdinalIgnoreCase) ||
                query.Equals("bits", StringComparison.OrdinalIgnoreCase),

            FunctionCategory.SpecialAndProbability =>
                query.Equals("special", StringComparison.OrdinalIgnoreCase) ||
                query.Equals("prob", StringComparison.OrdinalIgnoreCase) ||
                query.Equals("probability", StringComparison.OrdinalIgnoreCase),

            FunctionCategory.Inspection =>
                query.Equals("check", StringComparison.OrdinalIgnoreCase) ||
                query.Equals("test", StringComparison.OrdinalIgnoreCase) ||
                query.Equals("inspect", StringComparison.OrdinalIgnoreCase),

            FunctionCategory.Random =>
                query.Equals("rand", StringComparison.OrdinalIgnoreCase) ||
                query.Equals("rng", StringComparison.OrdinalIgnoreCase),

            _ => false,
        };
    }

    private static List<FunctionInfo> BuildCanonicalCatalog()
    {
        static FunctionSignature Sig1(string p1) =>
            new(1, 1, [new ParameterInfo(p1)]);

        static FunctionSignature Sig2(string p1, string p2) =>
            new(2, 2, [new ParameterInfo(p1), new ParameterInfo(p2)]);

        static FunctionSignature Sig3(string p1, string p2, string p3) =>
            new(3, 3, [new ParameterInfo(p1), new ParameterInfo(p2), new ParameterInfo(p3)]);

        return
        [
            // Trigonometry.
            new FunctionInfo(
                Name: "sin",
                CanonicalName: "sin",
                Category: FunctionCategory.Trigonometry,
                Signature: Sig1("x"),
                Description: "Computes the sine of an angle in the configured angle unit.",
                Aliases: [],
                Keywords: ["sine", "trig", "wave", "periodic", "ratio"]),

            new FunctionInfo(
                Name: "cos",
                CanonicalName: "cos",
                Category: FunctionCategory.Trigonometry,
                Signature: Sig1("x"),
                Description: "Computes the cosine of an angle in the configured angle unit.",
                Aliases: [],
                Keywords: ["cosine", "trig", "wave", "periodic", "ratio"]),

            new FunctionInfo(
                Name: "tan",
                CanonicalName: "tan",
                Category: FunctionCategory.Trigonometry,
                Signature: Sig1("x"),
                Description: "Computes the tangent of an angle in the configured angle unit.",
                Aliases: [],
                Keywords: ["tangent", "slope", "trig", "ratio"]),

            new FunctionInfo(
                Name: "arcsin",
                CanonicalName: "arcsin",
                Category: FunctionCategory.Trigonometry,
                Signature: Sig1("x"),
                Description: "Computes the inverse sine (arcsine) returning an angle in the configured angle unit.",
                Aliases: ["asin"],
                Keywords: ["arcsine", "inverse sine", "asin"]),

            new FunctionInfo(
                Name: "arccos",
                CanonicalName: "arccos",
                Category: FunctionCategory.Trigonometry,
                Signature: Sig1("x"),
                Description: "Computes the inverse cosine (arccosine) returning an angle in the configured angle unit.",
                Aliases: ["acos"],
                Keywords: ["arccosine", "inverse cosine", "acos"]),

            new FunctionInfo(
                Name: "arctan",
                CanonicalName: "arctan",
                Category: FunctionCategory.Trigonometry,
                Signature: Sig1("x"),
                Description: "Computes the inverse tangent (arctangent) returning an angle in the configured angle unit.",
                Aliases: ["atan"],
                Keywords: ["arctangent", "inverse tangent", "atan"]),

            new FunctionInfo(
                Name: "atan2",
                CanonicalName: "atan2",
                Category: FunctionCategory.Trigonometry,
                Signature: Sig2("y", "x"),
                Description: "Computes the two-argument four-quadrant arctangent of y and x.",
                Aliases: [],
                Keywords: ["angle", "polar", "quadrant", "direction", "heading"]),

            new FunctionInfo(
                Name: "cot",
                CanonicalName: "cot",
                Category: FunctionCategory.Trigonometry,
                Signature: Sig1("x"),
                Description: "Computes the cotangent (1/tan) of an angle in the configured angle unit.",
                Aliases: [],
                Keywords: ["cotangent", "reciprocal"]),

            new FunctionInfo(
                Name: "sec",
                CanonicalName: "sec",
                Category: FunctionCategory.Trigonometry,
                Signature: Sig1("x"),
                Description: "Computes the secant (1/cos) of an angle in the configured angle unit.",
                Aliases: [],
                Keywords: ["secant", "reciprocal"]),

            new FunctionInfo(
                Name: "csc",
                CanonicalName: "csc",
                Category: FunctionCategory.Trigonometry,
                Signature: Sig1("x"),
                Description: "Computes the cosecant (1/sin) of an angle in the configured angle unit.",
                Aliases: [],
                Keywords: ["cosecant", "reciprocal"]),

            new FunctionInfo(
                Name: "arccot",
                CanonicalName: "arccot",
                Category: FunctionCategory.Trigonometry,
                Signature: Sig1("x"),
                Description: "Computes the inverse cotangent returning an angle in the configured angle unit.",
                Aliases: [],
                Keywords: ["inverse cotangent", "arc cotangent"]),

            new FunctionInfo(
                Name: "arcsec",
                CanonicalName: "arcsec",
                Category: FunctionCategory.Trigonometry,
                Signature: Sig1("x"),
                Description: "Computes the inverse secant returning an angle in the configured angle unit.",
                Aliases: [],
                Keywords: ["inverse secant", "arc secant"]),

            new FunctionInfo(
                Name: "arccsc",
                CanonicalName: "arccsc",
                Category: FunctionCategory.Trigonometry,
                Signature: Sig1("x"),
                Description: "Computes the inverse cosecant returning an angle in the configured angle unit.",
                Aliases: [],
                Keywords: ["inverse cosecant", "arc cosecant"]),

            // Hyperbolic.
            new FunctionInfo(
                Name: "sinh",
                CanonicalName: "sinh",
                Category: FunctionCategory.Hyperbolic,
                Signature: Sig1("x"),
                Description: "Computes the hyperbolic sine.",
                Aliases: [],
                Keywords: ["hyperbolic sine"]),

            new FunctionInfo(
                Name: "cosh",
                CanonicalName: "cosh",
                Category: FunctionCategory.Hyperbolic,
                Signature: Sig1("x"),
                Description: "Computes the hyperbolic cosine.",
                Aliases: [],
                Keywords: ["hyperbolic cosine", "catenary"]),

            new FunctionInfo(
                Name: "tanh",
                CanonicalName: "tanh",
                Category: FunctionCategory.Hyperbolic,
                Signature: Sig1("x"),
                Description: "Computes the hyperbolic tangent.",
                Aliases: [],
                Keywords: ["hyperbolic tangent", "sigmoid", "activation"]),

            new FunctionInfo(
                Name: "arsinh",
                CanonicalName: "arsinh",
                Category: FunctionCategory.Hyperbolic,
                Signature: Sig1("x"),
                Description: "Computes the inverse hyperbolic sine (area hyperbolic sine).",
                Aliases: ["asinh"],
                Keywords: ["asinh", "area hyperbolic sine", "inverse hyperbolic sine"]),

            new FunctionInfo(
                Name: "arcosh",
                CanonicalName: "arcosh",
                Category: FunctionCategory.Hyperbolic,
                Signature: Sig1("x"),
                Description: "Computes the inverse hyperbolic cosine (area hyperbolic cosine).",
                Aliases: ["acosh"],
                Keywords: ["acosh", "area hyperbolic cosine", "inverse hyperbolic cosine"]),

            new FunctionInfo(
                Name: "artanh",
                CanonicalName: "artanh",
                Category: FunctionCategory.Hyperbolic,
                Signature: Sig1("x"),
                Description: "Computes the inverse hyperbolic tangent (area hyperbolic tangent).",
                Aliases: ["atanh"],
                Keywords: ["atanh", "area hyperbolic tangent", "inverse hyperbolic tangent", "fisher"]),

            new FunctionInfo(
                Name: "coth",
                CanonicalName: "coth",
                Category: FunctionCategory.Hyperbolic,
                Signature: Sig1("x"),
                Description: "Computes the hyperbolic cotangent.",
                Aliases: [],
                Keywords: ["hyperbolic cotangent"]),

            new FunctionInfo(
                Name: "sech",
                CanonicalName: "sech",
                Category: FunctionCategory.Hyperbolic,
                Signature: Sig1("x"),
                Description: "Computes the hyperbolic secant.",
                Aliases: [],
                Keywords: ["hyperbolic secant"]),

            new FunctionInfo(
                Name: "csch",
                CanonicalName: "csch",
                Category: FunctionCategory.Hyperbolic,
                Signature: Sig1("x"),
                Description: "Computes the hyperbolic cosecant.",
                Aliases: [],
                Keywords: ["hyperbolic cosecant"]),

            new FunctionInfo(
                Name: "arcoth",
                CanonicalName: "arcoth",
                Category: FunctionCategory.Hyperbolic,
                Signature: Sig1("x"),
                Description: "Computes the inverse hyperbolic cotangent.",
                Aliases: [],
                Keywords: ["inverse hyperbolic cotangent"]),

            new FunctionInfo(
                Name: "arsech",
                CanonicalName: "arsech",
                Category: FunctionCategory.Hyperbolic,
                Signature: Sig1("x"),
                Description: "Computes the inverse hyperbolic secant.",
                Aliases: [],
                Keywords: ["inverse hyperbolic secant"]),

            new FunctionInfo(
                Name: "arcsch",
                CanonicalName: "arcsch",
                Category: FunctionCategory.Hyperbolic,
                Signature: Sig1("x"),
                Description: "Computes the inverse hyperbolic cosecant.",
                Aliases: [],
                Keywords: ["inverse hyperbolic cosecant"]),

            // Angle Conversion.
            new FunctionInfo(
                Name: "deg2rad",
                CanonicalName: "deg2rad",
                Category: FunctionCategory.AngleConversion,
                Signature: Sig1("degrees"),
                Description: "Converts degrees to radians.",
                Aliases: ["rad"],
                Keywords: ["degrees", "radians", "convert", "rad"]),

            new FunctionInfo(
                Name: "rad2deg",
                CanonicalName: "rad2deg",
                Category: FunctionCategory.AngleConversion,
                Signature: Sig1("radians"),
                Description: "Converts radians to degrees.",
                Aliases: ["deg"],
                Keywords: ["radians", "degrees", "convert", "deg"]),

            new FunctionInfo(
                Name: "grad2rad",
                CanonicalName: "grad2rad",
                Category: FunctionCategory.AngleConversion,
                Signature: Sig1("gradians"),
                Description: "Converts gradians to radians.",
                Aliases: ["grad"],
                Keywords: ["gradians", "radians", "convert", "grad"]),

            new FunctionInfo(
                Name: "rad2grad",
                CanonicalName: "rad2grad",
                Category: FunctionCategory.AngleConversion,
                Signature: Sig1("radians"),
                Description: "Converts radians to gradians.",
                Aliases: [],
                Keywords: ["radians", "gradians", "convert"]),

            new FunctionInfo(
                Name: "deg2grad",
                CanonicalName: "deg2grad",
                Category: FunctionCategory.AngleConversion,
                Signature: Sig1("degrees"),
                Description: "Converts degrees to gradians.",
                Aliases: [],
                Keywords: ["degrees", "gradians", "convert"]),

            new FunctionInfo(
                Name: "grad2deg",
                CanonicalName: "grad2deg",
                Category: FunctionCategory.AngleConversion,
                Signature: Sig1("gradians"),
                Description: "Converts gradians to degrees.",
                Aliases: [],
                Keywords: ["gradians", "degrees", "convert"]),

            // Exponentials and Logarithms.
            new FunctionInfo(
                Name: "exp",
                CanonicalName: "exp",
                Category: FunctionCategory.ExponentialsAndLogarithms,
                Signature: Sig1("x"),
                Description: "Computes the natural exponential e^x.",
                Aliases: [],
                Keywords: ["exponential", "euler", "power"]),

            new FunctionInfo(
                Name: "expm1",
                CanonicalName: "expm1",
                Category: FunctionCategory.ExponentialsAndLogarithms,
                Signature: Sig1("x"),
                Description: "Computes e^x - 1 accurately for small values of x.",
                Aliases: [],
                Keywords: ["exponential minus 1", "relative accuracy"]),

            new FunctionInfo(
                Name: "ln",
                CanonicalName: "ln",
                Category: FunctionCategory.ExponentialsAndLogarithms,
                Signature: Sig1("x"),
                Description: "Computes the natural logarithm (base e).",
                Aliases: [],
                Keywords: ["natural log", "logarithm", "base e"]),

            new FunctionInfo(
                Name: "log10",
                CanonicalName: "log10",
                Category: FunctionCategory.ExponentialsAndLogarithms,
                Signature: Sig1("x"),
                Description: "Computes the common logarithm (base 10).",
                Aliases: ["log"],
                Keywords: ["log10", "common log", "decadic", "base 10", "log"]),

            new FunctionInfo(
                Name: "log2",
                CanonicalName: "log2",
                Category: FunctionCategory.ExponentialsAndLogarithms,
                Signature: Sig1("x"),
                Description: "Computes the binary logarithm (base 2).",
                Aliases: [],
                Keywords: ["binary log", "bits", "base 2"]),

            new FunctionInfo(
                Name: "log1p",
                CanonicalName: "log1p",
                Category: FunctionCategory.ExponentialsAndLogarithms,
                Signature: Sig1("x"),
                Description: "Computes ln(1 + x) accurately for small values of x.",
                Aliases: [],
                Keywords: ["log 1 plus", "relative accuracy"]),

            new FunctionInfo(
                Name: "logn",
                CanonicalName: "logn",
                Category: FunctionCategory.ExponentialsAndLogarithms,
                Signature: Sig2("x", "base"),
                Description: "Computes the logarithm of x to an arbitrary base.",
                Aliases: [],
                Keywords: ["logarithm", "arbitrary base"]),

            new FunctionInfo(
                Name: "sqrt",
                CanonicalName: "sqrt",
                Category: FunctionCategory.ExponentialsAndLogarithms,
                Signature: Sig1("x"),
                Description: "Computes the square root of a non-negative number.",
                Aliases: [],
                Keywords: ["square root", "radical"]),

            new FunctionInfo(
                Name: "cbrt",
                CanonicalName: "cbrt",
                Category: FunctionCategory.ExponentialsAndLogarithms,
                Signature: Sig1("x"),
                Description: "Computes the cube root of a number.",
                Aliases: [],
                Keywords: ["cube root", "radical"]),

            new FunctionInfo(
                Name: "pow",
                CanonicalName: "pow",
                Category: FunctionCategory.ExponentialsAndLogarithms,
                Signature: Sig2("base", "exponent"),
                Description: "Raises a base to an exponent power.",
                Aliases: [],
                Keywords: ["power", "exponentiation", "^"]),

            new FunctionInfo(
                Name: "root",
                CanonicalName: "root",
                Category: FunctionCategory.ExponentialsAndLogarithms,
                Signature: Sig2("x", "n"),
                Description: "Computes the nth root of x.",
                Aliases: [],
                Keywords: ["nth root", "radical", "degree"]),

            // Arithmetic and Rounding.
            new FunctionInfo(
                Name: "abs",
                CanonicalName: "abs",
                Category: FunctionCategory.ArithmeticAndRounding,
                Signature: Sig1("x"),
                Description: "Computes the absolute value (magnitude) of a number.",
                Aliases: [],
                Keywords: ["absolute", "magnitude", "modulus"]),

            new FunctionInfo(
                Name: "round",
                CanonicalName: "round",
                Category: FunctionCategory.ArithmeticAndRounding,
                Signature: new FunctionSignature(1, 2, [new ParameterInfo("x"), new ParameterInfo("places", IsOptional: true)]),
                Description: "Rounds a value to the nearest integer or specified decimal places.",
                Aliases: [],
                Keywords: ["rounding", "places", "decimal places"]),

            new FunctionInfo(
                Name: "ceil",
                CanonicalName: "ceil",
                Category: FunctionCategory.ArithmeticAndRounding,
                Signature: Sig1("x"),
                Description: "Rounds up to the nearest integer.",
                Aliases: [],
                Keywords: ["ceiling", "round up"]),

            new FunctionInfo(
                Name: "floor",
                CanonicalName: "floor",
                Category: FunctionCategory.ArithmeticAndRounding,
                Signature: Sig1("x"),
                Description: "Rounds down to the nearest integer.",
                Aliases: [],
                Keywords: ["floor", "round down"]),

            new FunctionInfo(
                Name: "trunc",
                CanonicalName: "trunc",
                Category: FunctionCategory.ArithmeticAndRounding,
                Signature: Sig1("x"),
                Description: "Truncates the fractional part, rounding toward zero.",
                Aliases: [],
                Keywords: ["truncate", "integer part"]),

            new FunctionInfo(
                Name: "frac",
                CanonicalName: "frac",
                Category: FunctionCategory.ArithmeticAndRounding,
                Signature: Sig1("x"),
                Description: "Extracts the fractional component of a number (x - trunc(x)).",
                Aliases: [],
                Keywords: ["fraction", "mantissa", "fractional part"]),

            new FunctionInfo(
                Name: "sign",
                CanonicalName: "sign",
                Category: FunctionCategory.ArithmeticAndRounding,
                Signature: Sig1("x"),
                Description: "Returns 1 for positive, -1 for negative, or 0 for zero.",
                Aliases: ["sgn"],
                Keywords: ["signum", "sgn", "polarity"]),

            new FunctionInfo(
                Name: "clamp",
                CanonicalName: "clamp",
                Category: FunctionCategory.ArithmeticAndRounding,
                Signature: Sig3("val", "min", "max"),
                Description: "Restricts a value between minimum and maximum bounds.",
                Aliases: [],
                Keywords: ["constrain", "bounds", "range", "limit"]),

            new FunctionInfo(
                Name: "lerp",
                CanonicalName: "lerp",
                Category: FunctionCategory.ArithmeticAndRounding,
                Signature: Sig3("a", "b", "t"),
                Description: "Linearly interpolates between a and b by factor t.",
                Aliases: [],
                Keywords: ["linear interpolation", "mix", "blend"]),

            new FunctionInfo(
                Name: "mod",
                CanonicalName: "mod",
                Category: FunctionCategory.ArithmeticAndRounding,
                Signature: Sig2("a", "b"),
                Description: "Computes the remainder/modulo operation.",
                Aliases: [],
                Keywords: ["modulo", "remainder", "%"]),

            new FunctionInfo(
                Name: "hypot",
                CanonicalName: "hypot",
                Category: FunctionCategory.ArithmeticAndRounding,
                Signature: Sig2("x", "y"),
                Description: "Computes sqrt(x^2 + y^2) without intermediate overflow.",
                Aliases: [],
                Keywords: ["hypotenuse", "pythagoras", "euclidean", "distance", "norm"]),

            // Combinatorics and Number Theory.
            new FunctionInfo(
                Name: "factorial",
                CanonicalName: "factorial",
                Category: FunctionCategory.CombinatoricsAndNumberTheory,
                Signature: Sig1("n"),
                Description: "Computes the exact factorial of a non-negative integer.",
                Aliases: [],
                Keywords: ["factorial", "gamma", "permutations", "n!"]),

            new FunctionInfo(
                Name: "gcd",
                CanonicalName: "gcd",
                Category: FunctionCategory.CombinatoricsAndNumberTheory,
                Signature: new FunctionSignature(1, null, [new ParameterInfo("values", IsVariadic: true)]),
                Description: "Computes the greatest common divisor of one or more integers.",
                Aliases: [],
                Keywords: ["gcd", "greatest common divisor", "hcf", "highest common factor", "divisors"]),

            new FunctionInfo(
                Name: "lcm",
                CanonicalName: "lcm",
                Category: FunctionCategory.CombinatoricsAndNumberTheory,
                Signature: new FunctionSignature(1, null, [new ParameterInfo("values", IsVariadic: true)]),
                Description: "Computes the least common multiple of one or more integers.",
                Aliases: [],
                Keywords: ["lcm", "least common multiple", "lowest common multiple", "multiples"]),

            new FunctionInfo(
                Name: "comb",
                CanonicalName: "comb",
                Category: FunctionCategory.CombinatoricsAndNumberTheory,
                Signature: Sig2("n", "k"),
                Description: "Computes the number of combinations (n choose k).",
                Aliases: ["ncr", "choose", "binomial"],
                Keywords: ["combinations", "choose", "ncr", "binomial", "pascal", "combinatorics"]),

            new FunctionInfo(
                Name: "perm",
                CanonicalName: "perm",
                Category: FunctionCategory.CombinatoricsAndNumberTheory,
                Signature: Sig2("n", "k"),
                Description: "Computes the number of permutations (nPr).",
                Aliases: ["npr"],
                Keywords: ["permutations", "npr", "arrangements", "combinatorics", "order"]),

            new FunctionInfo(
                Name: "isprime",
                CanonicalName: "isprime",
                Category: FunctionCategory.CombinatoricsAndNumberTheory,
                Signature: Sig1("n"),
                Description: "Tests whether an integer is prime (returns 1 for prime, 0 otherwise).",
                Aliases: [],
                Keywords: ["prime", "primality", "isprime", "number theory", "factors"]),

            // Statistical and Aggregation.
            new FunctionInfo(
                Name: "min",
                CanonicalName: "min",
                Category: FunctionCategory.StatisticalAndAggregation,
                Signature: new FunctionSignature(1, null, [new ParameterInfo("values", IsVariadic: true)]),
                Description: "Returns the minimum value from one or more arguments.",
                Aliases: [],
                Keywords: ["minimum", "smallest", "least"]),

            new FunctionInfo(
                Name: "max",
                CanonicalName: "max",
                Category: FunctionCategory.StatisticalAndAggregation,
                Signature: new FunctionSignature(1, null, [new ParameterInfo("values", IsVariadic: true)]),
                Description: "Returns the maximum value from one or more arguments.",
                Aliases: [],
                Keywords: ["maximum", "largest", "greatest"]),

            new FunctionInfo(
                Name: "avg",
                CanonicalName: "avg",
                Category: FunctionCategory.StatisticalAndAggregation,
                Signature: new FunctionSignature(1, null, [new ParameterInfo("values", IsVariadic: true)]),
                Description: "Computes the arithmetic mean (average) of one or more arguments.",
                Aliases: [],
                Keywords: ["average", "mean", "arithmetic mean", "expected value"]),

            new FunctionInfo(
                Name: "sum",
                CanonicalName: "sum",
                Category: FunctionCategory.StatisticalAndAggregation,
                Signature: new FunctionSignature(1, null, [new ParameterInfo("values", IsVariadic: true)]),
                Description: "Computes the sum of one or more arguments.",
                Aliases: [],
                Keywords: ["addition", "total", "sigma"]),

            new FunctionInfo(
                Name: "mul",
                CanonicalName: "mul",
                Category: FunctionCategory.StatisticalAndAggregation,
                Signature: new FunctionSignature(1, null, [new ParameterInfo("values", IsVariadic: true)]),
                Description: "Computes the product of one or more arguments.",
                Aliases: [],
                Keywords: ["product", "multiply", "pi product"]),

            // Bitwise.
            new FunctionInfo(
                Name: "shl",
                CanonicalName: "shl",
                Category: FunctionCategory.Bitwise,
                Signature: Sig2("val", "count"),
                Description: "Performs an exact arithmetic left bit-shift.",
                Aliases: [],
                Keywords: ["shift left", "<<", "bit shift"]),

            new FunctionInfo(
                Name: "shr",
                CanonicalName: "shr",
                Category: FunctionCategory.Bitwise,
                Signature: Sig2("val", "count"),
                Description: "Performs an exact arithmetic right bit-shift.",
                Aliases: [],
                Keywords: ["shift right", ">>", "bit shift"]),

            new FunctionInfo(
                Name: "band",
                CanonicalName: "band",
                Category: FunctionCategory.Bitwise,
                Signature: Sig2("a", "b"),
                Description: "Computes the exact bitwise AND of two integers.",
                Aliases: [],
                Keywords: ["bitwise and", "mask", "&"]),

            new FunctionInfo(
                Name: "bor",
                CanonicalName: "bor",
                Category: FunctionCategory.Bitwise,
                Signature: Sig2("a", "b"),
                Description: "Computes the exact bitwise OR of two integers.",
                Aliases: [],
                Keywords: ["bitwise or", "|"]),

            new FunctionInfo(
                Name: "bxor",
                CanonicalName: "bxor",
                Category: FunctionCategory.Bitwise,
                Signature: Sig2("a", "b"),
                Description: "Computes the exact bitwise XOR of two integers.",
                Aliases: [],
                Keywords: ["bitwise xor", "exclusive or", "^"]),

            new FunctionInfo(
                Name: "bnot",
                CanonicalName: "bnot",
                Category: FunctionCategory.Bitwise,
                Signature: Sig1("val"),
                Description: "Computes the exact bitwise NOT (one's complement) of an integer.",
                Aliases: [],
                Keywords: ["bitwise not", "complement", "~", "invert"]),

            // Special and Probability.
            new FunctionInfo(
                Name: "erf",
                CanonicalName: "erf",
                Category: FunctionCategory.SpecialAndProbability,
                Signature: Sig1("x"),
                Description: "Computes the Gauss error function.",
                Aliases: [],
                Keywords: ["error function", "normal", "gauss"]),

            new FunctionInfo(
                Name: "erfc",
                CanonicalName: "erfc",
                Category: FunctionCategory.SpecialAndProbability,
                Signature: Sig1("x"),
                Description: "Computes the complementary Gauss error function (1 - erf(x)).",
                Aliases: [],
                Keywords: ["complementary error function", "tail"]),

            new FunctionInfo(
                Name: "ncdf",
                CanonicalName: "ncdf",
                Category: FunctionCategory.SpecialAndProbability,
                Signature: Sig1("x"),
                Description: "Computes the standard normal cumulative distribution function.",
                Aliases: [],
                Keywords: ["gaussian", "normal", "cdf", "bell", "phi", "probability"]),

            new FunctionInfo(
                Name: "sinc",
                CanonicalName: "sinc",
                Category: FunctionCategory.SpecialAndProbability,
                Signature: Sig1("x"),
                Description: "Computes the unnormalized cardinal sine (sin(x)/x).",
                Aliases: [],
                Keywords: ["cardinal sine", "sampling", "fourier", "signal"]),

            new FunctionInfo(
                Name: "gamma",
                CanonicalName: "gamma",
                Category: FunctionCategory.SpecialAndProbability,
                Signature: Sig1("x"),
                Description: "Computes the complete Euler Gamma function Gamma(x).",
                Aliases: [],
                Keywords: ["gamma", "factorial", "euler", "special"]),

            new FunctionInfo(
                Name: "lgamma",
                CanonicalName: "lgamma",
                Category: FunctionCategory.SpecialAndProbability,
                Signature: Sig1("x"),
                Description: "Computes the natural logarithm of the absolute value of the Gamma function, ln(|Gamma(x)|).",
                Aliases: [],
                Keywords: ["log gamma", "log-gamma", "lgamma", "gammaln", "logarithm"]),

            new FunctionInfo(
                Name: "beta",
                CanonicalName: "beta",
                Category: FunctionCategory.SpecialAndProbability,
                Signature: Sig2("a", "b"),
                Description: "Computes the Euler Beta function B(a, b).",
                Aliases: [],
                Keywords: ["beta", "euler beta", "special", "integral"]),

            new FunctionInfo(
                Name: "zeta",
                CanonicalName: "zeta",
                Category: FunctionCategory.SpecialAndProbability,
                Signature: Sig1("s"),
                Description: "Computes the Riemann zeta function zeta(s).",
                Aliases: [],
                Keywords: ["zeta", "riemann", "riemann zeta", "dirichlet"]),

            // Random.
            new FunctionInfo(
                Name: "rand",
                CanonicalName: "rand",
                Category: FunctionCategory.Random,
                Signature: new FunctionSignature(0, 0, []),
                Description: "Returns a pseudo-random floating-point number in the range [0.0, 1.0).",
                Aliases: [],
                Keywords: ["random", "uniform", "rng"]),

            new FunctionInfo(
                Name: "randi",
                CanonicalName: "randi",
                Category: FunctionCategory.Random,
                Signature: Sig1("maxExclusive"),
                Description: "Returns a pseudo-random integer in the range [0, maxExclusive).",
                Aliases: [],
                Keywords: ["random integer", "dice", "rng"]),
        ];
    }
}
