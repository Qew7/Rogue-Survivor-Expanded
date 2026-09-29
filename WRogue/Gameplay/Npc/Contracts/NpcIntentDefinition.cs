using System;
using System.Collections.Generic;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;

namespace djack.RogueSurvivor.Gameplay.Personality
{
    enum NpcIntentMethod { ShareFood, RequestFood, LeaveGroup, SeekPerson, AvoidPerson, ConfrontPerson, GatherFood, ReachShelter, Coordinate, ObtainFood, RestoreHealth,
        MedicalAid, FulfilPromise, RestoreProperty, ObtainValuedItem }
    sealed class NpcIntentWeight
    {
        public readonly DecisionKind Axis;
        public readonly int Numerator, Denominator;
        public NpcIntentWeight(DecisionKind axis, int numerator, int denominator = 1)
        { Axis = axis; Numerator = numerator; Denominator = denominator; }
    }
    sealed class NpcIntentDefinition
    {
        public readonly string Id, Name;
        public readonly NpcIntentMethod Method;
        public readonly int BasePriority, Threshold, Duration, Cooldown, RelationWeight;
        public readonly NpcIntentWeight[] Weights;
        public NpcIntentDefinition(string id, string name, NpcIntentMethod method, int priority,
            int threshold, int duration, int cooldown, int relationWeight, params NpcIntentWeight[] weights)
        { Id = id; Name = name; Method = method; BasePriority = priority; Threshold = threshold;
            Duration = duration; Cooldown = cooldown; RelationWeight = relationWeight; Weights = weights; }
        public NpcIntentDefinition(string id, string name, int duration, int cooldown = 0)
            : this(id, name, default(NpcIntentMethod), 20, 20, duration, cooldown, 0) { LegacyMethod = false; }
        public int Score(Actor owner, NpcIntent intent, PersonalityRegistry registry = null)
        { return intent.Generated != null ? intent.Generated.Utility : ScoreKnown(owner, intent.KnownAttitude,
            owner.Leader != null && owner.Leader.PersonalityIdentity == intent.TargetId, registry) + SocialScore(owner, intent.TargetId); }
        public int ThresholdFor(NpcIntent intent) { return intent.Generated == null ? Threshold : NpcGoalGenerator.MinimumUtility; }
        public int ScoreKnown(Actor owner, int attitude, bool isLeader, PersonalityRegistry registry = null)
        {
            int score = BasePriority + RelationWeight * attitude / 2;
            foreach (NpcIntentWeight weight in Weights)
                score += PersonalitySystem.Bias(owner, weight.Axis, registry: registry) * weight.Numerator / weight.Denominator;
            if (Departure && isLeader)
                score -= Math.Min(2 * Rules.TRUST_TRUSTING_THRESHOLD, Math.Max(0, owner.TrustInLeader)) * 30 / Rules.TRUST_TRUSTING_THRESHOLD;
            return score;
        }
        public bool LegacyMethod = true, Selectable = true, PauseWhenTired = true, Departure, AllowHostile, PauseWhenHungry, WaitingAfterAnnouncement, ReportAfterDelivery, CompleteEpisodeOnSuccess;
        public Func<Actor, bool> TravelArrived;
        public Func<RelationshipRecord, int> SocialPriority;
        public Func<NpcContentCatalog, NpcGeneratedGoal, ulong> Result;
        public Func<NpcContentCatalog, NpcGeneratedGoal, NpcPlanningState> ResultFacts;
        public NpcPlanningState GetResult(NpcContentCatalog catalog, NpcGeneratedGoal goal)
        { return ResultFacts != null ? ResultFacts(catalog, goal) : Result == null ? default(NpcPlanningState) : (NpcPlanningState)Result(catalog, goal); }
        public Action<NpcPlanDomain> BuildPlan;
        public Func<RogueGame, Actor, NpcIntent, NpcIntentOutcome> Assess;
        public Func<NpcExecutionContext, Location> TravelDestination;
        public int SocialScore(Actor owner, Guid target)
        { RelationshipRecord opinion = owner.Personality.Person(target); return opinion == null || SocialPriority == null ? 0 : SocialPriority(opinion); }
    }
}
