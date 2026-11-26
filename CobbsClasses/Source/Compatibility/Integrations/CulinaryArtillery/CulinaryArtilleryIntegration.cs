using System;
using System.Collections.Generic;

using CobbsClasses.Source.Patches;

using HarmonyLib;

using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace CobbsClasses.Source.Compatibility.Integrations
{
    internal sealed class CulinaryArtilleryIntegration : IModIntegration
    {
        public string ModId => "aculinaryartillery";

        public void Register(ICoreAPI api, Harmony harmony)
        {
            OptionalPatchRegistrar integration = new(api, harmony, ModId);
            integration.Patch("ACulinaryArtillery.BlockEntityMixingBowl", "mixInput", prefix: new HarmonyMethod(typeof(MixingBowl), nameof(MixingBowl.BeforeMix)), postfix: new HarmonyMethod(typeof(MixingBowl), nameof(MixingBowl.AfterMix)), compatible: type => AccessTools.PropertyGetter(type, "OutputSlot") is not null && AccessTools.Field(type, "playersMixing")?.FieldType == typeof(Dictionary<string, long>));
        }

        private static class MixingBowl
        {
            public static void BeforeMix(BlockEntityContainer __instance, Dictionary<string, long> ___playersMixing, out Batch __state)
            {
                __state = default;
                if (__instance.Api.World.Side != EnumAppSide.Server)
                {
                    return;
                }

                IPlayer? cook = null;
                long latest = long.MinValue;
                foreach ((string uid, long time) in ___playersMixing)
                {
                    if (time > latest && __instance.Api.World.PlayerByUid(uid) is { } player)
                    {
                        latest = time;
                        cook = player;
                    }
                }

                if (cook is null)
                {
                    return;
                }
                ItemSlot output = GetOutput(__instance);
                __state = new Batch(output.StackSize, cook.Entity.Stats.GetBlended(TraitAttributes.Food.FoodExtraServings), Math.Clamp(cook.Entity.Stats.GetBlended(TraitAttributes.Food.FoodDecayRate), 0.01f, 1f));
            }

            public static void AfterMix(BlockEntityContainer __instance, Batch __state)
            {
                if (__state.Servings <= 1f && (__state.Decay <= 0f || __state.Decay >= 1f))
                {
                    return;
                }

                ItemSlot output = GetOutput(__instance);
                CookedFoodPatch.Grant(__instance.Api.World, output, output.StackSize - __state.Count, __state.Servings, __state.Decay, __state.Count > 0);
            }

            private static ItemSlot GetOutput(BlockEntityContainer bowl)
            {
                return (ItemSlot)AccessTools.PropertyGetter(bowl.GetType(), "OutputSlot").Invoke(bowl, null)!;
            }
        }

        private readonly record struct Batch(int Count, float Servings, float Decay);
    }
}
