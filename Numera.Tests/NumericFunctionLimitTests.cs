using System.Numerics;

namespace Numera.Tests;

[TestClass]
public class NumericFunctionLimitTests
{
    [TestMethod]
    [DataRow("6!", 9, 720)]
    [DataRow("factorial(6)", 9, 720)]
    [DataRow("comb(10,5)", 7, 252)]
    [DataRow("ncr(10,5)", 7, 252)]
    [DataRow("choose(10,5)", 7, 252)]
    [DataRow("binomial(10,5)", 7, 252)]
    [DataRow("perm(10,5)", 9, 30240)]
    [DataRow("npr(10,5)", 9, 30240)]
    [DataRow("sigma(3,8)", 9, 6562)]
    [DataRow("divsum(3,8)", 9, 6562)]
    [DataRow("sigma(127)", 7, 128)]
    [DataRow("divsum(127)", 7, 128)]
    [DataRow("lcm(31,30)", 9, 930)]
    public void ArithmeticTemporariesRespectIndependentIntermediateLimit(
        string expression, int maxBits, int defaultResult)
    {
        Assert.AreEqual(new BigInteger(defaultResult), Calculator.Evaluate(expression).IntegerValue);
        var engine = new CalculatorEngine(new CalculatorOptions { MaxIntegerBits = maxBits });
        var exception = Assert.Throws<CalculationLimitException>(() => engine.Evaluate(expression));
        Assert.AreEqual(EvaluationError.LimitExceeded, exception.Diagnostic.Code);
        Assert.AreEqual(0, exception.Diagnostic.Position);
        Assert.AreEqual(expression.Length, exception.Diagnostic.Length);
    }

    [TestMethod]
    public void CombinationChecksUndividedProductEvenWhenStoredResultFits()
    {
        Assert.AreEqual(new BigInteger(252), NumberTheory.Comb(10, 5));
        var engine = new CalculatorEngine(new CalculatorOptions { MaxIntegerBits = 7 });
        Assert.Throws<CalculationLimitException>(() => engine.Evaluate("comb(10,5)"));
        var engine2 = new CalculatorEngine(new CalculatorOptions { MaxIntegerBits = 8 });
        Assert.AreEqual(new BigInteger(252), engine2.Evaluate("comb(10,5)").IntegerValue);
    }

    [TestMethod]
    public void ProductChecksActualMagnitudeAfterAmbiguousBitBound()
    {
        Assert.AreEqual(new BigInteger(510), MathValue.Product(85, 6));
        Assert.AreEqual(new BigInteger(720), Calculator.Evaluate("6!").IntegerValue);
        var engine = new CalculatorEngine(new CalculatorOptions { MaxIntegerBits = 9 });
        Assert.Throws<CalculationLimitException>(() => engine.Evaluate("6!"));
    }

    [TestMethod]
    public void ProductRejectsLargeOperandsBeforeMultiplicationAndHandlesZero()
    {
        var large = BigInteger.One << 17000;
        Assert.Throws<CalculationLimitException>(() => MathValue.Product(large, large));
        Assert.AreEqual(BigInteger.Zero, MathValue.Product(large, BigInteger.Zero));
        Assert.Throws<CalculationLimitException>(() => NumberTheory.Sigma(3, 10001));
    }

    [TestMethod]
    [DataRow("isprime(10007)", 1)]
    [DataRow("divcount(10403)", 4)]
    [DataRow("divisorcount(10403)", 4)]
    [DataRow("sigma(10403,0)", 4)]
    [DataRow("divsum(10403,0)", 4)]
    [DataRow("totient(10403)", 10200)]
    [DataRow("phi(10403)", 10200)]
    [DataRow("eulerphi(10403)", 10200)]
    [DataRow("sigma(10403)", 10608)]
    [DataRow("divsum(10403)", 10608)]
    public void ModularAndFactorizationWorkCannotHideBehindSmallReturns(string expression, int expected)
    {
        Assert.AreEqual(new BigInteger(expected), Calculator.Evaluate(expression).IntegerValue);
    }

    [TestMethod]
    public void StandaloneFactorizationAndPrimalityUsePassedPolicy()
    {
        Assert.IsTrue(NumberTheory.IsPrime(97));
        Assert.AreEqual(new BigInteger(6), NumberTheory.DivCount(12));
        Assert.AreEqual(new BigInteger(4), NumberTheory.Totient(12));
        Assert.AreEqual(new BigInteger(28), NumberTheory.Sigma(12));
        Assert.AreEqual(new BigInteger(6), NumberTheory.Sigma(12, 0));
        Assert.Throws<CalculationLimitException>(() => NumberTheory.Factorize(BigInteger.One << 129));

        var primeProduct = BigInteger.One;
        int[] primesUnder100 = [2, 3, 5, 7, 11, 13, 17, 19, 23, 29, 31, 37, 41, 43, 47, 53, 59, 61, 67, 71, 73, 79, 83, 89, 97];
        foreach (int p in primesUnder100)
        {
            primeProduct *= p;
        }
        var candidateAboveLimit = primeProduct * (BigInteger.One << CalculationLimits.MaxPrimalityBits) + 1;
        Assert.Throws<CalculationLimitException>(() => NumberTheory.IsPrime(candidateAboveLimit));
    }

    [TestMethod]
    public void GcdChecksConservativeOpaqueIntermediateBound()
    {
        Assert.AreEqual(BigInteger.One, NumberTheory.Gcd(255, 1));
        Assert.AreEqual(BigInteger.One, Calculator.Evaluate("gcd(255,1)").IntegerValue);
        Assert.Throws<CalculationLimitException>(() => NumberTheory.Gcd(BigInteger.One << 16500, 1));
    }

    [TestMethod]
    [DataRow("popcount(x)")]
    [DataRow("bitcount(x)")]
    public void PopulationCountChecksTemporaryCountWithStoredInput(string expression)
    {
        var context = new EvaluationContext { Variables = [new Variable("x", new MathValue(255), definition: null)] };
        Assert.AreEqual(new BigInteger(8), NumberTheory.PopCount(255));
        Assert.AreEqual(new BigInteger(8), Calculator.Evaluate(expression, context).IntegerValue);
        Assert.Throws<CalculationLimitException>(() => NumberTheory.PopCount(BigInteger.One << 16500));
    }

    [TestMethod]
    public void StandaloneFunctionsValidateInputsEvenForTrivialReturns()
    {
        var large = BigInteger.One << 16500;
        Action[] operations =
        [
            () => NumberTheory.Gcd(large, 0),
            () => NumberTheory.Lcm(large, 0),
            () => NumberTheory.Comb(large, 0),
            () => NumberTheory.Comb(1, large),
            () => NumberTheory.Perm(large, 0),
            () => NumberTheory.Perm(1, large),
            () => NumberTheory.IsPrime(large),
            () => NumberTheory.Factorize(large),
            () => NumberTheory.DivCount(large),
            () => NumberTheory.Sigma(large),
            () => NumberTheory.Sigma(1, 10001),
            () => NumberTheory.Totient(large),
            () => NumberTheory.PopCount(large)
        ];
        foreach (var operation in operations)
        {
            Assert.Throws<CalculationLimitException>(operation);
        }
    }

    [TestMethod]
    [DataRow("lcm(15,14)", 4)]
    [DataRow("comb(7,3)", 3)]
    [DataRow("perm(5,3)", 3)]
    [DataRow("sigma(7)", 3)]
    [DataRow("factorial(4)", 3)]
    public void StoredIntegerLimitRemainsIndependent(string expression, int storedBits)
    {
        var engine = new CalculatorEngine(new CalculatorOptions { MaxIntegerBits = storedBits });
        Assert.Throws<CalculationLimitException>(() => engine.Evaluate(expression));
    }

    [TestMethod]
    public void SigmaHonorsConfiguredExponentCeiling()
    {
        Assert.AreEqual(new BigInteger(9), NumberTheory.Sigma(2, 3));
        Assert.Throws<CalculationLimitException>(() => NumberTheory.Sigma(2, 10001));
        var engine = new CalculatorEngine(new CalculatorOptions { MaxExponentMagnitude = 3 });
        Assert.Throws<CalculationLimitException>(() => engine.Evaluate("sigma(2,4)"));
    }

    [TestMethod]
    [DataRow("0!", 1)]
    [DataRow("factorial(5)", 120)]
    [DataRow("comb(10,5)", 252)]
    [DataRow("ncr(10,5)", 252)]
    [DataRow("choose(10,5)", 252)]
    [DataRow("binomial(10,5)", 252)]
    [DataRow("perm(10,5)", 30240)]
    [DataRow("npr(10,5)", 30240)]
    [DataRow("gcd(12,18,30)", 6)]
    [DataRow("lcm(12,18,30)", 180)]
    [DataRow("isprime(10007)", 1)]
    [DataRow("isprime(10403)", 0)]
    [DataRow("divcount(12)", 6)]
    [DataRow("divisorcount(12)", 6)]
    [DataRow("sigma(12)", 28)]
    [DataRow("sigma(12,0)", 6)]
    [DataRow("sigma(12,2)", 210)]
    [DataRow("divsum(12)", 28)]
    [DataRow("divsum(12,2)", 210)]
    [DataRow("totient(12)", 4)]
    [DataRow("phi(12)", 4)]
    [DataRow("eulerphi(12)", 4)]
    [DataRow("popcount(255)", 8)]
    [DataRow("bitcount(255)", 8)]
    public void DefaultBuiltinResultsAreUnchanged(string expression, int expected)
    {
        Assert.AreEqual(new BigInteger(expected), Calculator.Evaluate(expression).IntegerValue);
    }

    [TestMethod]
    public void FunctionLimitExceptionKeepsItsSourceSpan()
    {
        var engine = new CalculatorEngine(new CalculatorOptions { MaxIntegerBits = 9 });
        var exception = Assert.Throws<CalculationLimitException>(() => engine.Evaluate("1 + factorial(6)"));
        Assert.AreEqual(EvaluationError.LimitExceeded, exception.Diagnostic.Code);
        Assert.AreEqual(4, exception.Diagnostic.Position);
        Assert.AreEqual(12, exception.Diagnostic.Length);
    }
}
