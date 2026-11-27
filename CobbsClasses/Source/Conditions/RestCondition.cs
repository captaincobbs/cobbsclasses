using System;
using System.Runtime.CompilerServices;

using CobbsClasses.Source.API;

using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace CobbsClasses.Source.Conditions;

internal sealed class RestCondition(float maximumPenalty, float penaltyPerDay, float minimumRestHours) : IConditionalTrait
{
    private const string LastRestKey = "cobbsclasses:lastRestDay";
    private sealed class RestState
    {
        public double LastRest;
        public double? SleepStarted;
        public bool QualifiedSleep;
    }

    private readonly ConditionalWeakTable<EntityPlayer, RestState> states = [];

    internal void Forget(EntityPlayer entity)
    {
        states.Remove(entity);
    }

    public void Evaluate(in ConditionContext context, ConditionalModifiers modifiers)
    {
        if (context.Stats.GetBlended(TraitAttributes.Survival.UnrestedSpeedPenalty) <= 1f)
        {
            states.Remove(context.Entity);
            return;
        }

        double now = context.Entity.World.Calendar.TotalDays;
        RestState state;
        if (!states.TryGetValue(context.Entity, out state!))
        {
            double last = TraitPersistence.ReadTimestamp(context.Player, LastRestKey, now);
            context.Player.SetModData(LastRestKey, last);
            if (!double.IsFinite(last) || last > now)
            {
                last = now;
                context.Player.SetModData(LastRestKey, last);
            }

            state = new RestState { LastRest = last };
            states.Add(context.Entity, state);
        }

        if (now < state.LastRest)
        {
            state.LastRest = now;
            state.SleepStarted = null;
            state.QualifiedSleep = false;
            context.Player.SetModData(LastRestKey, now);
        }

        bool sleeping = context.Entity.GetBehavior<EntityBehaviorTiredness>()?.IsSleeping == true;
        double hoursPerDay = context.Entity.World.Calendar.HoursPerDay;
        double requiredDays = minimumRestHours / Math.Max(1d, hoursPerDay);
        if (sleeping)
        {
            state.SleepStarted ??= now;
            if (now - state.SleepStarted.Value >= requiredDays)
            {
                state.QualifiedSleep = true;
                state.LastRest = now;
                context.Player.SetModData(LastRestKey, now);
            }
        }
        else
        {
            // Sleep interval must be actually observed ingame
            if (state.QualifiedSleep)
            {
                state.LastRest = now;
                context.Player.SetModData(LastRestKey, now);
            }

            state.SleepStarted = null;
            state.QualifiedSleep = false;
        }

        double elapsed = Math.Max(0d, now - state.LastRest);
        float penalty = (float)Math.Min(maximumPenalty, elapsed * penaltyPerDay);
        modifiers.Add(TraitAttributes.Movement.Walkspeed, -penalty);
    }
}