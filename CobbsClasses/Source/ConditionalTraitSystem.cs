using System;
using System.Runtime.CompilerServices;

using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
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
            JsonObject? imm =  ReadSettingsAsset(api, "cobbsclasses:config/imm-conditional-traits.json");

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
            float jump = 0f;
            float hunger = 0f;
            float toolSpeed = 0f;

            if (IsActivePlayer(player))
            {
                walk = stats.GetBlended("moveSpeedAlone") - 1f;
                jump = stats.GetBlended("jumpHeightMulAlone") - 1f;
                hunger = stats.GetBlended("hungerrateAlone") - 1f;
                if ((walk != 0f || jump != 0f || hunger != 0f) && HasNearbyPlayer(player, onlinePlayers))
                {
                    walk = jump = hunger = 0f;
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
                if (walk == 0f && jump == 0f && hunger == 0f && toolSpeed == 0f)
                {
                    return;
                }
                applied = appliedBonuses.GetValue(stats, static _ => new AppliedBonuses());
            }

            ApplyBonus(stats, "walkspeed", walk, ref applied.Walk);
            ApplyBonus(stats, "jumpHeightMul", jump, ref applied.Jump);
            ApplyBonus(stats, "hungerrate", hunger, ref applied.Hunger);
            ApplyBonus(stats, "globalSpeedMul", toolSpeed, ref applied.ToolSpeed);
        }

        private static bool IsActivePlayer(IServerPlayer player) => player.ConnectionState == EnumClientState.Playing && player.Entity is { Alive: true } && player.WorldData.CurrentGameMode == EnumGameMode.Survival;

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

        private sealed class AppliedBonuses
        {
            public float Walk;
            public float Jump;
            public float Hunger;
            public float ToolSpeed;
        }
    }
}
