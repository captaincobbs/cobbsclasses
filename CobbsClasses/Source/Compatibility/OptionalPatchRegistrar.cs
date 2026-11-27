using System;
using System.Reflection;

using CobbsClasses.Source.API;

using HarmonyLib;

using Vintagestory.API.Common;

namespace CobbsClasses.Source.Compatibility
{
    public sealed class OptionalPatchRegistrar(ICoreAPI api, Harmony harmony, string modId)
    {
        public void Patch(string typeName, string methodName, HarmonyMethod? prefix = null, HarmonyMethod? postfix = null, HarmonyMethod? transpiler = null, Type[]? arguments = null, Predicate<Type>? compatible = null, HarmonyMethod? finalizer = null)
        {
            string name = typeName + "." + methodName;
            void Record(PatchStatus status, string? details = null) => CobbsClassesAPI.For(api)?.Record(new PatchDiagnostic(modId, name, status, details));
            try
            {
                Type? type = AccessTools.TypeByName(typeName);
                MethodInfo? target = type is null ? null : AccessTools.DeclaredMethod(type, methodName, arguments);
                if (type is null || target is null || (compatible is not null && !compatible(type)))
                {
                    api.Logger.Warning($"CobbsClasses integration for {modId} could not find a compatible {typeName}.{methodName}.");
                    Record(PatchStatus.MissingTarget);
                    return;
                }

                if (Harmony.GetPatchInfo(target)?.Owners.Contains(harmony.Id) != true)
                {
                    harmony.Patch(target, prefix: prefix, postfix: postfix, transpiler: transpiler, finalizer: finalizer);
                    Record(PatchStatus.Applied);
                }
                else
                {
                    Record(PatchStatus.AlreadyApplied);
                }
            }
            catch (Exception exception)
            {
                Record(PatchStatus.Failed, exception.Message);
                api.Logger.Warning($"CobbsClasses integration for {modId} failed at {typeName}.{methodName}: {exception.Message}");
            }
        }
    }
}