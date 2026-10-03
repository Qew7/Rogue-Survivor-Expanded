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
        public readonly long[] SupportingCauses;
        [System.Runtime.Serialization.OptionalField] public int RecordCategories;
        public ResidentEntry(int turn, string text, int sequence, string kind = "note", bool direct = false,
            Guid subjectId = default(Guid), Guid otherId = default(Guid), bool gainedTrait = false,
            long eventId = 0, long causeId = 0, string storyId = null, long[] supportingCauses = null)
        { Turn = turn; Text = text; Sequence = sequence; Kind = kind; Direct = direct; SubjectId = subjectId; OtherId = otherId; GainedTrait = gainedTrait;
            EventId = eventId; CauseId = causeId; StoryId = storyId; SupportingCauses = supportingCauses; RecordCategories = NpcRecordDescriptions.Categories(kind); }
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
        public int EntryCount { get { return m_Entries.Count; } }
        public ResidentRecord(Actor actor)
        {
            Identity = actor.PersonalityIdentity;
            Name = actor.UnmodifiedName;
            SpawnTurn = actor.SpawnTime;
        }
        public void Add(string key, int turn, string text, ObservedEvent observed = null, bool gainedTrait = false, long[] supportingCauses = null)
        {
            if (m_Keys.ContainsKey(key)) return;
            m_Keys.Add(key, true);
            string kind = key.Split(':')[0];
            var entry = new ResidentEntry(turn, text, m_Entries.Count, observed == null ? kind : observed.Kind,
                observed != null && observed.Direct, observed == null ? Guid.Empty : observed.SubjectId,
                observed == null ? Guid.Empty : observed.OtherId, gainedTrait,
                observed == null ? 0 : observed.EventId, observed == null ? 0 : observed.CauseId, observed == null ? null : observed.StoryId, supportingCauses);
            if (observed != null && observed.RecordCategories != 0) entry.RecordCategories = observed.RecordCategories;
            m_Entries.Add(entry);
        }
    }

    // No Actor references: the chronicle survives removal of actors and corpses.
    [Serializable]
    sealed partial class ResidentRecords
    {
        readonly Dictionary<Guid, ResidentRecord> m_Residents = new Dictionary<Guid, ResidentRecord>();
        [System.Runtime.Serialization.OptionalField] Dictionary<string, DistrictKind> m_DistrictKinds;
        public bool IsPartial;
        public int RecoveryTurn;
        public ICollection<ResidentRecord> Residents { get { return m_Residents.Values; } }
        public IDictionary<string, DistrictKind> DistrictKinds
        { get { return m_DistrictKinds ?? (IDictionary<string, DistrictKind>)new Dictionary<string, DistrictKind>(); } }

        public static Dictionary<string, DistrictKind> DistrictKindsFrom(World world)
        {
            var kinds = new Dictionary<string, DistrictKind>(StringComparer.Ordinal);
            if (world == null) return kinds;
            for (int x = 0; x < world.Size; x++)
                for (int y = 0; y < world.Size; y++)
                {
                    District district = world[x, y];
                    if (district != null)
                        kinds[World.CoordToString(district.WorldPosition.X, district.WorldPosition.Y)] = district.Kind;
                }
            return kinds;
        }

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
        public void MemoryStarted(Actor actor, MemoryInstance memory, MemoryDefinition definition = null)
        {
            ResidentRecord record = Register(actor);
            if (record != null) record.Add("memory:" + MemoryKey(memory), memory.StartTurn,
                "Memory: " + (definition == null ? MemoryName(memory.Id) : definition.Name) +
                (String.IsNullOrEmpty(memory.Subject) ? "" : " (" + memory.Subject + ")") + ".");
        }
        public void MemoryResolved(Actor actor, MemoryInstance memory, PersonalityRegistry registry = null)
        {
            ResidentRecord record = Register(actor);
            if (record == null) return;
            string outcome = memory.OutcomeId ?? "none";
            if (outcome.StartsWith("trait:"))
            { TraitDefinition trait = registry == null ? null : registry.Trait(outcome.Substring(6)); outcome = "gained trait " + (trait == null ? TraitName(outcome.Substring(6)) : trait.Name); }
            else if (outcome.StartsWith("skill:"))
            {
                string skill = outcome.Substring(6);
                try { skill = Skills.Name((Skills.IDs)Enum.Parse(typeof(Skills.IDs), skill)); }
                catch (ArgumentException) { }
                outcome = "improved skill " + skill;
            }
            else outcome = "no new trait or skill";
            record.Add("resolved:" + MemoryKey(memory), memory.ResolvedTurn,
                "Resolved memory: " + (registry == null || registry.Memory(memory.Id) == null ? MemoryName(memory.Id) : registry.Memory(memory.Id).Name) + "; " + outcome + ".", null,
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
        public void RecordThreat(Actor actor, Rules rules, int worldTurn, Actor enemy,
            int distance, int threat, int resolve, bool mortal)
        {
            if (threat < 20 && !mortal) return;
            ResidentRecord record = Register(actor);
            if (record == null || record.ThreatSnapshot != null || record.DeathTurn >= 0) return;
            var snapshot = new ResidentSurvivalSnapshot(actor, rules, worldTurn);
            snapshot.Threat = threat; snapshot.Resolve = resolve; snapshot.Mortal = mortal;
            snapshot.EnemyDistance = distance; snapshot.SetOther(enemy);
            record.ThreatSnapshot = snapshot;
        }
        public void RecordDeath(Actor actor, Rules rules, int worldTurn, Actor killer, string reason)
        {
            ResidentRecord record = Register(actor);
            if (record == null || record.DeathSnapshot != null) return;
            var snapshot = new ResidentSurvivalSnapshot(actor, rules, worldTurn);
            snapshot.Reason = reason; snapshot.SetOther(killer);
            record.DeathSnapshot = snapshot;
        }
        static string EventText(ObservedEvent e)
        {
            return e.RecordText ?? NpcRecordDescriptions.Describe(e);
        }

        public static ResidentRecords Recover(Session session)
        {
            ResidentRecords records = new ResidentRecords {
                IsPartial = true, RecoveryTurn = session.WorldTime.TurnCounter };
            records.m_DistrictKinds = DistrictKindsFrom(session.World);
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
                IntentChanged(actor, intent, "started", "recovered from available intent state", NpcContentCatalog.Default);
                if (intent.Finished) IntentChanged(actor, intent, intent.Status.ToString().ToLowerInvariant(),
                    intent.Outcome, NpcContentCatalog.Default);
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
