using System.Numerics;
using Numera.Ast;

namespace Numera.Tests;

[TestClass]
public class AstFuzzing
{
    private readonly record struct Factor(BigInteger Value, bool IsDenominator)
    {
        public Factor Flip() => this with { IsDenominator = !IsDenominator };
    }

    [TestMethod]
    public void RationalReduction_GenerativeFuzzer_PreservesExactIntegerTruth()
    {
        var engine = CalculatorEngine.Default;
        var rng = new Random(7331);

        for (int run = 0; run < 150; run++)
        {
            // Target integer answer.
            int expectedK = rng.Next(-50, 50);
            if (expectedK == 0)
            {
                expectedK = 1;
            }

            // Invariant: (K * d1 * d2 * ...) / (d1 * d2 * ...) == K.
            var factors = new List<Factor> { new(expectedK, false) };
            int divisorCount = rng.Next(2, 5);
            for (int d = 0; d < divisorCount; d++)
            {
                BigInteger divisor = rng.Next(2, 11);
                factors.Add(new Factor(divisor, false));
                factors.Add(new Factor(divisor, true));
            }

            var context = EvaluationContext.Empty;
            var tree = BuildRationalTree(factors, rng, engine, ref context, depth: 0);
            var result = engine.Evaluate(tree, context);

            // Must reduce to an exact Integer.
            Assert.AreEqual(MathValue.DataType.Integer, result.Value.Kind,
                $"Fuzzer run {run} failed to reduce to Integer! Kind was {result.Value.Kind}, Value was {result.Value}");

            // Must reduce to the expected integer K.
            Assert.AreEqual(new BigInteger(expectedK), result.Value.IntegerValue,
                $"Fuzzer run {run} computed wrong integer value! Expected {expectedK}, got {result.Value.IntegerValue}");
        }
    }

    private static ExpressionNode BuildRationalTree(
        List<Factor> factors,
        Random rng,
        CalculatorEngine engine,
        ref EvaluationContext context,
        int depth)
    {
        // Base case: a single factor. This is the only terminal.
        if (factors.Count == 1)
        {
            var factor = factors[0];
            var number = new NumberNode(new MathValue(factor.Value));
            return factor.IsDenominator ?
                new BinaryNode(BinaryOperator.Divide, new NumberNode(1), number) :
                number;
        }

        // Shuffle then cut at 1..Count-1 so both groups are non-empty.
        var shuffled = new List<Factor>(factors);
        for (int i = shuffled.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (shuffled[i], shuffled[j]) = (shuffled[j], shuffled[i]);
        }

        int cut = rng.Next(1, shuffled.Count);
        var leftFactors = shuffled.GetRange(0, cut);
        var rightFactors = shuffled.GetRange(cut, shuffled.Count - cut);

        bool useDivide = rng.Next(2) == 0;
        if (useDivide)
        {
            // L / R' where R' is the right group with flipped denominator status.
            rightFactors = rightFactors.ConvertAll(f => f.Flip());
        }

        var left = BuildRationalTree(leftFactors, rng, engine, ref context, depth + 1);
        var right = BuildRationalTree(rightFactors, rng, engine, ref context, depth + 1);

        var node = new BinaryNode(useDivide ? BinaryOperator.Divide : BinaryOperator.Multiply, left, right);

        return MaybeWrapInVariable(node, engine, ref context, rng, depth);
    }

    private static ExpressionNode MaybeWrapInVariable(
        BinaryNode node,
        CalculatorEngine engine,
        ref EvaluationContext context,
        Random rng,
        int depth)
    {
        // Randomly wrap the node in a variable assignment to test context chaining.
        if (depth <= 2 && rng.Next(3) == 0)
        {
            var varName = $"v_{context.Variables.Count}_{depth}";
            var eval = engine.Evaluate(node, context);
            context = context.Advance([new Variable(varName, eval.Value, node)], eval.Value);
            return new IdentifierNode(varName);
        }

        return node;
    }

    [TestMethod]
    public void ContextChaining_MultiRoundFuzzer_NeverCrashesOrCorrupts()
    {
        var engine = CalculatorEngine.Default;
        var rng = new Random(999);

        for (int session = 0; session < 20; session++)
        {
            var contexts = new List<EvaluationContext> { EvaluationContext.Empty };
            var snapshots = new List<Variable[]> { Array.Empty<Variable>() };
            var varNames = new List<string>();
           
            string Pick() => varNames[rng.Next(varNames.Count)];
            string RandomCase(string name) => rng.Next(3) switch
            {
                0 => name.ToUpperInvariant(),
                1 => name.ToLowerInvariant(),
                _ => name
            };

            for (int round = 0; round < 15; round++)
            {
                var before = contexts[^1];
                string? assigned = null;
                string expr;

                int action = varNames.Count == 0 ? 0 : rng.Next(4);
                switch (action)
                {
                    case 0:
                        // Define a new variable.
                        assigned = $"x{round}";
                        varNames.Add(assigned);
                        expr = $"{assigned} = {rng.Next(1, 20)} / {rng.Next(1, 10)}";
                        break;

                    case 1:
                        // Self-referential update; must never throw or cycle.
                        assigned = Pick();
                        expr = $"{RandomCase(assigned)} = {RandomCase(assigned)} * {rng.Next(1, 5)}";
                        break;

                    case 2:
                        // Expression using ans and a variable.
                        expr = $"ans + {RandomCase(Pick())}";
                        break;

                    default:
                        // Cross-variable multiplication.
                        expr = $"{RandomCase(Pick())} * {RandomCase(Pick())}";
                        break;
                }

                string where = $"session {session}, round {round}, expr '{expr}'";

                bool ok = engine.TryEvaluate(expr, out var result, out var diag, before);
                Assert.IsTrue(ok, $"Evaluation failed ({where}): {diag?.Message}");

                var after = result.Context;

                // Only the assigned variable should be new or updated; all others should
                // be preserved.
                foreach (var prior in before.Variables)
                {
                    if (assigned is not null &&
                        VariableCollection.NameComparer.Equals(prior.Name, assigned))
                    {
                        continue;
                    }

                    Assert.IsTrue(after.Variables.TryGetVariable(prior.Name, out var kept),
                        $"Variable '{prior.Name}' was lost ({where}).");
                    Assert.AreEqual(prior.Value, kept.Value,
                        $"Variable '{prior.Name}' changed unexpectedly ({where}).");
                }

                // An assingment stores exactly the value it evaluates to.
                if (assigned is not null)
                {
                    Assert.IsTrue(after.Variables.TryGetVariable(assigned, out var stored),
                        $"Assigned variable '{assigned}' missing ({where}).");
                    Assert.AreEqual(result.Value, stored.Value,
                        $"Stored value differs from result value ({where}).");
                }

                // Only a new definition grows the collection.
                int expectedCount = before.Variables.Count + (action == 0 ? 1 : 0);
                Assert.AreEqual(expectedCount, after.Variables.Count,
                    $"Unexpected variable count ({where}).");

                contexts.Add(after);
                snapshots.Add(after.Variables.ToArray());

                // Immutability: every earlier context must still match its snapshot.
                for (int i = 0; i < contexts.Count; i++)
                {
                    Assert.IsTrue(contexts[i].Variables.SequenceEqual(snapshots[i]),
                        $"Context {i} was mutated ({where}).");
                }
            }
        }
    }
}
