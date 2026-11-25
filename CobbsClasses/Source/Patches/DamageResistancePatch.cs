using HarmonyLib;

using System;

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

        private static void Prefix(EntityBehaviorHealth __instance, DamageSource damageSource, ref float damage)
        {
            if (__instance.entity is not EntityPlayer player || player.World.Side != EnumAppSide.Server || damage <= 0f ||damageSource.Type == EnumDamageType.Heal)
            {
                return;
            }

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
                return "beeResistance";
            }

            if (attacker is null)
            {
                return null;
            }

            if (attacker is EntityPlayer)
            {
                return "playerResistance";
            }

            if (attacker.Tags.Overlaps(mechanicalTag) || attacker.Properties.Attributes?["isMechanical"].AsBool() == true)
            {
                return "mechanicalResistance";
            }

            if (attacker.Tags.Overlaps(rustCreatureTag))
            {
                return "rustedResistance";
            }

            if (attacker.Tags.Overlaps(animalTag))
            {
                return "animalResistance";
            }

            return null;
        }

        private static string? GetTypeResistance(EnumDamageType damageType)
        {
            return damageType switch
            {
                EnumDamageType.Acid => "acidResistance",
                EnumDamageType.BluntAttack => "bludgeoningResistance",
                EnumDamageType.Crushing => "crushingResistance",
                EnumDamageType.Electricity => "electricityResistance",
                EnumDamageType.Fire => "fireResistance",
                EnumDamageType.Frost => "frostResistance",
                EnumDamageType.Gravity => "gravityResistance",
                EnumDamageType.Heat => "heatResistance",
                EnumDamageType.Hunger => "hungerDamageResistance",
                EnumDamageType.Injury => "injuryResistance",
                EnumDamageType.PiercingAttack => "piercingResistance",
                EnumDamageType.Poison => "poisonResistance",
                EnumDamageType.SlashingAttack => "slashingResistance",
                EnumDamageType.Suffocation => "suffocationResistance",
                _ => null
            };
        }

        private static float RemainingDamage(EntityPlayer player, string? stat)
        {
            if (stat is null)
            {
                return 1f;
            }

            return 1f - Math.Clamp(player.Stats.GetBlended(stat) - 1f, 0f, 1f);
        }
    }
}
