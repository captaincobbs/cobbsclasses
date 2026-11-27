using CobbsClasses.Source.API;

using Vintagestory.API.Datastructures;

namespace CobbsClasses.Tests;

public sealed class TraitPersistenceTests
{
    [Test]
    public void LegacyFloatIsRead()
    {
        TreeAttribute tree = new();
        tree.SetFloat("trap", 1.5f);
        Assert.That(TraitPersistence.ReadVersionedFloat(tree, "trap", 1, 0.05f, 100), Is.EqualTo(1.5f));
    }

    [Test]
    public void MissingValueUsesDefault()
    {
        Assert.That(TraitPersistence.ReadVersionedFloat(new TreeAttribute(), "trap", 1, 0.05f, 100), Is.EqualTo(1f));
    }

    [Test]
    public void InvalidStoredValueUsesDefault()
    {
        TreeAttribute tree = new();
        TraitPersistence.WriteVersionedFloat(tree, "trap", float.NaN, 1, 0.05f, 100);
        Assert.That(TraitPersistence.ReadVersionedFloat(tree, "trap", 1, 0.05f, 100), Is.EqualTo(1f));
    }

    [Test]
    public void FutureVersionUsesDefault()
    {
        TreeAttribute tree = new();
        tree.SetFloat("trap", 2);
        tree.SetInt("trap:version", 999);
        Assert.That(TraitPersistence.ReadVersionedFloat(tree, "trap", 1, 0.05f, 100), Is.EqualTo(1f));
    }

    [Test]
    public void PersistedValueIsClamped()
    {
        TreeAttribute tree = new();
        TraitPersistence.WriteVersionedFloat(tree, "trap", 200, 1, 0.05f, 100);
        Assert.That(TraitPersistence.ReadVersionedFloat(tree, "trap", 1, 0.05f, 100), Is.EqualTo(100f));
    }
}