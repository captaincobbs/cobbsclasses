using HarmonyLib;

using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;

using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;

namespace CobbsClasses.Source.Patches
{
    [HarmonyPatch]
    internal static class AnimalBehaviorPatch
    {
        private static TagSetFast animals;
        private static TagSetFast predators;
        private static readonly ConditionalWeakTable<Entity, Dictionary<string, long>> Provocations = [];

        internal static void InitializeTags(ICoreAPI api)
        {
            animals = api.EntityTagRegistry.CreateTagSet("animal");
            predators = api.EntityTagRegistry.CreateTagSet("predator");
        }

        internal static void RecordHit(Entity animal, EntityPlayer player)
        {
            if (animal.Tags.Overlaps(animals) && player.PlayerUID is { } playerUid)
            {
                Dictionary<string, long> players = Provocations.GetOrCreateValue(animal);
                long now = animal.World.ElapsedMilliseconds;
                foreach (string uid in new List<string>(players.Keys))
                {
                    if (players[uid] <= now)
                    {
                        players.Remove(uid);
                    }
                }
                players[playerUid] = now + 120_000;
            }
        }

        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.DeclaredMethod(typeof(AiTaskBaseTargetable), "IsTargetableEntity");
            yield return AccessTools.DeclaredMethod(typeof(AiTaskBaseTargetableR), "IsTargetableEntity");
            yield return AccessTools.DeclaredMethod(typeof(AiTaskBaseTargetable), "IsTargetableEntityNoTagsNoAll");
            yield return AccessTools.DeclaredMethod(typeof(AiTaskBaseTargetable), "IsTargetableEntityNoTagsAll");
            yield return AccessTools.DeclaredMethod(typeof(AiTaskBaseTargetable), "CanSense");
            yield return AccessTools.DeclaredMethod(typeof(AiTaskBaseTargetableR), "CanSense");
        }

        private static void Postfix(object __instance, EntityAgent ___entity, Entity? __0, ref bool __result)
        {
            if (!__result || __0 is not EntityPlayer player || ___entity.World.Side != EnumAppSide.Server || !___entity.Tags.Overlaps(animals))
            {
                return;
            }

            bool flee = __instance is AiTaskFleeEntity or AiTaskFleeEntityR;
            bool attack = __instance is AiTaskSeekEntity or AiTaskSeekEntityR or AiTaskMeleeAttack or AiTaskMeleeAttackR;
            bool predator = ___entity.Tags.Overlaps(predators);
            bool domesticated = ___entity.WatchedAttributes.GetInt("generation") > 0;
            string? stat = domesticated ? (flee && !predator ? TraitAttributes.Gathering.DomesticatedAnimalNoFlee : null) : predator && attack ? TraitAttributes.Gathering.WildAnimalPassive : !predator && flee ? TraitAttributes.Gathering.WildAnimalNoFlee : null;
            if (stat is null || player.Stats.GetBlended(stat) <= 1f || (player.PlayerUID is { } uid && Provocations.TryGetValue(___entity, out Dictionary<string, long>? recent) && recent.TryGetValue(uid, out long expiry) && expiry > ___entity.World.ElapsedMilliseconds))
            {
                return;
            }

            __result = false;
        }
    }
}

