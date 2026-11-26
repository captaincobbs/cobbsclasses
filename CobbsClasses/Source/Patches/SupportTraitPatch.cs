using HarmonyLib;

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;

using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

using static CobbsClasses.Source.Patches.PatchUtils;

namespace CobbsClasses.Source.Patches
{
    public sealed class SupportTraitSystem : ModSystem
    {
        private const string SpeedSource = "cobbsclasses-protector";
        private static readonly ConditionalWeakTable<EntityPlayer, Effects> Active = [];
        private ICoreServerAPI? server;
        private static SystemTemporalStability? serverSideStorm;
        private long listener;

        public override bool ShouldLoad(EnumAppSide side) => side == EnumAppSide.Server;

        public override void StartServerSide(ICoreServerAPI api)
        {
            server = api;
            serverSideStorm = api.ModLoader.GetModSystem<SystemTemporalStability>();
            api.Event.PlayerRespawn += Respawn;
            listener = api.Event.RegisterGameTickListener(Tick, 250);
        }

        public override void Dispose()
        {
            if (server is { } api)
            {
                serverSideStorm = null;
                api.Event.PlayerRespawn -= Respawn;
                api.Event.UnregisterGameTickListener(listener);
                foreach (IPlayer player in api.World.AllOnlinePlayers)
                {
                    if (player.Entity is { } entity)
                    {
                        Clear(entity);
                    }
                }
            }
        }

        internal static void Register(Harmony harmony)
        {
            harmony.CreateClassProcessor(typeof(Revival)).Patch();
            harmony.CreateClassProcessor(typeof(Incoming)).Patch();
            harmony.CreateClassProcessor(typeof(PlayerHurt)).Patch();
            harmony.CreateClassProcessor(typeof(AnimalHurt)).Patch();
            harmony.CreateClassProcessor(typeof(Bandage)).Patch();
        }

        private void Respawn(IServerPlayer player)
        {
            StartReviveProtection(player.Entity);
        }

        private static void StartReviveProtection(EntityPlayer entity)
        {
            Clear(entity);
            double duration = Duration(entity, "damageReductionReviveDuration");
            if (duration > 0d && Bonus(entity, "damageReductionPostRevive") > 0f)
            {
                Active.GetOrCreateValue(entity).ReviveUntil = entity.World.ElapsedMilliseconds + duration;
            }
        }

        private static void Clear(EntityPlayer player)
        {
            if (Active.TryGetValue(player, out Effects? effects) && effects.SpeedActive)
            {
                player.Stats.Remove("walkspeed", SpeedSource);
            }
            Active.Remove(player);
        }

        private void Tick(float _)
        {
            if (server is null)
            {
                return;
            }

            long now = server.World.ElapsedMilliseconds;
            foreach (IPlayer player in server.World.AllOnlinePlayers)
            {
                if (player.Entity is not { } entity || !Active.TryGetValue(entity, out Effects? effects))
                {
                    continue;
                }

                if (!entity.Alive || player.WorldData.CurrentGameMode != EnumGameMode.Survival)
                {
                    Clear(entity);
                    continue;
                }

                if (effects.SpeedActive && now >= effects.SpeedUntil)
                {
                    entity.Stats.Remove("walkspeed", SpeedSource);
                    effects.SpeedActive = false;
                }

                if (now >= effects.RetaliateUntil)
                {
                    effects.Attacker = null;
                }

                if (now <= effects.HealUntil && now >= effects.NextHeal)
                {
                    effects.NextHeal = now + 1000;
                    entity.ReceiveDamage(new DamageSource { Source = EnumDamageSource.Internal, Type = EnumDamageType.Heal }, effects.HealPerSecond);
                }
            }
        }

        private static float Bonus(EntityPlayer player, string stat) => player.Stats.GetBlended(stat) - 1f;
        private static double Duration(EntityPlayer player, string stat) => 1000d * Math.Clamp(Bonus(player, stat), 0f, 3600f);

        private static bool Nearby(EntityPlayer center, IPlayer candidate, double radius)
        {
            if (candidate.Entity is not { Alive: true } player || candidate.WorldData.CurrentGameMode != EnumGameMode.Survival || center.Pos.Dimension != player.Pos.Dimension)
            {
                return false;
            }

            double x = center.Pos.X - player.Pos.X;
            double y = center.Pos.Y - player.Pos.Y;
            double z = center.Pos.Z - player.Pos.Z;
            return x * x + y * y + z * z <= radius * radius;
        }

        [HarmonyPatch(typeof(EntityPlayer), nameof(EntityPlayer.Revive))]
        private static class Revival
        {
            private static void Postfix(EntityPlayer __instance)
            {
                if (__instance.World.Side == EnumAppSide.Server && __instance.Alive)
                {
                    StartReviveProtection(__instance);
                }
            }
        }

        [HarmonyPatch(typeof(EntityBehaviorHealth), nameof(EntityBehaviorHealth.OnEntityReceiveDamage))]
        private static class Incoming
        {
            private static void Prefix(EntityBehaviorHealth __instance, DamageSource? __0, ref float __1)
            {
                Entity victim = __instance.entity;
                if (__1 <= 0f || __0 is null || __0.Type == EnumDamageType.Heal || victim.World.Side != EnumAppSide.Server)
                {
                    return;
                }

                long now = victim.World.ElapsedMilliseconds;
                if (victim is EntityPlayer stormPlayer && Bonus(stormPlayer, "damageReductionTemporalStorm") is > 0f and var stormReduction
                    && serverSideStorm?.StormData?.nowStormActive == true)
                {
                    __1 *= 1f - Math.Clamp(stormReduction, 0f, 1f);
                }

                if (victim is EntityPlayer protectedPlayer && Active.TryGetValue(protectedPlayer, out Effects? protection) && now < protection.ReviveUntil)
                {
                    __1 *= 1f - Math.Clamp(Bonus(protectedPlayer, "damageReductionPostRevive"), 0f, 1f);
                }

                if (__0.GetCauseEntity() is EntityPlayer defender && Active.TryGetValue(defender, out Effects? effects) && now < effects.RetaliateUntil && ReferenceEquals(victim, effects.Attacker))
                {
                    __1 *= Math.Max(0f, 1f + Bonus(defender, "damageToHostiles"));
                }
            }
        }

        [HarmonyPatch(typeof(EntityPlayer), nameof(EntityPlayer.OnHurt))]
        private static class PlayerHurt
        {
            private static void Postfix(EntityPlayer __instance, DamageSource? __0, float __1)
            {
                if (__1 <= 0f || __0 is null || __0.Type == EnumDamageType.Heal || __instance.World.Side != EnumAppSide.Server || __0.GetCauseEntity() is not EntityAgent attacker || attacker is EntityPlayer || attacker == __instance)
                {
                    return;
                }

                long now = __instance.World.ElapsedMilliseconds;
                foreach (IPlayer ally in __instance.World.AllOnlinePlayers)
                {
                    if (ally.Entity is not { } defender || defender == __instance || !Nearby(__instance, ally, 16d))
                    {
                        continue;
                    }

                    float speed = Bonus(defender, "movespeedFromProtector");
                    double sprintDuration = Duration(defender, "movespeedFromProtectionDuration");
                    double retaliation = Bonus(defender, "damageToHostiles") > 0f ? Duration(defender, "damageToHostilesDuration") : 0d;
                    if ((speed <= 0f || sprintDuration <= 0d) && retaliation <= 0d)
                    {
                        continue;
                    }

                    Effects effects = Active.GetOrCreateValue(defender);
                    if (speed > 0f && sprintDuration > 0d && now >= effects.NextSprint)
                    {
                        effects.SpeedUntil = now + sprintDuration;
                        effects.NextSprint = effects.SpeedUntil + 5000d;
                        effects.SpeedActive = true;
                        defender.Stats.Set("walkspeed", SpeedSource, speed, persistent: false);
                    }

                    if (retaliation > 0d && now >= effects.RetaliateUntil)
                    {
                        effects.Attacker = attacker;
                        effects.RetaliateUntil = now + retaliation;
                    }
                }
            }
        }

        [HarmonyPatch(typeof(Entity), nameof(Entity.OnHurt))]
        private static class AnimalHurt
        {
            private static void Postfix(Entity __instance, DamageSource? __0, float __1)
            {
                if (__1 > 0f && __0 is not null && __0.Type != EnumDamageType.Heal && __instance.World.Side == EnumAppSide.Server && __0.GetCauseEntity() is EntityPlayer player)
                {
                    AnimalBehaviorPatch.RecordHit(__instance, player);
                }
            }
        }

        [HarmonyPatch(typeof(CollectibleBehaviorHealingItem), nameof(CollectibleBehaviorHealingItem.OnHeldInteractStop))]
        private static class Bandage
        {
            private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                MethodInfo receive = AccessTools.Method(typeof(Entity), nameof(Entity.ReceiveDamage), [typeof(DamageSource), typeof(float)]);
                MethodInfo heal = AccessTools.Method(typeof(Bandage), nameof(HealWithSupport));
                return ReplaceCall(instructions, receive, heal, OpCodes.Ldarg_3, "targeted support healing");
            }

            private static bool HealWithSupport(Entity recipient, DamageSource source, float amount, EntityAgent actor)
            {
                bool applied = recipient.ReceiveDamage(source, amount);
                if (applied && source.Type == EnumDamageType.Heal && recipient is EntityPlayer { Alive: true } target && actor is EntityPlayer healer && healer.World.Side == EnumAppSide.Server && target != healer)
                {
                    GrantAura(healer, target);
                }
                return applied;
            }

            private static void GrantAura(EntityPlayer healer, EntityPlayer target)
            {
                float rate = Bonus(healer, "targetedRegenPerSecond");
                double duration = Duration(healer, "targetedRegenDuration");
                if (rate <= 0f || duration <= 0d)
                {
                    return;
                }

                long now = healer.World.ElapsedMilliseconds;
                foreach (IPlayer recipient in healer.World.AllOnlinePlayers)
                {
                    if (Nearby(target, recipient, 4d))
                    {
                        Effects effects = Active.GetOrCreateValue(recipient.Entity);
                        if (now > effects.HealUntil || rate >= effects.HealPerSecond)
                        {
                            effects.HealPerSecond = rate;
                            effects.HealUntil = now + duration;
                            effects.NextHeal = Math.Max(effects.NextHeal, now + 1000);
                        }
                    }
                }
            }
        }

        private sealed class Effects
        {
            public double ReviveUntil;
            public double SpeedUntil;
            public double NextSprint;
            public bool SpeedActive;
            public Entity? Attacker;
            public double RetaliateUntil;
            public float HealPerSecond;
            public double HealUntil;
            public long NextHeal;
        }
    }
}
