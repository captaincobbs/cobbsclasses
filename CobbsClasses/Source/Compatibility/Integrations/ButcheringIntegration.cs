using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

using HarmonyLib;

using Vintagestory.API.Common;

namespace CobbsClasses.Source.Compatibility.Integrations;

internal sealed class ButcheringIntegration : IModIntegration
{
    public string ModId => "butchering";

    public void Register(ICoreAPI api, Harmony harmony)
    {
        OptionalPatchRegistrar registrar = new(api, harmony, ModId);
        registrar.Patch("Butchering.src.system.ButcheringSystem", "UpdatePlayerSpeed", transpiler: new HarmonyMethod(typeof(ButcheringIntegration), nameof(AdjustCarry)));
        registrar.Patch("Butchering.src.common.blockentity.BlockEntityButcherHook", "processItem", transpiler: new HarmonyMethod(typeof(ButcheringIntegration), nameof(AdjustDrainStart)));
        HarmonyMethod speed = new(typeof(ButcheringIntegration), nameof(AdjustSpeed));
        registrar.Patch("Butchering.src.common.block.BlockButcherWorkstation", "OnBlockInteractStep", transpiler: speed);
        registrar.Patch("Butchering.src.common.blockentity.BlockEntityButcherWorkstation", "OnInteract", transpiler: speed);
        registrar.Patch("Butchering.src.common.blockentity.BlockEntityButcherWorkstation", "DropLoot", transpiler: new HarmonyMethod(typeof(ButcheringIntegration), nameof(AdjustYield)));
    }

    private static List<CodeInstruction> AdjustCarry(IEnumerable<CodeInstruction> instructions)
    {
        List<CodeInstruction> code = [.. instructions];
        int index = code.FindIndex(instruction => Equals(instruction.operand, "animalCarryingEfficiencyMul"));
        if (index < 0 || index + 1 >= code.Count || code[index + 1].operand is not MethodInfo getter || getter.Name != "GetBlended")
        {
            throw new InvalidOperationException("Expected carcass carrying stat not found.");
        }

        code.InsertRange(index + 2, [
            new CodeInstruction(OpCodes.Ldarg_2),
            new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(ButcheringIntegration), nameof(Carry)))
        ]);
        return code;
    }

    private static float Carry(float native, IPlayer player)
    {
        return native * Math.Max(0f, player?.Entity?.Stats?.GetBlended("heavyCarryMovespeedAffectedness") ?? 1f);
    }

    private static List<CodeInstruction> AdjustDrainStart(IEnumerable<CodeInstruction> instructions)
    {
        List<CodeInstruction> code = [.. instructions];
        int index = code.FindIndex(instruction => instruction.opcode == OpCodes.Stfld
            && instruction.operand is FieldInfo field && field.Name == "skinnedAt");
        if (index < 0)
        {
            throw new InvalidOperationException("Expected carcass drain timestamp not found.");
        }

        CodeInstruction loadHook = new(OpCodes.Ldarg_0);
        loadHook.labels.AddRange(code[index].labels);
        code[index].labels.Clear();
        code.InsertRange(index, [loadHook, new CodeInstruction(OpCodes.Ldarg_1),
            new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(ButcheringIntegration), nameof(DrainStart)))]);
        return code;
    }

    private static double DrainStart(double original, BlockEntity hook, IPlayer player)
    {
        float speed = player?.Entity?.Stats?.GetBlended("carcassDrainRate") ?? 1f;
        if (speed <= 1f || hook is not Vintagestory.GameContent.BlockEntityContainer container)
        {
            return original;
        }

        string? size = container.Inventory?[0]?.Itemstack?.ItemAttributes?["butcheringWorkLoad"]?.AsString();
        double duration = size switch
        {
            "small" => 1d,
            "medium" => 2d,
            "large" => 4d,
            _ => 0d
        };
        // The native timer persists this timestamp in the block entity.
        return Math.Max(double.Epsilon, original - duration * (1d - 1d / speed));
    }

    private static bool IsHook(object workstation)
    {
        return workstation.GetType().Name is "BlockButcherHook" or "BlockEntityButcherHook";
    }

    private static float Speed(float native, IPlayer player, object workstation)
    {
        string stat = IsHook(workstation) ? TraitAttributes.Crafting.SkinningSpeedMul : TraitAttributes.Crafting.ButcherSpeedMul;
        return native * Math.Max(0.05f, player?.Entity?.Stats?.GetBlended(stat) ?? 1f);
    }

    private static float Yield(float native, IPlayer player, object workstation)
    {
        string stat = IsHook(workstation) ? TraitAttributes.Crafting.SkinningBonus : TraitAttributes.Crafting.ButcherBonus;
        return native * Math.Max(0f, player?.Entity?.Stats?.GetBlended(stat) ?? 1f);
    }

    private static IEnumerable<CodeInstruction> AdjustSpeed(IEnumerable<CodeInstruction> instructions, MethodBase original)
    {
        return Adjust(instructions, original, true);
    }

    private static IEnumerable<CodeInstruction> AdjustYield(IEnumerable<CodeInstruction> instructions, MethodBase original)
    {
        return Adjust(instructions, original, false);
    }

    private static List<CodeInstruction> Adjust(IEnumerable<CodeInstruction> instructions, MethodBase original, bool speed)
    {
        int playerArgument = Array.FindIndex(original.GetParameters(), parameter => parameter.ParameterType == typeof(IPlayer)) + 1;
        if (playerArgument == 0)
        {
            throw new InvalidOperationException("No player argument found.");
        }

        MethodInfo statGetter = AccessTools.Method(typeof(EntityStats), nameof(EntityStats.GetBlended), [typeof(string)]);
        MethodInfo roll = AccessTools.Method(typeof(BlockDropItemStack), nameof(BlockDropItemStack.GetNextItemStack), [typeof(float)]);
        MethodInfo adjust = AccessTools.Method(typeof(ButcheringIntegration), speed ? nameof(Speed) : nameof(Yield));
        List<CodeInstruction> code = [.. instructions];
        int index = code.FindIndex(instruction => speed ? instruction.Calls(statGetter) : instruction.Calls(roll));
        if (index < 0 || (speed && (index == 0 || !Equals(code[index - 1].operand, "butcheringSpeedMul"))))
        {
            throw new InvalidOperationException("Expected butchering stat or loot roll not found.");
        }

        int insertion = speed ? index + 1 : index;
        CodeInstruction loadPlayer = CodeInstruction.LoadArgument(playerArgument);
        if (!speed)
        {
            loadPlayer.labels.AddRange(code[index].labels);
            code[index].labels.Clear();
        }
        code.InsertRange(insertion, [loadPlayer, new CodeInstruction(OpCodes.Ldarg_0), new CodeInstruction(OpCodes.Call, adjust)]);
        return code;
    }
}