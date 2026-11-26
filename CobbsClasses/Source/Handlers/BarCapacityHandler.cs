using System;

using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace CobbsClasses.Source.Handlers
{
    public static class BarCapacityHandler
    {
        private const int BASE_LUNG_CAPACITY = 40000;
        private const float BASE_HUNGER_CAPACITY = 1500f;
        private const string HEALTH_MODIFIER_KEY = "cobbsclasses:maxHealthMul";

        public static void Refresh(IServerPlayer player)
        {
            EntityPlayer entity = player.Entity;
            if (entity.GetBehavior<EntityBehaviorBreathe>() is { } breathing)
            {
                int baseOxygen = entity.World.Config.GetAsInt("lungCapacity", BASE_LUNG_CAPACITY);
                float maximum = baseOxygen * Math.Max(0.01f, entity.Stats.GetBlended(TraitAttributes.Survival.BreathCapacity));
                if (breathing.MaxOxygen != maximum)
                {
                    breathing.MaxOxygen = maximum;
                    breathing.Oxygen = Math.Min(breathing.Oxygen, maximum);
                }
            }

            if (entity.GetBehavior<EntityBehaviorHunger>() is { } hunger)
            {
                float maximum = BASE_HUNGER_CAPACITY * Math.Max(0.01f, entity.Stats.GetBlended(TraitAttributes.Survival.MaxHungerMul));
                if (hunger.MaxSaturation != maximum)
                {
                    hunger.MaxSaturation = maximum;
                    hunger.Saturation = Math.Min(hunger.Saturation, maximum);
                }
            }

            if (entity.GetBehavior<EntityBehaviorHealth>() is { } health)
            {
                float bonusHealth = health.BaseMaxHealth * ((float)Math.Max(0.01f, entity.Stats.GetBlended(TraitAttributes.Survival.MaxHealthmul)) - 1);
                health.SetMaxHealthModifiers(HEALTH_MODIFIER_KEY, bonusHealth);
            }
        }
    }
}
