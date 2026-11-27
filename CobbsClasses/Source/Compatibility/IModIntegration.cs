using HarmonyLib;

using Vintagestory.API.Common;

namespace CobbsClasses.Source.Compatibility
{
    public interface IModIntegration
    {
        string ModId { get; }
        void Register(ICoreAPI api, Harmony harmony);
    }
}