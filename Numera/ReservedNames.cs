namespace Numera;

internal enum ReservedNameKind
{
    None,
    PreviousResult,
    Keyword,
    Constant,
    BuiltInFunction,
    CustomFunction
}

internal readonly record struct ReservedNameInfo(
    ReservedNameKind Kind,
    ConstantInfo? Constant = null,
    FunctionInfo? BuiltInFunction = null)
{
    /// <summary>
    /// Describes what the name would shadow, or returns null when the name is available.
    /// </summary>
    internal string? Description => Kind switch
    {
        ReservedNameKind.None => null,
        ReservedNameKind.PreviousResult => $"the '{EvaluationContext.PreviousResultVariableName}' variable",
        ReservedNameKind.Keyword => "a language keyword",
        ReservedNameKind.Constant => "a built-in constant",
        ReservedNameKind.BuiltInFunction => "a built-in function",
        ReservedNameKind.CustomFunction => "a registered custom function",
        _ => throw new InvalidOperationException("Unexpected reserved-name classification.")
    };
}

/// <summary>
/// Classifies names that cannot be used for variable or custom-function declarations.
/// Retains catalog metadata so callers can provide their own diagnostics and exception
/// types without repeating lookups.
/// </summary>
internal static class ReservedNames
{
    internal static ReservedNameInfo Classify(
        string name, CustomFunctionCollection? customFunctions = null)
    {
        if (name.Equals(EvaluationContext.PreviousResultVariableName, Identifier.Comparison))
        {
            return new(ReservedNameKind.PreviousResult);
        }

        if (KeywordCatalog.IsReserved(name))
        {
            return new(ReservedNameKind.Keyword);
        }

        if (ConstantCatalog.TryGetInfo(name, out var constant))
        {
            return new(ReservedNameKind.Constant, Constant: constant);
        }

        if (FunctionCatalog.TryGetInfo(name, out var function))
        {
            return new(ReservedNameKind.BuiltInFunction, BuiltInFunction: function);
        }

        if (customFunctions?.ContainsName(name) == true)
        {
            return new(ReservedNameKind.CustomFunction);
        }

        return new(ReservedNameKind.None);
    }
}
