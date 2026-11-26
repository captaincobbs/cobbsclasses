using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace CobbsClasses.Source.Conditions
{
    internal sealed class EnvironmentCondition : IConditionalTrait
    {
        private static readonly ConditionRules Outside = ConditionRules.WithSuffix("Outside");
        private static readonly ConditionRules Indoors = ConditionRules.WithSuffix("Indoors");
        private static readonly ConditionRules Underground = ConditionRules.WithSuffix("Underground");

        public void Evaluate(in ConditionContext context, ConditionalModifiers modifiers)
        {
            EntityStats stats = context.Stats;
            float surface = stats.GetBlended("surfaceStabilityLoss") - 1f;
            float underground = stats.GetBlended("undergroundStabilityResistance") - 1f;
            float darkvision = stats.GetBlended("darkvisionStrengthUnderground") - 1f;
            if (surface == 0f && underground == 0f && darkvision == 0f && !Outside.HasAny(stats) && !Indoors.HasAny(stats) && !Underground.HasAny(stats))
            {
                return;
            }

            EntityPos position = context.Entity.Pos;
            BlockPos blockPosition = position.AsBlockPos;
            IBlockAccessor blocks = context.Entity.World.BlockAccessor;

            bool outside = position.Y + 1 >= blocks.GetRainMapHeightAt(blockPosition);
            (outside ? Outside : Indoors).Apply(stats, modifiers);

            if (position.Y + 2 < blocks.GetTerrainMapheightAt(blockPosition))
            {
                modifiers.Add("environmentStabilityResistance", underground);
                modifiers.Add("darkvisionStrength", darkvision);
                Underground.Apply(stats, modifiers);
            }
            else
            {
                modifiers.Add("environmentStabilityChange", surface);
            }
        }
    }
}