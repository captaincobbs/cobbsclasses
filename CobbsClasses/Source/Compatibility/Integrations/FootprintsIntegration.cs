using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

using HarmonyLib;

using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace CobbsClasses.Source.Compatibility.Integrations;

internal sealed class FootprintsIntegration : IModIntegration
{
    public string ModId => "footprints";

    public void Register(ICoreAPI api, Harmony harmony)
    {
        if (api is not ICoreClientAPI)
        {
            return;
        }

        OptionalPatchRegistrar registrar = new(api, harmony, ModId);
        HarmonyMethod patch = new(typeof(FootprintsIntegration), nameof(AdjustLifetime));
        registrar.Patch("components.Footprints.Footprint", "get_IsExpired", transpiler: patch);
        registrar.Patch("components.Footprints.Footprint", "get_RemainingLifeFraction", transpiler: patch);
    }

    private static double Lifetime(int original, ICoreClientAPI? client)
    {
        return original * (double)Math.Max(0.01f, client?.World?.Player?.Entity?.Stats?.GetBlended("trackdecay") ?? 1f);
    }

    private static List<CodeInstruction> AdjustLifetime(IEnumerable<CodeInstruction> instructions)
    {
        List<CodeInstruction> code = [.. instructions];
        int index = code.FindIndex(instruction => instruction.operand is MethodInfo method
            && method.DeclaringType?.FullName == "Footprints.src.Config.ConfigClient"
            && method.Name == "get_AnimalFootprintLifespan");
        if (index < 0 || code.Find(instruction => instruction.operand is FieldInfo field && field.IsStatic && field.FieldType == typeof(ICoreClientAPI))?.operand is not FieldInfo clientField || code[index + 1].opcode != OpCodes.Br_S)
        {
            throw new InvalidOperationException("Unexpected Footprints lifetime calculation.");
        }

        // Keep the integer stack type at control-flow joins; adjust age instead of config.
        code.InsertRange(index + 1, [
            new CodeInstruction(OpCodes.Ldsfld, clientField),
            new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(FootprintsIntegration), nameof(RoundedLifetime)))
        ]);
        return code;
    }

    private static int RoundedLifetime(int original, ICoreClientAPI? client)
    {
        return (int)Math.Clamp(Math.Round(Lifetime(original, client)), 0, int.MaxValue);
    }
}