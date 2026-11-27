using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Vintagestory.API.Server;
using System.Runtime.CompilerServices;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace CobbsClasses.Source.Compatibility.Requirements;

internal static class ClassMigration
{
    private static readonly ConditionalWeakTable<ICoreAPI, Dictionary<string, string>> Mappings = new();

    internal static void Load(ICoreAPI api)
    {
        Dictionary<string, string> mappings = Mappings.GetOrCreateValue(api);
        mappings.Clear();
        IAsset? asset = api.Assets.TryGet(new AssetLocation("cobbsclasses:config/class-migrations.json"));
        if (asset is null)
        {
            return;
        }

        try
        {
            foreach (JProperty mapping in JObject.Parse(asset.ToText()).Properties())
            {
                if (mapping.Value.Type == JTokenType.String && (string?)mapping.Value is string target)
                {
                    mappings[mapping.Name] = target;
                }
            }
        }
        catch (Exception exception)
        {
            api.Logger.Warning($"CobbsClasses could not read class migrations: {exception.Message}");
        }
    }

    internal static void BeforePlayerJoin(CharacterSystem __instance, IServerPlayer __0, ICoreAPI ___api, List<CharacterClass> ___characterClasses)
    {
        if (__0?.Entity is not { } player)
        {
            return;
        }

        string? before = player.WatchedAttributes.GetString("characterClass");
        BeforeApplyTraits(player, ___api, ___characterClasses);
        if (before != player.WatchedAttributes.GetString("characterClass"))
        {
            AccessTools.Method(typeof(CharacterSystem), "applyTraitAttributes")?.Invoke(__instance, [player]);
        }
    }

    // USE THE ACTUAL LOADER, do not mess with class ids!
    internal static void BeforeApplyTraits(EntityPlayer __0, ICoreAPI ___api, List<CharacterClass> ___characterClasses)
    {
        if (___api.Side != EnumAppSide.Server || __0 is null || ___characterClasses is null)
        {
            return;
        }

        string? current = __0.WatchedAttributes.GetString("characterClass");
        string? target = ClassRosterPolicy.MigrationTarget(current,
            ___characterClasses.Select(characterClass => characterClass.Code), Mappings.GetOrCreateValue(___api));
        if (target is null)
        {
            return;
        }

        // Let vanilla applyTraitAttributes remove and rebuild the native trait source, do not call setCharacterClass with starting gear
        __0.WatchedAttributes.SetString("characterClass", target);
        __0.WatchedAttributes.MarkPathDirty("characterClass");
        ___api.Logger.Notification($"CobbsClasses migrated unavailable class {current} to {target} for player {__0.PlayerUID}.");
    }
}

