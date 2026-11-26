using HarmonyLib;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;

using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

using static CobbsClasses.Source.Patches.PatchUtils;

namespace CobbsClasses.Source.Patches
{
    internal static class WorkpieceCoolingPatch
    {
        private const double WorkingWindowHours = 0.04d;
        private static readonly ConditionalWeakTable<ItemStack, WorkingWindow> Windows = [];

        internal static void Register(Harmony harmony)
        {
            harmony.CreateClassProcessor(typeof(AnvilStrike)).Patch();
            harmony.CreateClassProcessor(typeof(PassiveCooling)).Patch();
        }

        [HarmonyPatch(typeof(BlockEntityAnvil), "OnUseOver", [typeof(IPlayer), typeof(Vec3i), typeof(BlockSelection)])]
        private static class AnvilStrike
        {
            private static void Prefix(BlockEntityAnvil __instance, IPlayer __0, Vec3i? __1)
            {
                if (__1 is null || __instance.WorkItemStack is not { } stack || __instance.Api?.World is not { } world)
                {
                    return;
                }

                float reduction = Math.Clamp(__0.Entity.Stats.GetBlended(TraitAttributes.Crafting.WorkItemCoolingReduction) - 1f, 0f, 1f);
                double now = world.Calendar.TotalHours;
                if (Windows.TryGetValue(stack, out WorkingWindow? previous) && previous.Anvil == __instance && previous.Reduction == reduction && previous.End >= now)
                {
                    previous.End = now + WorkingWindowHours;
                    return;
                }

                // Settle elapsed cooling under the previous worker before starting a new window
                if (previous is not null)
                {
                    stack.Collectible.GetTemperature(world, stack);
                    Windows.Remove(stack);
                }

                if (reduction > 0f)
                {
                    Windows.Add(stack, new WorkingWindow(__instance, now, now + WorkingWindowHours, reduction));
                }
            }
        }

        [HarmonyPatch(typeof(CollectibleObject), nameof(CollectibleObject.GetTemperature), [typeof(IWorldAccessor), typeof(ItemStack), typeof(double)])]
        private static class PassiveCooling
        {
            private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                List<CodeInstruction> body = [.. instructions];
                RequireMatches(body.Count(instruction => instruction.LoadsConstant("cooldownSpeed")), 1, "workpiece cooling");
                int key = body.FindIndex(instruction => instruction.LoadsConstant("cooldownSpeed"));
                int get = key + 2;
                MethodInfo getFloat = AccessTools.Method(typeof(Vintagestory.API.Datastructures.ITreeAttribute), "GetFloat", [typeof(string), typeof(float)]);
                if (get + 1 >= body.Count || !body[get].Calls(getFloat) || body[get + 1].opcode != OpCodes.Mul)
                {
                    throw new InvalidOperationException("Could not find the passive cooling rate.");
                }

                body.InsertRange(get + 1,
                [
                    new CodeInstruction(OpCodes.Ldarg_1),
                    new CodeInstruction(OpCodes.Ldarg_2),
                    new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(PassiveCooling), nameof(AdjustRate)))
                ]);
                return body;
            }

            private static float AdjustRate(float rate, IWorldAccessor world, ItemStack stack)
            {
                if (!Windows.TryGetValue(stack, out WorkingWindow? work) || work.Anvil.WorkItemStack != stack)
                {
                    return rate;
                }

                double now = world.Calendar.TotalHours;
                double lastUpdate = stack.Attributes.GetTreeAttribute("temperature")?.GetDouble("temperatureLastUpdate") ?? now;
                double elapsed = now - lastUpdate;
                if (elapsed <= 0d)
                {
                    return rate;
                }

                double overlap = Math.Clamp(Math.Min(now, work.End) - Math.Max(lastUpdate, work.Start), 0d, elapsed);
                return (float)(rate * (1d - work.Reduction * overlap / elapsed));
            }
        }

        private sealed class WorkingWindow(BlockEntityAnvil anvil, double start, double end, float reduction)
        {
            public BlockEntityAnvil Anvil { get; } = anvil;
            public double Start { get; } = start;
            public double End { get; set; } = end;
            public float Reduction { get; } = reduction;
        }
    }
}
