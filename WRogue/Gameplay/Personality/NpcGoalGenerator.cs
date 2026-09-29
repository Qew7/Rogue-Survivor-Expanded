using System;
using System.Collections.Generic;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;

namespace djack.RogueSurvivor.Gameplay.Personality
{
    sealed class NpcGoalCandidate
    {
        public NpcGeneratedGoal State;
        public NpcKnownPerson Target;
        public NpcIntentDefinition Capability;
        public long Cause;
        public string Story;
    }
    static partial class NpcGoalGenerator
    {
        public const int MinimumUtility = 20;
        public static void MedicineUsed(RogueGame game, Actor owner, int previousHP)
        {
            if (!NpcIntentSystem.Enabled(owner) || owner.HitPoints <= previousHP) return;
            foreach (NpcIntent intent in owner.Personality.Intents)
                if (!intent.Finished && intent.DefinitionId == NpcIntentContent.Recover.Id)
                {
                    NpcPlanExecution.Publish(game, "treated_wounds", owner, null, intent);
                    if (owner.HitPoints >= game.Rules.ActorMaxHPs(owner)) NpcIntentSystem.Finish(owner, intent, NpcIntentStatus.Completed, "actually recovered health");
                    else if (intent.Plan != null) { intent.Plan.Invalidate(); intent.Plan.NextPlanningTurn = owner.Location.Map.LocalTime.TurnCounter; }
                    break;
                }
        }
        public static void Refresh(RogueGame game, Actor owner, bool maintain = false)
        {
            if (!NpcIntentSystem.Enabled(owner) || owner.IsSleeping) return;
            List<NpcGoalCandidate> candidates = Evaluate(game, owner);
            foreach (NpcIntent intent in owner.Personality.Intents)
                if (!intent.Finished && intent.Generated != null)
                {
                    NpcGoalCandidate fresh = candidates.Find(c => c.State.Key == intent.Generated.Key);
                    if (fresh != null)
                    {
                        if (intent.Plan != null && intent.Generated.Current != fresh.State.Current)
                        { intent.Plan.Invalidate(); intent.Plan.NextPlanningTurn = owner.Location.Map.LocalTime.TurnCounter; intent.NextAttempt = owner.Location.Map.LocalTime.TurnCounter; }
                        intent.Generated = fresh.State;
                    }
                    else { intent.Generated.Utility = 0; intent.Generated.EvaluatedTurn = owner.Location.Map.LocalTime.TurnCounter; }
                }
            foreach (NpcIntent intent in owner.Personality.Intents)
                if (maintain && !intent.Finished && intent.Generated != null && intent.Generated.Utility < MinimumUtility)
                {
                    bool satisfied = intent.Generated.Deficit == 0 && (intent.Generated.Value == NpcGoalValue.Nutrition ||
                        intent.Generated.Value == NpcGoalValue.Care || intent.Generated.Value == NpcGoalValue.Reciprocity || intent.Generated.Value == NpcGoalValue.Recovery ||
                        intent.Generated.Value == NpcGoalValue.MedicalCare || intent.Generated.Value == NpcGoalValue.Restitution);
                    NpcIntentSystem.Finish(owner, intent, satisfied ? NpcIntentStatus.Completed : NpcIntentStatus.Abandoned,
                        satisfied ? "observed that the desired state was satisfied" : "motivation changed");
                }
            candidates.Sort((a, b) => { int score = b.State.Utility.CompareTo(a.State.Utility);
                return score != 0 ? score : String.CompareOrdinal(a.State.Key, b.State.Key); });
            foreach (NpcGoalCandidate candidate in candidates)
            {
                if (candidate.State.Utility < MinimumUtility || HasEquivalent(owner, candidate)) continue;
                int turn = owner.Location.Map.LocalTime.TurnCounter;
                if (!owner.Personality.LegacyCooldownReady(candidate.Capability.Id, turn) ||
                    (candidate.State.Value == NpcGoalValue.Nutrition && !owner.Personality.LegacyCooldownReady(NpcIntentContent.Request.Id, turn))) continue;
                if (!owner.Personality.CanGenerateGoal(candidate.State.Key, owner.Location.Map.LocalTime.TurnCounter))
                {
                    if (!maintain) continue;
                    NpcIntent weakest = null; int active = 0;
                    foreach (NpcIntent intent in owner.Personality.Intents)
                        if (!intent.Finished)
                        {
                            active++;
                            if (intent.Generated != null && (weakest == null || intent.Generated.Utility < weakest.Generated.Utility)) weakest = intent;
                        }
                    if (active < 4 || weakest == null || candidate.State.Utility < weakest.Generated.Utility + 10 ||
                        !owner.Personality.GoalCooldownReady(candidate.State.Key, owner.Location.Map.LocalTime.TurnCounter)) continue;
                    NpcIntentSystem.Finish(owner, weakest, NpcIntentStatus.Abandoned, "a more important state became unsatisfied");
                }
                Start(owner, candidate);
            }
        }
        static bool HasEquivalent(Actor owner, NpcGoalCandidate candidate)
        {
            foreach (NpcIntent intent in owner.Personality.Intents)
                if (!intent.Finished && ((intent.Generated != null && intent.Generated.Key == candidate.State.Key) ||
                    (intent.DefinitionId == candidate.Capability.Id && intent.TargetId == candidate.Target.Id && (intent.Generated == null || candidate.State.Resource == null)) ||
                    (candidate.State.Value == NpcGoalValue.Nutrition && (intent.DefinitionId == NpcIntentContent.Request.Id || intent.DefinitionId == NpcIntentContent.Obtain.Id)))) return true;
            return false;
        }
        static void Start(Actor owner, NpcGoalCandidate candidate)
        {
            int turn = owner.Location.Map.LocalTime.TurnCounter;
            string parent = candidate.Story;
            NpcStoryDirector director = Session.Get.NpcDirector;
            lock (director)
            {
                NpcStory prior = parent == null ? null : director.Find(parent);
                string id = parent != null && prior != null && !prior.Finished ? parent : owner.PersonalityIdentity.ToString("N") + ":" + owner.Personality.NextIntentSequence;
                NpcStory story = director.Open(id, candidate.Capability.Id, owner, candidate.Cause, turn + candidate.Capability.Duration,
                    "value:" + owner.PersonalityIdentity + ":" + candidate.State.Key);
                if (story == null || story.Roles.Count >= 8) return;
                NpcIntent intent = owner.Personality.StartGeneratedGoal(candidate.Capability.Id, owner, candidate.Target, candidate.State,
                    turn, candidate.Capability.Duration, candidate.Capability.Cooldown, candidate.Cause, id);
                if (intent == null) return;
                if (candidate.State.ObjectPlace.Map != null) intent.Destination = candidate.State.ObjectPlace;
                director.Bind(story, owner, intent);
                director.Link(story, parent, owner, candidate.Cause);
                foreach (NpcFact fact in owner.Personality.Knowledge.Facts)
                    if (candidate.State.Causes != null && Array.IndexOf(candidate.State.Causes, fact.EventId) >= 0) director.Link(story, fact.StoryId, owner, fact.EventId);
                Session.Get.ResidentRecords.IntentChanged(owner, intent, "started", candidate.State.Explanation);
            }
        }
        static bool Pending(Actor owner, NpcGoalValue value, Guid subject)
        {
            foreach (NpcIntent intent in owner.Personality.Intents)
                if (!intent.Finished && intent.Generated != null && intent.Generated.Value == value && intent.Generated.SubjectId == subject) return true;
            return false;
        }
        static void Add(List<NpcGoalCandidate> list, Actor owner, NpcKnownPerson target, NpcGoalValue value,
            NpcIntentDefinition capability, int current, int desired, int deficit, int confidence, long cause = 0, string story = null)
        {
            if (list.Count >= 192 || target == null || target.Dead || target.Place.Map == null) return;
            var candidate = new NpcGoalCandidate { Target = target, Capability = capability, Cause = cause, Story = story,
                State = NpcValues.Evaluate(owner, value, value == NpcGoalValue.Nutrition ? owner.PersonalityIdentity : target.Id,
                    current, desired, deficit, confidence, NpcGoalPlanner.Desired(capability.Method)) };
            candidate.State.Causes = cause > 0 && target.SocialCause > 0 && target.SocialCause != cause ? new[] { cause, target.SocialCause } :
                cause > 0 ? new[] { cause } : target.SocialCause > 0 ? new[] { target.SocialCause } : new long[0];
            list.Add(candidate);
        }
    }
}
