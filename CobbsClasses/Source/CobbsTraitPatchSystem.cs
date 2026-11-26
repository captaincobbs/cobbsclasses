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
            harmony.CreateClassProcessor(typeof(ArmorPenaltyPatch)).Patch();
            harmony.CreateClassProcessor(typeof(ArmorProtectionPatch)).Patch();
            harmony.CreateClassProcessor(typeof(RegenPatch)).Patch();
            harmony.CreateClassProcessor(typeof(FruitingBushCuttingPatch)).Patch();
            harmony.CreateClassProcessor(typeof(PanningLuckPatch)).Patch();
            harmony.CreateClassProcessor(typeof(PanningSpeedPatch)).Patch();
            harmony.CreateClassProcessor(typeof(MillingSpeedPatch)).Patch();
            harmony.CreateClassProcessor(typeof(MechanicalRecoveryPatch)).Patch();
            harmony.CreateClassProcessor(typeof(TargetDamagePatch)).Patch();
            harmony.CreateClassProcessor(typeof(DamageResistancePatch)).Patch();
            harmony.CreateClassProcessor(typeof(KnockbackPatch)).Patch();
            harmony.CreateClassProcessor(typeof(AnimalHarvestYieldPatch)).Patch();
            harmony.CreateClassProcessor(typeof(FishFilletYieldPatch)).Patch();
            harmony.CreateClassProcessor(typeof(BloomeryCapacityPatch)).Patch();
            harmony.CreateClassProcessor(typeof(ClayVoxelYieldPatch)).Patch();
            ClayRecoveryPatch.Register(harmony);
            BloomeryFuelPatch.Register(harmony);
            harmony.CreateClassProcessor(typeof(ChiselMaterialPatch)).Patch();
            WorkpieceCoolingPatch.Register(harmony);
            harmony.CreateClassProcessor(typeof(HotItemPatch)).Patch();
            CrockFreshnessPatch.Register(harmony);
            FoodConsumptionPatch.Register(harmony);
            CookedFoodPatch.Register(harmony);
            harmony.CreateClassProcessor(typeof(AnimalBehaviorPatch)).Patch();
            harmony.CreateClassProcessor(typeof(RoadSpeedPatch)).Patch();
            SupportTraitSystem.Register(harmony);
            harmony.CreateClassProcessor(typeof(SharpeningPatch)).Patch();
            QuenchModifierPatch.Register(harmony);
            CapacityInventoryPatch.Register(harmony);
            HoneycombYieldPatch.Register(harmony);
            harmony.CreateClassProcessor(typeof(PigmentUsePatch)).Patch();
        }

        public override void AssetsFinalize(ICoreAPI api)
        {
            if (api.Side == EnumAppSide.Server)
            {
                TargetDamagePatch.InitializeTags(api);
                AnimalHarvestYieldPatch.InitializeTags(api);
                DamageResistancePatch.InitializeTags(api);
                AnimalBehaviorPatch.InitializeTags(api);
                HoneycombYieldPatch.RegisterOptional(new Harmony(HarmonyOwner), api.Logger);
                CookedFoodPatch.RegisterOptionalCooking(new Harmony(HarmonyOwner));
            }
        }

        public override void Dispose()
        {
            harmony?.UnpatchAll(HarmonyOwner);
        }
    }
}
