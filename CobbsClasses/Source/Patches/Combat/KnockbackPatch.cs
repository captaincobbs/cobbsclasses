using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

using HarmonyLib;

using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.GameContent;

using static CobbsClasses.Source.Patches.PatchUtils;

namespace CobbsClasses.Source.Patches
{
    [HarmonyPatch(typeof(Entity), nameof(Entity.ReceiveDamage))]
    internal static class KnockbackPatch
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            FieldInfo strength = AccessTools.Field(typeof(DamageSource), nameof(DamageSource.KnockbackStrength));
            MethodInfo adjust = AccessTools.Method(typeof(KnockbackPatch), nameof(AdjustStrength));
            List<CodeInstruction> body = [.. instructions];
            RequireMatches(body.Count(instruction => instruction.LoadsField(strength)), 1, "incoming knockback");

            foreach (CodeInstruction instruction in body)
            {
                yield return instruction;
                if (instruction.LoadsField(strength))
                {
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Call, adjust);
                }
            }
        }

        private static float AdjustStrength(float strength, Entity entity)
        {
            if (entity is not EntityPlayer player || strength == 0f)
            {
                return strength;
            }

            float multiplier = Multiplier(player, TraitAttributes.Combat.KnockbackReceived);
            float armorMultiplier = Multiplier(player, TraitAttributes.Combat.KnockbackWhileArmor);
            if (armorMultiplier != 1f && WearsArmor(player))
            {
                multiplier *= armorMultiplier;
            }

            float blockingMultiplier = Multiplier(player, TraitAttributes.Combat.KnockbackWhileBlocking);
            if (blockingMultiplier != 1f && player.Controls.Sneak && player.LeftHandItemSlot?.Itemstack?.Collectible is ItemShield or ItemShieldFromAttributes)
            {
                multiplier *= blockingMultiplier;
            }

            return strength * multiplier;
        }

        private static bool WearsArmor(EntityPlayer player)
        {
            IInventory? equipment = player.Player?.InventoryManager.GetOwnInventory(GlobalConstants.characterInvClassName);
            if (equipment is null)
            {
                return false;
            }

            foreach (ItemSlot slot in equipment)
            {
                if (slot.Itemstack?.Collectible.GetCollectibleInterface<IWearableStatsSupplier>() is { } wearable
                    && wearable.IsArmorType(slot))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
