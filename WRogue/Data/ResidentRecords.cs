using System;
using System.Collections.Generic;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Gameplay;
using djack.RogueSurvivor.Gameplay.Personality;

namespace djack.RogueSurvivor.Data
{
    [Serializable]
    sealed class ResidentEntry
    {
        public readonly int Turn;
        public readonly int Sequence;
        public readonly string Text;
        public readonly string Kind;
        public readonly bool Direct, GainedTrait;
        public readonly Guid SubjectId, OtherId;
        public readonly long EventId, CauseId;
        public readonly string StoryId;
        public ResidentEntry(int turn, string text, int sequence, string kind = "note", bool direct = false,
            Guid subjectId = default(Guid), Guid otherId = default(Guid), bool gainedTrait = false,
            long eventId = 0, long causeId = 0, string storyId = null)
        { Turn = turn; Text = text; Sequence = sequence; Kind = kind; Direct = direct; SubjectId = subjectId; OtherId = otherId; GainedTrait = gainedTrait;
            EventId = eventId; CauseId = causeId; StoryId = storyId; }
    }

    [Serializable]
    sealed partial class ResidentRecord
    {
        public readonly Guid Identity;
        public string Name;
        public readonly int SpawnTurn;
        public int DeathTurn = -1;
        readonly List<ResidentEntry> m_Entries = new List<ResidentEntry>();
        readonly Dictionary<string, bool> m_Keys = new Dictionary<string, bool>();
        public IList<ResidentEntry> Entries { get { return m_Entries.AsReadOnly(); } }
        public ResidentRecord(Actor actor)
        {
            Identity = actor.PersonalityIdentity;
            Name = actor.UnmodifiedName;
            SpawnTurn = actor.SpawnTime;
        }
        public void Add(string key, int turn, string text, ObservedEvent observed = null, bool gainedTrait = false)
        {
            if (m_Keys.ContainsKey(key)) return;
            m_Keys.Add(key, true);
            string kind = key.Split(':')[0];
            m_Entries.Add(new ResidentEntry(turn, text, m_Entries.Count, observed == null ? kind : observed.Kind,
                observed != null && observed.Direct, observed == null ? Guid.Empty : observed.SubjectId,
                observed == null ? Guid.Empty : observed.OtherId, gainedTrait,
                observed == null ? 0 : observed.EventId, observed == null ? 0 : observed.CauseId, observed == null ? null : observed.StoryId));
        }
    }

    // No Actor references: the chronicle survives removal of actors and corpses.
    [Serializable]
    sealed partial class ResidentRecords
    {
        readonly Dictionary<Guid, ResidentRecord> m_Residents = new Dictionary<Guid, ResidentRecord>();
        public bool IsPartial;
        public int RecoveryTurn;
        public ICollection<ResidentRecord> Residents { get { return m_Residents.Values; } }

        public ResidentRecord Register(Actor actor)
        {
            if (actor == null || actor.IsPlayer || actor.Model == null ||
                actor.Model.Abilities.IsUndead || !actor.Model.Abilities.IsIntelligent) return null;
            ResidentRecord record;
            if (!m_Residents.TryGetValue(actor.PersonalityIdentity, out record))
            {
                record = new ResidentRecord(actor);
                if (actor.IsDead) record.DeathTurn = -2;
                m_Residents.Add(record.Identity, record);
                record.Add("spawn", actor.SpawnTime, "Entered the world" +
                    (actor.Faction == null ? "." : " as " + actor.Faction.MemberName + "."));
                if (actor.Personality != null)
                {
                    foreach (TraitInstance trait in actor.Personality.Traits)
                        record.Add("initial-trait:" + trait.Id, IsPartial ? RecoveryTurn : actor.SpawnTime,
                            (IsPartial ? "Trait in saved snapshot: " : "Starting trait: ") + TraitLabel(trait) + ".");
                    foreach (MemoryInstance memory in actor.Personality.Memories)
                        MemoryStarted(actor, memory);
                }
            }
            record.Name = actor.UnmodifiedName;
            if (record.DeathTurn < 0) record.Snapshot(actor);
            return record;
        }

        static string TraitName(string id)
        {
            TraitDefinition definition = PersonalitySystem.Registry.Trait(id);
            return definition == null ? id : definition.Name;
        }
        static string TraitLabel(TraitInstance trait)
        {
            string name = TraitName(trait.Id);
            if (trait.ItemModelId < 0) return name;
            string item = Models.Items != null && trait.ItemModelId < (int)GameItems.IDs._COUNT &&
                Models.Items[trait.ItemModelId] != null
                ? Models.Items[trait.ItemModelId].PluralName : "unknown items";
            return name + " " + item;
        }
        static string MemoryName(string id)
        {
            MemoryDefinition definition = PersonalitySystem.Registry.Memory(id);
            return definition == null ? id : definition.Name;
        }
        static string MemoryKey(MemoryInstance memory)
        {
            return memory.Id + ":" + memory.StartTurn + ":" + memory.SubjectId + ":" +
                memory.RelatedPersonId + ":" + memory.Subject;
        }
        public void MemoryStarted(Actor actor, MemoryInstance memory)
        {
            ResidentRecord record = Register(actor);
            if (record != null) record.Add("memory:" + MemoryKey(memory), memory.StartTurn,
                "Memory: " + MemoryName(memory.Id) +
                (String.IsNullOrEmpty(memory.Subject) ? "" : " (" + memory.Subject + ")") + ".");
        }
        public void MemoryResolved(Actor actor, MemoryInstance memory)
        {
            ResidentRecord record = Register(actor);
            if (record == null) return;
            string outcome = memory.OutcomeId ?? "none";
            if (outcome.StartsWith("trait:")) outcome = "gained trait " + TraitName(outcome.Substring(6));
            else if (outcome.StartsWith("skill:"))
            {
                string skill = outcome.Substring(6);
                try { skill = Skills.Name((Skills.IDs)Enum.Parse(typeof(Skills.IDs), skill)); }
                catch (ArgumentException) { }
                outcome = "improved skill " + skill;
            }
            else outcome = "no new trait or skill";
            record.Add("resolved:" + MemoryKey(memory), memory.ResolvedTurn,
                "Resolved memory: " + MemoryName(memory.Id) + "; " + outcome + ".", null,
                memory.OutcomeId != null && memory.OutcomeId.StartsWith("trait:", StringComparison.Ordinal));
        }
        public void Observe(Actor actor, ObservedEvent observed)
        {
            ResidentRecord record = Register(actor);
            if (record == null) return;
            string key = "event:" + observed.Kind + ":" + observed.Turn + ":" +
                observed.SubjectId + ":" + observed.OtherId + ":" + observed.Subject + ":" + observed.Other;
            if (observed.EventId != 0) key = "event:" + observed.EventId;
            record.Add(key, observed.Turn, (observed.Direct ? "Experienced: " : "Witnessed: ") +
                EventText(observed) + (observed.StoryId == null ? "" : " [story " + observed.StoryId + "]"), observed);
            if (observed.Kind == "death" && observed.SubjectId == actor.PersonalityIdentity)
                record.DeathTurn = observed.Turn;
        }
        static string EventText(ObservedEvent e)
        {
            string special = PersonalityWorldContent.EventName(e.Kind);
            if (special != null) return special +
                (e.Subject == null ? "." : ": " + e.Subject + ".");
            string subject = e.Subject ?? "Someone";
            string other = e.Other ?? "someone";
            switch (e.Kind)
            {
                case "death": return subject + " died" + (e.Other == null ? "." : "; killed by " + other + ".");
                case "murder": return other + " murdered " + subject + ".";
                case "kill_human": return other + " killed " + subject + ".";
                case "attack": return other + " attacked " + subject + ".";
                case "helped": return other + " helped " + subject + ".";
                case "requested_food": return subject + " asked " + other + " for food.";
                case "request_refused": return subject + " declined " + other + "'s request.";
                case "shared_food": return subject + " shared food with " + other + ".";
                case "left_group": return subject + " chose to leave " + other + "'s group.";
                case "rumor_shared": return subject + " told " + other + " a report about an earlier event.";
                case "asked_location": return subject + " asked " + other + " about a missing companion.";
                case "location_reported": return subject + " answered " + other + " about a companion's last known location.";
                case "supplies_requested": return subject + " asked " + other + " to gather supplies for the group.";
                case "supplies_acquired": return subject + " acquired the supplies they were seeking.";
                case "supplies_delivered": return subject + " reported the completed delivery to " + other + ".";
                case "task_declined": return subject + " declined " + other + "'s task.";
                case "shelter_suggested": return subject + " proposed moving the group to known shelter.";
                case "shelter_declined": return subject + " declined " + other + "'s shelter proposal.";
                case "food_offered": return subject + " offered to exchange food with " + other + ".";
                case "bartered_food": return subject + " obtained food by exchanging supplies with " + other + ".";
                case "shelter_reached": return subject + " reached the agreed shelter.";
                case "reunited": return subject + " found " + other + " after searching.";
                case "confronted": return subject + " warned " + other + " about reported violence.";
                case "withdrew": return subject + " withdrew from the last reported danger location.";
                case "group_succession": return subject + " succeeded " + other + " as group leader.";
                case "aid_acknowledged": return subject + " acknowledged aid from " + other + ".";
                case "joined_group": return subject + " joined " + other + "'s group.";
                case "abandoned": return subject + " was abandoned by " + other + ".";
                case "base_theft": return subject + " stole from " + other + "'s base.";
                case "base_loss": return subject + " lost a base.";
                case "supplies_lost": return subject + " lost supplies.";
                case "zombified": return other + " turned into " + subject + ".";
                case "starvation": return subject + " faced starvation.";
                case "raid": return "A raid occurred.";
                case "unique_arrival": return subject + " arrived.";
                default: return e.Kind + ": " + subject + (e.Other == null ? "." : "; " + other + ".");
            }
        }

        public static ResidentRecords Recover(Session session)
        {
            ResidentRecords records = new ResidentRecords {
                IsPartial = true, RecoveryTurn = session.WorldTime.TurnCounter };
            if (session.World == null) return records;
            for (int x = 0; x < session.World.Size; x++)
                for (int y = 0; y < session.World.Size; y++)
                {
                    District district = session.World[x, y];
                    if (district == null) continue;
                    foreach (Map map in district.Maps)
                    {
                        foreach (Actor actor in map.Actors) records.RecoverActor(actor);
                        foreach (Corpse corpse in map.Corpses)
                        {
                            records.RecoverActor(corpse.DeadGuy);
                            ResidentRecord dead = records.Register(corpse.DeadGuy);
                            if (dead != null) dead.DeathTurn = corpse.Turn;
                        }
                    }
                }
            return records;
        }
        void RecoverActor(Actor actor)
        {
            if (actor.Personality == null) return;
            ResidentRecord record = Register(actor);
            if (record == null) return;
            foreach (ObservedEvent observed in actor.Personality.Events) Observe(actor, observed);
            foreach (NpcIntent intent in actor.Personality.Intents)
            {
                IntentChanged(actor, intent, "started", "recovered from available intent state");
                if (intent.Finished) IntentChanged(actor, intent, intent.Status.ToString().ToLowerInvariant(), intent.Outcome);
            }
            List<RelationshipRecord> relations = new List<RelationshipRecord>(actor.Personality.People);
            relations.AddRange(actor.Personality.Groups);
            relations.AddRange(actor.Personality.Factions);
            foreach (RelationshipRecord relation in relations)
                foreach (MemoryInstance memory in relation.Memories)
                {
                    MemoryStarted(actor, memory);
                    if (memory.OutcomeId != null) MemoryResolved(actor, memory);
                }
        }
    }
}
