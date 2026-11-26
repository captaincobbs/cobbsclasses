using HarmonyLib;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

using static CobbsClasses.Source.Patches.PatchUtils;

namespace CobbsClasses.Source.Patches
{
    [HarmonyPatch]
    internal static class SharpeningPatch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(BlockEntityGrindingWheel), "checkIsGrinding");
            yield return AccessTools.Method(typeof(BlockEntityGrindingWheel), "canBuff");
            yield return AccessTools.Method(typeof(BlockEntityGrindingWheel), "OnInteractStart");
            yield return AccessTools.Method(typeof(BlockEntityGrindingWheel), "OnInteractStep");
        }

        [ThreadStatic]
        private static IPlayer? checkingPlayer;

        private static void Prefix(MethodBase __originalMethod, object[] __args, out IPlayer? __state)
        {
            __state = checkingPlayer;
            if (__originalMethod.Name is "OnInteractStart" or "OnInteractStep")
            {
                checkingPlayer = __args[__originalMethod.Name == "OnInteractStart" ? 0 : 1] as IPlayer;
            }
        }

        private static void Finalizer(IPlayer? __state)
        {
            checkingPlayer = __state;
        }

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase original)
        {
            List<CodeInstruction> body = [.. instructions];
            bool ticking = original.Name == "checkIsGrinding";
            if (!ticking && original.Name != "canBuff")
            {
                return body;
            }

            RequireMatches(body.Count(instruction => instruction.LoadsConstant(1.099)), 1, "sharpening cap");
            int cap = body.FindIndex(instruction => instruction.LoadsConstant(1.099));
            if (ticking && original.GetMethodBody()!.LocalVariables[2].LocalType != typeof(IPlayer))
            {
                throw new InvalidOperationException("Could not locate the player sharpening at the wheel.");
            }

            body.InsertRange(cap + 1,
            [
                new CodeInstruction(ticking ? OpCodes.Ldloc_2 : OpCodes.Ldnull),
                new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(SharpeningPatch), nameof(AdjustCap)))
            ]);
            if (ticking)
            {
                MethodInfo damage = AccessTools.Method(typeof(CollectibleObject), nameof(CollectibleObject.DamageItem), [typeof(IWorldAccessor), typeof(Entity), typeof(ItemSlot), typeof(int), typeof(bool)]);
                RequireMatches(body.Count(instruction => instruction.Calls(damage)), 1, "sharpening wear");
                int wear = body.FindIndex(instruction => instruction.Calls(damage));
                body[wear] = new CodeInstruction(body[wear])
                {
                    opcode = OpCodes.Call,
                    operand = AccessTools.Method(typeof(SharpeningPatch), nameof(ApplyWear))
                };
            }

            return body;
        }

        private static double AdjustCap(double cap, IPlayer? player)
        {
            float bonus = (player ?? checkingPlayer)?.Entity?.Stats.GetBlended(TraitAttributes.Equipment.SharpeningMax) ?? 1f;
            return Math.Max(1d, cap + (bonus - 1f));
        }

        private static void ApplyWear(CollectibleObject tool, IWorldAccessor world, Entity player, ItemSlot slot, int amount, bool destroy)
        {
            float factor = Multiplier(player, TraitAttributes.Equipment.SharpeningDurabilityLoss);
            int wear = factor == 1f ? amount : GameMath.RoundRandom(world.Rand, factor * amount);
            if (wear > 0)
            {
                tool.DamageItem(world, player, slot, wear, destroy);
            }
        }
    }
}
