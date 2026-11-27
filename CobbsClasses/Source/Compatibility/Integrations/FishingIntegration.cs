using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;

using CobbsClasses.Source.API;

using HarmonyLib;

using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;

namespace CobbsClasses.Source.Compatibility.Integrations;

internal sealed class FishingIntegration : IModIntegration
{
    public string ModId => "ithaniaexpandedfishing";

    private const string TrapSpeedKey = "cobbsclasses:trapSpeed";
    private sealed class TrapState
    {
        public float Speed = 1f;
    }
    private static readonly ConditionalWeakTable<BlockEntity, TrapState> Traps = [];

    public void Register(ICoreAPI api, Harmony harmony)
    {
        OptionalPatchRegistrar registrar = new(api, harmony, ModId);
        const string trap = "IthaniaExpandedFishing.BlockEntities.BlockEntityFishTrap";
        registrar.Patch(trap, "OnInteract", transpiler: new(typeof(FishingIntegration), nameof(BaitTrap)));
        registrar.Patch(trap, "OnServerTick", transpiler: new(typeof(FishingIntegration), nameof(TrapTick)));
        registrar.Patch(trap, "ToTreeAttributes", postfix: new(typeof(FishingIntegration), nameof(SaveTrap)));
        registrar.Patch(trap, "FromTreeAttributes", postfix: new(typeof(FishingIntegration), nameof(LoadTrap)));
        registrar.Patch("IthaniaExpandedFishing.Common.Items.ItemFishNet", "TryPickupFish", transpiler: new(typeof(FishingIntegration), nameof(NetWear)));
        registrar.Patch("Vintagestory.GameContent.EntityBehaviorRightClickPickup", "OnInteract", prefix: new(typeof(FishingIntegration), nameof(BeginWormPickup)), finalizer: new(typeof(FishingIntegration), nameof(EndWormPickup)));
    }

    private static List<CodeInstruction> BaitTrap(IEnumerable<CodeInstruction> instructions)
    {
        List<CodeInstruction> code = [.. instructions];
        int matches = 0;
        for (int index = code.Count - 1; index >= 0; index--)
        {
            if (code[index].opcode == OpCodes.Stfld && code[index].operand is FieldInfo field && field.Name == "lastCatchHours")
            {
                code.InsertRange(index + 1, [new CodeInstruction(OpCodes.Ldarg_0), new CodeInstruction(OpCodes.Ldarg_1), new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(FishingIntegration), nameof(RecordBait)))]);
                matches++;
            }
        }

        if (matches != 2)
        {
            throw new InvalidOperationException("Expected two baiting timestamp writes.");
        }

        return code;
    }

    private static void RecordBait(BlockEntity trap, IPlayer player)
    {
        if (player?.Entity?.World?.Side == EnumAppSide.Server)
        {
            Traps.GetOrCreateValue(trap).Speed = Math.Max(0.05f, player.Entity.Stats.GetBlended(TraitAttributes.Crafting.FishTrapSpeedMul));
        }
    }

    private static List<CodeInstruction> TrapTick(IEnumerable<CodeInstruction> instructions)
    {
        List<CodeInstruction> code = [.. instructions];
        int index = code.FindIndex(instruction => instruction.opcode == OpCodes.Ldfld && instruction.operand is FieldInfo field && field.Name == "CatchIntervalHours");
        if (index < 0)
        {
            throw new InvalidOperationException("Expected fish trap interval not found.");
        }

        code.InsertRange(index + 1, [new CodeInstruction(OpCodes.Ldarg_0), new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(FishingIntegration), nameof(TrapInterval)))]);
        return code;
    }

    private static float TrapInterval(float original, BlockEntity trap)
    {
        return Traps.TryGetValue(trap, out TrapState? state) ? original / Math.Max(0.05f, state.Speed) : original;
    }

    private static void SaveTrap(BlockEntity __instance, ITreeAttribute __0)
    {
        if (Traps.TryGetValue(__instance, out TrapState? state))
        {
            TraitPersistence.WriteVersionedFloat(__0, TrapSpeedKey, state.Speed, 1f, 0.05f, float.MaxValue);
        }
    }

    private static void LoadTrap(BlockEntity __instance, ITreeAttribute __0)
    {
        Traps.GetOrCreateValue(__instance).Speed = TraitPersistence.ReadVersionedFloat(__0, TrapSpeedKey, 1f, 0.05f, float.MaxValue);
    }

    private static List<CodeInstruction> NetWear(IEnumerable<CodeInstruction> instructions)
    {
        List<CodeInstruction> code = [.. instructions];
        int index = code.FindIndex(instruction => instruction.operand is MethodInfo method && method.Name == "DamageItem");
        if (index < 2 || code[index - 2].opcode != OpCodes.Ldc_I4_5 || code[index - 1].opcode != OpCodes.Ldc_I4_1)
        {
            throw new InvalidOperationException("Expected fishing net wear calculation not found.");
        }

        code.InsertRange(index - 1, [new CodeInstruction(OpCodes.Ldarg_2), new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(FishingIntegration), nameof(AdjustWear)))]);
        return code;
    }

    private static int AdjustWear(int original, EntityAgent player)
    {
        if (player is not EntityPlayer || player.World.Side != EnumAppSide.Server)
        {
            return original;
        }

        double amount = Math.Max(0d, original * (double)player.Stats.GetBlended(TraitAttributes.Crafting.FishingDurabilityLoss));
        return (int)Math.Floor(amount) + (player.World.Rand.NextDouble() < amount % 1d ? 1 : 0);
    }

    private static void BeginWormPickup(EntityBehavior __instance, EntityAgent __0, ref ItemStack ___collectedStack, out ItemStack? __state)
    {
        __state = null;
        if (__0 is not EntityPlayer player || player.World.Side != EnumAppSide.Server || __instance.entity.Code?.Domain != "ithaniaexpandedfishing" || __instance.entity.Code.Path.StartsWith("wormbin-", StringComparison.Ordinal) != true || ___collectedStack is null)
        {
            return;
        }

        float multiplier = player.Stats.GetBlended(TraitAttributes.Gathering.WormYieldMul);
        if (multiplier == 1f)
        {
            return;
        }

        __state = ___collectedStack;
        ___collectedStack = __state.Clone();
        double amount = Math.Max(1d, __state.StackSize * (double)multiplier);
        ___collectedStack.StackSize = (int)Math.Floor(amount) + (player.World.Rand.NextDouble() < amount % 1d ? 1 : 0);
    }

    private static void EndWormPickup(ref ItemStack ___collectedStack, ItemStack? __state)
    {
        if (__state is not null)
        {
            ___collectedStack = __state;
        }
    }
}