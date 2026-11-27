using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;

using CobbsClasses.Source.API;
using CobbsClasses.Source.Core;

using HarmonyLib;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace CobbsClasses.Source.Compatibility.Requirements;

public sealed class ModRequirementSystem : ModSystem
{
    private const string HarmonyOwner = "cobbsclasses.modrequirements";
    private const string WorldKey = "cobbsclasses:resolvedClassDefinitions";
    private const string TraitPath = "cobbsclasses:config/traits.json";
    private const string ClassPath = "cobbsclasses:config/characterclasses.json";
    private static readonly Lock PatchLock = new();
    private static int users;
    private bool registered;
    private sealed class OriginalAssets
    {
        public string Traits = "";
        public string Classes = "";
    }

    private static readonly ConditionalWeakTable<ICoreAPI, OriginalAssets> Originals = [];

    public override void Start(ICoreAPI api)
    {
        lock (PatchLock)
        {
            if (registered)
            {
                return;
            }

            if (users == 0)
            {
                Harmony harmony = new(HarmonyOwner);
                harmony.Patch(AccessTools.Method(typeof(CharacterSystem), "loadCharacterClasses"), prefix: new HarmonyMethod(typeof(ModRequirementSystem), nameof(BeforeClassLoad)));
                harmony.Patch(AccessTools.Method(typeof(CharacterSystem), "applyTraitAttributes"), prefix: new HarmonyMethod(typeof(ClassMigration), nameof(ClassMigration.BeforeApplyTraits)));
                MethodInfo? join = AccessTools.Method(typeof(CharacterSystem), "Event_PlayerJoinServer");
                if (join is not null)
                {
                    try
                    {
                        harmony.Patch(join, prefix: new HarmonyMethod(typeof(ClassMigration), nameof(ClassMigration.BeforePlayerJoin)));
                    }
                    catch (Exception exception)
                    {
                        api.Logger.Warning($"CobbsClasses could not install join-time class migration: {exception.Message}");
                    }
                }
            }

            if (api.Side == EnumAppSide.Client)
            {
                Harmony harmony = new(HarmonyOwner);
                foreach (MethodInfo target in new[] {
                    AccessTools.Method(typeof(CharacterSystem), "getClassTraitText"),
                    AccessTools.Method(typeof(GuiDialogCreateCharacter), "changeClass")
                })
                {
                    try
                    {
                        if (target is not null && Harmony.GetPatchInfo(target)?.Owners.Contains(HarmonyOwner) != true)
                        {
                            harmony.Patch(target, transpiler: new HarmonyMethod(typeof(TraitDescriptionOverrides), nameof(TraitDescriptionOverrides.Transpiler)));
                        }
                    }
                    catch (Exception exception)
                    {
                        api.Logger.Warning($"CobbsClasses tooltip override could not be applied: {exception.Message}");
                    }
                }
            }

            users++;
            registered = true;
        }
    }

    private static void BeforeClassLoad(ICoreAPI ___api)
    {
        try
        {
            IAsset traitAsset = ___api.Assets.Get(new AssetLocation(TraitPath));
            IAsset classAsset = ___api.Assets.Get(new AssetLocation(ClassPath));
            if (___api.Side == EnumAppSide.Client)
            {
                string? definitions = ___api.World.Config.GetString(WorldKey);
                if (string.IsNullOrEmpty(definitions))
                {
                    ___api.Logger.Warning("CobbsClasses: server did not provide resolved class definitions. Using local assets.");
                    TraitDescriptionOverrides.Load(JArray.Parse(traitAsset.ToText()));
                    return;
                }

                JObject resolved = ResolvedDefinitionEncoding.Decode(definitions);
                JArray traits = (JArray)resolved["traits"]!;
                JArray classes = (JArray)resolved["classes"]!;
                ApplyVanillaPolicy(___api, (bool?)resolved["disableVanillaClasses"] ?? true);
                PublishClasses(___api, classes);
                TraitDescriptionOverrides.Load(traits);
                WriteAssets(traitAsset, classAsset, traits, classes);
                return;
            }

            OriginalAssets original = Originals.GetValue(___api, _ => new OriginalAssets
            {
                Traits = traitAsset.ToText(),
                Classes = classAsset.ToText()
            });

            JArray serverTraits = JArray.Parse(original.Traits);
            JArray serverClasses = JArray.Parse(original.Classes);
            JObject configLib = ReadOptions(___api, "class-options.json");
            JObject imm = ReadOptions(___api, "imm-class-options.json");
            bool Option(string key) => (bool?)configLib[key] == true || (bool?)imm[key] == true;
            bool IsEnabled(string modId) => ___api.ModLoader.IsModEnabled(modId);

            DefinitionValidation.Validate(serverTraits, serverClasses, CobbsClassesAPI.For(___api)?.Attributes ?? new TraitRegistry(), message => ___api.Logger.Warning("CobbsClasses: " + message), ExternalTraitCodes(___api));
            HashSet<string> unavailable = TraitCompatibilityResolver.Resolve(serverTraits, IsEnabled);
            ClassCompatibilityResolver.Resolve(serverClasses, unavailable, IsEnabled, code => Option(code + "-disable-class"), Option("ignore-class-mod-requirements"));

            bool frameworkOnly = Option("framework-only-mode");
            bool disableVanilla = !frameworkOnly && ((bool?)configLib["disable-vanilla-classes"] ?? true) && ((bool?)imm["disable-vanilla-classes"] ?? true);
            ApplyVanillaPolicy(___api, disableVanilla);
            string[] externalClasses = [.. ExternalClassCodes(___api).Distinct(StringComparer.Ordinal)];
            ClassRosterPolicy.ApplyLocal(serverClasses, frameworkOnly, externalClasses);
            if (externalClasses.Length == 0)
            {
                DefinitionValidation.EnsureAvailable(serverClasses, message => ___api.Logger.Warning("CobbsClasses: " + message));
            }
            ClassMigration.Load(___api);
            PublishClasses(___api, serverClasses);
            JObject payload = new()
            {
                ["traits"] = serverTraits,
                ["classes"] = serverClasses,
                ["disableVanillaClasses"] = disableVanilla
            };
            ___api.World.Config.SetString(WorldKey, ResolvedDefinitionEncoding.Encode(payload));
            WriteAssets(traitAsset, classAsset, serverTraits, serverClasses);
        }
        catch (Exception exception)
        {
            if (___api.Side == EnumAppSide.Server)
            {
                ___api.World.Config.RemoveAttribute(WorldKey);
            }

            ___api.Logger.Error($"CobbsClasses mod requirement resolution failed: {exception}");
        }
    }

    private static void ApplyVanillaPolicy(ICoreAPI api, bool disableVanilla)
    {
        IAsset? asset = api.Assets.TryGet(new AssetLocation("game:config/characterclasses.json"));
        if (asset is null || !disableVanilla)
        {
            return;
        }

        JArray vanilla = JArray.Parse(asset.ToText());
        ClassRosterPolicy.ApplyVanilla(vanilla, true);
        asset.Data = Encoding.UTF8.GetBytes(vanilla.ToString(Formatting.None, []));
    }

    private static IEnumerable<string> ExternalClassCodes(ICoreAPI api)
    {
        foreach (IAsset asset in api.Assets.GetMany("config/characterclasses", null))
        {
            if (asset.Location.Domain == "cobbsclasses")
            {
                continue;
            }

            JArray? classes = null;
            try
            {
                classes = JArray.Parse(asset.ToText());
            }
            catch (JsonException)
            {
                api.Logger.Warning($"CobbsClasses could not inspect external classes in {asset.Location}.");
            }

            if (classes is not null)
            {
                foreach (string code in ClassRosterPolicy.EnabledCodes(classes))
                {
                    yield return code;
                }
            }
        }
    }

    private static IEnumerable<string> ExternalTraitCodes(ICoreAPI api)
    {
        foreach (IAsset asset in api.Assets.GetMany("config/traits", null))
        {
            if (asset.Location.Domain == "cobbsclasses")
            {
                continue;
            }

            JArray? entries = null;
            try
            {
                entries = JArray.Parse(asset.ToText());
            }
            catch (JsonException)
            {
                api.Logger.Warning($"CobbsClasses could not inspect external traits in {asset.Location}.");
            }

            if (entries is not null)
            {
                foreach (JObject entry in entries.OfType<JObject>())
                {
                    if ((string?)entry["code"] is string code)
                    {
                        yield return code;
                    }
                }
            }
        }
    }

    private static void PublishClasses(ICoreAPI api, JArray classes)
    {
        CobbsClassesAPI.For(api)?.SetClasses(classes.OfType<JObject>().Where(entry => (bool?)entry["enabled"] != false)
            .Select(entry => (string)entry["code"]!));
    }

    private static JObject ReadOptions(ICoreAPI api, string file)
    {
        IAsset? asset = api.Assets.TryGet(new AssetLocation("cobbsclasses:config/" + file));
        return asset is null ? [] : JObject.Parse(asset.ToText());
    }

    private static void WriteAssets(IAsset traitAsset, IAsset classAsset, JArray traits, JArray classes)
    {
        traitAsset.Data = Encoding.UTF8.GetBytes(traits.ToString(Formatting.None, []));
        classAsset.Data = Encoding.UTF8.GetBytes(classes.ToString(Formatting.None, []));
    }

    public override void Dispose()
    {
        lock (PatchLock)
        {
            if (registered)
            {
                registered = false;
                if (--users == 0)
                {
                    new Harmony(HarmonyOwner).UnpatchAll(HarmonyOwner);
                    TraitDescriptionOverrides.Clear();
                }
            }
        }

        base.Dispose();
    }
}