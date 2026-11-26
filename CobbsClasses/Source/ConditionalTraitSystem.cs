using System;
using System.Linq;
using System.Runtime.CompilerServices;

using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace CobbsClasses.Source
{
    public sealed class ConditionalTraitSystem : ModSystem
    {
        private const string MODIFIER_SOURCE = "cobbsclasses-conditional";
        private const int DefaultRefreshInterval = 500;
        private const float DefaultAloneRadius = 32f;
        private const float DefaultSatedThreshold = 0.6f;
        private const float DefaultFullThreshold = 0.85f;

        private static readonly ArmorStat[] ArmorStats =
        [
            new("walkspeed", "moveSpeed"),
            new("sneakSpeed", "sneakSpeed"),
            new("healthRegenRate", "healthRegenRate"),
            new("healthRegenFlat", "healthRegenFlat"),
            new("animalSeekingRange", "animalSeekingRange"),
            new("armorProtection", "armorProtection"),
            new("armorFlatProtection", "armorFlatProtection"),
            new("armorWalkSpeedAffectedness", "armorWalkSpeedAffectedness"),
            new("armorHungerAffectedness", "armorHungerAffectedness"),
            new("armorHealingAffectedness", "armorHealingAffectedness"),
            new("armorRangedAffectedness", "armorRangedAffectedness")
        ];

        private static readonly string[] OutputStats =
        [.. ArmorStats.Select(rule => rule.Output),
            "jumpHeightMul",
            "hungerrate",
            "globalSpeedMul",
            "globalDamage",
            "environmentStabilityChange",
            "environmentStabilityResistance",
            "darkvisionStrength"
        ];

        private static readonly (string Input, string Output)[] SatedStats =
        [
            ("globalDamageWhenSated", "globalDamage"),
            ("globalSpeedMulWhenSated", "globalSpeedMul"),
            ("moveSpeedWhenSated", "walkspeed"),
            ("healthRegenRateWhenSated", "healthRegenRate")
        ];

        private readonly ConditionalWeakTable<EntityStats, float[]> appliedBonuses = [];
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
            Span<float> bonuses = stackalloc float[OutputStats.Length];
            bonuses.Clear();

            if (IsActivePlayer(player))
            {
                AddAloneBonuses(player, onlinePlayers, stats, bonuses);
                AddArmorBonuses(player, stats, bonuses);
                AddFoodBonus(entity, stats, bonuses);
                AddEnvironmentBonuses(player, onlinePlayers, stats, bonuses);
            }

            if (!appliedBonuses.TryGetValue(stats, out float[]? applied))
            {
                if (bonuses.IndexOfAnyExcept(0f) < 0)
                {
                    return;
                }

                applied = appliedBonuses.GetValue(stats, static _ => new float[OutputStats.Length]);
            }

            for (int index = 0; index < OutputStats.Length; index++)
            {
                ApplyBonus(stats, OutputStats[index], bonuses[index], ref applied[index]);
            }
        }

        private void AddAloneBonuses(IServerPlayer player, IPlayer[] onlinePlayers, EntityStats stats, Span<float> bonuses)
        {
            float walk = stats.GetBlended("moveSpeedAlone") - 1f;
            float jump = stats.GetBlended("jumpHeightMulAlone") - 1f;
            float hunger = stats.GetBlended("hungerrateAlone") - 1f;
            if ((walk != 0f || jump != 0f || hunger != 0f) && !HasNearbyPlayer(player, onlinePlayers))
            {
                bonuses[Array.IndexOf(OutputStats, "walkspeed")] += walk;
                bonuses[Array.IndexOf(OutputStats, "jumpHeightMul")] += jump;
                bonuses[Array.IndexOf(OutputStats, "hungerrate")] += hunger;
            }
        }

        private static void AddArmorBonuses(IServerPlayer player, EntityStats stats, Span<float> bonuses)
        {
            if (!HasArmorBonuses(stats))
            {
                return;
            }

            ArmorWeight weight = GetArmorWeight(player);
            for (int index = 0; index < ArmorStats.Length; index++)
            {
                ArmorStat rule = ArmorStats[index];
                string? input = weight switch
                {
                    ArmorWeight.Unarmored => rule.Unarmored,
                    ArmorWeight.Light => rule.Light,
                    ArmorWeight.Heavy => rule.Heavy,
                    _ => null
                };
                if (input is not null)
                {
                    bonuses[index] += stats.GetBlended(input) - 1f;
                }
            }
        }

        private static bool HasArmorBonuses(EntityStats stats)
        {
            foreach (ArmorStat rule in ArmorStats)
            {
                if (stats.GetBlended(rule.Unarmored) != 1f || stats.GetBlended(rule.Light) != 1f
                    || stats.GetBlended(rule.Heavy) != 1f)
                {
                    return true;
                }
            }

            return false;
        }

        private void AddFoodBonus(EntityPlayer entity, EntityStats stats, Span<float> bonuses)
        {
            if (entity.GetBehavior<EntityBehaviorHunger>() is not { } food || food.MaxSaturation <= 0f)
            {
                return;
            }

            float fullness = food.Saturation / food.MaxSaturation;
            if (fullness < satedThreshold)
            {
                return;
            }

            float multiplier = fullness >= fullThreshold
                ? Math.Max(0f, stats.GetBlended("satedBonusFullMultiplier"))
                : 1f;
            foreach ((string input, string output) in SatedStats)
            {
                bonuses[Array.IndexOf(OutputStats, output)] += (stats.GetBlended(input) - 1f) * multiplier;
            }

            // Preserve existing configurations with a separate full-only speed bonus
            if (fullness >= fullThreshold)
            {
                bonuses[Array.IndexOf(OutputStats, "globalSpeedMul")] += stats.GetBlended("globalSpeedMulWhenFull") - 1f;
            }
        }
        private void AddEnvironmentBonuses(IServerPlayer player, IPlayer[] onlinePlayers, EntityStats stats, Span<float> bonuses)
        {
            float hunger = stats.GetBlended("hungerrateOutside") - 1f;
            float surface = stats.GetBlended("surfaceStabilityLoss") - 1f;
            float underground = stats.GetBlended("undergroundStabilityResistance") - 1f;
            float nearby = stats.GetBlended("stabilityLossFromPlayers") - 1f;
            float darkvision = stats.GetBlended("darkvisionStrengthUnderground") - 1f;
            if (hunger == 0f && surface == 0f && underground == 0f && nearby == 0f && darkvision == 0f)
            {
                return;
            }

            EntityPos position = player.Entity.Pos;
            BlockPos blockPosition = position.AsBlockPos;
            IBlockAccessor blocks = player.Entity.World.BlockAccessor;
            if (hunger != 0f && position.Y + 1 >= blocks.GetRainMapHeightAt(blockPosition))
            {
                bonuses[Array.IndexOf(OutputStats, "hungerrate")] += hunger;
            }

            bool belowTerrain = position.Y + 2 < blocks.GetTerrainMapheightAt(blockPosition);
            if (belowTerrain)
            {
                bonuses[Array.IndexOf(OutputStats, "environmentStabilityResistance")] += underground;
                bonuses[Array.IndexOf(OutputStats, "darkvisionStrength")] += darkvision;
            }
            else
            {
                bonuses[Array.IndexOf(OutputStats, "environmentStabilityChange")] += surface;
            }

            if (nearby != 0f)
            {
                foreach (IPlayer other in onlinePlayers)
                {
                    if (other is IServerPlayer candidate && candidate.PlayerUID != player.PlayerUID && IsActivePlayer(candidate) && IsWithinAloneRadius(position, candidate.Entity.Pos) && candidate.Entity.Stats.GetBlended("stabilityLossFromPlayers") == 1f)
                    {
                        bonuses[Array.IndexOf(OutputStats, "environmentStabilityChange")] += nearby;
                    }
                }
            }
        }
        private static bool IsActivePlayer(IServerPlayer player) => player.ConnectionState == EnumClientState.Playing && player.Entity is { Alive: true } && player.WorldData.CurrentGameMode == EnumGameMode.Survival;

        private static ArmorWeight GetArmorWeight(IPlayer player)
        {
            IInventory? equipment = player.InventoryManager.GetOwnInventory(GlobalConstants.characterInvClassName);
            if (equipment is null)
            {
                return ArmorWeight.Unknown;
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
            Unknown,
            Unarmored,
            Light,
            Heavy
        }

        private sealed class ArmorStat(string output, string input)
        {
            public string Output { get; } = output;
            public string Unarmored { get; } = input + "Unarmored";
            public string Light { get; } = input + "LightArmor";
            public string Heavy { get; } = input + "HeavyArmor";
        }
    }
}
