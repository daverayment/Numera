using Numera;

var engine = new CalculatorEngine(CalculatorOptions.ForCurrentCulture());
var context = EvaluationContext.Empty;

Console.WriteLine(Calculator.Evaluate("2,000 + 2'000'000"));

while (true)
{
    Console.Write("Enter an expression (q to quit): ");
    string? input = Console.ReadLine();
    if (string.IsNullOrWhiteSpace(input) || input == "q") break;

    if (engine.TryEvaluate(input ?? "", out var result, out var diagnostic, context))
    {
        var card = engine.FormatResult(result);
        Console.WriteLine($"Result: {card.PrimaryDisplay} ({result.Type})");

        // Update the context for the next evaluation, so that variables and ans are
        // preserved.
        context = result.Context;
    }
    else
    {
        Console.WriteLine($"Error: {diagnostic.Code} - {diagnostic.Message} Position: {diagnostic.Position}.");
        continue;
    }
}
