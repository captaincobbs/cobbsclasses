using System;

using HarmonyLib;

using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.GameContent;

namespace CobbsClasses.Source.Patches
{
    [HarmonyPatch(typeof(EntityProjectileBase), nameof(EntityProjectileBase.SpawnProjectile))]
    internal static class ArrowBreakChancePatch
    {
        private static void Prefix(Entity __0, EntityAgent __1)
        {
            if (__0 is EntityProjectileBase projectile && __1 is EntityPlayer player && player.World.Side == EnumAppSide.Server && projectile.ProjectileStack?.Collectible is ItemArrow)
            {
                float change = player.Stats.GetBlended(TraitAttributes.Combat.ArrowBreakChance) - 1f;
                projectile.DropOnImpactChance = 1f - Math.Clamp((1f - projectile.DropOnImpactChance) * (1f + change), 0f, 1f);
            }
        }
    }
}

