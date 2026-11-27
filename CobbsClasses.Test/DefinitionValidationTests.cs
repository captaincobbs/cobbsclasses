using CobbsClasses.Source.API;
using CobbsClasses.Source.Core;

using Newtonsoft.Json.Linq;

namespace CobbsClasses.Tests;

public sealed class DefinitionValidationTests
{
    [Test]
    public void DuplicateDefinitionsAndReferencesAreRemoved()
    {
        JArray traits = JArray.Parse("""[{"code":"a","attributes":{}},{"code":"a"}]""");
        JArray classes = JArray.Parse("""[{"code":"x","traits":["a","a"]},{"code":"x"}]""");
        DefinitionValidation.Validate(traits, classes, new TraitRegistry(), _ => { });
        Assert.Multiple(() =>
        {
            Assert.That(traits, Has.Count.EqualTo(1));
            Assert.That(classes, Has.Count.EqualTo(1));
            Assert.That(((JArray)classes[0]!["traits"]!), Has.Count.EqualTo(1));
        });
    }

    [Test]
    public void InvalidNumericAttributesAreRemovedButUnknownNumericAttributesRemain()
    {
        JArray traits = JArray.Parse("""[{"code":"a","attributes":{"bad":"str","test:flag":1}}]""");
        DefinitionValidation.Validate(traits, [], new TraitRegistry(), _ => { });
        Assert.Multiple(() =>
        {
            Assert.That(traits[0]!["attributes"]!["bad"], Is.Null);
            Assert.That((int)traits[0]!["attributes"]!["test:flag"]!, Is.EqualTo(1));
        });
    }

    private static readonly string[] expected = ["external"];

    [Test]
    public void ExternalTraitReferencesAreRetained()
    {
        JArray classes = JArray.Parse("""[{"code":"x","traits":["external","missing"]}]""");
        DefinitionValidation.Validate([], classes, new TraitRegistry(), _ => { }, ["external"]);
        Assert.That(((JArray)classes[0]!["traits"]!).Values<string>(), Is.EqualTo(expected));
    }

    [Test]
    public void EmergencyCommonerIsEnabledWhenAllClassesAreDisabled()
    {
        JArray classes = JArray.Parse("""[{"code":"cobbs-commoner","enabled":false,"traits":[]}]""");
        DefinitionValidation.EnsureAvailable(classes, _ => { });
        Assert.That((bool)classes[0]!["enabled"]!, Is.True);
    }

    [Test]
    public void AvailableRosterIsNotChanged()
    {
        JArray classes = JArray.Parse("""[{"code":"x","enabled":true,"traits":[]}]""");
        JArray original = (JArray)classes.DeepClone();
        DefinitionValidation.EnsureAvailable(classes, _ => { });
        Assert.That(JToken.DeepEquals(original, classes), Is.True);
    }
}