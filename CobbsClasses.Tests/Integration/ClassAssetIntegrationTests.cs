using CobbsClasses.Source.API;
using CobbsClasses.Source.Compatibility.Requirements;
using CobbsClasses.Source.Core;

using Newtonsoft.Json.Linq;

using Vintagestory.API.Datastructures;

namespace CobbsClasses.Tests.Integration;

[Category("Integration")]
public sealed class ClassAssetIntegrationTests
{
    private static string Asset(string name) => Path.Combine(TestContext.CurrentContext.TestDirectory, "TestAssets", name);

    private static (JArray Traits, JArray Classes) ReadAssets() => (JArray.Parse(File.ReadAllText(Asset("traits.json"))),
            JArray.Parse(File.ReadAllText(Asset("characterclasses.json"))));

    private static IEnumerable<JObject> EnabledClasses(JArray classes) => classes.OfType<JObject>().Where(entry => (bool?)entry["enabled"] != false);

    [Test]
    public void ShippedDefinitionsHaveUniqueCodesAndValidReferences()
    {
        (JArray? traits, JArray? classes) = ReadAssets();
        List<string> warnings = [];
        int traitCount = traits.Count;
        int classCount = classes.Count;
        DefinitionValidation.Validate(traits, classes, new TraitRegistry(), warnings.Add);

        Assert.Multiple(() =>
        {
            Assert.That(traits, Has.Count.EqualTo(traitCount), string.Join(Environment.NewLine, warnings));
            Assert.That(classes, Has.Count.EqualTo(classCount), string.Join(Environment.NewLine, warnings));
            Assert.That(warnings.Where(warning => warning.StartsWith("Removed", StringComparison.Ordinal)), Is.Empty);
        });
    }

    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    public void ShippedAssetsResolveAndRoundTrip(bool simulateAllModsPresent, bool ignoreClassRequirements)
    {
        (JArray? traits, JArray? classes) = ReadAssets();
        DefinitionValidation.Validate(traits, classes, new TraitRegistry(), _ => { });
        HashSet<string> unavailable = TraitCompatibilityResolver.Resolve(traits, _ => simulateAllModsPresent);
        ClassCompatibilityResolver.Resolve(classes, unavailable, _ => simulateAllModsPresent, _ => false, ignoreClassRequirements);
        DefinitionValidation.EnsureAvailable(classes, _ => { });

        HashSet<string> known = [.. traits.OfType<JObject>().Select(trait => (string)trait["code"]!)];
        Assert.That(EnabledClasses(classes), Is.Not.Empty);
        foreach (JObject characterClass in EnabledClasses(classes))
        {
            foreach (string code in ((JArray)characterClass["traits"]!).Values<string>()!)
            {
                using (Assert.EnterMultipleScope())
                {
                    Assert.That(known, Does.Contain(code), (string?)characterClass["code"]);
                    Assert.That(unavailable, Does.Not.Contain(code), (string?)characterClass["code"]);
                }
            }
        }

        using (Assert.EnterMultipleScope())
        {
            Assert.That(traits.OfType<JObject>().All(trait => trait["requiresMods"] is null && trait["modifiers"] is null), Is.True);
            Assert.That(classes.OfType<JObject>().All(entry => entry["requiresMods"] is null && entry["conditionalTraits"] is null), Is.True);
        }
        JObject payload = new() { ["traits"] = traits, ["classes"] = classes };
        TreeAttribute worldConfig = new();
        worldConfig.SetString("cobbsclasses:resolvedClassDefinitions", ResolvedDefinitionEncoding.Encode(payload));

        using MemoryStream stream = new();
        using (BinaryWriter writer = new(stream, System.Text.Encoding.UTF8, true))
        {
            worldConfig.ToBytes(writer);
        }

        stream.Position = 0;
        TreeAttribute clientConfig = new();
        using (BinaryReader reader = new(stream))
        {
            clientConfig.FromBytes(reader);
        }

        JObject clientJson = JObject.Parse(clientConfig.ToJsonToken());
        JObject decoded = ResolvedDefinitionEncoding.Decode((string)clientJson["cobbsclasses:resolvedClassDefinitions"]!);
        Assert.That(JToken.DeepEquals(payload, decoded), Is.True);
    }

    private static readonly string[] expected = ["cobbs-commoner"];

    [Test]
    public void DisablingAllClassesLeavesEmergencyCommoner()
    {
        (JArray? traits, JArray? classes) = ReadAssets();
        DefinitionValidation.Validate(traits, classes, new TraitRegistry(), _ => { });
        HashSet<string> unavailable = TraitCompatibilityResolver.Resolve(traits, _ => false);
        ClassCompatibilityResolver.Resolve(classes, unavailable, _ => false, _ => true, false);
        DefinitionValidation.EnsureAvailable(classes, _ => { });
        Assert.That(EnabledClasses(classes).Select(entry => (string?)entry["code"]), Is.EqualTo(expected));
    }

    [Test]
    public void ShippedClassAndTraitNamesAreLocalized()
    {
        (JArray? traits, JArray? classes) = ReadAssets();
        JObject localization = JObject.Parse(File.ReadAllText(Asset("en.json")));
        List<string> missing = [];
        foreach (JObject characterClass in classes.OfType<JObject>().Where(entry => ((string?)entry["code"])?.StartsWith("cobbs-", StringComparison.Ordinal) == true))
        {
            string code = (string)characterClass["code"]!;
            foreach (string key in new[] { $"game:characterclass-{code}", $"game:characterdesc-{code}" })
            {
                if (localization[key] is null)
                {
                    missing.Add(key);
                }
            }
        }

        foreach (JObject trait in traits.OfType<JObject>())
        {
            string key = $"game:trait-{(string)trait["code"]!}";
            if (localization[key] is null)
            {
                missing.Add(key);
            }
        }

        Assert.That(missing, Is.Empty, string.Join(Environment.NewLine, missing));
    }

    [Test]
    public void AllShippedAttributeValuesAndOverridesHaveEnglishLocalization()
    {
        (JArray? traits, JArray _) = ReadAssets();
        JObject localization = JObject.Parse(File.ReadAllText(Asset("en.json")));
        List<string> missing = [];
        foreach (JObject trait in traits.OfType<JObject>())
        {
            IEnumerable<JObject> attributeSets = new[] { trait["attributes"] }.OfType<JObject>()
                .Concat((trait["modifiers"] as JArray ?? []).OfType<JObject>()
                .Select(modifier => modifier["attributes"]).OfType<JObject>());
            foreach (JObject attributes in attributeSets)
            {
                foreach (JProperty attribute in attributes.Properties())
                {
                    string key = string.Format(System.Globalization.CultureInfo.InvariantCulture,
                        "game:charattribute-{0}-{1}", attribute.Name, (double)attribute.Value);
                    if (localization[key] is null)
                    {
                        missing.Add($"{trait["code"]}: {key}");
                    }
                }
            }

            if (trait["attributeDescriptions"] is JObject overrides)
            {
                foreach (JProperty entry in overrides.Properties())
                {
                    string key = (string)entry.Value!;
                    if (key.StartsWith("cobbsclasses:", StringComparison.Ordinal))
                    {
                        key = key["cobbsclasses:".Length..];
                    }

                    if (localization[key] is null)
                    {
                        missing.Add($"{trait["code"]}: {key}");
                    }
                }
            }
        }

        Assert.That(missing, Is.Empty, string.Join(Environment.NewLine, missing));
    }
}