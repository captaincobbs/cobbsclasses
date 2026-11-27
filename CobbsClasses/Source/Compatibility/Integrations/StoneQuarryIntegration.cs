using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

using HarmonyLib;

using Vintagestory.API.Common;

namespace CobbsClasses.Source.Compatibility.Integrations;

internal sealed class StoneQuarryIntegration : IModIntegration
{
    public string ModId => "stonequarryrepckfipil";

    [ThreadStatic]
    private static IPlayer? currentPlayer;

    public void Register(ICoreAPI api, Harmony harmony)
    {
        OptionalPatchRegistrar registrar = new(api, harmony, ModId);
        registrar.Patch("StoneQuarry.BlockPlugAndFeather", "OnBlockInteractStart",
            prefix: new(typeof(StoneQuarryIntegration), nameof(BeginHit)),
            finalizer: new(typeof(StoneQuarryIntegration), nameof(EndHit)));
        registrar.Patch("StoneQuarry.BEPlugAndFeather", "TryHitPlug",
            transpiler: new(typeof(StoneQuarryIntegration), nameof(AdjustImpact)));
    }

    private static void BeginHit(IPlayer __1, out IPlayer? __state)
    {
        __state = currentPlayer;
        currentPlayer = __1;
    }

    private static void EndHit(IPlayer? __state)
    {
        currentPlayer = __state;
    }

    private static List<CodeInstruction> AdjustImpact(IEnumerable<CodeInstruction> instructions)
    {
        List<CodeInstruction> code = [.. instructions];
        int index = code.FindIndex(instruction => instruction.operand is MethodInfo method && method.Name == "AsInt");
        if (index < 0 || index + 1 >= code.Count || code[index + 1].opcode != OpCodes.Add)
        {
            throw new InvalidOperationException("Expected plug impact calculation not found.");
        }

        code.Insert(index + 1, new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(StoneQuarryIntegration), nameof(Impact))));
        return code;
    }

    private static int Impact(int original)
    {
        if (original <= 0 || currentPlayer?.Entity?.World is not IWorldAccessor world || world.Side != EnumAppSide.Server)
        {
            return original;
        }

        float multiplier = currentPlayer.Entity.Stats.GetBlended(TraitAttributes.Crafting.PlugWorkModifier);
        if (multiplier == 1f)
        {
            return original;
        }

        double amount = Math.Clamp(original * (double)multiplier, 0, int.MaxValue - 1d);
        return (int)Math.Floor(amount) + (world.Rand.NextDouble() < amount % 1d ? 1 : 0);
    }
}