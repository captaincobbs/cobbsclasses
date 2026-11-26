using HarmonyLib;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

using Vintagestory.API.Common;
using Vintagestory.GameContent;

using static CobbsClasses.Source.Patches.PatchUtils;

namespace CobbsClasses.Source.Patches
{
    [HarmonyPatch]
    internal static class BloomeryCapacityPatch
    {
        private const int DefaultFuelLimit = 6;

        private static readonly System.Func<BlockEntityBloomery, ItemSlot> GetOreSlot = AccessTools.MethodDelegate<System.Func<BlockEntityBloomery, ItemSlot>>(AccessTools.PropertyGetter(typeof(BlockEntityBloomery), "OreSlot"));
        private static readonly System.Func<BlockEntityBloomery, int> GetOreFuelRatio = AccessTools.MethodDelegate<System.Func<BlockEntityBloomery, int>>(AccessTools.PropertyGetter(typeof(BlockEntityBloomery), "Ore2FuelRatio"));

        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(BlockEntityBloomery), nameof(BlockEntityBloomery.TryAdd));
            yield return AccessTools.Method(typeof(BlockEntityBloomery), nameof(BlockEntityBloomery.CanAdd));
        }

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase original)
        {
            List<CodeInstruction> body = [.. instructions];
            if (original.Name == nameof(BlockEntityBloomery.CanAdd))
            {
                RequireMatches(body.Count(instruction => instruction.LoadsConstant(DefaultFuelLimit)), 1, "bloomery fuel preview");
                int fuelLimit = body.FindIndex(instruction => instruction.LoadsConstant(DefaultFuelLimit));
                body[fuelLimit] = new CodeInstruction(body[fuelLimit]) { opcode = OpCodes.Ldarg_0, operand = null };
                body.Insert(fuelLimit + 1, new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(BloomeryCapacityPatch), nameof(GetFuelLimit))));
                return body;
            }

            MethodInfo ceiling = AccessTools.Method(typeof(Math), nameof(Math.Ceiling), [typeof(double)]);
            int[] fuelCalculations = [.. Enumerable.Range(0, body.Count - 1).Where(index => body[index].Calls(ceiling) && body[index + 1].opcode == OpCodes.Conv_I4)];
            RequireMatches(fuelCalculations.Length, 1, "bloomery fuel loading");
            body.Insert(fuelCalculations[0] + 2, new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(BloomeryCapacityPatch), nameof(ApplyFuelFloor))));

            MethodInfo capacity = AccessTools.PropertyGetter(typeof(BlockEntityBloomery), "OreCapacity");
            MethodInfo adjust = AccessTools.Method(typeof(BloomeryCapacityPatch), nameof(AdjustCapacity));
            RequireMatches(body.Count(instruction => instruction.Calls(capacity)), 1, "bloomery ore capacity");

            int index = body.FindIndex(instruction => instruction.Calls(capacity));
            body.InsertRange(index + 1,
            [
                new CodeInstruction(OpCodes.Ldarg_1),
                new CodeInstruction(OpCodes.Call, adjust)
            ]);
            return body;
        }

        private static int GetFuelLimit(BlockEntityBloomery bloomery)
        {
            int ratio = Math.Max(1, GetOreFuelRatio(bloomery));
            return ApplyFuelFloor((int)Math.Ceiling(GetOreSlot(bloomery).StackSize / (double)ratio));
        }

        private static int ApplyFuelFloor(int neededFuel)
        {
            return Math.Max(DefaultFuelLimit, neededFuel);
        }

        private static int AdjustCapacity(int capacity, IPlayer player)
        {
            return (int)Math.Clamp(Math.Floor(capacity * (double)Multiplier(player.Entity, TraitAttributes.Crafting.BloomeryAmountMul)), 0, int.MaxValue);
        }
    }
}
