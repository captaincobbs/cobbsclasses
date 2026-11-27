using System;

using HarmonyLib;

using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;

namespace CobbsClasses.Source.Patches
{
    [HarmonyPatch(typeof(EntityBehaviorHealth), nameof(EntityBehaviorHealth.OnEntityReceiveDamage))]
    internal static class DamageResistancePatch
    {
        private static TagSetFast animalTag;
        private static TagSetFast rustCreatureTag;
        private static TagSetFast mechanicalTag;

        internal static void InitializeTags(ICoreAPI api)
        {
            animalTag = api.EntityTagRegistry.CreateTagSet("animal");
            rustCreatureTag = api.EntityTagRegistry.CreateTagSet("rust-creature");
            mechanicalTag = api.EntityTagRegistry.CreateTagSet("mechanical");
        }

        private static void Prefix(EntityBehaviorHealth __instance, DamageSource? damageSource, ref float damage)
        {
            if (__instance.entity is not EntityPlayer player || player.World.Side != EnumAppSide.Server || damage <= 0f || damageSource is null || damageSource.Type == EnumDamageType.Heal)
            {
                return;
            }

            damage *= RemainingDamage(player, TraitAttributes.Combat.DamageResistance);
            damage *= RemainingDamage(player, GetSourceResistance(damageSource));
            if (damageSource.Duration <= TimeSpan.Zero)
            {
                damage *= RemainingDamage(player, GetTypeResistance(damageSource.Type));
            }
        }

        private static string? GetSourceResistance(DamageSource damageSource)
        {
            Entity? attacker = damageSource.GetCauseEntity();
            if (attacker is EntityBeeMob || damageSource.SourceEntity is EntityBeeMob)
            {
                return TraitAttributes.Combat.BeeResistance;
            }

            if (attacker is null)
            {
                return null;
            }

            if (attacker is EntityPlayer)
            {
                return TraitAttributes.Combat.PlayerResistance;
            }

            if (attacker.Tags.Overlaps(mechanicalTag) || attacker.Properties.Attributes?["isMechanical"].AsBool() == true)
            {
                return TraitAttributes.Combat.MechanicalResistance;
            }

            if (attacker.Tags.Overlaps(rustCreatureTag))
            {
                return TraitAttributes.Combat.RustedResistance;
            }

            if (attacker.Tags.Overlaps(animalTag))
            {
                return TraitAttributes.Combat.AnimalResistance;
            }

            return null;
        }

        private static string? GetTypeResistance(EnumDamageType damageType)
        {
            return damageType switch
            {
                EnumDamageType.Acid => TraitAttributes.Combat.AcidResistance,
                EnumDamageType.BluntAttack => TraitAttributes.Combat.BludgeoningResistance,
                EnumDamageType.Crushing => TraitAttributes.Combat.CrushingResistance,
                EnumDamageType.Electricity => TraitAttributes.Combat.ElectricityResistance,
                EnumDamageType.Fire => TraitAttributes.Combat.FireResistance,
                EnumDamageType.Frost => TraitAttributes.Combat.FrostResistance,
                EnumDamageType.Gravity => TraitAttributes.Combat.GravityResistance,
                EnumDamageType.Heat => TraitAttributes.Combat.HeatResistance,
                EnumDamageType.Hunger => TraitAttributes.Survival.HungerDamageResistance,
                EnumDamageType.Injury => TraitAttributes.Combat.InjuryResistance,
                EnumDamageType.PiercingAttack => TraitAttributes.Combat.PiercingResistance,
                EnumDamageType.Poison => TraitAttributes.Combat.PoisonResistance,
                EnumDamageType.SlashingAttack => TraitAttributes.Combat.SlashingResistance,
                EnumDamageType.Suffocation => TraitAttributes.Combat.SuffocationResistance,
                _ => null
            };
        }

        private static float RemainingDamage(EntityPlayer player, string? stat)
        {
            if (stat is null)
            {
                return 1f;
            }

            return 1f - Math.Clamp(player.Stats.GetBlended(stat) - 1f, -1f, 1f);
        }
    }
}