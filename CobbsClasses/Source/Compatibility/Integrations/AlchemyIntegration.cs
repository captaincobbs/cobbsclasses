using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Linq;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;
using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;

namespace CobbsClasses.Source.Compatibility.Integrations;

internal sealed class AlchemyIntegration : IModIntegration
{
    public string ModId => "alchemy";

    private const string BrewSpeedKey = "cobbsclasses:brewSpeed";
    private sealed class BrewState
    {
        public float Speed = 1f;
    }

    private static readonly ConditionalWeakTable<BlockEntity, BrewState> Brews = new();

    public void Register(ICoreAPI api, Harmony harmony)
    {
        OptionalPatchRegistrar registrar = new(api, harmony, ModId);
        const string firepit = "Vintagestory.GameContent.BlockEntityFirepit";
        registrar.Patch(firepit, "OnReceivedClientPacket",
            prefix: new(typeof(AlchemyIntegration), nameof(BeforeIngredients)),
            postfix: new(typeof(AlchemyIntegration), nameof(AfterIngredients)));
        registrar.Patch(firepit, "heatInput",
            prefix: new(typeof(AlchemyIntegration), nameof(BeforeHeat)),
            postfix: new(typeof(AlchemyIntegration), nameof(AfterHeat)));
        registrar.Patch(firepit, "ToTreeAttributes", postfix: new(typeof(AlchemyIntegration), nameof(SaveBrew)));
        registrar.Patch(firepit, "FromTreeAttributes", postfix: new(typeof(AlchemyIntegration), nameof(LoadBrew)));
        const string logic = "Alchemy.PotionConsumableLogic";
        registrar.Patch(logic, "TryProcessPotionEffects", transpiler: new(typeof(AlchemyIntegration), nameof(Duration)));
        registrar.Patch(logic, "ApplySideEffects", transpiler: new(typeof(AlchemyIntegration), nameof(Penalty)));
        registrar.Patch(logic, "ApplyCoating", transpiler: new(typeof(AlchemyIntegration), nameof(Coating)));
    }

    private static bool IsCauldron(BlockEntity instance)
    {
        return instance.GetType().FullName == "Alchemy.BlockEntityCauldronFirepit";
    }

    private static string? Ingredients(BlockEntityFirepit instance)
    {
        if (!IsCauldron(instance) || instance.Inventory is not InventorySmelting inventory)
        {
            return null;
        }

        return string.Join("|", inventory.CookingSlots.Select(slot =>
            slot?.Itemstack is ItemStack stack ? $"{stack.Collectible.Code}:{stack.StackSize}" : ""));
    }

    private static void BeforeIngredients(BlockEntityFirepit __instance, out string? __state)
    {
        __state = Ingredients(__instance);
    }

    private static void AfterIngredients(BlockEntityFirepit __instance, IPlayer __0, string? __state)
    {
        if (__state is not null && __state != Ingredients(__instance) && __0?.Entity?.World?.Side == EnumAppSide.Server)
        {
            Brews.GetOrCreateValue(__instance).Speed = Math.Max(0.05f, __0.Entity.Stats.GetBlended(TraitAttributes.Crafting.PotionBrewTime));
            __instance.MarkDirty();
        }
    }

    private static void BeforeHeat(BlockEntityFirepit __instance, float ___inputStackCookingTime, out float __state)
    {
        __state = ___inputStackCookingTime;
    }

    private static void AfterHeat(BlockEntityFirepit __instance, ref float ___inputStackCookingTime, float __state)
    {
        if (___inputStackCookingTime > __state && IsCauldron(__instance)
            && Brews.TryGetValue(__instance, out BrewState? state))
        {
            ___inputStackCookingTime = __state + (___inputStackCookingTime - __state) * state.Speed;
        }
    }

    private static void SaveBrew(BlockEntityFirepit __instance, ITreeAttribute __0)
    {
        if (IsCauldron(__instance) && Brews.TryGetValue(__instance, out BrewState? state))
        {
            __0.SetFloat(BrewSpeedKey, state.Speed);
        }
    }

    private static void LoadBrew(BlockEntityFirepit __instance, ITreeAttribute __0)
    {
        if (IsCauldron(__instance))
        {
            Brews.GetOrCreateValue(__instance).Speed = Math.Max(0.05f, __0.GetFloat(BrewSpeedKey, 1f));
        }
    }

    private static IEnumerable<CodeInstruction> Duration(IEnumerable<CodeInstruction> instructions)
    {
        return InsertAfter(instructions, "Build", nameof(AdjustDuration), 0);
    }

    private static IEnumerable<CodeInstruction> Penalty(IEnumerable<CodeInstruction> instructions)
    {
        return InsertAfter(instructions, "GetDrinkingSideEffectTotals", nameof(AdjustPenalty), 0);
    }

    private static IEnumerable<CodeInstruction> InsertAfter(IEnumerable<CodeInstruction> instructions, string target, string helper, int playerArgument)
    {
        List<CodeInstruction> code = new(instructions);
        int index = code.FindIndex(instruction => instruction.operand is MethodInfo method && method.Name == target);
        if (index < 0)
        {
            throw new InvalidOperationException($"Expected Alchemy calculation {target} not found.");
        }

        if (target == "Build")
        {
            code.InsertRange(index + 1, [new CodeInstruction(OpCodes.Dup), CodeInstruction.LoadArgument(playerArgument),
                new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(AlchemyIntegration), helper))]);
        }
        else
        {
            code.InsertRange(index + 1, [CodeInstruction.LoadArgument(playerArgument),
                new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(AlchemyIntegration), helper))]);
        }

        return code;
    }

    private static void AdjustDuration(object? effect, EntityAgent player)
    {
        if (effect is null || player is not EntityPlayer)
        {
            return;
        }

        PropertyInfo? duration = AccessTools.Property(effect.GetType(), "Duration");
        if (duration?.GetValue(effect) is int original && original > 0 && duration.CanWrite)
        {
            duration.SetValue(effect, (int)Math.Clamp(Math.Round(original * (double)player.Stats.GetBlended(TraitAttributes.Crafting.PotionDuration)), 1, int.MaxValue));
        }
    }

    private static (float, float, float, float) AdjustPenalty((float, float, float, float) original, Entity entity)
    {
        if (entity is not EntityPlayer player)
        {
            return original;
        }

        float multiplier = Math.Clamp(player.Stats.GetBlended(TraitAttributes.Crafting.PotionPenalty), 0f, 1f);
        return (original.Item1 * multiplier, original.Item2 * multiplier, original.Item3 * multiplier, original.Item4 * multiplier);
    }

    private static IEnumerable<CodeInstruction> Coating(IEnumerable<CodeInstruction> instructions)
    {
        List<CodeInstruction> code = new(instructions);
        int index = code.FindIndex(instruction => instruction.operand is MethodInfo method && method.Name == "SetWeaponCoat");
        if (index < 0)
        {
            throw new InvalidOperationException("Expected weapon coating hit count not found.");
        }

        code.InsertRange(index, [new CodeInstruction(OpCodes.Ldarg_2),
            new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(AlchemyIntegration), nameof(AdjustCoating)))]);
        return code;
    }

    private static int AdjustCoating(int original, EntityAgent player)
    {
        return player is EntityPlayer ? (int)Math.Clamp(Math.Round(original * (double)player.Stats.GetBlended(TraitAttributes.Crafting.CoatingDuration)), 1, int.MaxValue) : original;
    }
}

