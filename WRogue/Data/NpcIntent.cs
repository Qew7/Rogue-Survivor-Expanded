using System;
using System.Collections.Generic;

namespace djack.RogueSurvivor.Data
{
    enum NpcIntentStatus { Active, Paused, Waiting, Completed, Failed, Abandoned }

    [Serializable]
    sealed class NpcIntent
    {
        public readonly long Sequence, CauseId;
        public readonly string DefinitionId, TargetName, StoryId;
        public readonly Guid TargetId;
        public readonly int StartedTurn, Priority;
        public int Deadline, Progress;
        public NpcPlan Plan;
        public NpcGeneratedGoal Generated;
        public Location Destination;
        public Guid GroupId;
        public Guid CoordinatorId;
        public Location CoordinatorPlace;
        public Location LastKnown;
        public int KnownAttitude;
        public int NextAttempt, BlockedAttempts, FinishedTurn;
        public int LastKnownTurn;
        public bool Announced;
        public NpcIntentStatus Status;
        public string Outcome;
        public bool Finished { get { return Status >= NpcIntentStatus.Completed; } }
        public NpcIntent(long sequence, string definition, Actor owner, Actor target, int turn,
            int duration, int priority, long causeId, string storyId, int knownAttitude)
            : this(sequence, definition, owner, new NpcKnownPerson { Id = target.PersonalityIdentity,
                Name = target.UnmodifiedName, Place = target.Location }, turn, duration, priority, causeId, storyId, knownAttitude) { }
        public NpcIntent(long sequence, string definition, Actor owner, NpcKnownPerson target, int turn,
            int duration, int priority, long causeId, string storyId, int knownAttitude)
        {
            Sequence = sequence; DefinitionId = definition; TargetId = target.Id;
            TargetName = target.Name; LastKnown = target.Place;
            LastKnownTurn = target.SeenTurn;
            StartedTurn = turn; Deadline = turn + duration; Priority = priority; CauseId = causeId;
            KnownAttitude = knownAttitude;
            StoryId = storyId ?? owner.PersonalityIdentity.ToString("N") + ":" + sequence;
        }
    }

    [Serializable]
    sealed class NpcReaction
    {
        public readonly Guid TargetId;
        public readonly string Text, Kind, StoryId;
        public readonly long CauseId;
        public readonly int Deadline;
        public readonly NpcKnownPerson ReportedPerson;
        [System.Runtime.Serialization.OptionalField] public bool Overheard;
        public Location ResourcePlace;
        public string Resource;
        public NpcReaction(Actor target, string text, long causeId, int turn, string kind = "aid_acknowledged", string storyId = null, NpcKnownPerson report = null, bool overheard = false)
        { TargetId = target.PersonalityIdentity; Text = text; CauseId = causeId; Deadline = turn + 30; Kind = kind; StoryId = storyId; ReportedPerson = report; Overheard = overheard; }
    }

    sealed partial class PersonalityState
    {
        List<NpcIntent> m_Intents;
        List<NpcReaction> m_Reactions;
        Dictionary<string, int> m_IntentCooldowns;
        long m_IntentSequence;
        internal long LastIntentEventId;
        internal long NextIntentSequence { get { return m_IntentSequence + 1; } }
        internal bool HasIntentState { get { return m_Intents != null && m_Intents.Count > 0; } }
        internal bool HasPendingSocialState
        { get { return (m_Intents != null && m_Intents.Exists(i => !i.Finished)) || (m_Reactions != null && m_Reactions.Count > 0); } }
        public IList<NpcIntent> Intents { get { return IntentList.AsReadOnly(); } }
        internal List<NpcIntent> IntentList { get { return m_Intents ?? (m_Intents = new List<NpcIntent>()); } }
        internal List<NpcReaction> Reactions { get { return m_Reactions ?? (m_Reactions = new List<NpcReaction>()); } }
        internal bool CanStartIntent(string id, int turn)
        {
            int until;
            if (m_IntentCooldowns != null && m_IntentCooldowns.TryGetValue(id, out until) && turn < until) return false;
            int active = 0;
            foreach (NpcIntent intent in IntentList)
            { if (!intent.Finished) { active++; if (intent.DefinitionId == id) return false; } }
            return active < 4;
        }
        internal NpcIntent StartGoal(string id, Actor owner, NpcKnownPerson target, int turn, int duration,
            int cooldown, int priority, long causeId, string storyId, int knownAttitude, NpcGeneratedGoal generated = null)
        {
            if (generated == null ? !CanStartIntent(id, turn) : !CanGenerateGoal(generated.Key, turn)) return null;
            if (generated == null)
            {
                if (m_IntentCooldowns == null) m_IntentCooldowns = new Dictionary<string, int>();
                m_IntentCooldowns[id] = turn + cooldown;
            }
            else RememberGoalCooldown(generated.Key, turn + cooldown);
            var intent = new NpcIntent(++m_IntentSequence, id, owner, target, turn, duration, priority, causeId, storyId, knownAttitude) { Generated = generated };
            IntentList.Add(intent);
            while (IntentList.Count > 12)
            { int index = IntentList.FindIndex(i => i.Finished); if (index < 0) break; IntentList.RemoveAt(index); }
            return intent;
        }
    }
}
