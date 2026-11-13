using Numera;

// Use case 1: "I just want to evaluate an expression and get a result for my service/app"
string? input = "194.40 - 100";
Console.WriteLine($"Evaluating {input}");
var result = Calculator.Evaluate(input);
Console.WriteLine($"Result: {result}");
// Prints: "Result: 94.40"


// Use case 2: "I want to use the result in further work and not lose precision"
input = "0.125 / 74";
Console.WriteLine($"\n\n\nEvaluating {input}");
var result2 = Calculator.Evaluate(input);
Console.WriteLine($"Result: {result2} ({result2.Value.Kind})");
// Prints: "Result: 0.0016891891891891891891891892 (Decimal)"
result2 *= 74;
Console.WriteLine($"Result: {result2}");
// Prints: Result: 0.125

// User story 3: "Users paste expressions in my app that are invalid and I want to show them where the error was."
input = "2 + 3 *";
Console.WriteLine($"\n\n\nEvaluating {input}");
if (Calculator.TryEvaluate(input, out var value, out var diagnostic))
{
    var card = value.FormatResult();
    Console.WriteLine(card.PrimaryDisplay);
}
else
{
    Console.WriteLine(input);
    string marker = new('-', (int)diagnostic.Position! - 2);
    Console.WriteLine($".{marker}^");
    Console.WriteLine($"`- Error: {diagnostic.Code}. {diagnostic.Message}");
}
// Prints:
// 2 + 3 *
// .-----^
// `-Error: InvalidSyntax. Unexpected end of input.

// User story 4: "I want a REPL that preserves variables and the previous result."
var engine = CalculatorEngine.ForCurrentCulture();
var context = EvaluationContext.Empty;

// Step 1.
var r1 = engine.Evaluate("subtotal = 100", context);
context = r1.Context;

// Step 2.
var r2 = engine.Evaluate("tax = 7.5% of subtotal", context);
context = r2.Context;

// Step 3.
var r3 = engine.Evaluate("total = subtotal + tax", context);
context = r3.Context;

Console.WriteLine($"Total: {engine.FormatResult(r3)}");
Console.WriteLine("Variables");
foreach (var v in r3.AssignedVariables)
{
    Console.WriteLine($"  Assigned: {v.Name} = {v.Value} (from '{v.Definition}')");
}

// User story 5: "I need a physics engine that supports Celsius conversions and 3D distance."
var physicsEngine = CalculatorEngine.ForCurrentCulture(options =>
    options with
    {
        CustomFunctions =
        [
            CustomFunction.Create("celsius", f => (f - 32m) * 5m / 9m),
            CustomFunction.Create("hypot3", (x, y, z) =>
                new MathValue(Math.Sqrt((x * x + y * y + z * z).AsDouble()))),
        ]
    });

Console.WriteLine(physicsEngine.Evaluate("celsius(212)"));
// Prints: 100

// User story 6: "I want to parse an expression and render typeset math for a blog post."
string latex = Calculator.FormatExpression(
    "x = 2 * sin(30) + 3",
    new ExpressionFormatOptions { Style = ExpressionFormatStyle.MathJax });
Console.WriteLine(latex);
// Prints: "x = 2 \cdot \sin{\left(30\right)} + 3"


engine = new CalculatorEngine(CalculatorOptions.ForCurrentCulture());
context = EvaluationContext.Empty;

while (true)
{
    Console.Write("Enter an expression (q to quit): ");
    input = Console.ReadLine();
    if (string.IsNullOrWhiteSpace(input) || input == "q")
    {
        break;
    }

    if (engine.TryEvaluate(input ?? "", out result, out diagnostic, context))
    {
        var card = engine.FormatResult(result);
        Console.WriteLine($"Result: {card.PrimaryDisplay} ({result.Kind})");
        string jax = engine.FormatExpression(
            input!,
            new ExpressionFormatOptions { Style = ExpressionFormatStyle.MathJax });
        Console.WriteLine($"MathJax: {jax}");

        // Dependency / reference metadata for caller inspection (REPLs, reactive graphs)
        if (result.ReferencedVariables.Count > 0)
        {
            Console.WriteLine($"References: {string.Join(", ", result.ReferencedVariables)}");
        }

        if (result.HasAssignments)
        {
            foreach (var variable in result.AssignedVariables)
            {
                Console.WriteLine($"Assigned: {variable.Name} = {variable.Value} (from '{variable.Definition}')");
            }
        }

        // Advance to the new immutable context, preserving snapshot variables and 'ans'
        context = result.Context;
    }
    else
    {
        Console.WriteLine($"Error: {diagnostic.Code} - {diagnostic.Message} Position: {diagnostic.Position}.");
        continue;
    }
}
