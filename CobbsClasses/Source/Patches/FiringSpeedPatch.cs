using System;
using System.Runtime.CompilerServices;

using HarmonyLib;

using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace CobbsClasses.Source.Patches
{
    internal static class FiringSpeedPatch
    {
        private const string SpeedKey = "cobbsclasses:firingSpeed";
        private const string IgnitionKey = "cobbsclasses:firingIgnition";
        private static readonly ConditionalWeakTable<BlockEntity, FiringCycle> Cycles = [];
        private static readonly AccessTools.FieldRef<BlockEntityBloomery, double> BloomeryDeadline = AccessTools.FieldRefAccess<BlockEntityBloomery, double>("burningUntilTotalDays");
        private static readonly AccessTools.FieldRef<BlockEntityBloomery, double> BloomeryStart = AccessTools.FieldRefAccess<BlockEntityBloomery, double>("burningStartTotalDays");
        private static readonly AccessTools.FieldRef<BlockEntityPitKiln, double> PitDeadline = AccessTools.FieldRefAccess<BlockEntityPitKiln, double>("BurningUntilTotalHours");
        private static readonly AccessTools.FieldRef<BlockEntityBeeHiveKiln, BlockPos[]> FuelPositions = AccessTools.FieldRefAccess<BlockEntityBeeHiveKiln, BlockPos[]>("particlePositions");
        private static readonly AccessTools.FieldRef<BlockEntityBeeHiveKiln, bool> ReceivesHeat = AccessTools.FieldRefAccess<BlockEntityBeeHiveKiln, bool>("receivesHeat");

        internal static void Register(Harmony harmony)
        {
            foreach (Type patch in new[] { typeof(BloomeryIgnition), typeof(BloomeryRecovery), typeof(PitIgnition), typeof(PitInitialize), typeof(CoalIgnition), typeof(GroundIgnition), typeof(KilnProgress), typeof(KilnTick) })
            {
                harmony.CreateClassProcessor(patch).Patch();
            }

            foreach (Type type in new[] { typeof(BlockEntityCoalPile), typeof(BlockEntityGroundStorage), typeof(BlockEntityBeeHiveKiln) })
            {
                harmony.Patch(AccessTools.DeclaredMethod(type, nameof(BlockEntity.ToTreeAttributes)), postfix: new HarmonyMethod(typeof(FiringSpeedPatch), nameof(Save)));
                harmony.Patch(AccessTools.DeclaredMethod(type, nameof(BlockEntity.FromTreeAttributes)), postfix: new HarmonyMethod(typeof(FiringSpeedPatch), nameof(Load)));
            }
        }

        private static float Speed(EntityAgent? entity, string stat)
        {
            float factor = entity is EntityPlayer player ? player.Stats.GetBlended(stat) : 1f;
            return float.IsFinite(factor) ? Math.Clamp(factor, 0.05f, 100f) : 1f;
        }

        private static readonly AccessTools.FieldRef<BlockEntityCoalPile, double> CoalStart = AccessTools.FieldRefAccess<BlockEntityCoalPile, double>("burnStartTotalHours");
        private static readonly AccessTools.FieldRef<BlockEntityGroundStorage, double> GroundStart = AccessTools.FieldRefAccess<BlockEntityGroundStorage, double>("burnStartTotalHours");

        private static double FuelStart(BlockEntity entity) => entity switch
        {
            BlockEntityCoalPile coal => CoalStart(coal),
            BlockEntityGroundStorage storage => GroundStart(storage),
            _ => double.NegativeInfinity
        };

        private static bool IsBurning(BlockEntity? entity) => entity switch
        {
            BlockEntityCoalPile coal => coal.IsBurning,
            BlockEntityGroundStorage storage => storage.IsBurning,
            _ => false
        };

        private static BlockEntity? At(EntityAgent entity, BlockPos position) => entity.World.BlockAccessor.GetBlockEntity(position);

        [HarmonyPatch(typeof(BlockBloomery), nameof(BlockBloomery.OnTryIgniteBlockOver))]
        private static class BloomeryIgnition
        {
            private static void Prefix(EntityAgent __0, BlockPos __1, out BlockEntityBloomery? __state)
            {
                __state = __0.World.Side == EnumAppSide.Server && At(__0, __1) is BlockEntityBloomery { IsBurning: false } bloomery ? bloomery : null;
            }

            private static void Postfix(EntityAgent __0, BlockEntityBloomery? __state)
            {
                if (__state is not { IsBurning: true })
                {
                    return;
                }

                float speed = Speed(__0, "bloomerySpeedMul");
                if (speed != 1f)
                {
                    double start = BloomeryStart(__state);
                    BloomeryDeadline(__state) = start + (BloomeryDeadline(__state) - start) / speed;
                    __state.MarkDirty();
                }
            }
        }

        [HarmonyPatch(typeof(BlockEntityBloomery), nameof(BlockEntityBloomery.OnBlockBroken))]
        private static class BloomeryRecovery
        {
            private static void Prefix(BlockEntityBloomery __instance, IPlayer? __0)
            {
                if (__instance.Api?.Side != EnumAppSide.Server || !__instance.IsBurning || __0?.Entity is not { } player)
                {
                    return;
                }

                float fraction = Math.Clamp(player.Stats.GetBlended("bloomeryRecoveryAmount") - 1f, 0f, 1f);
                ItemSlot ore = AccessTools.MethodDelegate<System.Func<BlockEntityBloomery, ItemSlot>>(AccessTools.PropertyGetter(typeof(BlockEntityBloomery), "OreSlot"))(__instance);
                if (fraction <= 0f || ore.Empty)
                {
                    return;
                }

                int count = GameMath.RoundRandom(__instance.Api.World.Rand, ore.StackSize * fraction);
                if (count > 0 && ore.TakeOut(count) is { } recovered)
                {
                    __instance.Api.World.SpawnItemEntity(recovered, __instance.Pos.ToVec3d().Add(0.5, 0.5, 0.5));
                }
            }
        }

        [HarmonyPatch(typeof(BlockEntityPitKiln), nameof(BlockEntityPitKiln.TryIgnite))]
        private static class PitIgnition
        {
            private static void Prefix(BlockEntityPitKiln __instance, IPlayer? __0, out bool __state)
            {
                __state = __instance.Api?.Side == EnumAppSide.Server && __0?.Entity is not null && !__instance.Lit;
            }

            private static void Postfix(BlockEntityPitKiln __instance, IPlayer? __0, bool __state)
            {
                if (!__state || !__instance.Lit)
                {
                    return;
                }

                float speed = Speed(__0?.Entity, "potteryCookSpeed");
                if (speed != 1f)
                {
                    double now = __instance.Api.World.Calendar.TotalHours;
                    PitDeadline(__instance) = now + (PitDeadline(__instance) - now) / speed;
                    __instance.MarkDirty();
                }
            }
        }

        [HarmonyPatch(typeof(BlockEntityPitKiln), nameof(BlockEntityPitKiln.Initialize))]
        private static class PitInitialize
        {
            private static void Prefix(BlockEntityPitKiln __instance, out double __state)
            {
                __state = __instance.Lit ? PitDeadline(__instance) : 0d;
            }

            private static void Postfix(BlockEntityPitKiln __instance, ICoreAPI __0, double __state)
            {
                if (__0.Side == EnumAppSide.Server && __instance.Lit && __state > PitDeadline(__instance))
                {
                    // Preserve the saved process
                    PitDeadline(__instance) = __state;
                }
            }
        }

        private static void BeforeFuelIgnition(EntityAgent entity, BlockPos position, out BlockEntity? state)
        {
            state = entity.World.Side == EnumAppSide.Server ? At(entity, position) : null;
            if (IsBurning(state))
            {
                state = null;
            }
        }

        private static void AfterFuelIgnition(EntityAgent entity, BlockEntity? state)
        {
            if (state is null || !IsBurning(state))
            {
                return;
            }

            // Record all ignitions
            FiringCycle cycle = Cycles.GetOrCreateValue(state);
            cycle.Speed = Speed(entity, "potteryCookSpeed");
            cycle.Ignition = FuelStart(state);
            state.MarkDirty();
        }

        [HarmonyPatch(typeof(BlockCoalPile), nameof(BlockCoalPile.OnTryIgniteBlockOver))]
        private static class CoalIgnition
        {
            private static void Prefix(EntityAgent __0, BlockPos __1, out BlockEntity? __state) => BeforeFuelIgnition(__0, __1, out __state);

            private static void Postfix(EntityAgent __0, BlockEntity? __state) => AfterFuelIgnition(__0, __state);
        }

        [HarmonyPatch(typeof(BlockGroundStorage), nameof(BlockGroundStorage.OnTryIgniteBlockOver))]
        private static class GroundIgnition
        {
            private static void Prefix(EntityAgent __0, BlockPos __1, out BlockEntity? __state) => BeforeFuelIgnition(__0, __1, out __state);

            private static void Postfix(EntityAgent __0, BlockEntity? __state) => AfterFuelIgnition(__0, __state);
        }

        [HarmonyPatch(typeof(BlockEntityBeeHiveKiln), "UpdateGroundStorage")]
        private static class KilnProgress
        {
            private static void Prefix(BlockEntityBeeHiveKiln __instance, ref float __0)
            {
                if (__instance.Api?.Side != EnumAppSide.Server)
                {
                    return;
                }

                if (!Cycles.TryGetValue(__instance, out FiringCycle? cycle))
                {
                    // Last required fuel starts the process
                    cycle = new FiringCycle();
                    foreach (BlockPos position in FuelPositions(__instance))
                    {
                        BlockEntity? fuel = __instance.Api.World.BlockAccessor.GetBlockEntity(position.DownCopy());
                        if (IsBurning(fuel) && fuel is not null && FuelStart(fuel) >= cycle.Ignition)
                        {
                            cycle.Speed = Cycles.TryGetValue(fuel, out FiringCycle? ignition)
                                && ignition.Ignition == FuelStart(fuel) ? ignition.Speed : 1f;
                            cycle.Ignition = FuelStart(fuel);
                        }
                    }

                    Cycles.Add(__instance, cycle);
                    __instance.MarkDirty();
                }

                // Only scale finishing speed
                __0 *= cycle.Speed;
            }
        }

        [HarmonyPatch(typeof(BlockEntityBeeHiveKiln), "OnServerTick3s")]
        private static class KilnTick
        {
            private static void Postfix(BlockEntityBeeHiveKiln __instance)
            {
                if (!ReceivesHeat(__instance) && Cycles.Remove(__instance))
                {
                    __instance.MarkDirty();
                }
            }
        }

        private static void Save(BlockEntity __instance, ITreeAttribute __0)
        {
            if (Cycles.TryGetValue(__instance, out FiringCycle? cycle)
                && (__instance is BlockEntityBeeHiveKiln || IsBurning(__instance)))
            {
                __0.SetFloat(SpeedKey, cycle.Speed);
                __0.SetDouble(IgnitionKey, cycle.Ignition);
            }
            else
            {
                __0.RemoveAttribute(SpeedKey);
                __0.RemoveAttribute(IgnitionKey);
            }
        }

        private static void Load(BlockEntity __instance, ITreeAttribute __0)
        {
            Cycles.Remove(__instance);
            if (__0.HasAttribute(SpeedKey))
            {
                float speed = __0.GetFloat(SpeedKey, 1f);
                if (float.IsFinite(speed) && speed > 0f)
                {
                    Cycles.Add(__instance, new FiringCycle
                    {
                        Speed = Math.Clamp(speed, 0.05f, 100f),
                        Ignition = __0.GetDouble(IgnitionKey)
                    });
                }
            }
        }

        private sealed class FiringCycle
        {
            public float Speed = 1f;
            public double Ignition = double.NegativeInfinity;
        }
    }
}