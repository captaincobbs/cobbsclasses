using CobbsClasses.Source.Patches;

using HarmonyLib;

using Vintagestory.API.Common;

namespace CobbsClasses.Source
{
    public sealed class CobbsTraitPatchSystem : ModSystem
    {
        private const string HarmonyOwner = "cobbsclasses.player-traits";
        private Harmony? harmony;

        public override void Start(ICoreAPI api)
        {
            if (Harmony.HasAnyPatches(HarmonyOwner))
            {
                return;
            }

            harmony = new Harmony(HarmonyOwner);

            harmony.CreateClassProcessor(typeof(CrouchPatch)).Patch();
            harmony.CreateClassProcessor(typeof(BowDrawSpeedPatch)).Patch();
            harmony.CreateClassProcessor(typeof(SwimPatch)).Patch();
            harmony.CreateClassProcessor(typeof(BoatPatch)).Patch();
            harmony.CreateClassProcessor(typeof(MountPatch)).Patch();
            harmony.CreateClassProcessor(typeof(TemperatureTolerancePatch)).Patch();
            harmony.CreateClassProcessor(typeof(RegenPatch)).Patch();
            harmony.CreateClassProcessor(typeof(FruitingBushCuttingPatch)).Patch();
            harmony.CreateClassProcessor(typeof(PanningLuckPatch)).Patch();
        }

        public override void AssetsFinalize(ICoreAPI api)
        {
            if (api.Side == EnumAppSide.Server)
            {
                TargetDamagePatch.InitializeTags(api);
            }
        }

        public override void Dispose()
        {
            harmony?.UnpatchAll(HarmonyOwner);
        }
    }
}
