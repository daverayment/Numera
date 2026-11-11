using System.Globalization;
using Numera.Ast;

namespace Numera.Tests;

[TestClass]
public sealed class ConstantMetadataTests
{
    [TestMethod]
    public void CatalogEnumeratesCanonicalNamesAndEverySupportedAliasInNameOrder()
    {
        string[] canonicalNames = ["e", "false", "phi", "pi", "tau", "true"];
        string[] aliases = ["\u03C0", "\u03C4", "\u03C6", "\u03D5"];
        var allNames = canonicalNames.Concat(aliases).OrderBy(name => name, Identifier.Comparer).ToArray();
        Assert.AreSequenceEqual(canonicalNames, Constants.CanonicalNames.ToArray());
        Assert.AreSequenceEqual(allNames, Constants.AllNames.ToArray());
        Assert.AreSequenceEqual(allNames, Constants.All.Select(info => info.Name).ToArray());
        Assert.AreSequenceEqual(canonicalNames, Constants.Canonical.Select(info => info.Name).ToArray());
        Assert.AreSequenceEqual(Enum.GetValues<ConstantKind>().Order().ToArray(),
            Constants.Canonical.Select(info => info.Kind).Order().ToArray());

        foreach (var entry in Constants.All)
        {
            Assert.IsTrue(Constants.TryGetInfo(entry.Name, out var found));
            Assert.IsNotNull(found);
            Assert.AreSame(entry, found);
            Assert.IsFalse(string.IsNullOrWhiteSpace(entry.Description));
            Assert.IsTrue(Constants.TryGetInfo(entry.CanonicalName, out var canonical));
            Assert.IsNotNull(canonical);
            Assert.IsFalse(canonical.IsAlias);
            Assert.AreEqual(canonical.Kind, entry.Kind);
            Assert.AreEqual(canonical.Value.Serialize(), entry.Value.Serialize());
            Assert.AreSame(canonical.Aliases, entry.Aliases);
            Assert.AreEqual(entry.Name != entry.CanonicalName, entry.IsAlias);
            if (entry.IsAlias)
            {
                Assert.Contains(entry.Name, canonical.Aliases.ToList());
            }
        }

        foreach (var canonical in Constants.Canonical)
        {
            foreach (var alias in canonical.Aliases)
            {
                Assert.IsTrue(Constants.TryGetInfo(alias, out var info));
                Assert.IsNotNull(info);
                Assert.IsTrue(info.IsAlias);
                Assert.AreEqual(alias, info.Name);
                Assert.AreEqual(canonical.Name, info.CanonicalName);
            }
        }
    }

    [TestMethod]
    [DataRow("e", ConstantKind.E, "2.7182818284590452353602874714", CalculationExactness.Unverified)]
    [DataRow("pi", ConstantKind.Pi, "3.1415926535897932384626433833", CalculationExactness.Unverified)]
    [DataRow("tau", ConstantKind.Tau, "6.2831853071795864769252867666", CalculationExactness.Unverified)]
    [DataRow("phi", ConstantKind.Phi, "1.6180339887498948482045868344", CalculationExactness.Unverified)]
    [DataRow("true", ConstantKind.True, "1", CalculationExactness.Exact)]
    [DataRow("false", ConstantKind.False, "0", CalculationExactness.Exact)]
    public void ResolvedValuesRetainExistingPrecisionRepresentationAndExactness(
        string name, ConstantKind kind, string expected, CalculationExactness exactness)
    {
        Assert.IsTrue(Constants.TryGetInfo(name, out var info));
        Assert.IsNotNull(info);
        Assert.AreEqual(kind, info.Kind);
        Assert.IsTrue(Constants.TryResolve(name, out var value));
        Assert.AreEqual(MathValue.DataType.Decimal, value.Kind);
        Assert.AreEqual(decimal.Parse(expected, CultureInfo.InvariantCulture), value.DecimalValue);
        Assert.AreEqual(exactness, value.Exactness);
        Assert.AreEqual(info.Value.Serialize(), value.Serialize());
        Assert.AreEqual(exactness == CalculationExactness.Exact, value.TryGetExactFraction(out _, out _));
    }

    [TestMethod]
    public void EveryDiscoveredSpellingMatchesParsingEvaluationAndConstantReservation()
    {
        var engine = CalculatorEngine.ForCulture(CultureInfo.InvariantCulture);
        foreach (var entry in Constants.All)
        {
            foreach (string spelling in new[] { entry.Name, entry.Name.ToUpperInvariant() })
            {
                Assert.IsTrue(Constants.TryResolve(spelling, out var value));
                Assert.AreEqual(entry.Value.Serialize(), value.Serialize());
                Assert.AreEqual(value.Serialize(), engine.Evaluate(spelling).Value.Serialize());
                Assert.AreEqual(value.Serialize(), engine.Evaluate(new IdentifierNode(spelling, entry.Kind)).Value.Serialize());
                var parsed = engine.Parse(spelling);
                Assert.IsInstanceOfType<IdentifierNode>(parsed);
                Assert.AreEqual(entry.Kind, ((IdentifierNode)parsed).Constant);
                Assert.IsEmpty(parsed.GetVariableReferences());
                Assert.Throws<ArgumentException>(() => CustomFunction.Create(spelling, () => MathValue.One));
                var exception = Assert.Throws<ExpressionException>(() => engine.Evaluate(spelling + " = 2"));
                Assert.AreEqual(EvaluationError.InvalidSyntax, exception.Diagnostic.Code);
            }
        }
    }

    [TestMethod]
    [DataRow("en-US")]
    [DataRow("tr-TR")]
    [DataRow("de-DE")]
    public void DiscoveryAndResolutionAreIndependentOfCurrentCulture(string cultureName)
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(cultureName);
            foreach (var entry in Constants.All)
            {
                Assert.IsTrue(Constants.TryGetInfo(entry.Name.ToUpperInvariant(), out var info));
                Assert.IsNotNull(info);
                Assert.AreEqual(entry.CanonicalName, info.CanonicalName);
                Assert.IsTrue(Constants.TryResolve(entry.Name.ToUpperInvariant(), out var value));
                Assert.AreEqual(entry.Value.Serialize(), value.Serialize());
            }
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [TestMethod]
    public void GreekSymbolsMatchTheExistingParserAlphabetWithoutBroadeningCaseFolding()
    {
        for (char symbol = '\u0370'; symbol <= '\u03FF'; symbol++)
        {
            string name = symbol.ToString();
            bool expected = ConstantCatalog.IsConstantLetter(symbol);
            Assert.AreEqual(expected, Constants.TryGetInfo(name, out _), $"U+{(int)symbol:X4}");
            Assert.AreEqual(expected, Constants.TryResolve(name, out _), $"U+{(int)symbol:X4}");
        }

        Assert.IsTrue(Constants.TryGetInfo("\u03C6", out var phiLetter));
        Assert.IsTrue(Constants.TryGetInfo("\u03D5", out var phiSymbol));
        Assert.IsNotNull(phiLetter);
        Assert.IsNotNull(phiSymbol);
        Assert.AreEqual("\u03C6", phiLetter.Name);
        Assert.AreEqual("\u03D5", phiSymbol.Name);
        Assert.AreEqual(phiLetter.Kind, phiSymbol.Kind);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow(" ")]
    [DataRow(" pi")]
    [DataRow("pi ")]
    [DataRow("pi*2")]
    [DataRow("2")]
    [DataRow("unknown")]
    [DataRow("NaN")]
    [DataRow("Infinity")]
    [DataRow("ans")]
    [DataRow("p\u0130")]
    [DataRow("\u03D6")]
    public void UnknownNamesReturnFalseWithoutEvaluatingOrTrimming(string name)
    {
        Assert.IsFalse(Constants.TryGetInfo(name, out var info));
        Assert.IsNull(info);
        Assert.IsFalse(Constants.TryResolve(name, out var value));
        Assert.AreEqual(default(MathValue).Serialize(), value.Serialize());
    }

    [TestMethod]
    public void NullNamesThrowArgumentNullException()
    {
        Assert.AreEqual("name", Assert.Throws<ArgumentNullException>(() => Constants.TryGetInfo(null!, out _)).ParamName);
        Assert.AreEqual("name", Assert.Throws<ArgumentNullException>(() => Constants.TryResolve(null!, out _)).ParamName);
    }

    [TestMethod]
    public void DiscoveryCollectionsAndNestedAliasesCannotBeModified()
    {
        AssertReadOnly(Constants.AllNames, "changed");
        AssertReadOnly(Constants.CanonicalNames, "changed");
        AssertReadOnly(Constants.All, Constants.All[0] with { Name = "changed" });
        AssertReadOnly(Constants.Canonical, Constants.Canonical[0] with { Name = "changed" });
        foreach (var info in Constants.All)
        {
            AssertReadOnly(info.Aliases, "changed");
        }

        Assert.IsFalse(Constants.TryGetInfo("changed", out _));
    }

    private static void AssertReadOnly<T>(IReadOnlyCollection<T> collection, T replacement)
    {
        Assert.IsInstanceOfType<IList<T>>(collection);
        var list = (IList<T>)collection;
        Assert.IsTrue(list.IsReadOnly);
        Assert.Throws<NotSupportedException>(() => list.Add(replacement));
        if (list.Count > 0)
        {
            Assert.Throws<NotSupportedException>(() => list[0] = replacement);
        }
    }
}