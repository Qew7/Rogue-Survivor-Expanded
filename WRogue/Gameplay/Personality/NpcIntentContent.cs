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
        public int Score(Actor owner, Actor target)
        { return ScoreKnown(owner, PersonalitySystem.Attitude(owner, target), owner.Leader == target) + SocialScore(owner, target.PersonalityIdentity); }
        public int Score(Actor owner, NpcIntent intent)
        { return intent.Generated != null ? intent.Generated.Utility : ScoreKnown(owner, intent.KnownAttitude,
            owner.Leader != null && owner.Leader.PersonalityIdentity == intent.TargetId) + SocialScore(owner, intent.TargetId); }
        public int ThresholdFor(NpcIntent intent) { return intent.Generated == null ? Threshold : NpcGoalGenerator.MinimumUtility; }
        public int ScoreKnown(Actor owner, int attitude, bool isLeader)
        {
            int score = BasePriority + RelationWeight * attitude / 2;
            foreach (NpcIntentWeight weight in Weights)
                score += PersonalitySystem.Bias(owner, weight.Axis) * weight.Numerator / weight.Denominator;
            if (Method == NpcIntentMethod.LeaveGroup && isLeader)
                score -= Math.Min(2 * Rules.TRUST_TRUSTING_THRESHOLD, Math.Max(0, owner.TrustInLeader)) * 30 / Rules.TRUST_TRUSTING_THRESHOLD;
            return score;
        }
        public int SocialScore(Actor owner, Guid target)
        {
            RelationshipRecord opinion = owner.Personality.Person(target); if (opinion == null) return 0;
            if (Id == "repay_aid") return opinion.Debt / 2;
            if (Method == NpcIntentMethod.AvoidPerson) return opinion.Fear / 2;
            if (Method == NpcIntentMethod.ConfrontPerson) return opinion.Grievance / 2;
            if (Method == NpcIntentMethod.SeekPerson) return opinion.Attachment / 2;
            return 0;
        }
    }
    static class NpcIntentContent
    {
        static readonly Dictionary<string, NpcIntentDefinition> definitions = new Dictionary<string, NpcIntentDefinition>();
        public static readonly NpcIntentDefinition Repay = Add(new NpcIntentDefinition("repay_aid", "Repay remembered aid",
            NpcIntentMethod.ShareFood, 25, 20, 2 * WorldTime.TURNS_PER_DAY, WorldTime.TURNS_PER_DAY, 1,
            new NpcIntentWeight(DecisionKind.Compassion, 1), new NpcIntentWeight(DecisionKind.Trade, 1)));
        public static readonly NpcIntentDefinition Obtain = Add(new NpcIntentDefinition("obtain_food", "Obtain usable food",
            NpcIntentMethod.ObtainFood, 35, 20, 180, 180, 0, new NpcIntentWeight(DecisionKind.Explore, 1),
            new NpcIntentWeight(DecisionKind.Supplies, 1), new NpcIntentWeight(DecisionKind.Group, -1, 2)));
        public static readonly NpcIntentDefinition Recover = Add(new NpcIntentDefinition("restore_health", "Recover health",
            NpcIntentMethod.RestoreHealth, 40, 20, 180, 180, 0));
        public static readonly NpcIntentDefinition Request = Add(new NpcIntentDefinition("request_food", "Ask for food",
            NpcIntentMethod.RequestFood, 35, 20, 60, 180, 1,
            new NpcIntentWeight(DecisionKind.Group, 1), new NpcIntentWeight(DecisionKind.Trade, 1, 2)));
        public static readonly NpcIntentDefinition MedicalAid = Add(new NpcIntentDefinition("medical_aid", "Meet a person's medical need", NpcIntentMethod.MedicalAid, 25, 20, 180, 60, 1));
        public static readonly NpcIntentDefinition Promise = Add(new NpcIntentDefinition("fulfil_promise", "Fulfil a promise", NpcIntentMethod.FulfilPromise, 35, 20, 180, 0, 1));
        public static readonly NpcIntentDefinition Restitution = Add(new NpcIntentDefinition("restore_property", "Replace lost supplies", NpcIntentMethod.RestoreProperty, 30, 20, 180, 180, 1));
        public static readonly NpcIntentDefinition ValuedItem = Add(new NpcIntentDefinition("recover_valued_item", "Recover a valued item", NpcIntentMethod.ObtainValuedItem, 25, 20, 180, 180, 0));
        public static readonly NpcIntentDefinition Help = Add(new NpcIntentDefinition("answer_food_request", "Answer a food request",
            NpcIntentMethod.ShareFood, 25, 20, 60, 60, 1,
            new NpcIntentWeight(DecisionKind.Compassion, 1), new NpcIntentWeight(DecisionKind.Trade, 1)));
        public static readonly NpcIntentDefinition Leave = Add(new NpcIntentDefinition("leave_unsafe_group", "Leave an unsafe leader",
            NpcIntentMethod.LeaveGroup, 40, 55, WorldTime.TURNS_PER_DAY, WorldTime.TURNS_PER_DAY, -1,
            new NpcIntentWeight(DecisionKind.Group, -1), new NpcIntentWeight(DecisionKind.Courage, -1, 2)));
        public static readonly NpcIntentDefinition Seek = Add(new NpcIntentDefinition("seek_companion", "Find a missing companion",
            NpcIntentMethod.SeekPerson, 20, 35, WorldTime.TURNS_PER_DAY, 180, 1, new NpcIntentWeight(DecisionKind.Group, 1), new NpcIntentWeight(DecisionKind.Compassion, 1, 2)));
        public static readonly NpcIntentDefinition Avoid = Add(new NpcIntentDefinition("avoid_reported_threat", "Avoid a reported aggressor",
            NpcIntentMethod.AvoidPerson, 20, 35, 180, 180, -1, new NpcIntentWeight(DecisionKind.Courage, -1)));
        public static readonly NpcIntentDefinition Confront = Add(new NpcIntentDefinition("confront_reported_aggressor", "Confront a reported aggressor",
            NpcIntentMethod.ConfrontPerson, 15, 35, 180, 180, -1, new NpcIntentWeight(DecisionKind.Courage, 1), new NpcIntentWeight(DecisionKind.Law, 1)));
        public static readonly NpcIntentDefinition Gather = Add(new NpcIntentDefinition("gather_group_supplies", "Gather supplies for a companion",
            NpcIntentMethod.GatherFood, 35, 30, WorldTime.TURNS_PER_DAY, 180, 1, new NpcIntentWeight(DecisionKind.Compassion, 1), new NpcIntentWeight(DecisionKind.Supplies, 1), new NpcIntentWeight(DecisionKind.Explore, 1, 2)));
        public static readonly NpcIntentDefinition Coordinate = Add(new NpcIntentDefinition("coordinate_group_supplies", "Coordinate supplies for the group",
            NpcIntentMethod.Coordinate, 20, 25, WorldTime.TURNS_PER_DAY, 180, 0, new NpcIntentWeight(DecisionKind.Group, 1), new NpcIntentWeight(DecisionKind.Compassion, 1, 2)));
        public static readonly NpcIntentDefinition Shelter = Add(new NpcIntentDefinition("seek_group_shelter", "Reach the group's known shelter",
            NpcIntentMethod.ReachShelter, 25, 35, 180, 180, 0, new NpcIntentWeight(DecisionKind.Group, 1), new NpcIntentWeight(DecisionKind.Courage, -1, 2)));
        static NpcIntentDefinition Add(NpcIntentDefinition definition)
        {
            if (String.IsNullOrEmpty(definition.Id) || definitions.ContainsKey(definition.Id))
                throw new ArgumentException("Duplicate intent definition.");
            definitions.Add(definition.Id, definition);
            return definition;
        }
        public static NpcIntentDefinition Find(string id)
        { NpcIntentDefinition definition; return id != null && definitions.TryGetValue(id, out definition) ? definition : null; }
        public static void RegisterMemories(PersonalityRegistry registry)
        {
            registry.Register(new MemoryDefinition("refused_aid", "A request for aid was declined", 2, 6,
                new[] { new MemoryTrigger("request_refused", (a, e) => a == e.Other) },
                new MemoryOutcome(null, "mistrustful", null), new MemoryOutcome(null, null, Skills.IDs.CHARISMATIC))
                .Relate(MemoryRelationRole.Subject, -10), false);
            registry.Register(new MemoryDefinition("left_unsafe_group", "Left an unsafe group", 2, 6,
                new[] { new MemoryTrigger("left_group", (a, e) => a == e.Subject) },
                new MemoryOutcome(null, "hermit", null), new MemoryOutcome(null, null, Skills.IDs.STRONG_PSYCHE))
                .Relate(MemoryRelationRole.Other, 0, MemoryRelationRole.Other), false);
            registry.Register(new MemoryDefinition("companion_departed", "A companion chose to leave", 2, 6,
                new[] { new MemoryTrigger("left_group", (a, e) => a == e.Other) },
                new MemoryOutcome(null, "protector", null), new MemoryOutcome(null, null, Skills.IDs.LEADERSHIP))
                .Relate(MemoryRelationRole.Subject, -5), false);
        }
    }
}
