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
    internal static class HoneycombYieldPatch
    {
        internal static void Register(Harmony harmony)
        {
            harmony.CreateClassProcessor(typeof(Skep)).Patch();
        }

        internal static void RegisterOptional(Harmony harmony, ILogger logger)
        {
            foreach ((string typeName, string methodName) in new[]
            {
                ("FromGoldenCombs.Blocks.ClayHiveTop", "OnBlockInteractStep"),
                ("FromGoldenCombs.BlockEntities.BEFrameRack", "TryHarvest"),
                ("FromGoldenCombs.BlockEntities.BEFrameRack", "OnInteract")
            })
            {
                if (AccessTools.TypeByName(typeName) is not { } type || AccessTools.DeclaredMethod(type, methodName) is not { } method || Harmony.GetPatchInfo(method)?.Owners.Contains(harmony.Id) == true)
                {
                    continue;
                }

                try
                {
                    harmony.Patch(method, transpiler: new HarmonyMethod(typeof(HoneycombYieldPatch), nameof(AdjustCreatedStacks)));
                }
                catch (Exception exception)
                {
                    logger.Warning($"CobbsClasses could not patch {typeName}.{methodName} for honeycomb yield: {exception.Message}");
                }
            }
        }

        private static IEnumerable<CodeInstruction> AdjustCreatedStacks(IEnumerable<CodeInstruction> instructions, MethodBase original)
        {
            ConstructorInfo stack = AccessTools.Constructor(typeof(ItemStack), [typeof(Item), typeof(int)]);
            MethodInfo adjust = AccessTools.Method(typeof(HoneycombYieldPatch), nameof(Adjust));
            ParameterInfo player = original.GetParameters().Single(parameter => parameter.ParameterType == typeof(IPlayer));
            int matches = 0;
            foreach (CodeInstruction instruction in instructions)
            {
                yield return instruction;
                if (instruction.opcode == OpCodes.Newobj && Equals(instruction.operand, stack))
                {
                    matches++;
                    yield return new CodeInstruction(OpCodes.Dup);
                    yield return CodeInstruction.LoadArgument(player.Position + 1);
                    yield return new CodeInstruction(OpCodes.Call, adjust);
                }
            }
            if (matches == 0)
            {
                throw new InvalidOperationException("No honeycomb output constructor was found.");
            }
        }

        private static void Adjust(ItemStack stack, IPlayer? player)
        {
            if (player?.Entity is not { } entity || entity.World.Side != EnumAppSide.Server || stack.Collectible.FirstCodePart() != "honeycomb")
            {
                return;
            }

            float factor = Multiplier(entity, "honeycombYield");
            if (float.IsFinite(factor) && factor > 1f)
            {
                stack.StackSize = Math.Min(stack.Collectible.MaxStackSize, GameMath.RoundRandom(entity.World.Rand, stack.StackSize * factor));
            }
        }

        [HarmonyPatch(typeof(BlockSkep), "getHarvestableDrops")]
        private static class Skep
        {
            private static void Postfix(IPlayer? __2, ItemStack[]? __result)
            {
                foreach (ItemStack stack in __result ?? [])
                {
                    Adjust(stack, __2);
                }
            }
        }
    }
}
