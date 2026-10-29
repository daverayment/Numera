namespace Numera;

/// <summary>
/// Represents an immutable named variable with its evaluated <see cref="MathValue"/> and
/// optional defining expression
/// text.
/// </summary>
/// <remarks>Use <see cref="Definition"/> to preserve the original expression when the
/// variable is created or updated from parsed input.</remarks>
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
    /// Gets the original expression text that defined this variable (e.g.
    /// <c>"105 + 15%"</c>), or <see langword="null"/> if the variable was supplied
    /// externally without a defining expression.
    /// </summary>
    public string? Definition { get; }

    /// <summary>
    /// Initializes a new instance of the Variable class with a name, value, and optional
    /// definition.
    /// </summary>
    /// <param name="name">Variable name.</param>
    /// <param name="value">Variable value.</param>
    /// <param name="definition">Optional expression text that defines the variable.
    /// </param>
    public Variable(string name, MathValue value, string? definition = null)
    {
        ArgumentNullException.ThrowIfNull(name, nameof(name));
        Name = name;
        Value = value;
        Definition = definition;
    }

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
    /// Outputs the variable name and value, and the definition, if provided.
    /// </summary>
    public override string ToString() =>
        Definition is null ? $"{Name} = {Value}" : $"{Name} = {Value} ({Definition})";
}
