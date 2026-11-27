using System;
using System.Linq;

using HarmonyLib;

using Vintagestory.API.Common;

namespace CobbsClasses.Source.Compatibility.Integrations;

internal sealed class TailorsDelightIntegration : IModIntegration
{
    public string ModId => "tailorsdelight";

    public void Register(ICoreAPI api, Harmony harmony)
    {
        OptionalPatchRegistrar registrar = new(api, harmony, ModId);
        registrar.Patch("Vintagestory.API.Common.CollectibleObject", "OnConsumedByCrafting",
            prefix: new(typeof(TailorsDelightIntegration), nameof(AdjustConsumption)));
    }

    private static bool AdjustConsumption(ItemSlot[] __0, ItemSlot __1, IRecipeBase __2,
        IRecipeIngredient __3, IPlayer __4, ref int __5)
    {
        if (__4?.Entity?.World is not IWorldAccessor world || world.Side != EnumAppSide.Server || __2 is not GridRecipe recipe || __1?.Itemstack?.Collectible?.Code is not AssetLocation code || recipe.Ingredients is null || !recipe.Ingredients.Values.Any(slot => slot?.Code is AssetLocation ingredient && ingredient.Domain == "tailorsdelight" && (ingredient.Path.StartsWith("needle-") || ingredient.Path.StartsWith("awl-"))) || __3.ReturnedStack is not null || !__3.ConsumeProperties.Consume || __3.ConsumeProperties.DurabilityCost != 0 || __5 <= 0)
        {
            return true;
        }

        string? stat = code.Domain == "tailorsdelight" && (code.Path.StartsWith("needle-") || code.Path.StartsWith("awl-")) ? TraitAttributes.Crafting.SewingDurabilityLoss : code.Path.Contains("twine", StringComparison.Ordinal) || code.Path.Contains("thread", StringComparison.Ordinal) ? TraitAttributes.Crafting.SewingThreadMul : null;
        if (stat is null)
        {
            return true;
        }

        float multiplier = __4.Entity.Stats.GetBlended(stat);
        if (multiplier == 1f)
        {
            return true;
        }

        double amount = Math.Clamp(__5 * (double)multiplier, 0, __5);
        __5 = (int)Math.Floor(amount) + (world.Rand.NextDouble() < amount % 1d ? 1 : 0);
        return __5 > 0;
    }
}


