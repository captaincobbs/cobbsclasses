using Vintagestory.GameContent;

namespace CobbsClasses.Source.Conditions
{
    internal sealed class MovementCondition : IConditionalTrait
    {
        private static readonly ConditionRules Sprinting = ConditionRules.WithSuffix("Sprinting");
        private static readonly ConditionRules Sneaking = ConditionRules.WithSuffix("Sneaking");
        private static readonly ConditionRules Blocking = ConditionRules.WithSuffix("Blocking");
        private static readonly ConditionRules Mounted = ConditionRules.WithSuffix("Mounted");

        public void Evaluate(in ConditionContext context, ConditionalModifiers modifiers)
        {
            var entity = context.Entity;
            if (entity.Controls.Sprint)
            {
                Sprinting.Apply(context.Stats, modifiers);
                modifiers.Add("hungerrate", context.Stats.GetBlended("sprintHungerRate") - 1f);
            }

            if (entity.Controls.Sneak)
            {
                Sneaking.Apply(context.Stats, modifiers);
                if (entity.LeftHandItemSlot?.Itemstack?.Collectible is ItemShield or ItemShieldFromAttributes)
                {
                    Blocking.Apply(context.Stats, modifiers);
                }
            }

            if (entity.MountedOn is not null)
            {
                Mounted.Apply(context.Stats, modifiers);
            }
        }
    }
}