using System;
using System.Runtime.CompilerServices;

using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace CobbsClasses.Source
{
    public sealed class ConditionalTraitSystem : ModSystem
    {
        private const string MODIFIER_SOURCE = "cobbsclasses-conditional";
        private const int DefaultRefreshInterval = 500;
        private const float DefaultAloneRadius = 32 * 32;
        private const float DefaultSatedThreshold = 0.6f;
        private const float DefaultFullThreshold = 0.85f;


        private readonly ConditionalWeakTable<EntityStats, AppliedBonuses> appliedBonuses = [];
        private ICoreServerAPI? serverAPI;
        private long refreshListener;
        private float satedThreshold = DefaultSatedThreshold;
        private float fullThreshold = DefaultFullThreshold;
        private double aloneRadiusSquared = DefaultAloneRadius * DefaultAloneRadius;

        public override bool ShouldLoad(EnumAppSide side) => side == EnumAppSide.Server;
        public override void StartServerSide(ICoreServerAPI api)
        {
            serverAPI = api;
            JsonObject? configLib = ReadSettingsAsset(api, "cobbsclasses:config/conditional-traits.json");
            JsonObject? imm = ReadSettingsAsset(api, "cobbsclasses:config/imm-conditional-traits.json");

            int refreshInterval = (int)Math.Clamp(ReadSetting(configLib, imm, "RefreshInterval", DefaultRefreshInterval), 50f, 60_000f);
            satedThreshold = Math.Clamp(ReadSetting(configLib, imm, "SatedThreshold", DefaultSatedThreshold), 0f, 1f);
            fullThreshold = Math.Clamp(ReadSetting(configLib, imm, "FullThreshold", DefaultFullThreshold), satedThreshold, 1f);
            double aloneRadius = Math.Clamp(ReadSetting(configLib, imm, "AloneRadius", DefaultAloneRadius), 0f, 1024f);
            aloneRadiusSquared = aloneRadius * aloneRadius;

            refreshListener = api.Event.RegisterGameTickListener(RefreshPlayers, refreshInterval);
        }

        private static JsonObject? ReadSettingsAsset(ICoreServerAPI api, string location)
        {
            IAsset? asset = api.Assets.TryGet(new AssetLocation(location));
            return asset is null ? null : JsonObject.FromJson(asset.ToText());
        }

        private static float ReadSetting(JsonObject? configLib, JsonObject? imm, string name, float fallback)
        {
            float immValue = imm?[name].AsFloat(fallback) ?? fallback;
            if (float.IsFinite(immValue) && immValue != fallback)
            {
                return immValue;
            }

            float configLibValue = configLib?[name].AsFloat(fallback) ?? fallback;
            return float.IsFinite(configLibValue) ? configLibValue : fallback;
        }

        public override void Dispose()
        {
            serverAPI?.Event.UnregisterGameTickListener(refreshListener);
            base.Dispose();
        }

        private void RefreshPlayers(float _)
        {
            if (serverAPI is null)
            {
                return;
            }

            IPlayer[] onlinePlayers = serverAPI.World.AllOnlinePlayers;
            foreach (IPlayer onlinePlayer in onlinePlayers)
            {
                if (onlinePlayer is IServerPlayer player && player.Entity is { } entity)
                {
                    RefreshPlayer(player, entity, onlinePlayers);
                }
            }
        }

        private void RefreshPlayer(IServerPlayer player, EntityPlayer entity, IPlayer[] onlinePlayers)
        {
            EntityStats stats = entity.Stats;
            float walk = 0f;
            float crouch = 0f;
            float jump = 0f;
            float hunger = 0f;
            float toolSpeed = 0f;
            float regenRate = 0f;
            float regenFlat = 0f;
            float detectionRange = 0f;

            if (IsActivePlayer(player))
            {
                walk = stats.GetBlended("moveSpeedAlone") - 1f;
                jump = stats.GetBlended("jumpHeightMulAlone") - 1f;
                hunger = stats.GetBlended("hungerrateAlone") - 1f;
                if ((walk != 0f || jump != 0f || hunger != 0f) && HasNearbyPlayer(player, onlinePlayers))
                {
                    walk = jump = hunger = 0f;
                }

                float unarmoredWalk = stats.GetBlended("moveSpeedUnarmored") - 1f;
                float unarmoredCrouch = stats.GetBlended("sneakSpeedUnarmored") - 1f;
                float unarmoredRegenRate = stats.GetBlended("healthRegenRateUnarmored") - 1f;
                float unarmoredRegenFlat = stats.GetBlended("healthRegenFlatUnarmored") - 1f;
                float unarmoredDetectionRange = stats.GetBlended("animalSeekingRangeUnarmored") - 1f;
                float lightArmorWalk = stats.GetBlended("moveSpeedLightArmor") - 1f;
                float lightArmorCrouch = stats.GetBlended("sneakSpeedLightArmor") - 1f;
                float lightArmorRegenRate = stats.GetBlended("healthRegenRateLightArmor") - 1f;
                float lightArmorRegenFlat = stats.GetBlended("healthRegenFlatLightArmor") - 1f;
                float lightArmorDetectionRange = stats.GetBlended("animalSeekingRangeLightArmor") - 1f;
                float heavyArmorWalk = stats.GetBlended("moveSpeedHeavyArmor") - 1f;
                float heavyArmorCrouch = stats.GetBlended("sneakSpeedHeavyArmor") - 1f;
                float heavyArmorRegenRate = stats.GetBlended("healthRegenRateHeavyArmor") - 1f;
                float heavyArmorRegenFlat = stats.GetBlended("healthRegenFlatHeavyArmor") - 1f;
                float heavyArmorDetectionRange = stats.GetBlended("animalSeekingRangeHeavyArmor") - 1f;
                if (unarmoredWalk != 0f || unarmoredCrouch != 0f || unarmoredRegenRate != 0f || unarmoredRegenFlat != 0f || lightArmorWalk != 0f || lightArmorCrouch != 0f || lightArmorRegenRate != 0f || lightArmorRegenFlat != 0f || heavyArmorWalk != 0f || heavyArmorCrouch != 0f || heavyArmorRegenRate != 0f || heavyArmorRegenFlat != 0f
                    || unarmoredDetectionRange != 0f || lightArmorDetectionRange != 0f || heavyArmorDetectionRange != 0f)
                {
                    switch (GetArmorWeight(player))
                    {
                        case ArmorWeight.Unarmored:
                        {
                            walk += unarmoredWalk;
                            crouch += unarmoredCrouch;
                            regenRate = unarmoredRegenRate;
                            regenFlat = unarmoredRegenFlat;
                            detectionRange = unarmoredDetectionRange;
                            break;
                        }
                        case ArmorWeight.Light:
                        {
                            walk += lightArmorWalk;
                            crouch += lightArmorCrouch;
                            regenRate = lightArmorRegenRate;
                            regenFlat = lightArmorRegenFlat;
                            detectionRange = lightArmorDetectionRange;
                            break;
                        }
                        case ArmorWeight.Heavy:
                        {
                            walk += heavyArmorWalk;
                            crouch += heavyArmorCrouch;
                            regenRate = heavyArmorRegenRate;
                            regenFlat = heavyArmorRegenFlat;
                            detectionRange = heavyArmorDetectionRange;
                            break;
                        }
                    }
                }

                float satedBonus = stats.GetBlended("globalSpeedMulWhenSated") - 1f;
                float fullBonus = stats.GetBlended("globalSpeedMulWhenFull") - 1f;
                if ((satedBonus != 0f || fullBonus != 0f) && entity.GetBehavior<EntityBehaviorHunger>() is { } food)
                {
                    toolSpeed = GetFoodSpeedBonus(food.Saturation, food.MaxSaturation, satedBonus, fullBonus);
                }
            }

            if (!appliedBonuses.TryGetValue(stats, out AppliedBonuses? applied))
            {
                if (walk == 0f && crouch == 0f && jump == 0f && hunger == 0f && toolSpeed == 0f && regenRate == 0f && regenFlat == 0f
                    && detectionRange == 0f)
                {
                    return;
                }
                applied = appliedBonuses.GetValue(stats, static _ => new AppliedBonuses());
            }

            ApplyBonus(stats, "walkspeed", walk, ref applied.Walk);
            ApplyBonus(stats, "sneakSpeed", crouch, ref applied.Sneak);
            ApplyBonus(stats, "jumpHeightMul", jump, ref applied.Jump);
            ApplyBonus(stats, "hungerrate", hunger, ref applied.Hunger);
            ApplyBonus(stats, "globalSpeedMul", toolSpeed, ref applied.ToolSpeed);
            ApplyBonus(stats, "healthRegenRate", regenRate, ref applied.RegenRate);
            ApplyBonus(stats, "healthRegenFlat", regenFlat, ref applied.RegenFlat);
            ApplyBonus(stats, "animalSeekingRange", detectionRange, ref applied.DetectionRange);
        }

        private static bool IsActivePlayer(IServerPlayer player) => player.ConnectionState == EnumClientState.Playing && player.Entity is { Alive: true } && player.WorldData.CurrentGameMode == EnumGameMode.Survival;

        private static ArmorWeight GetArmorWeight(IPlayer player)
        {
            IInventory? equipment = player.InventoryManager.GetOwnInventory(GlobalConstants.characterInvClassName);
            if (equipment is null)
            {
                return ArmorWeight.Heavy;
            }

            bool hasArmor = false;
            float movementPenalty = 0f;
            foreach (ItemSlot slot in equipment)
            {
                if (slot.Itemstack?.Collectible.GetCollectibleInterface<IWearableStatsSupplier>() is not { } wearable
                    || !wearable.IsArmorType(slot))
                {
                    continue;
                }

                hasArmor = true;
                float walkSpeedModifier = wearable.GetStatModifiers(slot)?.walkSpeed ?? 0f;
                movementPenalty += Math.Max(0f, -walkSpeedModifier);
            }

            if (!hasArmor)
            {
                return ArmorWeight.Unarmored;
            }

            // Class movement bonuses and positive gear modifiers do not offset armor weight.
            return movementPenalty <= 0.10001f ? ArmorWeight.Light : ArmorWeight.Heavy;
        }

        private bool HasNearbyPlayer(IServerPlayer player, IPlayer[] onlinePlayers)
        {
            EntityPos position = player.Entity.Pos;
            foreach (IPlayer onlinePlayer in onlinePlayers)
            {
                if (onlinePlayer is not IServerPlayer other || other.PlayerUID == player.PlayerUID || !IsActivePlayer(other))
                {
                    continue;
                }

                if (IsWithinAloneRadius(position, other.Entity.Pos))
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsWithinAloneRadius(EntityPos position, EntityPos otherPosition)
        {
            if (position.Dimension != otherPosition.Dimension)
            {
                return false;
            }

            double x = position.X - otherPosition.X;
            double y = position.Y - otherPosition.Y;
            double z = position.Z - otherPosition.Z;
            return x * x + y * y + z * z <= aloneRadiusSquared;
        }

        private float GetFoodSpeedBonus(float saturation, float maximum, float satedBonus, float fullBonus)
        {
            if (maximum <= 0f)
            {
                return 0f;
            }

            float fullness = saturation / maximum;
            if (fullness >= fullThreshold)
            {
                return satedBonus + fullBonus;
            }

            return fullness >= satedThreshold ? satedBonus : 0f;
        }

        private static void ApplyBonus(EntityStats stats, string stat, float bonus, ref float applied)
        {
            if (bonus == applied)
            {
                return;
            }

            if (bonus == 0f)
            {
                stats.Remove(stat, MODIFIER_SOURCE);
            }
            else
            {
                stats.Set(stat, MODIFIER_SOURCE, bonus, persistent: false);
            }

            applied = bonus;
        }

        private enum ArmorWeight
        {
            Unarmored,
            Light,
            Heavy
        }

        private sealed class AppliedBonuses
        {
            public float Walk;
            public float Sneak;
            public float Jump;
            public float Hunger;
            public float ToolSpeed;
            public float RegenRate;
            public float RegenFlat;
            public float DetectionRange;
        }
    }
}
