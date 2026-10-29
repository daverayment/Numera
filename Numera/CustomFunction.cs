namespace Numera;

/// <summary>
/// Delegate invoked to evaluate a custom calculator function.
/// </summary>
/// <param name="args">The evaluated arguments passed to the function.</param>
/// <returns>The calculated result as a <see cref="MathValue"/>.</returns>
public delegate MathValue CustomFunctionCallback(IReadOnlyList<MathValue> args);

/// <summary>
/// Represents a caller-defined custom function that can be supplied via
/// <see cref="CalculatorOptions.CustomFunctions"/> to extend Numera's evaluation capabilities.
/// </summary>
public sealed class CustomFunction
{
    /// <summary>
    /// Gets the invocation identifier of the function.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the parameter bounds and signature metadata for arity validation.
    /// </summary>
    public FunctionSignature Signature { get; }

    /// <summary>
    /// Gets the callback delegate that implements the function.
    /// </summary>
    public CustomFunctionCallback Implementation { get; }

    /// <summary>
    /// Gets an optional human-readable description of the function.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Initializes a new instance of the <see cref="CustomFunction"/> class.
    /// </summary>
    /// <param name="name">The name of the function.</param>
    /// <param name="signature">The argument bounds and parameter metadata.</param>
    /// <param name="implementation">The callback delegate that executes the function.</param>
    /// <param name="description">Optional description of the function.</param>
    public CustomFunction(
        string name,
        FunctionSignature signature,
        CustomFunctionCallback implementation,
        string? description = null)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(signature);
        ArgumentNullException.ThrowIfNull(implementation);

        Name = name;
        Signature = signature;
        Implementation = implementation;
        Description = description;
    }

    /// <summary>
    /// Creates a custom function taking zero arguments: <c>func()</c>.
    /// </summary>
    public static CustomFunction Create(string name, Func<MathValue> handler, string? description = null)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return new(name, new FunctionSignature(0, 0, []), _ => handler(), description);
    }

    /// <summary>
    /// Creates a custom function taking a single argument: <c>func(x)</c>.
    /// </summary>
    public static CustomFunction Create(string name, Func<MathValue, MathValue> handler, string? description = null)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return new(name, new FunctionSignature(1, 1, [new ParameterInfo("x")]), args => handler(args[0]), description);
    }

    /// <summary>
    /// Creates a custom function taking two arguments: <c>func(x, y)</c>.
    /// </summary>
    public static CustomFunction Create(string name, Func<MathValue, MathValue, MathValue> handler, string? description = null)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return new(name, new FunctionSignature(2, 2, [new ParameterInfo("x"), new ParameterInfo("y")]), args => handler(args[0], args[1]), description);
    }

    /// <summary>
    /// Creates a custom function taking three arguments: <c>func(x, y, z)</c>.
    /// </summary>
    public static CustomFunction Create(string name, Func<MathValue, MathValue, MathValue, MathValue> handler, string? description = null)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return new(name, new FunctionSignature(3, 3, [new ParameterInfo("x"), new ParameterInfo("y"), new ParameterInfo("z")]), args => handler(args[0], args[1], args[2]), description);
    }

    /// <summary>
    /// Creates a variadic custom function accepting at least <paramref name="minArgs"/> arguments: <c>func(...args)</c>.
    /// </summary>
    public static CustomFunction CreateVariadic(string name, int minArgs, Func<IReadOnlyList<MathValue>, MathValue> handler, string? description = null)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return new(name, new FunctionSignature(minArgs, null, [new ParameterInfo("values", IsVariadic: true)]), handler.Invoke, description);
    }

    /// <inheritdoc/>
    public override string ToString() => Description is not null ? $"{Name} - {Description}" : Name;
}
