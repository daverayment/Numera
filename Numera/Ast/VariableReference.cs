namespace Numera.Ast;

/// <summary>
/// Represents a syntactic variable reference within an expression syntax tree,
/// including the variable identifier name and the source text span where it occurs.
/// </summary>
/// <remarks>
/// Explains why the reference exists at a specific location in source text, enabling
/// callers and IDE tooling to highlight, underline, or attach tooltips to variable
/// usages.
/// </remarks>
/// <param name="Name">The variable name as referenced in the expression.</param>
/// <param name="Span">The source text span of the variable identifier.</param>
public readonly record struct VariableReference(string Name, TextSpan Span)
{
    /// <summary>
    /// Gets a value indicating whether this reference points to the special previous-result
    /// identifier (<c>ans</c>).
    /// </summary>
    public bool IsAns =>
        Identifier.Comparer.Equals(Name, EvaluationContext.PreviousResultVariableName);
}
