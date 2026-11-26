using System;
using System.Collections.Generic;
using System.Linq;

using HarmonyLib;

using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace CobbsClasses.Source.Patches
{
    internal static class MerchantSpecialStockPatch
    {
        private const string StockKey = "cobbsclasses:specialStock";
        private const string ShopCode = "cobbsclasses-special-stock";
        private const string PurchaseTrigger = "cobbsclasses-special-buy";
        private const int OfferCount = 3;
        private const float Markup = 1.5f;

        internal static void Register(Harmony harmony)
        {
            foreach (Type patch in new[] { typeof(RefreshStock), typeof(InitializeStock), typeof(Dialogue), typeof(Purchase) })
            {
                harmony.CreateClassProcessor(patch).Patch();
            }
        }

        private static bool Eligible(EntityPlayer player) => player.Stats.GetBlended(TraitAttributes.Trading.MerchantSpecialStock) > 1f;

        private static void FillStock(EntityTradingHumanoid trader)
        {
            if (trader.World.Side != EnumAppSide.Server || trader.TradeProps?.Selling?.List is not { } items)
            {
                return;
            }

            // Find some expensive items with low stock to put in the special stock
            ResolvedTradeItem[] candidates = [.. items.Where(item => item.ResolvedItemstack is not null || ((JsonItemStack)item).Resolve(trader.World, "CobbsClasses special stock")).Where(item => item.ResolvedItemstack is not null && item.Price is not null).Select(item => new ResolvedTradeItem { Stack = item.ResolvedItemstack, Price = (int)Math.Clamp(Math.Round((double)item.Price.avg), 1d, int.MaxValue), Stock = (int)Math.Max(1f, item.Stock?.avg ?? 1f) }).Where(item => item?.Stack is not null && item.Price > 0).OrderByDescending(item => item.Price / (double)Math.Max(1, item.Stock))];
            if (candidates.Length == 0)
            {
                return;
            }

            int generation = (trader.WatchedAttributes.GetTreeAttribute(StockKey)?.GetInt("generation") ?? 0) + 1;
            TreeAttribute stock = new();
            stock.SetInt("generation", generation);
            int poolSize = Math.Min(candidates.Length, Math.Max(OfferCount, candidates.Length / 3));
            List<ResolvedTradeItem> pool = [.. candidates.Take(poolSize).OrderBy(_ => trader.World.Rand.Next())];
            for (int index = 0; index < Math.Min(OfferCount, pool.Count); index++)
            {
                ResolvedTradeItem offer = pool[index];
                TreeAttribute entry = new();
                entry.SetItemstack("stack", offer.Stack.Clone());
                entry.SetInt("price", (int)Math.Clamp(Math.Ceiling(offer.Price * (double)Markup), 1d, int.MaxValue));
                entry.SetInt("remaining", 1);
                stock.SetAttribute(index.ToString(), entry);
            }

            trader.WatchedAttributes.SetAttribute(StockKey, stock);
            trader.WatchedAttributes.MarkPathDirty(StockKey);
        }

        [HarmonyPatch(typeof(EntityTradingHumanoid), "RefreshBuyingSellingInventory")]
        private static class RefreshStock
        {
            private static void Postfix(EntityTradingHumanoid __instance, float __0)
            {
                if (__0 <= 1f || !__instance.WatchedAttributes.HasAttribute(StockKey))
                {
                    FillStock(__instance);
                }
            }
        }

        [HarmonyPatch(typeof(EntityTradingHumanoid), nameof(EntityTradingHumanoid.Initialize))]
        private static class InitializeStock
        {
            private static void Postfix(EntityTradingHumanoid __instance)
            {
                if (!__instance.WatchedAttributes.HasAttribute(StockKey))
                {
                    FillStock(__instance);
                }
            }
        }

        [HarmonyPatch(typeof(EntityBehaviorConversable), "loadDialogue")]
        private static class Dialogue
        {
            private static void Postfix(EntityBehaviorConversable __instance, EntityPlayer __1, DialogueConfig? __result)
            {
                if (__result is null || __instance.entity is not EntityTradingHumanoid trader || !Eligible(__1)
                    || trader.WatchedAttributes.GetTreeAttribute(StockKey) is not { } stock
                    || __result.components.OfType<DlgTalkComponent>().FirstOrDefault(component =>
                        component.Code == "main" && component.Owner == "player") is not { } main)
                {
                    return;
                }

                int nextId = __result.components.OfType<DlgTalkComponent>().SelectMany(component => component.Text)
                    .Select(text => text.Id).DefaultIfEmpty(0).Max() + 1;
                main.Text = [.. main.Text, new DialogeTextElement
                {
                    Value = "cobbsclasses:merchant-special-stock",
                    JumpTo = ShopCode,
                    Id = nextId++
                }];

                List<DialogeTextElement> choices = [];
                List<DialogueComponent> components = [.. __result.components];
                for (int index = 0; index < OfferCount; index++)
                {
                    ITreeAttribute? entry = stock.GetTreeAttribute(index.ToString());
                    ItemStack? stack = entry?.GetItemstack("stack");
                    stack?.ResolveBlockOrItem(trader.World);
                    int price = entry?.GetInt("price") ?? 0;
                    string code = ShopCode + "-" + index;
                    string label = stack is null || entry?.GetInt("remaining") <= 0
                        ? Lang.Get("cobbsclasses:merchant-special-sold-out")
                        : Lang.Get("cobbsclasses:merchant-special-offer", stack.StackSize, stack.GetName(), price);
                    choices.Add(new DialogeTextElement { Value = label, JumpTo = code, Id = nextId++ });
                    components.Add(new DlgGenericComponent
                    {
                        Code = code,
                        Owner = "trader",
                        Trigger = PurchaseTrigger,
                        TriggerData = JsonObject.FromJson("{\"index\":" + index + ",\"generation\":" + stock.GetInt("generation") + "}"),
                        JumpTo = "main"
                    });
                }

                choices.Add(new DialogeTextElement
                {
                    Value = "cobbsclasses:merchant-special-back",
                    JumpTo = "main",
                    Id = nextId
                });
                components.Add(new DlgTalkComponent { Code = ShopCode, Owner = "player", Type = "talk", Text = [.. choices] });
                __result.components = [.. components];
            }
        }

        [HarmonyPatch(typeof(EntityTradingHumanoid), "Dialog_DialogTriggers")]
        private static class Purchase
        {
            private static bool Prefix(EntityTradingHumanoid __instance, EntityAgent __0, string __1,
                JsonObject __2, ref int __result)
            {
                if (__1 != PurchaseTrigger)
                {
                    return true;
                }

                __result = 0;
                if (__instance.World.Side != EnumAppSide.Server || __0 is not EntityPlayer player || player.Player is not IServerPlayer serverPlayer || !Eligible(player) || !__instance.Alive || player.Pos.Dimension != __instance.Pos.Dimension || player.Pos.SquareDistanceTo(__instance.Pos) > 7f || __instance.WatchedAttributes.HasAttribute("tradingPlayerUID"))
                {
                    return false;
                }

                ITreeAttribute? stock = __instance.WatchedAttributes.GetTreeAttribute(StockKey);
                int index = __2?["index"].AsInt(-1) ?? -1;
                if (stock is null || index < 0 || index >= OfferCount || stock.GetInt("generation") != (__2?["generation"].AsInt(-1) ?? -1)
                    || stock.GetTreeAttribute(index.ToString()) is not { } entry || entry.GetInt("remaining") <= 0 || entry.GetItemstack("stack") is not { } stack)
                {
                    serverPlayer.SendMessage(GlobalConstants.GeneralChatGroup, Lang.Get("cobbsclasses:merchant-special-sold-out"), EnumChatType.Notification);
                    return false;
                }

                int price = entry.GetInt("price");
                if (price <= 0 || InventoryTrader.GetPlayerAssets(player) < price)
                {
                    serverPlayer.SendMessage(GlobalConstants.GeneralChatGroup, Lang.Get("cobbsclasses:merchant-special-no-money"), EnumChatType.Notification);
                    return false;
                }

                ItemStack purchased = stack.Clone();
                purchased.ResolveBlockOrItem(__instance.World);
                if (purchased.Collectible is null)
                {
                    return false;
                }

                AccessTools.Method(typeof(InventoryTrader), "DeductFromEntity").Invoke(null, [__instance.Api, player, price]);
                AccessTools.Method(typeof(InventoryTrader), "GiveToTrader").Invoke(__instance.Inventory, [price]);
                if (AccessTools.Method(typeof(EntityTradingHumanoid), "GetOrCreateTradeStore").Invoke(__instance, null) is ITreeAttribute tradeStore)
                {
                    __instance.Inventory.ToTreeAttributes(tradeStore);
                }
                entry.SetInt("remaining", 0);
                __instance.WatchedAttributes.MarkPathDirty(StockKey);
                if (!serverPlayer.InventoryManager.TryGiveItemstack(purchased))
                {
                    player.World.SpawnItemEntity(purchased, player.Pos.XYZ);
                }

                serverPlayer.SendMessage(GlobalConstants.GeneralChatGroup, Lang.Get("cobbsclasses:merchant-special-purchased"), EnumChatType.Notification);
                return false;
            }
        }
    }
}