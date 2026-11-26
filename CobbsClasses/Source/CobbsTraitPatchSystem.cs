using CobbsClasses.Source.Patches;

using HarmonyLib;

using System;

using Vintagestory.API.Common;

namespace CobbsClasses.Source
{
    public sealed class CobbsTraitPatchSystem : ModSystem
    {
        private const string HarmonyOwner = "cobbsclasses.player-traits";
        private Harmony? harmony;

        private static readonly (string Name, Action<Harmony> Install)[] TraitPatches =
        [
            Patch(typeof(CrouchPatch)),
            Patch(typeof(BowDrawSpeedPatch)),
            Patch(typeof(SwimPatch)),
            Patch(typeof(BoatPatch)),
            Patch(typeof(MountPatch)),
            Patch(typeof(TemperatureTolerancePatch)),
            Patch(typeof(ArmorPenaltyPatch)),
            Patch(typeof(ArmorProtectionPatch)),
            Patch(typeof(RegenPatch)),
            Patch(typeof(FruitingBushCuttingPatch)),
            Patch(typeof(PanningLuckPatch)),
            Patch(typeof(PanningSpeedPatch)),
            Patch(typeof(MillingSpeedPatch)),
            Patch(typeof(MechanicalRecoveryPatch)),
            Patch(typeof(TargetDamagePatch)),
            Patch(typeof(DamageResistancePatch)),
            Patch(typeof(KnockbackPatch)),
            Patch(typeof(AnimalHarvestYieldPatch)),
            Patch(typeof(FishFilletYieldPatch)),
            Patch(typeof(BloomeryCapacityPatch)),
            Patch(typeof(ClayVoxelYieldPatch)),
            (nameof(ClayRecoveryPatch), ClayRecoveryPatch.Register),
            (nameof(BloomeryFuelPatch), BloomeryFuelPatch.Register),
            Patch(typeof(ChiselMaterialPatch)),
            (nameof(WorkpieceCoolingPatch), WorkpieceCoolingPatch.Register),
            Patch(typeof(HotItemPatch)),
            (nameof(CrockFreshnessPatch), CrockFreshnessPatch.Register),
            (nameof(FoodConsumptionPatch), FoodConsumptionPatch.Register),
            (nameof(CookedFoodPatch), CookedFoodPatch.Register),
            Patch(typeof(AnimalBehaviorPatch)),
            Patch(typeof(RoadSpeedPatch)),
            (nameof(SupportTraitSystem), SupportTraitSystem.Register),
            Patch(typeof(SharpeningPatch)),
            (nameof(QuenchModifierPatch), QuenchModifierPatch.Register),
            (nameof(CapacityInventoryPatch), CapacityInventoryPatch.Register),
            (nameof(HoneycombYieldPatch), HoneycombYieldPatch.Register),
            Patch(typeof(PigmentUsePatch)),
        ];

        private static (string, Action<Harmony>) Patch(Type type)
            => (type.Name, owner => owner.CreateClassProcessor(type).Patch());

        public override void Start(ICoreAPI api)
        {
            if (Harmony.HasAnyPatches(HarmonyOwner))
            {
                return;
            }

            Harmony owner = new(HarmonyOwner);
            harmony = owner;
            foreach ((string? name, Action<Harmony>? install) in TraitPatches)
            {
                Apply(name, () => install(owner), api.Logger);
            }
        }

        public override void AssetsFinalize(ICoreAPI api)
        {
            if (api.Side == EnumAppSide.Server)
            {
                TargetDamagePatch.InitializeTags(api);
                AnimalHarvestYieldPatch.InitializeTags(api);
                DamageResistancePatch.InitializeTags(api);
                AnimalBehaviorPatch.InitializeTags(api);
                OptionalModPatches.Apply(api, new Harmony(HarmonyOwner));
            }
        }

        private static void Apply(string feature, Action install, ILogger logger)
        {
            try
            {
                install();
            }
            catch (Exception exception)
            {
                logger.Warning($"CobbsClasses could not fully install {feature}. Other features will still load. {exception}");
            }
        }

        public override void Dispose()
        {
            harmony?.UnpatchAll(HarmonyOwner);
        }
    }
}
