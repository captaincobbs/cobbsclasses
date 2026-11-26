using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace CobbsClasses.Source.Conditions
{
    internal sealed class HealthCondition : IConditionalTrait
    {
        private static readonly ConditionRules Wet = ConditionRules.WithSuffix("WhenWet");
        private static readonly ConditionRules Hungry = ConditionRules.WithSuffix("WhenHungry");
        private static readonly ConditionRules Injured = ConditionRules.WithSuffix("WhenInjured");

        public void Evaluate(in ConditionContext context, ConditionalModifiers modifiers)
        {
            EntityPlayer entity = context.Entity;
            if (Wet.HasAny(context.Stats) && entity.GetBehavior<EntityBehaviorBodyTemperature>() is { Wetness: > 0.1f })
            {
                Wet.Apply(context.Stats, modifiers);
            }

            if (Hungry.HasAny(context.Stats) && entity.GetBehavior<EntityBehaviorHunger>() is { } hunger && hunger.Saturation < hunger.MaxSaturation * 0.25f)
            {
                Hungry.Apply(context.Stats, modifiers);
            }

            if (Injured.HasAny(context.Stats) && entity.GetBehavior<EntityBehaviorHealth>() is { } health && health.Health < health.MaxHealth * 0.5f)
            {
                Injured.Apply(context.Stats, modifiers);
            }
        }
    }
}