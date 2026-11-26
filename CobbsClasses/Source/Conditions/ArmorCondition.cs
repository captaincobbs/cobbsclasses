using System;

using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace CobbsClasses.Source.Conditions
{
    internal sealed class ArmorCondition : IConditionalTrait
    {
        private static readonly (string Input, string Output)[] Stats =
        [
            (TraitAttributes.Movement.MoveSpeed, TraitAttributes.Movement.Walkspeed),
            (TraitAttributes.Movement.SneakSpeed, TraitAttributes.Movement.SneakSpeed),
            (TraitAttributes.Survival.HealthRegenRate, TraitAttributes.Survival.HealthRegenRate),
            (TraitAttributes.Survival.HealthRegenFlat, TraitAttributes.Survival.HealthRegenFlat),
            (TraitAttributes.Gathering.AnimalSeekingRange, TraitAttributes.Gathering.AnimalSeekingRange),
            (TraitAttributes.Equipment.ArmorProtection, TraitAttributes.Equipment.ArmorProtection),
            (TraitAttributes.Equipment.ArmorFlatProtection, TraitAttributes.Equipment.ArmorFlatProtection),
            (TraitAttributes.Equipment.ArmorWalkSpeedAffectedness, TraitAttributes.Equipment.ArmorWalkSpeedAffectedness),
            (TraitAttributes.Equipment.ArmorHungerAffectedness, TraitAttributes.Equipment.ArmorHungerAffectedness),
            (TraitAttributes.Equipment.ArmorHealingAffectedness, TraitAttributes.Equipment.ArmorHealingAffectedness),
            (TraitAttributes.Equipment.ArmorRangedAffectedness, TraitAttributes.Equipment.ArmorRangedAffectedness),
            (TraitAttributes.Tools.ToolSpeed, TraitAttributes.Tools.ToolSpeedMul),
            (TraitAttributes.Combat.GlobalDamage, TraitAttributes.Combat.GlobalDamage),
            (TraitAttributes.Survival.Hungerrate, TraitAttributes.Survival.Hungerrate),
            (TraitAttributes.Movement.SwimSpeed, TraitAttributes.Movement.SwimSpeed)
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


