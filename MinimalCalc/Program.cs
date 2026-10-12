using Numera;

while (true)
{
    Console.Write("Enter an expression (q to quit): ");
    string? input = Console.ReadLine();

    if (input?.ToLowerInvariant() == "q")
    {
        break;
    }

    if (!Calculator.TryEvaluate(input ?? "", out var result, out var diagnostic))
    {
        Console.WriteLine($"Error: {diagnostic.Code} - {diagnostic.Message}");
        continue;
    }

    // Check for valid numbers before using the result.
    if (!result.IsFinite)
    {
        Console.WriteLine("Result is not a finite number.");
        continue;
    }

    // Display the result directly via string interpolation.
    Console.WriteLine($"Result: {result} ({result.Type})");
}
