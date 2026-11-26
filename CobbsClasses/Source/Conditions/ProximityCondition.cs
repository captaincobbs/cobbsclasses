using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Server;

namespace CobbsClasses.Source.Conditions
{
    internal sealed class ProximityCondition(double radius) : IConditionalTrait
    {
        private readonly double radiusSquared = radius * radius;
        private static readonly ConditionRules Alone = new(
            (TraitAttributes.Movement.MoveSpeedAlone, TraitAttributes.Movement.Walkspeed),
            (TraitAttributes.Movement.JumpHeightMulAlone, TraitAttributes.Movement.JumpHeightMul),
            (TraitAttributes.Survival.HungerrateAlone, TraitAttributes.Survival.Hungerrate));

        public void Evaluate(in ConditionContext context, ConditionalModifiers modifiers)
        {
            bool hasAloneBonus = Alone.HasAny(context.Stats);
            float nearbyLoss = context.Stats.GetBlended(TraitAttributes.Survival.StabilityLossFromPlayers) - 1f;
            if (!hasAloneBonus && nearbyLoss == 0f)
            {
                return;
            }

            bool hasNearbyPlayer = false;
            foreach (IPlayer onlinePlayer in context.OnlinePlayers)
            {
                if (onlinePlayer is not IServerPlayer other || other.PlayerUID == context.Player.PlayerUID || !ConditionContext.IsActive(other) || !IsNearby(context.Entity.Pos, other.Entity.Pos))
                {
                    continue;
                }

                hasNearbyPlayer = true;
                if (nearbyLoss != 0f && other.Entity.Stats.GetBlended(TraitAttributes.Survival.StabilityLossFromPlayers) == 1f)
                {
                    modifiers.Add(TraitAttributes.Survival.EnvironmentStabilityChange, nearbyLoss);
                }

                if (nearbyLoss == 0f)
                {
                    break;
                }
            }

            if (hasAloneBonus && !hasNearbyPlayer)
            {
                Alone.Apply(context.Stats, modifiers);
            }
        }

        private bool IsNearby(EntityPos first, EntityPos second)
        {
            if (first.Dimension != second.Dimension)
            {
                return false;
            }

            double x = first.X - second.X;
            double y = first.Y - second.Y;
            double z = first.Z - second.Z;
            return x * x + y * y + z * z <= radiusSquared;
        }
    }
}