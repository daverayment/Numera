using System.Collections.Frozen;
using System.Numerics;
using Numera.Ast;

namespace Numera;

/// <summary>
/// Represents an immutable named variable with its evaluated <see cref="MathValue"/>,
/// optional canonical defining abstract syntax tree <see cref="ExpressionNode"/>, and
/// optional defining expression text.
/// </summary>
/// <remarks>
/// Use <see cref="Expression"/> to preserve the parsed syntax tree across calculations,
/// for formatting and dependency inspection. Evaluation uses the captured
/// <see cref="Value"/>, never a replay of the defining expression.
/// Use <see cref="Definition"/> to preserve the original expression text when the
/// variable is created or updated from parsed input.
/// </remarks>
public readonly record struct Variable
{
    /// <summary>
    /// Gets the identifier name of the variable.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the evaluated mathematical value of the variable.
    /// </summary>
    public MathValue Value { get; }

    /// <summary>
    /// Gets the canonical abstract syntax tree defining this variable, or
    /// <see langword="null"/> if the variable was supplied externally without an AST.
    /// </summary>
    public ExpressionNode? Expression { get; }

    /// <summary>
    /// Gets the original expression text that defined this variable (e.g.
    /// <c>"105 + 15%"</c>), or <see langword="null"/> if the variable was supplied
    /// externally without a defining expression.
    /// </summary>
    public string? Definition { get; }

    /// <summary>
    /// Gets the unique set of variable names referenced by this variable's defining expression,
    /// or an empty set if the variable has no defining expression.
    /// </summary>
    public IReadOnlySet<string> ReferencedVariables =>
        Expression?.GetReferencedVariables() ?? FrozenSet<string>.Empty;

    /// <summary>
    /// Initializes a new instance of the Variable class with a name, value, optional AST expression,
    /// and optional definition text.
    /// </summary>
    /// <param name="name">Variable name.</param>
    /// <param name="value">Variable value.</param>
    /// <param name="expression">Optional canonical AST defining the variable.</param>
    /// <param name="definition">Optional expression text that defines the variable.</param>
    public Variable(string name, MathValue value, ExpressionNode? expression, string? definition = null)
    {
        ArgumentNullException.ThrowIfNull(name, nameof(name));
        Name = name;
        Value = value;
        Expression = expression;
        Definition = definition;
    }

    /// <summary>
    /// Initializes a new instance of the Variable class with a name, value, and optional
    /// definition text.
    /// </summary>
    /// <param name="name">Variable name.</param>
    /// <param name="value">Variable value.</param>
    /// <param name="definition">Optional expression text that defines the variable.</param>
    public Variable(string name, MathValue value, string? definition)
        : this(name, value, null, definition)
    {
    }

    /// <summary>
    /// Initializes a new instance of the Variable class with a name and value.
    /// </summary>
    /// <param name="name">Variable name.</param>
    /// <param name="value">Variable value.</param>
    public Variable(string name, MathValue value)
        : this(name, value, null, null)
    {
    }

    /// <summary>
    /// Gets the decimal value of the variable's <see cref="MathValue"/>. 
    /// </summary>
    public decimal DecimalValue => Value.DecimalValue;

    /// <summary>
    /// Gets the integer value of the variable's <see cref="MathValue"/>.
    /// </summary>
    public BigInteger IntegerValue => Value.IntegerValue;

    /// <summary>
    /// Gets the double value of the variable's <see cref="MathValue"/>.
    /// </summary>
    public double DoubleValue => Value.DoubleValue;

    /// <summary>
    /// Defines an implicit conversion from <see cref="Variable"/> to
    /// <see cref="MathValue"/>, so that a variable can be used directly in calculations.
    /// </summary>
    public static implicit operator MathValue(Variable variable) => variable.Value;

    /// <summary>
    /// Defines an implicit conversion from a tuple containing a variable name and value
    /// to a <see cref="Variable"/> instance.
    /// </summary>
    /// <param name="pair">The tuple whose <c>Name</c> and <c>Value</c> elements
    /// initialize the resulting variable.</param>
    /// <remarks>Usage: <c>Variable v = ("x", 10);</c></remarks>
    public static implicit operator Variable((string Name, MathValue Value) pair) =>
        new(pair.Name, pair.Value);

    /// <summary>
    /// Deconstructs the variable into its name and value.
    /// </summary>
    public void Deconstruct(out string name, out MathValue value)
    {
        name = Name;
        value = Value;
    }

    /// <summary>
    /// Deconstructs the variable into its name, value, and definition expression.
    /// </summary>
    public void Deconstruct(out string name, out MathValue value, out string? definition)
    {
        name = Name;
        value = Value;
        definition = Definition;
    }

    /// <summary>
    /// Deconstructs the variable into its name, value, canonical AST expression, and definition text.
    /// </summary>
    public void Deconstruct(out string name, out MathValue value, out ExpressionNode? expression, out string? definition)
    {
        name = Name;
        value = Value;
        expression = Expression;
        definition = Definition;
    }

    /// <summary>
    /// Outputs the variable name and value, and the definition, if provided.
    /// </summary>
    public override string ToString() =>
        Definition is null ? $"{Name} = {Value}" : $"{Name} = {Value} ({Definition})";
}
