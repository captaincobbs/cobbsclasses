using HarmonyLib;

using System;
using System.Collections.Generic;

using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace CobbsClasses.Source.Patches
{
    internal static class CookedFoodPatch
    {
        private const string Servings = "cobbsclasses:cookServings";
        private const string Decay = "cobbsclasses:cookDecay";
        private const string Preserved = "cobbsclasses:cookedDecay";

        internal static void Register(Harmony harmony)
        {
            foreach (Type patch in new[] { typeof(FirepitInput), typeof(FirepitOutput), typeof(FirepitTransfer), typeof(OvenInput), typeof(OvenOutput), typeof(PerishRate) })
            {
                harmony.CreateClassProcessor(patch).Patch();
            }
        }

        internal static void RegisterOptionalCooking(Harmony harmony)
        {
            Type? mixingBowl = AccessTools.TypeByName("ACulinaryArtillery.BlockEntityMixingBowl");
            if (mixingBowl is null || AccessTools.DeclaredMethod(mixingBowl, "mixInput") is not { } mix || AccessTools.PropertyGetter(mixingBowl, "OutputSlot") is null || AccessTools.Field(mixingBowl, "playersMixing")?.FieldType != typeof(Dictionary<string, long>))
            {
                return;
            }

            if (Harmony.GetPatchInfo(mix)?.Owners.Contains(harmony.Id) == true)
            {
                return;
            }

            harmony.Patch(mix, prefix: new HarmonyMethod(typeof(MixingBowl), nameof(MixingBowl.BeforeMix)), postfix: new HarmonyMethod(typeof(MixingBowl), nameof(MixingBowl.AfterMix)));
        }

        private static void Attribute(ItemSlot slot, IPlayer player)
        {
            if (slot.Itemstack is not { } stack)
            {
                return;
            }

            stack.Attributes.RemoveAttribute(Servings);
            stack.Attributes.RemoveAttribute(Decay);
            float servings = player.Entity.Stats.GetBlended("foodExtraServings");
            float decay = player.Entity.Stats.GetBlended("foodDecayRate");
            if (servings > 1f && float.IsFinite(servings))
            {
                stack.Attributes.SetFloat(Servings, servings);
            }
            if (decay < 1f && float.IsFinite(decay))
            {
                stack.Attributes.SetFloat(Decay, Math.Max(0.01f, decay));
            }
            slot.MarkDirty();
        }

        private static bool Eligible(IWorldAccessor world, ItemStack stack)
        {
            return stack.Collectible is not IBlockMealContainer && stack.Collectible.GetNutritionProperties(world, stack, null)?.Satiety > 0f;
        }

        private static void Grant(IWorldAccessor world, ItemSlot slot, int produced, float servings, float decay, bool merged)
        {
            if (produced <= 0 || slot.Itemstack is not { } stack)
            {
                return;
            }

            if (stack.Collectible is IBlockMealContainer meal)
            {
                string? recipe = meal.GetRecipeCode(world, stack);
                if (merged || string.IsNullOrEmpty(recipe) || recipe.Contains("soup", StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                float count = meal.GetQuantityServings(world, stack);
                if (count <= 0f)
                {
                    return;
                }

                if (decay < 1f)
                {
                    ItemStack[] ingredients = meal.GetContents(world, stack);
                    foreach (ItemStack ingredient in ingredients)
                    {
                        ingredient?.Attributes.SetFloat(Preserved, Math.Min(decay, ingredient.Attributes.GetFloat(Preserved, 1f)));
                    }
                    meal.SetContents(recipe, stack, ingredients, count);
                }

                meal.SetQuantityServings(world, stack, count * Math.Max(1f, servings));
                stack.Attributes.RemoveAttribute(Servings);
                stack.Attributes.RemoveAttribute(Decay);
                slot.MarkDirty();
                return;
            }

            if (!Eligible(world, stack))
            {
                return;
            }

            stack.Attributes.RemoveAttribute(Servings);
            stack.Attributes.RemoveAttribute(Decay);
            if (servings > 1f)
            {
                int space = Math.Max(0, stack.Collectible.MaxStackSize - stack.StackSize);
                stack.StackSize += Math.Min(space, GameMath.RoundRandom(world.Rand, produced * (servings - 1f)));
            }

            if (decay < 1f && (!merged || stack.Attributes.HasAttribute(Preserved)))
            {
                stack.Attributes.SetFloat(Preserved, Math.Max(decay, merged ? stack.Attributes.GetFloat(Preserved, 1f) : 0f));
            }
            slot.MarkDirty();
        }

        [HarmonyPatch(typeof(ItemSlot), nameof(ItemSlot.ActivateSlot))]
        private static class FirepitInput
        {
            private static void Prefix(ItemSlot __instance, out int __state)
            {
                __state = __instance.Inventory is InventorySmelting inventory && ReferenceEquals(inventory[1], __instance) ? __instance.StackSize : -1;
            }

            private static void Postfix(ItemSlot __instance, ItemStackMoveOperation __1, int __state)
            {
                if (__state >= 0 && __instance.StackSize > __state && __1.ActingPlayer is { } player && player.Entity.World.Side == EnumAppSide.Server)
                {
                    Attribute(__instance, player);
                }
            }
        }

        [HarmonyPatch(typeof(ItemSlot), nameof(ItemSlot.TryPutInto),
            [typeof(ItemSlot), typeof(ItemStackMoveOperation)],
            [ArgumentType.Normal, ArgumentType.Ref])]
        private static class FirepitTransfer
        {
            private static void Postfix(ItemSlot __0, ItemStackMoveOperation __1, int __result)
            {
                if (__result > 0 && __0.Inventory is InventorySmelting inventory && ReferenceEquals(inventory[1], __0) && __1.ActingPlayer is { } player && player.Entity.World.Side == EnumAppSide.Server)
                {
                    Attribute(__0, player);
                }
            }
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
                __state = new Batch(output.StackSize, cook.Entity.Stats.GetBlended("foodExtraServings"),
                    Math.Clamp(cook.Entity.Stats.GetBlended("foodDecayRate"), 0.01f, 1f));
            }

            public static void AfterMix(BlockEntityContainer __instance, Batch __state)
            {
                if (__state.Servings <= 1f && (__state.Decay <= 0f || __state.Decay >= 1f))
                {
                    return;
                }

                ItemSlot output = GetOutput(__instance);
                Grant(__instance.Api.World, output, output.StackSize - __state.Count, __state.Servings, __state.Decay, __state.Count > 0);
            }

            private static ItemSlot GetOutput(BlockEntityContainer bowl)
            {
                return (ItemSlot)AccessTools.PropertyGetter(bowl.GetType(), "OutputSlot").Invoke(bowl, null)!;
            }
        }

        private readonly record struct Batch(int Count, float Servings, float Decay);

        [HarmonyPatch(typeof(BlockEntityFirepit), "smeltItems")]
        private static class FirepitOutput
        {
            private static void Prefix(BlockEntityFirepit __instance, out Batch __state)
            {
                ItemStack? input = __instance.Inventory[1].Itemstack;
                __state = new Batch(__instance.Inventory[2].StackSize, input?.Attributes.GetFloat(Servings, 1f) ?? 1f, input?.Attributes.GetFloat(Decay, 1f) ?? 1f);
            }

            private static void Postfix(BlockEntityFirepit __instance, Batch __state)
            {
                ItemSlot slot = __instance.Inventory[2];
                if (__instance.Api.World.Side == EnumAppSide.Server && (__state.Servings > 1f || __state.Decay < 1f))
                {
                    Grant(__instance.Api.World, slot, slot.StackSize - __state.Count, __state.Servings, __state.Decay, __state.Count > 0);
                }
            }
        }

        [HarmonyPatch(typeof(BlockEntityOven), nameof(BlockEntityOven.OnInteract))]
        private static class OvenInput
        {
            private static void Prefix(BlockEntityOven __instance, out HashSet<ItemStack>? __state)
            {
                __state = null;
                if (__instance.Api.World.Side == EnumAppSide.Server)
                {
                    __state = new HashSet<ItemStack>(ReferenceEqualityComparer.Instance);
                    foreach (ItemSlot slot in __instance.Inventory)
                    {
                        if (slot.Itemstack is { } stack)
                        {
                            __state.Add(stack);
                        }
                    }
                }
            }

            private static void Postfix(BlockEntityOven __instance, IPlayer __0, bool __result, HashSet<ItemStack>? __state)
            {
                if (!__result || __state is null)
                {
                    return;
                }

                foreach (ItemSlot slot in __instance.Inventory)
                {
                    if (slot.Itemstack is { } stack && !__state.Contains(stack) && BakingProperties.ReadFrom(stack) is not null)
                    {
                        Attribute(slot, __0);
                    }
                }
            }
        }

        [HarmonyPatch(typeof(BlockEntityOven), "IncrementallyBake")]
        private static class OvenOutput
        {
            private static void Prefix(BlockEntityOven __instance, int __1, out ItemStack? __state)
            {
                __state = __instance.Inventory[__1].Itemstack;
            }

            private static void Postfix(BlockEntityOven __instance, int __1, ItemStack? __state)
            {
                ItemSlot slot = __instance.Inventory[__1];
                if (__state is null || slot.Itemstack is not { } output || output.Collectible == __state.Collectible || __instance.Api.World.Side != EnumAppSide.Server)
                {
                    return;
                }

                float servings = __state.Attributes.GetFloat(Servings, 1f);
                float decay = __state.Attributes.GetFloat(Decay, 1f);
                if (servings > 1f || decay < 1f)
                {
                    Grant(__instance.Api.World, slot, slot.StackSize, servings, decay, merged: false);
                }
            }
        }

        [HarmonyPatch(typeof(CollectibleObject), nameof(CollectibleObject.GetTransitionRateMul))]
        private static class PerishRate
        {
            private static void Postfix(ItemSlot __1, EnumTransitionType __2, ref float __result)
            {
                if (__2 == EnumTransitionType.Perish && __1.Itemstack is { } stack)
                {
                    __result *= Math.Clamp(stack.Attributes.GetFloat(Preserved, 1f), 0.01f, 1f);
                }
            }
        }
    }
}
