using CobbsClasses.Source.API;

using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;

namespace CobbsClasses.Source.Behaviors
{
    public sealed class ToolTraitBehavior : CollectibleBehavior
    {
        private string?[] speedTraits = [];
        private string?[] durabilityLossTraits = [];
        private string?[] reachTraits = [];

        public ToolTraitBehavior(CollectibleObject collectible) : base(collectible) { }

        public override void Initialize(JsonObject properties)
        {
            base.Initialize(properties);
            speedTraits = properties["speedStat"].AsArray<string>() ?? [];
            durabilityLossTraits = properties["durabilityLossStat"].AsArray<string>() ?? [];
            reachTraits = properties["reachStat"].AsArray<string>() ?? [];
        }

        public override float GetMiningSpeed(ItemStack itemstack, BlockSelection blockSel, Block block, IPlayer player, ref EnumHandling bhHandling)
        {
            bhHandling = EnumHandling.Handled;
            return GetMultiplier(player.Entity, speedTraits);
        }

        public override void OnDamageItem(IWorldAccessor world, Entity byEntity, ItemSlot itemslot, ref int amount, ref EnumHandling bhHandling)
        {
            if (world.Side != EnumAppSide.Server || byEntity is not EntityPlayer player || amount <= 0)
            {
                return;
            }

            float multiplier = GetMultiplier(player, durabilityLossTraits);
            if (multiplier != 1f)
            {
                amount = TraitMath.ScaleDurabilityLoss(world.Rand, amount, multiplier);
            }
        }

        public float GetReachBonus(EntityPlayer player)
        {
            float bonus = 0f;
            foreach (string? statName in reachTraits)
            {
                if (!string.IsNullOrWhiteSpace(statName) && statName != TraitAttributes.Tools.GlobalReach)
                {
                    bonus += TraitMath.GetAdditive(player.Stats, statName);
                }
            }

            return bonus;
        }

        private static float GetMultiplier(EntityPlayer player, string?[] statNames)
            => TraitMath.Combine(player.Stats, statNames);
    }
}