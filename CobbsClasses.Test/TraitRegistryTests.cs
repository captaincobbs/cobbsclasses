using CobbsClasses.Source;
using CobbsClasses.Source.API;

namespace CobbsClasses.Tests;

public sealed class TraitRegistryTests
{
    [Test]
    public void BuiltInAttributesAreRegistered()
    {
        TraitRegistry registry = new();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(registry.TryGet("wetResistance", out TraitAttributeDefinition? definition), Is.True);
            Assert.That(definition!.Kind, Is.EqualTo(AttributeKind.Probability));
            Assert.That(definition.Maximum, Is.EqualTo(1d));
        }
    }

    [Test]
    public void ExternalAttributesCanBeRegistered()
    {
        TraitRegistry registry = new();
        registry.Register(new TraitAttributeDefinition("test:flag", AttributeKind.Boolean, 0, 0, 1, ["test"]));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(registry.TryGet("test:flag", out TraitAttributeDefinition? definition), Is.True);
            Assert.That(definition!.Kind, Is.EqualTo(AttributeKind.Boolean));
        }
    }

    [Test]
    public void RegistrationAndLookupDefensivelyCopyRequirements()
    {
        TraitRegistry registry = new();
        string[] requirements = ["test"];
        registry.Register(new TraitAttributeDefinition("test:flag", AttributeKind.Boolean, 0, 0, 1, requirements));
        requirements[0] = "mutated";
        TraitAttributeDefinition? definition = null;
        Assert.Multiple(() =>
        {
            Assert.That(registry.TryGet("test:flag", out definition), Is.True);
            Assert.That(definition!.RequiredMods![0], Is.EqualTo("test"));
        });
        definition!.RequiredMods![0] = "also mutated";
        using (Assert.EnterMultipleScope())
        {
            Assert.That(registry.TryGet("test:flag", out definition), Is.True);
            Assert.That(definition!.RequiredMods![0], Is.EqualTo("test"));
        }
    }

    [Test]
    public void DuplicateRegistrationIsRejected()
    {
        TraitRegistry registry = new();
        TraitAttributeDefinition definition = new("test:flag", AttributeKind.Boolean, 0, 0, 1);
        registry.Register(definition);
        Assert.Throws<InvalidOperationException>(() => registry.Register(definition));
    }

    [Test]
    public void InvalidRangeIsRejected()
    {
        TraitRegistry registry = new();
        Assert.Throws<ArgumentException>(() => registry.Register(new TraitAttributeDefinition("test:invalid", AttributeKind.Multiplier, 0, 1, -1)));
    }
}