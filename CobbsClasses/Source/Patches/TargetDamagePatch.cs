using HarmonyLib;

using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;

using static CobbsClasses.Source.Patches.PatchUtils;

namespace CobbsClasses.Source.Patches
{
    [HarmonyPatch(typeof(EntityBehaviorHealth), nameof(EntityBehaviorHealth.OnEntityReceiveDamage))]
    internal static class TargetDamagePatch
    {
        private static TagSetFast animalTag;
        private static TagSetFast rustedTag;

        internal static void InitializeTags(ICoreAPI api)
        {
            animalTag = api.EntityTagRegistry.CreateTagSet("animal");
            rustedTag = api.EntityTagRegistry.CreateTagSet("rust-creature");
        }

        private static void Prefix(EntityBehaviorHealth __instance, DamageSource? damageSource, ref float damage)
        {
            Entity target = __instance.entity;
            if (target.World.Side != EnumAppSide.Server || damage <= 0f || damageSource is null || damageSource.Type == EnumDamageType.Heal || damageSource.GetCauseEntity() is not EntityPlayer attacker || attacker == target)
            {
                return;
            }

            damage *= Multiplier(attacker, "globalDamage");
            if (damageSource.SourceEntity == attacker && attacker.Player?.InventoryManager.ActiveHotbarSlot.Empty == true)
            {
                damage *= Multiplier(attacker, "bareHandDamage");
            }

            if (target is EntityPlayer)
            {
                damage *= Multiplier(attacker, "playersDamage");
            }
            else if (target.Tags.Overlaps(rustedTag))
            {
                damage *= Multiplier(attacker, "rustedDamage");
            }
            else if (target.Tags.Overlaps(animalTag))
            {
                damage *= Multiplier(attacker, "animalsDamage");
            }
        }
    }
}