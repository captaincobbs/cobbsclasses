using System.Reflection;

using CobbsClasses.Source;
using CobbsClasses.Source.API;
using CobbsClasses.Source.Conditions;

using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace CobbsClasses.Tests.Integration;

[Category("Integration")]
public sealed class ConditionalStatIntegrationTests
{
    private const string TraitSource = "test-class";
    private const string ExternalSource = "test-external";

    // This exercises the actual refresh application path without needing a server.
    // Reflection is limited to these two private implementation details.
    private static readonly FieldInfo ModifiersField = typeof(ConditionalTraitSystem)
        .GetField("modifiers", BindingFlags.Instance | BindingFlags.NonPublic)!;
    private static readonly MethodInfo ApplyMethod = typeof(ConditionalTraitSystem)
        .GetMethod("ApplyModifiers", BindingFlags.Instance | BindingFlags.NonPublic)!;

    private static ConditionalModifiers Modifiers(ConditionalTraitSystem system)
        => (ConditionalModifiers)ModifiersField.GetValue(system)!;

    private static void Apply(ConditionalTraitSystem system, EntityStats stats)
        => ApplyMethod.Invoke(system, [stats]);

    [Test]
    public void ConditionalRefreshAddsUpdatesAndRemovesOnlyItsOwnBonus()
    {
        EntityPlayer player = new();
        EntityStats stats = player.Stats;
        const string stat = "walkspeed";
        stats.Set(stat, TraitSource, 0.1f, true);
        stats.Set(stat, ExternalSource, 0.2f, true);
        ConditionalTraitSystem system = new();
        try
        {
            ConditionalModifiers modifiers = Modifiers(system);
            modifiers.Add(stat, 0.25f);
            Apply(system, stats);
            Assert.That(stats.GetBlended(stat), Is.EqualTo(1.55f).Within(0.0001f));

            Apply(system, stats);
            Assert.That(stats.GetBlended(stat), Is.EqualTo(1.55f).Within(0.0001f), "Repeated refresh must not stack.");

            modifiers.Clear();
            modifiers.Add(stat, -0.15f);
            Apply(system, stats);
            Assert.That(stats.GetBlended(stat), Is.EqualTo(1.15f).Within(0.0001f));

            modifiers.Clear();
            Apply(system, stats);
            Assert.That(stats.GetBlended(stat), Is.EqualTo(1.3f).Within(0.0001f));
        }
        finally
        {
            system.Dispose();
        }
    }

    [Test]
    public void ConditionalSourcesDoNotLeakAcrossPlayersAndDisposeRestoresBoth()
    {
        EntityPlayer first = new();
        EntityPlayer second = new();
        ConditionalTraitSystem system = new();
        try
        {
            ConditionalModifiers modifiers = Modifiers(system);
            modifiers.Add("walkspeed", 0.5f);
            Apply(system, first.Stats);
            modifiers.Clear();
            modifiers.Add("walkspeed", -0.2f);
            Apply(system, second.Stats);

            Assert.Multiple(() =>
            {
                Assert.That(first.Stats.GetBlended("walkspeed"), Is.EqualTo(1.5f).Within(0.0001f));
                Assert.That(second.Stats.GetBlended("walkspeed"), Is.EqualTo(0.8f).Within(0.0001f));
            });
        }
        finally
        {
            system.Dispose();
        }

        using (Assert.EnterMultipleScope())
        {
            Assert.That(first.Stats.GetBlended("walkspeed"), Is.EqualTo(1f).Within(0.0001f));
            Assert.That(second.Stats.GetBlended("walkspeed"), Is.EqualTo(1f).Within(0.0001f));
        }
    }

    [Test]
    public void ConditionalRuleReadsGameStatsAndPreservesOtherModifiers()
    {
        EntityPlayer player = new();
        player.Stats.Set("hungerrateOutside", TraitSource, -0.15f, true);
        player.Stats.Set("hungerrate", ExternalSource, 0.1f, true);
        ConditionalModifiers modifiers = new();
        ConditionRules outside = ConditionRules.WithSuffix("Outside");
        Assert.That(outside.HasAny(player.Stats), Is.True);
        outside.Apply(player.Stats, modifiers);
        Assert.That(modifiers.Values["hungerrate"], Is.EqualTo(-0.15f).Within(0.0001f));

        ConditionalTraitSystem system = new();
        try
        {
            Modifiers(system).Add("hungerrate", modifiers.Values["hungerrate"]);
            Apply(system, player.Stats);
            Assert.That(TraitMath.GetMultiplier(player.Stats, "hungerrate"), Is.EqualTo(0.95f).Within(0.0001f));
        }
        finally
        {
            system.Dispose();
        }

        Assert.That(player.Stats.GetBlended("hungerrate"), Is.EqualTo(1.1f).Within(0.0001f));
    }

    [Test]
    public void PersistentStatsSurviveGameSerializationButConditionalStatsDoNot()
    {
        EntityPlayer player = new();
        player.Stats.Set("walkspeed", TraitSource, 0.25f, true);
        player.Stats.Set("walkspeed", ConditionalTraitSystem.ModifierSource, 0.5f, false);
        TreeAttribute saved = new();
        player.Stats.ToTreeAttributes(saved, false);

        EntityPlayer restored = new();
        restored.Stats.FromTreeAttributes(saved);
        Assert.That(restored.Stats.GetBlended("walkspeed"), Is.EqualTo(1.25f).Within(0.0001f));
    }
}