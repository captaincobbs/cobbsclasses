using System;

using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;

namespace CobbsClasses.Source.Configuration
{
    internal sealed record SupportDurationSettings(float ReviveProtectionSeconds = 120f,
        float ProtectorRetaliationSeconds = 120f, float ProtectorSprintSeconds = 5f,
        float TargetedRegenSeconds = 10f)
    {
        internal static SupportDurationSettings Defaults { get; } = new();

        internal static SupportDurationSettings Load(ICoreServerAPI api)
        {
            JsonObject? configLib = ConditionalTraitSystem.ReadSettingsAsset(api, "cobbsclasses:config/conditional-traits.json");
            JsonObject? imm = ConditionalTraitSystem.ReadSettingsAsset(api, "cobbsclasses:config/imm-conditional-traits.json");
            return FromSettings(configLib, imm);
        }

        internal static SupportDurationSettings FromSettings(JsonObject? configLib, JsonObject? imm)
        {
            float Read(string name, float fallback)
                => Math.Clamp(ConditionalTraitSystem.ReadSetting(configLib, imm, name, fallback), 0f, 3600f);

            return new(
                Read(nameof(ReviveProtectionSeconds), Defaults.ReviveProtectionSeconds),
                Read(nameof(ProtectorRetaliationSeconds), Defaults.ProtectorRetaliationSeconds),
                Read(nameof(ProtectorSprintSeconds), Defaults.ProtectorSprintSeconds),
                Read(nameof(TargetedRegenSeconds), Defaults.TargetedRegenSeconds));
        }

        internal static double Milliseconds(float seconds)
        {
            return float.IsFinite(seconds) ? 1000d * Math.Clamp(seconds, 0f, 3600f) : 0d;
        }
    }
}