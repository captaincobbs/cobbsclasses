using System.Linq;

using CobbsClasses.Source.Behaviors;

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

            damage *= Multiplier(attacker, TraitAttributes.Combat.GlobalDamage);
            // Do not read the held item for a projectile, it could have changed since firing
            if (damageSource.SourceEntity is EntityProjectileBase ||
                (damageSource.SourceEntity == attacker &&
                 attacker.Player?.InventoryManager.ActiveHotbarSlot?.Itemstack?.Collectible.CollectibleBehaviors.OfType<ToolTraitBehavior>().Any(behavior => behavior.IsWeapon) == true))
            {
                damage *= Multiplier(attacker, TraitAttributes.Combat.WeaponDamage);
            }

            if (damageSource.SourceEntity == attacker && attacker.Player?.InventoryManager.ActiveHotbarSlot.Empty == true)
            {
                damage *= Multiplier(attacker, TraitAttributes.Combat.BareHandDamage);
            }

            if (target is EntityPlayer)
            {
                damage *= Multiplier(attacker, TraitAttributes.Combat.PlayersDamage);
            }
            else if (target.Tags.Overlaps(rustedTag))
            {
                damage *= Multiplier(attacker, TraitAttributes.Combat.RustedDamage);
            }
            else if (target.Tags.Overlaps(animalTag))
            {
                damage *= Multiplier(attacker, TraitAttributes.Combat.AnimalsDamage);
            }
        }
    }
}