using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

using CobbsClasses.Source.API;
using CobbsClasses.Source.Conditions;

using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;

namespace CobbsClasses.Source
{
    public sealed class ConditionalTraitSystem : ModSystem
    {
        internal const string ModifierSource = "cobbsclasses-conditional";
        private const int DefaultRefreshInterval = 500;
        private const float DefaultAloneRadius = 32f;
        private const float DefaultSatedThreshold = 0.5f;
        private const float DefaultFullThreshold = 0.9f;

        private readonly ConditionalWeakTable<EntityStats, Dictionary<string, float>> appliedBonuses = [];
        private readonly ConditionalModifiers modifiers = new();
        private IConditionalTrait[] conditions = [];
        private ICoreServerAPI? serverAPI;
        private long refreshListener;
        private RestCondition? restCondition;

        public override bool ShouldLoad(EnumAppSide side) => side == EnumAppSide.Server;

        public override void StartServerSide(ICoreServerAPI api)
        {
            // Read settings AFTER they've been patched
            api.Event.ServerRunPhase(EnumServerRunPhase.GameReady, () => Initialize(api));
        }

        private void Initialize(ICoreServerAPI api)
        {
            serverAPI = api;
            JsonObject? configLib = ReadSettingsAsset(api, "cobbsclasses:config/conditional-traits.json");
            JsonObject? imm = ReadSettingsAsset(api, "cobbsclasses:config/imm-conditional-traits.json");

            int refreshInterval = (int)Math.Clamp(ReadSetting(configLib, imm, "RefreshInterval", DefaultRefreshInterval), 50f, 60_000f);
            float satedThreshold = Math.Clamp(ReadSetting(configLib, imm, "SatedThreshold", DefaultSatedThreshold), 0f, 1f);
            float fullThreshold = Math.Clamp(ReadSetting(configLib, imm, "FullThreshold", DefaultFullThreshold), satedThreshold, 1f);
            double aloneRadius = Math.Clamp(ReadSetting(configLib, imm, "AloneRadius", DefaultAloneRadius), 0f, 1024f);

            restCondition = new RestCondition(
                Math.Clamp(ReadSetting(configLib, imm, "UnrestedMaximumPenalty", 0.2f), 0f, 0.95f),
                Math.Clamp(ReadSetting(configLib, imm, "UnrestedPenaltyPerDay", 0.04f), 0f, 1f),
                Math.Clamp(ReadSetting(configLib, imm, "MinimumRestHours", 1f), 0.1f, 24f));
            api.Event.PlayerDisconnect += ForgetRestSession;

            conditions =
            [
                new ProximityCondition(aloneRadius),
                new ArmorCondition(),
                new SatietyCondition(satedThreshold, fullThreshold),
                new EnvironmentCondition(),
                new MovementCondition(),
                new HealthCondition(),
                restCondition
            ];

            IEnumerable<KeyValuePair<string, Func<IConditionalTrait>>>? extensions = CobbsClassesAPI.For(api)?.Conditions();
            if (extensions is not null)
            {
                List<IConditionalTrait> combined = [.. conditions];
                foreach (KeyValuePair<string, Func<IConditionalTrait>> extension in extensions)
                {
                    try
                    {
                        if (extension.Value() is IConditionalTrait condition)
                        {
                            combined.Add(condition);
                        }
                    }
                    catch (Exception exception)
                    {
                        api.Logger.Warning($"CobbsClasses condition {extension.Key} failed to initialize: {exception.Message}");
                    }
                }

                conditions = [.. combined];
            }

            refreshListener = api.Event.RegisterGameTickListener(RefreshPlayers, refreshInterval);
        }

        private void ForgetRestSession(IServerPlayer player)
        {
            if (player.Entity is { } entity)
            {
                restCondition?.Forget(entity);
            }
        }

        internal static JsonObject? ReadSettingsAsset(ICoreServerAPI api, string location)
        {
            IAsset? asset = api.Assets.TryGet(new AssetLocation(location));
            return asset is null ? null : JsonObject.FromJson(asset.ToText());
        }

        internal static float ReadSetting(JsonObject? configLib, JsonObject? imm, string name, float fallback)
        {
            float immValue = imm?[name].AsFloat(fallback) ?? fallback;
            if (float.IsFinite(immValue) && immValue != fallback)
            {
                return immValue;
            }

            float configLibValue = configLib?[name].AsFloat(fallback) ?? fallback;
            return float.IsFinite(configLibValue) ? configLibValue : fallback;
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
                    RefreshPlayer(new ConditionContext(player, entity, onlinePlayers));
                }
            }
        }

        private void RefreshPlayer(in ConditionContext context)
        {
            modifiers.Clear();
            if (ConditionContext.IsActive(context.Player))
            {
                foreach (IConditionalTrait condition in conditions)
                {
                    condition.Evaluate(context, modifiers);
                }
            }

            else
            {
                restCondition?.Forget(context.Entity);
            }

            ApplyModifiers(context.Stats);
        }

        private void ApplyModifiers(EntityStats stats)
        {
            if (!appliedBonuses.TryGetValue(stats, out Dictionary<string, float>? previous))
            {
                if (modifiers.Values.Count == 0)
                {
                    return;
                }

                previous = appliedBonuses.GetOrCreateValue(stats);
            }

            foreach (string stat in previous.Keys)
            {
                if (!modifiers.Values.ContainsKey(stat))
                {
                    stats.Remove(stat, ModifierSource);
                }
            }

            foreach ((string? stat, float bonus) in modifiers.Values)
            {
                if (!previous.TryGetValue(stat, out float oldBonus) || oldBonus != bonus)
                {
                    stats.Set(stat, ModifierSource, bonus, persistent: false);
                }
            }

            previous.Clear();
            foreach ((string? stat, float bonus) in modifiers.Values)
            {
                previous.Add(stat, bonus);
            }

            if (previous.Count == 0)
            {
                appliedBonuses.Remove(stats);
            }
        }

        public override void Dispose()
        {
            if (serverAPI is not null)
            {
                serverAPI.Event.PlayerDisconnect -= ForgetRestSession;
                serverAPI.Event.UnregisterGameTickListener(refreshListener);
            }

            restCondition = null;
            serverAPI = null;

            foreach ((EntityStats? stats, Dictionary<string, float>? previous) in appliedBonuses)
            {
                foreach (string stat in previous.Keys)
                {
                    stats.Remove(stat, ModifierSource);
                }
            }

            appliedBonuses.Clear();
            modifiers.Clear();
            conditions = [];
            base.Dispose();
        }
    }
}
