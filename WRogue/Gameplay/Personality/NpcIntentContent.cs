using System;
using System.Collections.Generic;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;

namespace djack.RogueSurvivor.Gameplay.Personality
{
    enum NpcIntentMethod { ShareFood, RequestFood, LeaveGroup }
    sealed class NpcIntentTrigger
    {
        public readonly string Kind;
        public readonly Func<Actor, SignificantEvent, bool> Applies;
        public readonly Func<SignificantEvent, Actor> Target;
        public NpcIntentTrigger(string kind, Func<Actor, SignificantEvent, bool> applies, Func<SignificantEvent, Actor> target)
        { Kind = kind; Applies = applies; Target = target; }
    }
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
        public readonly List<NpcIntentTrigger> Triggers = new List<NpcIntentTrigger>();
        public NpcIntentDefinition(string id, string name, NpcIntentMethod method, int priority,
            int threshold, int duration, int cooldown, int relationWeight, params NpcIntentWeight[] weights)
        { Id = id; Name = name; Method = method; BasePriority = priority; Threshold = threshold;
            Duration = duration; Cooldown = cooldown; RelationWeight = relationWeight; Weights = weights; }
        public NpcIntentDefinition On(string kind, Func<Actor, SignificantEvent, bool> applies, Func<SignificantEvent, Actor> target)
        { Triggers.Add(new NpcIntentTrigger(kind, applies, target)); return this; }
        public Actor Bind(Actor owner, SignificantEvent source)
        {
            foreach (NpcIntentTrigger trigger in Triggers)
                if (trigger.Kind == source.Kind && trigger.Applies(owner, source)) return trigger.Target(source);
            return null;
        }
        public int Score(Actor owner, Actor target)
        { return Score(owner, PersonalitySystem.Attitude(owner, target), owner.Leader == target); }
        public int Score(Actor owner, NpcIntent intent)
        { return Score(owner, intent.KnownAttitude, owner.Leader != null && owner.Leader.PersonalityIdentity == intent.TargetId); }
        int Score(Actor owner, int attitude, bool isLeader)
        {
            int score = BasePriority + RelationWeight * attitude / 2;
            foreach (NpcIntentWeight weight in Weights)
                score += PersonalitySystem.Bias(owner, weight.Axis) * weight.Numerator / weight.Denominator;
            if (Method == NpcIntentMethod.LeaveGroup && isLeader)
                score -= Math.Min(2 * Rules.TRUST_TRUSTING_THRESHOLD, Math.Max(0, owner.TrustInLeader)) * 30 / Rules.TRUST_TRUSTING_THRESHOLD;
            return score;
        }
    }
    static class NpcIntentContent
    {
        static readonly Dictionary<string, NpcIntentDefinition> definitions = new Dictionary<string, NpcIntentDefinition>();
        static readonly Dictionary<string, List<NpcIntentDefinition>> byEvent = new Dictionary<string, List<NpcIntentDefinition>>();
        public static readonly NpcIntentDefinition Repay = Add(new NpcIntentDefinition("repay_aid", "Repay remembered aid",
            NpcIntentMethod.ShareFood, 25, 20, 2 * WorldTime.TURNS_PER_DAY, WorldTime.TURNS_PER_DAY, 1,
            new NpcIntentWeight(DecisionKind.Compassion, 1), new NpcIntentWeight(DecisionKind.Trade, 1))
            .On("helped", (a, e) => a == e.Subject && e.SubjectIsDirect && e.StoryId == null, e => e.Other));
        public static readonly NpcIntentDefinition Request = Add(new NpcIntentDefinition("request_food", "Ask for food",
            NpcIntentMethod.RequestFood, 35, 20, 60, 180, 1,
            new NpcIntentWeight(DecisionKind.Group, 1), new NpcIntentWeight(DecisionKind.Trade, 1, 2)));
        public static readonly NpcIntentDefinition Help = Add(new NpcIntentDefinition("answer_food_request", "Answer a food request",
            NpcIntentMethod.ShareFood, 25, 20, 60, 60, 1,
            new NpcIntentWeight(DecisionKind.Compassion, 1), new NpcIntentWeight(DecisionKind.Trade, 1))
            .On("requested_food", (a, e) => a == e.Other && e.OtherIsDirect, e => e.Subject));
        public static readonly NpcIntentDefinition Leave = Add(new NpcIntentDefinition("leave_unsafe_group", "Leave an unsafe leader",
            NpcIntentMethod.LeaveGroup, 40, 55, WorldTime.TURNS_PER_DAY, WorldTime.TURNS_PER_DAY, -1,
            new NpcIntentWeight(DecisionKind.Group, -1), new NpcIntentWeight(DecisionKind.Courage, -1, 2))
            .On("attack", (a, e) => a.Leader != null && a.Leader == e.Other && e.Subject == a, e => e.Other)
            .On("murder", (a, e) => a.Leader != null && a.Leader == e.Other && e.Subject != a, e => e.Other));
        static NpcIntentDefinition Add(NpcIntentDefinition definition)
        {
            if (String.IsNullOrEmpty(definition.Id) || definitions.ContainsKey(definition.Id))
                throw new ArgumentException("Duplicate intent definition.");
            definitions.Add(definition.Id, definition);
            foreach (NpcIntentTrigger trigger in definition.Triggers)
            {
                List<NpcIntentDefinition> list;
                if (!byEvent.TryGetValue(trigger.Kind, out list)) byEvent.Add(trigger.Kind, list = new List<NpcIntentDefinition>());
                if (!list.Contains(definition)) list.Add(definition);
            }
            return definition;
        }
        public static NpcIntentDefinition Find(string id)
        { NpcIntentDefinition definition; return id != null && definitions.TryGetValue(id, out definition) ? definition : null; }
        public static IEnumerable<NpcIntentDefinition> ForEvent(string kind)
        { List<NpcIntentDefinition> list; return byEvent.TryGetValue(kind, out list) ? (IEnumerable<NpcIntentDefinition>)list : new NpcIntentDefinition[0]; }

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
