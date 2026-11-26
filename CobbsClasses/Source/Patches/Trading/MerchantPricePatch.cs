using System;

using HarmonyLib;

using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace CobbsClasses.Source.Patches
{
    internal static class MerchantPricePatch
    {
        [ThreadStatic]
        private static EntityPlayer? activeTrader;

        internal static void Register(Harmony harmony)
        {
            foreach (Type patch in new[] { typeof(Transaction), typeof(Affordability), typeof(Payment), typeof(Cost), typeof(Gain) })
            {
                harmony.CreateClassProcessor(patch).Patch();
            }
        }

        private static void Begin(IPlayer? player, out EntityPlayer? previous)
        {
            previous = activeTrader;
            activeTrader = player?.Entity;
        }

        private static Exception? End(EntityPlayer? previous, Exception? exception)
        {
            activeTrader = previous;
            return exception;
        }

        [HarmonyPatch(typeof(InventoryTrader), "TryBuySell")]
        private static class Transaction
        {
            private static void Prefix(IPlayer __0, out EntityPlayer? __state) => Begin(__0, out __state);
            private static Exception? Finalizer(EntityPlayer? __state, Exception? __exception) => End(__state, __exception);
        }

        [HarmonyPatch(typeof(InventoryTrader), nameof(InventoryTrader.HasPlayerEnoughAssets))]
        private static class Affordability
        {
            private static void Prefix(IPlayer __0, out EntityPlayer? __state) => Begin(__0, out __state);
            private static Exception? Finalizer(EntityPlayer? __state, Exception? __exception) => End(__state, __exception);
        }

        [HarmonyPatch(typeof(InventoryTrader), "HandleMoneyTransaction")]
        private static class Payment
        {
            private static void Prefix(IPlayer __0, out EntityPlayer? __state) => Begin(__0, out __state);
            private static Exception? Finalizer(EntityPlayer? __state, Exception? __exception) => End(__state, __exception);
        }

        private static int Adjust(InventoryTrader inventory, int amount, string stat)
        {
            EntityPlayer? player = activeTrader ?? (inventory.Api as ICoreClientAPI)?.World.Player?.Entity;
            if (player is null || amount <= 0)
            {
                return amount;
            }

            float factor = player.Stats.GetBlended(stat);
            if (!float.IsFinite(factor))
            {
                return amount;
            }

            return (int)Math.Clamp(Math.Round(amount * (double)Math.Max(0f, factor), MidpointRounding.AwayFromZero), 1d, int.MaxValue);
        }

        [HarmonyPatch(typeof(InventoryTrader), nameof(InventoryTrader.GetTotalCost))]
        private static class Cost
        {
            private static void Postfix(InventoryTrader __instance, ref int __result) => __result = Adjust(__instance, __result, TraitAttributes.Trading.MerchantBuyModifier);
        }

        [HarmonyPatch(typeof(InventoryTrader), nameof(InventoryTrader.GetTotalGain))]
        private static class Gain
        {
            private static void Postfix(InventoryTrader __instance, ref int __result) => __result = Adjust(__instance, __result, TraitAttributes.Trading.MerchantSellModifier);
        }
    }
}