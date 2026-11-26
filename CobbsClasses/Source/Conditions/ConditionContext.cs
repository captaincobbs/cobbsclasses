using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace CobbsClasses.Source.Conditions
{
    internal readonly record struct ConditionContext(IServerPlayer Player, EntityPlayer Entity, IPlayer[] OnlinePlayers)
    {
        public EntityStats Stats => Entity.Stats;

        public static bool IsActive(IServerPlayer player) => player.ConnectionState == EnumClientState.Playing && player.Entity is { Alive: true } && player.WorldData.CurrentGameMode == EnumGameMode.Survival;
    }

}