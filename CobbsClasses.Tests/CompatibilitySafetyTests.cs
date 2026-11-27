using CobbsClasses.Source.Compatibility.Requirements;
using Newtonsoft.Json.Linq;

namespace CobbsClasses.Tests;

public sealed class CompatibilitySafetyTests
{
    [Test]
    public void VanillaPolicyPreservesThirdPartyClasses()
    {
        JArray classes = JArray.Parse("""[{"code":"commoner"},{"code":"hunter","enabled":false},{"code":"external"}]""");
        ClassRosterPolicy.ApplyVanilla(classes, false);
        Assert.That(ClassRosterPolicy.EnabledCodes(classes), Is.EquivalentTo(new[] { "commoner", "external" }));
        ClassRosterPolicy.ApplyVanilla(classes, true);
        Assert.That(ClassRosterPolicy.EnabledCodes(classes), Is.EquivalentTo(new[] { "external" }));
    }

    [Test]
    public void FrameworkModeDisablesOnlyLocalClasses()
    {
        JArray classes = JArray.Parse("""[{"code":"cobbs-commoner"},{"code":"cobbs-miner"}]""");
        ClassRosterPolicy.ApplyLocal(classes, false, ["cobbs-miner"]);
        Assert.That(ClassRosterPolicy.EnabledCodes(classes), Is.EquivalentTo(new[] { "cobbs-commoner" }));
        ClassRosterPolicy.ApplyLocal(classes, true, ["external"]);
        Assert.That(ClassRosterPolicy.EnabledCodes(classes), Is.Empty);
    }

    [TestCase("cobbs-miner", "cobbs-commoner")]
    [TestCase("cobbs-hunter", "hunter")]
    [TestCase("cobbs-commoner", null)]
    [TestCase("external", null)]
    [TestCase(null, null)]
    public void MigrationOnlyReplacesUnavailableBundledClasses(string? current, string? expected)
    {
        Dictionary<string, string> mappings = new() { ["cobbs-hunter"] = "hunter" };
        Assert.That(ClassRosterPolicy.MigrationTarget(current, ["cobbs-commoner", "hunter"], mappings), Is.EqualTo(expected));
    }

    [Test]
    public void EmptyRosterCannotMigrate()
    {
        Assert.That(ClassRosterPolicy.MigrationTarget("cobbs-miner", [], new Dictionary<string, string>()), Is.Null);
    }
}
