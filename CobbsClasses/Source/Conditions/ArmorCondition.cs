using System;

using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.GameContent;

namespace CobbsClasses.Source.Conditions
{
    internal sealed class ArmorCondition : IConditionalTrait
    {
        private static readonly (string Input, string Output)[] Stats =
        [
            ("moveSpeed", "walkspeed"),
            ("sneakSpeed", "sneakSpeed"),
            ("healthRegenRate", "healthRegenRate"),
            ("healthRegenFlat", "healthRegenFlat"),
            ("animalSeekingRange", "animalSeekingRange"),
            ("armorProtection", "armorProtection"),
            ("armorFlatProtection", "armorFlatProtection"),
            ("armorWalkSpeedAffectedness", "armorWalkSpeedAffectedness"),
            ("armorHungerAffectedness", "armorHungerAffectedness"),
            ("armorHealingAffectedness", "armorHealingAffectedness"),
            ("armorRangedAffectedness", "armorRangedAffectedness"),
            ("toolSpeed", "toolSpeedMul"),
            ("globalDamage", "globalDamage"),
            ("hungerrate", "hungerrate"),
            ("swimSpeed", "swimSpeed")
        ];

        private static readonly ConditionRules Unarmored = ConditionRules.WithSuffix("Unarmored", Stats);
        private static readonly ConditionRules Light = ConditionRules.WithSuffix("LightArmor", Stats);
        private static readonly ConditionRules Heavy = ConditionRules.WithSuffix("HeavyArmor", Stats);

        public void Evaluate(in ConditionContext context, ConditionalModifiers modifiers)
        {
            EntityStats stats = context.Stats;
            if (!Unarmored.HasAny(stats) && !Light.HasAny(stats) && !Heavy.HasAny(stats))
            {
                return;
            }

            ConditionRules? selected = GetArmorRules(context.Player);
            selected?.Apply(stats, modifiers);
        }

        private static ConditionRules? GetArmorRules(IPlayer player)
        {
            IInventory? equipment = player.InventoryManager.GetOwnInventory(GlobalConstants.characterInvClassName);
            if (equipment is null)
            {
                return null;
            }

            bool hasArmor = false;
            float movementPenalty = 0f;
            foreach (ItemSlot slot in equipment)
            {
                if (slot.Itemstack?.Collectible.GetCollectibleInterface<IWearableStatsSupplier>() is not { } wearable || !wearable.IsArmorType(slot))
                {
                    continue;
                }

                hasArmor = true;
                movementPenalty += Math.Max(0f, -(wearable.GetStatModifiers(slot)?.walkSpeed ?? 0f));
            }

            if (!hasArmor)
            {
                return Unarmored;
            }

            return movementPenalty <= 0.10001f ? Light : Heavy;
        }
    }
}


