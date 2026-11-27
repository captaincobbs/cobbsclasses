using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

using CobbsClasses.Source.Patches;

using HarmonyLib;

using Vintagestory.API.Common;

namespace CobbsClasses.Source.Compatibility.Integrations
{
    internal sealed class GoldenCombsIntegration : IModIntegration
    {
        public string ModId => "fromgoldencombs";

        public void Register(ICoreAPI api, Harmony harmony)
        {
            OptionalPatchRegistrar integration = new(api, harmony, ModId);
            HarmonyMethod transpiler = new(typeof(GoldenCombsIntegration), nameof(AdjustCreatedStacks));
            integration.Patch("FromGoldenCombs.Blocks.ClayHiveTop", "OnBlockInteractStep", transpiler: transpiler);
            integration.Patch("FromGoldenCombs.BlockEntities.BEFrameRack", "TryHarvest", transpiler: transpiler);
            integration.Patch("FromGoldenCombs.BlockEntities.BEFrameRack", "OnInteract", transpiler: transpiler);
        }

        private static IEnumerable<CodeInstruction> AdjustCreatedStacks(IEnumerable<CodeInstruction> instructions, MethodBase original)
        {
            ConstructorInfo stack = AccessTools.Constructor(typeof(ItemStack), [typeof(Item), typeof(int)]);
            MethodInfo adjust = AccessTools.Method(typeof(HoneycombYieldPatch), nameof(HoneycombYieldPatch.Adjust));
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

    }
}