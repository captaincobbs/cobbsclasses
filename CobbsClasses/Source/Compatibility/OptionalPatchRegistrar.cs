using HarmonyLib;

using System;
using System.Reflection;

using Vintagestory.API.Common;

namespace CobbsClasses.Source.Compatibility
{
    internal sealed class OptionalPatchRegistrar(ICoreAPI api, Harmony harmony, string modId)
    {
        internal void Patch(string typeName, string methodName, HarmonyMethod? prefix = null, HarmonyMethod? postfix = null, HarmonyMethod? transpiler = null, Type[]? arguments = null, Predicate<Type>? compatible = null)
        {
            try
            {
                Type? type = AccessTools.TypeByName(typeName);
                MethodInfo? target = type is null ? null : AccessTools.DeclaredMethod(type, methodName, arguments);
                if (type is null || target is null || (compatible is not null && !compatible(type)))
                {
                    api.Logger.Warning($"CobbsClasses integration for {modId} could not find a compatible {typeName}.{methodName}.");
                    return;
                }

                if (Harmony.GetPatchInfo(target)?.Owners.Contains(harmony.Id) != true)
                {
                    harmony.Patch(target, prefix: prefix, postfix: postfix, transpiler: transpiler);
                }
            }
            catch (Exception exception)
            {
                api.Logger.Warning($"CobbsClasses integration for {modId} failed at {typeName}.{methodName}: {exception.Message}");
            }
        }
    }
}