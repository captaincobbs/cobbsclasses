using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

using HarmonyLib;

using Vintagestory.API.Common;

namespace CobbsClasses.Source.Compatibility.Integrations;

internal sealed class WoodworkingIntegration : IModIntegration
{
    public string ModId => "immersivewoodworking";

    public void Register(ICoreAPI api, Harmony harmony)
    {
        OptionalPatchRegistrar registrar = new(api, harmony, ModId);
        const string sawhorse = "ImmersiveWoodworking.BlockEntitySawhorse";
        registrar.Patch(sawhorse, "AdvanceDebark", prefix: new(typeof(WoodworkingIntegration), nameof(Debark)));
        registrar.Patch(sawhorse, "DropStageBatch", prefix: new(typeof(WoodworkingIntegration), nameof(BoardYield)));
        registrar.Patch("ImmersiveWoodworking.BlockEntityChoppingBlock", "BeginChopHold", transpiler: new(typeof(WoodworkingIntegration), nameof(ChopSpeed)));
    }

    private static float Speed(IPlayer? player, string stat)
    {
        return Math.Max(0.05f, player?.Entity?.Stats?.GetBlended(stat) ?? 1f);
    }

    private static void Debark(ref float __0, IPlayer __1)
    {
        __0 *= Speed(__1, TraitAttributes.Crafting.DebarkingSpeedMul);
    }

    private static void BoardYield(ItemStack __0, ref int __1, ref int __2, IPlayer __3)
    {
        if (__3?.Entity?.World is not IWorldAccessor world || world.Side != EnumAppSide.Server || __0?.Collectible?.Code?.Path.StartsWith("board-", StringComparison.Ordinal) != true || __1 <= 0)
        {
            return;
        }

        float multiplier = __3.Entity.Stats.GetBlended(TraitAttributes.Crafting.BoardProductionMultiplier);
        if (multiplier == 1f)
        {
            return;
        }

        double amount = Math.Clamp(__1 * (double)multiplier, 0, int.MaxValue - 1d);
        __1 = (int)Math.Floor(amount) + (world.Rand.NextDouble() < amount % 1d ? 1 : 0);
        __2 = Math.Max(__2, (int)Math.Ceiling(__1 / (double)Math.Max(1, __0.Collectible.MaxStackSize)));
    }

    private static List<CodeInstruction> ChopSpeed(IEnumerable<CodeInstruction> instructions)
    {
        List<CodeInstruction> code = [.. instructions];
        int index = code.FindIndex(instruction => instruction.operand is MethodInfo method && method.Name == "ChopSharpnessFactor");
        if (index < 0)
        {
            throw new InvalidOperationException("Expected chopping sharpness calculation not found.");
        }

        code.InsertRange(index + 1, [new CodeInstruction(OpCodes.Ldarg_2), new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(WoodworkingIntegration), nameof(ChopFactor)))]);
        return code;
    }

    private static float ChopFactor(float original, IPlayer player)
    {
        return original * Speed(player, TraitAttributes.Crafting.WoodChopSpeedMul);
    }
}