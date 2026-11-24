using Vintagestory.API.Server;

namespace CobbsClasses.Source.Handlers
{
    internal static class PlayerStatHandler
    {
        public static void Refresh(IServerPlayer player)
        {
            BlockReachHandler.Refresh(player);
            BarCapacityHandler.Refresh(player);
        }
    }
}
