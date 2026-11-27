using System;

using CobbsClasses.Source.Patches;

using HarmonyLib;

using Vintagestory.API.Common;

namespace CobbsClasses.Source
{
    public sealed class CobbsTraitPatchSystem : ModSystem
    {
        private const string HarmonyOwner = "cobbsclasses.player-traits";
        private Harmony? harmony;

        private static readonly ITraitPatch[] TraitPatches =
        [
            new AttributedPatch(typeof(CrouchPatch)),
            new AttributedPatch(typeof(DryingPatch)),
            new AttributedPatch(typeof(HealingItemEffectivenessPatch)),
            new AttributedPatch(typeof(HealingItemSpeedPatch)),
            new AttributedPatch(typeof(WateringPatch)),
            new AttributedPatch(typeof(BerryYieldPatch)),
            new AttributedPatch(typeof(FruitYieldPatch)),
            new AttributedPatch(typeof(FruitingBushHarvestSpeedPatch)),
            new AttributedPatch(typeof(FertilizerPatch)),
            new AttributedPatch(typeof(NutritionDecayPatch)),
            new AttributedPatch(typeof(WormYieldPatch)),
            new AttributedPatch(typeof(ManualHarvestSpeedPatch)),
            new AttributedPatch(typeof(ResinYieldPatch)),
            new AttributedPatch(typeof(ShieldProtectionPatch)),
            new AttributedPatch(typeof(WearableRepairPatch)),
            new AttributedPatch(typeof(ArrowBreakChancePatch)),
            new AttributedPatch(typeof(BowDrawSpeedPatch)),
            new AttributedPatch(typeof(SwimPatch)),
            new AttributedPatch(typeof(BoatPatch)),
            new AttributedPatch(typeof(MountPatch)),
            new AttributedPatch(typeof(TemperatureTolerancePatch)),
            new AttributedPatch(typeof(TemporalStabilityPatch)),
            new AttributedPatch(typeof(ArmorPenaltyPatch)),
            new AttributedPatch(typeof(ArmorProtectionPatch)),
            new AttributedPatch(typeof(RegenPatch)),
            new AttributedPatch(typeof(FruitingBushCuttingPatch)),
            new RegisteredPatch(nameof(GatheringTraitPatch), GatheringTraitPatch.Register),
            new RegisteredPatch(nameof(EggYieldPatch), EggYieldPatch.Register),
            new RegisteredPatch(nameof(MerchantPricePatch), MerchantPricePatch.Register),
            new RegisteredPatch(nameof(MerchantSpecialStockPatch), MerchantSpecialStockPatch.Register),
            new AttributedPatch(typeof(PanningLuckPatch)),
            new AttributedPatch(typeof(PanningSpeedPatch)),
            new AttributedPatch(typeof(MillingSpeedPatch)),
            new AttributedPatch(typeof(MechanicalRecoveryPatch)),
            new AttributedPatch(typeof(TargetDamagePatch)),
            new AttributedPatch(typeof(DamageResistancePatch)),
            new AttributedPatch(typeof(KnockbackPatch)),
            new AttributedPatch(typeof(AnimalHarvestYieldPatch)),
            new AttributedPatch(typeof(FishFilletYieldPatch)),
            new AttributedPatch(typeof(BloomeryCapacityPatch)),
            new AttributedPatch(typeof(ClayVoxelYieldPatch)),
            new RegisteredPatch(nameof(ClayRecoveryPatch), ClayRecoveryPatch.Register),
            new RegisteredPatch(nameof(BloomeryFuelPatch), BloomeryFuelPatch.Register),
            new RegisteredPatch(nameof(FiringSpeedPatch), FiringSpeedPatch.Register),
            new AttributedPatch(typeof(ChiselMaterialPatch)),
            new RegisteredPatch(nameof(WorkpieceCoolingPatch), WorkpieceCoolingPatch.Register),
            new AttributedPatch(typeof(HotItemPatch)),
            new RegisteredPatch(nameof(CrockFreshnessPatch), CrockFreshnessPatch.Register),
            new RegisteredPatch(nameof(FoodConsumptionPatch), FoodConsumptionPatch.Register),
            new AttributedPatch(typeof(IntoxicationPatch)),
            new RegisteredPatch(nameof(CookedFoodPatch), CookedFoodPatch.Register),
            new AttributedPatch(typeof(AnimalBehaviorPatch)),
            new AttributedPatch(typeof(RoadSpeedPatch)),
            new RegisteredPatch(nameof(SupportTraitSystem), SupportTraitSystem.Register),
            new AttributedPatch(typeof(SharpeningPatch)),
            new RegisteredPatch(nameof(QuenchModifierPatch), QuenchModifierPatch.Register),
            new RegisteredPatch(nameof(CapacityInventoryPatch), CapacityInventoryPatch.Register),
            new RegisteredPatch(nameof(HoneycombYieldPatch), HoneycombYieldPatch.Register),
            new AttributedPatch(typeof(PigmentUsePatch)),
        ];


        public override void Start(ICoreAPI api)
        {
            if (Harmony.HasAnyPatches(HarmonyOwner))
            {
                return;
            }

            Harmony owner = new(HarmonyOwner);
            harmony = owner;
            foreach (ITraitPatch patch in TraitPatches)
            {
                Apply(patch.Name, () => patch.Register(owner), api.Logger);
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