using CobbsClasses.Source.API;
using CobbsClasses.Source.Compatibility.Requirements;
using CobbsClasses.Source.Core;

using Newtonsoft.Json.Linq;

namespace CobbsClasses.Tests;

public sealed class ModRequirementTests
{
    private static bool Enabled(string code) => code is "butchering" or "footprints";

    [TestCase("""{"allOf":["butchering","footprints"]}""", true)]
    [TestCase("""{"allOf":["butchering","alchemy"]}""", false)]
    [TestCase("""{"anyOf":["alchemy","footprints"]}""", true)]
    [TestCase("""{"anyOf":[]}""", false)]
    [TestCase("""{"noneOf":["alchemy"]}""", true)]
    [TestCase("""{"noneOf":["footprints"]}""", false)]
    [TestCase("""{"allOf":["butchering"],"noneOf":["alchemy"],"anyOf":["alchemy","footprints"]}""", true)]
    [TestCase("""{"allOf":["butchering"],"noneOf":["footprints"]}""", false)]
    public void EvaluatesModRequirements(string json, bool expected)
    {
        Assert.That(ModRequirementEvaluator.Matches(JObject.Parse(json), Enabled), Is.EqualTo(expected));
    }

    [Test]
    public void InvalidRequirementFailsClosed()
    {
        JArray traits = JArray.Parse("""[{"code":"a","requiresMods":{"typo":["mod"]},"attributes":{"walkspeed":1}}]""");
        DefinitionValidation.Validate(traits, new JArray(), new TraitRegistry(), _ => { });
        HashSet<string> unavailable = TraitCompatibilityResolver.Resolve(traits, Enabled);
        Assert.That(unavailable, Does.Contain("a"));
    }

    [Test]
    public void LastMatchingModifierOverridesEarlierValues()
    {
        JArray traits = JArray.Parse("""[{"code":"a","attributes":{"speed":1},"modifiers":[{"requiresMods":{"noneOf":["alchemy"]},"attributes":{"speed":2}},{"requiresMods":{"allOf":["butchering"]},"attributes":{"speed":3}}]}]""");
        TraitCompatibilityResolver.Resolve(traits, Enabled);
        Assert.That((int)traits[0]!["attributes"]!["speed"]!, Is.EqualTo(3));
    }

    [Test]
    public void UnavailableTraitIsRemovedFromClass()
    {
        JArray traits = JArray.Parse("""[{"code":"a","requiresMods":{"allOf":["alchemy"]},"attributes":{"speed":1}}]""");
        JArray classes = JArray.Parse("""[{"code":"x","traits":["a"]}]""");
        HashSet<string> unavailable = TraitCompatibilityResolver.Resolve(traits, Enabled);
        ClassCompatibilityResolver.Resolve(classes, unavailable, Enabled, _ => false, false);
        Assert.That(((JArray)classes[0]!["traits"]!), Is.Empty);
    }

    [TestCase(false, false, false)]
    [TestCase(true, false, true)]
    [TestCase(true, true, false)]
    public void OverrideNeverOverridesManualDisable(bool ignoreRequirements, bool manuallyDisabled, bool expected)
    {
        JArray classes = JArray.Parse("""[{"code":"x","enabled":true,"requiresMods":{"allOf":["alchemy"]},"traits":[]}]""");
        ClassCompatibilityResolver.Resolve(classes, [], Enabled, _ => manuallyDisabled, ignoreRequirements);
        Assert.That((bool)classes[0]!["enabled"]!, Is.EqualTo(expected));
    }
}