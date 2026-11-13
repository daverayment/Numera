using System.Diagnostics;
using Numera.Ast;

namespace Numera.Tests;

/// <summary>
/// Exercises <c>AstValidator</c> (internal) through the public <see cref="Calculator"/> entry points,
/// along with the node snapshot semantics and stack-safety guarantees it relies on.
/// </summary>
[TestClass]
public class AstValidationTests
{
    private static NumberNode Num(decimal value = 1m) => new(new MathValue(value));

    private static CalculatorEngine EngineWithDepth(int depth) =>
        new(new CalculatorOptions { MaxNestingDepth = depth });

    /// <summary>
    /// True when validation accepts the tree at the given nesting limit. Other evaluation
    /// failures are irrelevant here, so only limit failures count as rejection.
    /// </summary>
    private static bool PassesValidation(ExpressionNode node, int depth)
    {
        try
        {
            EngineWithDepth(depth).Evaluate(node);
            return true;
        }
        catch (CalculationLimitException)
        {
            return false;
        }
        catch (NumeraException)
        {
            return true;
        }
    }

    private static ExpressionNode RandomTree(Random random, int depth, List<ExpressionNode> pool)
    {
        ExpressionNode node;
        if (depth == 0 || random.Next(4) == 0)
        {
            node = pool.Count > 0 && random.Next(3) == 0 ? pool[random.Next(pool.Count)] : Num();
        }
        else if (random.Next(3) == 0)
        {
            node = new UnaryNode(UnaryOperator.Negate, RandomTree(random, depth - 1, pool));
        }
        else
        {
            var ops = new[] { BinaryOperator.Add, BinaryOperator.Subtract, BinaryOperator.Multiply, BinaryOperator.Power };
            // Left and right may be the same shared instance, which the validator must not mistake for a spine.
            var left = RandomTree(random, depth - 1, pool);
            var right = random.Next(5) == 0 ? left : RandomTree(random, depth - 1, pool);
            node = new BinaryNode(ops[random.Next(ops.Length)], left, right);
        }

        pool.Add(node);
        return node;
    }

    [TestMethod]
    public void ValidatorHeightMatchesNodeHeightOnRandomTrees()
    {
        var random = new Random(12345);
        for (int i = 0; i < 500; i++)
        {
            var tree = RandomTree(random, 6, []);
            int height = tree.Height;
            Assert.IsTrue(PassesValidation(tree, height), $"Tree #{i} should pass at its own height {height}.");
            if (height > 1)
            {
                Assert.IsFalse(PassesValidation(tree, height - 1), $"Tree #{i} should fail below its height {height}.");
            }
        }
    }

    [TestMethod]
    public void UnaryLeftOfAdditionCountsFullHeight()
    {
        var tree = new BinaryNode(BinaryOperator.Add, new UnaryNode(UnaryOperator.Negate, Num()), Num());
        Assert.AreEqual(3, tree.Height);
        Assert.IsTrue(PassesValidation(tree, 3));
        Assert.IsFalse(PassesValidation(tree, 2));
    }

    [TestMethod]
    public void PowerLeftOfAdditionCountsFullHeight()
    {
        var tree = new BinaryNode(BinaryOperator.Add, new BinaryNode(BinaryOperator.Power, Num(), Num()), Num());
        Assert.AreEqual(3, tree.Height);
        Assert.IsTrue(PassesValidation(tree, 3));
        Assert.IsFalse(PassesValidation(tree, 2));
    }

    [TestMethod]
    public void FlatSumFlattensLeftSpine()
    {
        var tree = new BinaryNode(BinaryOperator.Add, new BinaryNode(BinaryOperator.Add, Num(), Num()), Num());
        Assert.AreEqual(2, tree.Height);
        Assert.IsTrue(PassesValidation(tree, 2));
    }

    [TestMethod]
    public void SharedChildIsNotTreatedAsLeftSpineWhenOnTheRight()
    {
        var inner = new BinaryNode(BinaryOperator.Add, Num(), Num());
        var tree = new BinaryNode(BinaryOperator.Add, inner, inner);
        Assert.AreEqual(Math.Max(tree.Height, 1), tree.Height);
        Assert.IsTrue(PassesValidation(tree, tree.Height));
        Assert.IsFalse(PassesValidation(tree, tree.Height - 1));
    }

    [TestMethod]
    public void MillionDeepManualUnaryChainFailsWithoutOverflow()
    {
        ExpressionNode node = Num();
        for (int i = 0; i < 1_000_000; i++)
        {
            node = new UnaryNode(UnaryOperator.Negate, node);
        }

        Assert.Throws<CalculationLimitException>(() => Calculator.Evaluate(node));
        Assert.IsFalse(Calculator.TryEvaluate(node, out _, out var diagnostic));
        Assert.AreEqual(EvaluationError.LimitExceeded, diagnostic!.Code);
    }

    [TestMethod]
    public void DeepFunctionNestingParsesQuicklyAndLimitIsEnforced()
    {
        static string Nest(int depth) => string.Concat(Enumerable.Repeat("sin(", depth)) + "1" + new string(')', depth);

        var stopwatch = Stopwatch.StartNew();
        Assert.IsFalse(Calculator.TryParse(Nest(200), out _, out var diagnostic));
        Assert.AreEqual(EvaluationError.LimitExceeded, diagnostic!.Code);
        Assert.IsTrue(Calculator.TryParse(Nest(40), out _, out _));
        stopwatch.Stop();
        Assert.IsLessThan(2000, stopwatch.ElapsedMilliseconds, "Parsing nested calls must not be exponential.");
    }

    [TestMethod]
    public void ComparisonChainSnapshotsItsCollections()
    {
        var operands = new ExpressionNode[] { Num(1m), Num(2m) };
        var operators = new[] { ComparisonOperator.LessThan };
        var node = new ComparisonChainNode(operands, operators);

        operands[0] = Num(99m);
        operators[0] = ComparisonOperator.GreaterThan;

        Assert.AreEqual(Num(1m), node.Operands[0]);
        Assert.AreEqual(ComparisonOperator.LessThan, node.Operators[0]);
        Assert.IsNotInstanceOfType<ExpressionNode[]>(node.Operands);
        Assert.IsNotInstanceOfType<ComparisonOperator[]>(node.Operators);
    }

    [TestMethod]
    public void FunctionCallSnapshotsItsArguments()
    {
        var args = new ExpressionNode[] { Num(4m) };
        var node = new FunctionCallNode("sqrt", args);
        args[0] = Num(9m);

        Assert.AreEqual(Num(4m), node.Arguments[0]);
        Assert.IsNotInstanceOfType<ExpressionNode[]>(node.Arguments);
    }

    [TestMethod]
    public void NodeConstructorsRejectNull()
    {
        Assert.Throws<ArgumentNullException>(() => new FunctionCallNode("sqrt", null!));
        Assert.Throws<ArgumentNullException>(() => new FunctionCallNode(null!, []));
        Assert.Throws<ArgumentNullException>(() => new ComparisonChainNode(null!, []));
        Assert.Throws<ArgumentNullException>(() => new ComparisonChainNode([], null!));
    }

    [TestMethod]
    public void ManualNodesViolatingInvariantsAreRejected()
    {
        var unknownFunction = new FunctionCallNode("nosuchfunction", [Num()]);
        var unmarkedConstant = new IdentifierNode("pi");
        var mismatchedConstant = new IdentifierNode("foo", ConstantKind.Pi);
        var implicitAdd = new BinaryNode(BinaryOperator.Add, Num(), Num(), default, IsImplicit: true);
        var undefinedOperator = new BinaryNode((BinaryOperator)999, Num(), Num());

        Assert.IsFalse(Calculator.TryEvaluate(unknownFunction, out _, out var diagnostic));
        Assert.AreEqual(EvaluationError.UnknownFunction, diagnostic!.Code);

        foreach (var node in new ExpressionNode[] { unmarkedConstant, mismatchedConstant, implicitAdd, undefinedOperator })
        {
            Assert.IsFalse(Calculator.TryEvaluate(node, out _, out diagnostic), node.ToString());
            Assert.AreEqual(EvaluationError.InvalidSyntax, diagnostic!.Code, node.ToString());
        }
    }

    [TestMethod]
    public void ValidConstantAndImplicitMultiplicationAreAccepted()
    {
        Assert.IsTrue(Calculator.TryEvaluate(new IdentifierNode("pi", ConstantKind.Pi), out _, out _));
        var implicitMultiply = new BinaryNode(BinaryOperator.Multiply, Num(2m), new IdentifierNode("pi", ConstantKind.Pi), default, IsImplicit: true);
        Assert.IsTrue(Calculator.TryEvaluate(implicitMultiply, out _, out _));
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("   ")]
    [DataRow("1x")]
    [DataRow("a b")]
    [DataRow("a-b")]
    [DataRow(" x")]
    [DataRow("x ")]
    [DataRow("x!")]
    [DataRow("x\n")]
    [DataRow("\u03C0x")]
    [DataRow("x\u03C0")]
    [DataRow("x\u0661")]
    public void MalformedManualNamesFailBeforeCallbacksWithNodeSpans(string name)
    {
        int calls = 0;
        var engine = new CalculatorEngine(new CalculatorOptions
        {
            CustomFunctions = [CustomFunction.Create("tick", () =>
            {
                calls++;
                return new MathValue(7);
            })]
        });
        var context = new EvaluationContext([new Variable("existing", new MathValue(42))], new MathValue(11));
        var span = new TextSpan(7, Math.Max(1, name.Length));
        var invalidNodes = new ExpressionNode[]
        {
            new IdentifierNode(name, Span: span),
            new AssignmentNode(name, new FunctionCallNode("tick", []), span: span),
            new FunctionCallNode(name, [new FunctionCallNode("tick", [])], span),
        };

        foreach (var invalidNode in invalidNodes)
        {
            var tree = new BinaryNode(BinaryOperator.Add, new FunctionCallNode("tick", []), invalidNode);
            Assert.IsFalse(engine.TryEvaluate(tree, out _, out var diagnostic, context));
            Assert.IsNotNull(diagnostic);
            Assert.AreEqual(EvaluationError.InvalidSyntax, diagnostic.Code);
            Assert.AreEqual(span.Start, diagnostic.Position);
            Assert.AreEqual(span.Length, diagnostic.Length);
            Assert.IsFalse(string.IsNullOrWhiteSpace(diagnostic.Message));
            var exception = Assert.ThrowsExactly<ExpressionException>(() => engine.Evaluate(tree, context));
            Assert.AreEqual(diagnostic, exception.Diagnostic);
            Assert.AreEqual(0, calls);

            Assert.IsFalse(Calculator.TryEvaluate(invalidNode, out _, out var facadeDiagnostic, context));
            Assert.AreEqual(diagnostic, facadeDiagnostic);
            if (invalidNode is not FunctionCallNode)
            {
                foreach (var style in new[] { ExpressionFormatOptions.Ascii, ExpressionFormatOptions.Typographic, ExpressionFormatOptions.LaTeX })
                {
                    var formatException = Assert.ThrowsExactly<ExpressionException>(() => engine.FormatExpression(tree, style));
                    Assert.AreEqual(diagnostic, formatException.Diagnostic);
                    var facadeException = Assert.ThrowsExactly<ExpressionException>(() => Calculator.FormatExpression(invalidNode, style));
                    Assert.AreEqual(diagnostic, facadeException.Diagnostic);
                }
            }
            Assert.AreEqual(0, calls);
        }

        Assert.AreEqual(new MathValue(42), context.Variables["existing"].Value);
        Assert.AreEqual(new MathValue(11), context.PreviousResult);
        Assert.AreEqual(new MathValue(7), engine.Evaluate(new FunctionCallNode("tick", [])).Value);
        Assert.AreEqual(1, calls);
    }

    [TestMethod]
    [DataRow("_")]
    [DataRow("_x")]
    [DataRow("alpha2")]
    [DataRow("UPPER_case")]
    [DataRow("\u03B1")]
    [DataRow("\u53D8\u91CF2")]
    public void ValidManualNamesMatchParsedAssignmentsAndRegisteredFunctions(string name)
    {
        var engine = new CalculatorEngine();
        var assignment = new AssignmentNode(name, Num(42m));
        var result = engine.Evaluate(assignment);
        Assert.AreEqual(engine.Evaluate($"{name} = 42").Value, result.Value);
        Assert.AreEqual(new MathValue(42), result.Context.Variables[name].Value);
        var identifier = new IdentifierNode(name);
        Assert.AreEqual(result.Value, engine.Evaluate(identifier, result.Context).Value);
        Assert.AreEqual(name, engine.FormatExpression(identifier, ExpressionFormatOptions.Ascii));
        Assert.AreEqual($"{name} = 42", engine.FormatExpression(assignment, ExpressionFormatOptions.Ascii));

        var customEngine = new CalculatorEngine(new CalculatorOptions
        {
            CustomFunctions = [CustomFunction.Create(name, value => value)]
        });
        var call = new FunctionCallNode(name, [Num(42m)]);
        Assert.AreEqual(new MathValue(42), customEngine.Evaluate(call).Value);
        Assert.AreEqual(customEngine.Evaluate($"{name}(42)").Value, customEngine.Evaluate(call).Value);
    }

    [TestMethod]
    [DataRow("my_func", @"\operatorname{my\_func}(1)", EvaluationError.UnknownFunction)]
    [DataRow("my-func", @"\operatorname{my-func}(1)", EvaluationError.InvalidSyntax)]
    public void DisplayOnlyUnknownFunctionNamesRemainFormattable(string name, string expectedLatex, EvaluationError evaluationError)
    {
        var call = new FunctionCallNode(name, [Num()]);
        Assert.AreEqual(expectedLatex, Calculator.FormatExpression(call, ExpressionFormatOptions.LaTeX));
        Assert.AreEqual($"{name}(1)", Calculator.FormatExpression(call, ExpressionFormatOptions.Ascii));
        Assert.IsFalse(Calculator.TryEvaluate(call, out _, out var diagnostic));
        Assert.IsNotNull(diagnostic);
        Assert.AreEqual(evaluationError, diagnostic.Code);
    }

    [TestMethod]
    public void CapturedResultMetadataIsNotRequiredToBeExecutableSyntax()
    {
        var metadata = new IdentifierNode("display only");
        var value = new MathValue(5);
        var captured = new EvaluationResult(value, new EvaluationContext(VariableCollection.Empty, value, metadata));
        Assert.AreEqual(new MathValue(6), (captured + new MathValue(1)).Value);
        Assert.AreEqual(new MathValue(-5), (-captured).Value);
        Assert.IsFalse(Calculator.TryEvaluate(metadata, out _, out var diagnostic));
        Assert.IsNotNull(diagnostic);
        Assert.AreEqual(EvaluationError.InvalidSyntax, diagnostic.Code);
    }

    [TestMethod]
    public void FiveThousandTermManualSumEvaluatesOrThrowsLimit()
    {
        ExpressionNode sum = Num();
        for (int i = 1; i < 5000; i++)
        {
            sum = new BinaryNode(BinaryOperator.Add, sum, Num());
        }

        if (!Calculator.TryEvaluate(sum, out _, out var diagnostic))
        {
            Assert.AreEqual(EvaluationError.LimitExceeded, diagnostic!.Code);
        }
    }

    [TestMethod]
    public void MaxNestingDepthInputsNeverCrashOnSmallStack()
    {
        int depth = CalculationLimits.MaxNestingDepth;
        var inputs = new[]
        {
            new string('(', depth - 1) + "1" + new string(')', depth - 1),
            new string('-', depth - 1) + "1",
            string.Concat(Enumerable.Repeat("sin(", depth / 2)) + "1" + new string(')', depth / 2),
            string.Concat(Enumerable.Repeat("2^(", depth / 2)) + "1" + new string(')', depth / 2),
        };

        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                foreach (var input in inputs)
                {
                    try
                    {
                        Calculator.Evaluate(input);
                        foreach (var style in new[] { ExpressionFormatOptions.Ascii, ExpressionFormatOptions.Typographic, ExpressionFormatOptions.LaTeX })
                        {
                            Calculator.FormatExpression(input, style);
                        }
                    }
                    catch (CalculationLimitException)
                    {
                        // Acceptable: a limit failure, never a process crash.
                    }
                }
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        }, maxStackSize: 256 * 1024);

        thread.Start();
        thread.Join();
        Assert.IsNull(failure, failure?.ToString());
    }

    [TestMethod]
    public void LongParsedSumEvaluatesAndFormatsOnSmallStack()
    {
        // As many terms as the default input limit allows ("1+" repeated, then a final "1").
        var expression = string.Concat(Enumerable.Repeat("1+", (CalculationLimits.MaxInputLength - 1) / 2)) + "1";
        int terms = (CalculationLimits.MaxInputLength - 1) / 2 + 1;

        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                Assert.AreEqual(terms, (int)Calculator.Evaluate(expression).IntegerValue);
                foreach (var style in new[] { ExpressionFormatOptions.Ascii, ExpressionFormatOptions.Typographic, ExpressionFormatOptions.LaTeX })
                {
                    // The formatters walk left spines iteratively, so this must succeed rather than hit the stack guard.
                    var formatted = Calculator.FormatExpression(expression, style);
                    Assert.IsTrue(formatted.StartsWith("1 ", StringComparison.Ordinal));
                }
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        }, maxStackSize: 256 * 1024);

        thread.Start();
        thread.Join();
        Assert.IsNull(failure, failure?.ToString());
    }
}
