using HarmonyLib;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

using static CobbsClasses.Source.Patches.PatchUtils;

namespace CobbsClasses.Source.Patches
{
    [HarmonyPatch]
    internal static class PigmentUsePatch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.DeclaredMethod(typeof(CollectibleBehaviorArtPigment), "OnHeldInteractStop");
            yield return AccessTools.DeclaredMethod(typeof(BlockEntitySign), "OnRightClick");
            yield return AccessTools.DeclaredMethod(typeof(BlockEntitySignPost), "OnRightClick");
            yield return AccessTools.DeclaredMethod(typeof(BlockEntityLabeledChest), "OnPlayerRightClick");
            yield return AccessTools.DeclaredMethod(typeof(EntityBehaviorWritingSurface), "loadPigment");
            yield return AccessTools.DeclaredMethod(typeof(BlockEntitySign), "OnReceivedClientPacket");
            yield return AccessTools.DeclaredMethod(typeof(BlockEntitySignPost), "OnReceivedClientPacket");
            yield return AccessTools.DeclaredMethod(typeof(BlockEntityLabeledChest), "OnReceivedClientPacket");
            yield return AccessTools.DeclaredMethod(typeof(EntityBehaviorWritingSurface), "OnReceivedClientPacket");
        }

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase original)
        {
            List<CodeInstruction> body = [.. instructions];
            if (original.Name == "OnReceivedClientPacket")
            {
                MethodInfo refund = AccessTools.Method(typeof(IPlayerInventoryManager), nameof(IPlayerInventoryManager.TryGiveItemstack), [typeof(ItemStack), typeof(bool)]);
                MethodInfo safeRefund = AccessTools.Method(typeof(PigmentUsePatch), nameof(RefundIfPresent));
                for (int index = 0; index < body.Count; index++)
                {
                    if (body[index].Calls(refund))
                    {
                        body[index] = new CodeInstruction(body[index]) { opcode = OpCodes.Call, operand = safeRefund };
                    }
                }
                return body;
            }

            MethodInfo take = AccessTools.Method(typeof(ItemSlot), nameof(ItemSlot.TakeOut), [typeof(int)]);
            bool drawing = original.DeclaringType == typeof(CollectibleBehaviorArtPigment);
            bool entityLabel = original.DeclaringType == typeof(EntityBehaviorWritingSurface);
            MethodInfo replacement = AccessTools.Method(typeof(PigmentUsePatch), drawing ? nameof(TakeDrawingPigment) : entityLabel ? nameof(TakeEntityLabelPigment) : nameof(TakeLabelPigment));
            OpCode playerArgument = drawing ? OpCodes.Ldarg_3 : OpCodes.Ldarg_1;
            if (!drawing && !entityLabel)
            {
                MethodInfo getSlot = AccessTools.PropertyGetter(typeof(IPlayerInventoryManager), nameof(IPlayerInventoryManager.ActiveHotbarSlot));
                RequireMatches(body.Count(instruction => instruction.Calls(getSlot)), 1, "label pigment selection");
                int slot = body.FindIndex(instruction => instruction.Calls(getSlot));
                body.InsertRange(slot + 1,
                [
                    new CodeInstruction(OpCodes.Ldarg_1),
                    new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(PigmentUsePatch), nameof(ChooseLabelPigment)))
                ]);
            }
            if (entityLabel)
            {
                MethodInfo hand = AccessTools.PropertyGetter(typeof(EntityAgent), nameof(EntityAgent.RightHandItemSlot));
                RequireMatches(body.Count(instruction => instruction.Calls(hand)), 1, "entity label pigment selection");
                int slot = body.FindIndex(instruction => instruction.Calls(hand));
                body.InsertRange(slot + 1,
                [
                    new CodeInstruction(OpCodes.Ldarg_1),
                    new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(PigmentUsePatch), nameof(ChoosePigment)))
                ]);
            }
            return ReplaceCall(body, take, replacement, playerArgument, "pigment use");
        }

        private static bool RefundIfPresent(IPlayerInventoryManager inventory, ItemStack? pigment, bool notify) => pigment is not null && inventory.TryGiveItemstack(pigment, notify);

        private static ItemSlot ChooseLabelPigment(ItemSlot slot, IPlayer player) => ChoosePigment(slot, player.Entity);

        private static ItemSlot ChoosePigment(ItemSlot slot, EntityAgent player)
        {
            if (!FreeLabels(player) || slot.Itemstack?.Collectible.Attributes?["pigment"]["color"].Exists == true || player.World.GetItem(new AssetLocation("game", "charcoal")) is not { } charcoal)
            {
                return slot;
            }

            return new DummySlot(new ItemStack(charcoal));
        }

        private static ItemStack? TakeDrawingPigment(ItemSlot slot, int amount, EntityAgent artist)
        {
            float multiplier = Multiplier(artist, "pigmentCostReduction");
            int cost = multiplier == 1f ? amount : GameMath.RoundRandom(artist.World.Rand, amount * multiplier);
            return cost > 0 ? slot.TakeOut(cost) : null;
        }

        private static ItemStack? TakeLabelPigment(ItemSlot slot, int amount, IPlayer player) => FreeLabels(player.Entity) ? null : slot.TakeOut(amount);

        private static ItemStack? TakeEntityLabelPigment(ItemSlot slot, int amount, EntityAgent player) => FreeLabels(player) ? null : slot.TakeOut(amount);

        private static bool FreeLabels(EntityAgent player) => Multiplier(player, "labelsFree") > 1f;
    }
}
