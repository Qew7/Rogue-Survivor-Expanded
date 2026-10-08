using System;
using System.Collections.Generic;
using System.Drawing;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Engine.MapObjects;
using djack.RogueSurvivor.Gameplay.Personality;
using Message = djack.RogueSurvivor.Data.Message;

namespace djack.RogueSurvivor.Engine
{
    [Serializable]
    sealed class RadioProgram
    {
        public int Slot;
        public Guid HostId;
        public Actor Host, Source;
        public NpcFact[] Facts;
        public string Text;
        public long EventId;
        public int ForecastTurn;
    }

    partial class RogueGame
    {
        static readonly string[] RadioStationNames = { "Survivor Network", "Military Dispatch", "Local Calls", "Gang Frequency" };
        static readonly string[] RadioStoryLeads = { "From local survivors: ", "Situation report: ",
            "A caller says: ", "Word on the street: " };
        static readonly string[] RadioUpdateLeads = { "Follow-up from survivors: ", "Dispatch update: ",
            "The caller adds: ", "New word on the street: " };
        static readonly string[] RadioShelterNames = { "shelter", "hideout", "safehouse", "refuge", "home", "camp",
            "outpost", "haven", "dwelling", "squat", "stronghold", "den" };
        static readonly string[][] RadioBarks = {
            new[] { "Keep water covered and leave a note when you change shelter.", "Someone is teaching children to count the lights still on across town.",
                "Tonight's request: knock twice before entering an occupied room.", "A song for anyone walking home by the long route." },
            new[] { "Supply crews report delays. Watch the district bulletin, not the rooftops.", "Keep clear of marked landing routes and wait for the all-clear.",
                "Field reminder: boiled water is worth the fuel.", "The next dispatch follows after the signal check." },
            new[] { "The corner kitchen is trading hot tea for clean jars.", "A caller asks whether anyone has seen a brown dog near the bus stop.",
                "The evening quiz: which street still has its old name painted on the wall?", "Someone left a stack of books by the stairwell. Take one, leave one." },
            new[] { "Keep your eyes on the crossroads; someone else already is.", "Our DJ says the old records still sound better than the sirens.",
                "A road is only empty until the next crew claims it.", "This signal is stronger than the curfew." }
        };
        static readonly string[] RadioCities = { "Greyhaven", "Northport", "Bellwick", "Eastmere" };
        static readonly string[] RadioOrigins = {
            "Some blame a clinic shipment; nurses say the fever was there before it arrived.",
            "A laboratory is blamed, though transit workers logged attacks before its alarms.",
            "People point to the river, yet the first sealed blocks were uphill.",
            "A military injection is rumored to be the cause; soldiers were reportedly sick before the doses.",
            "A coastal freighter is blamed, while inland towns report older cases.",
            "Spoiled grain is the story today; locked granaries tell much the same tale."
        };

        public void DoSwitchRadio(Actor actor, RadioReceiver radio)
        {
            SpendActorActionPoints(actor, Rules.BASE_ACTION_COST);
            radio.TuneNext();
            if (IsVisibleToPlayer(radio))
                AddMessage(new Message(radio.IsOn ? "Radio tuned to " + RadioStationNames[radio.Station] + "." : "Radio switched off.",
                    m_Session.WorldTime.TurnCounter, Color.White));
            if (radio.IsOn) BroadcastRadio(radio.Station, radio.Location.Map, radio.Location.Position, null);
            else if (radio.Location.Map.RadioNoisePosition == radio.Location.Position)
                radio.Location.Map.RadioNoiseUntil = radio.Location.Map.LocalTime.TurnCounter;
        }

        void DoUseRadio(Actor actor, ItemRadio radio)
        {
            SpendActorActionPoints(actor, Rules.BASE_ACTION_COST);
            radio.IsOn = !radio.IsOn;
            if (actor.IsPlayer)
                AddMessage(new Message(radio.IsOn ? "You tune in to " + RadioStationNames[radio.Station] + "." : "You switch off the radio.",
                    m_Session.WorldTime.TurnCounter, Color.White));
            if (radio.IsOn) BroadcastRadio(radio.Station, actor.Location.Map, actor.Location.Position, actor);
            else if (actor.Location.Map.RadioNoisePosition == actor.Location.Position)
                actor.Location.Map.RadioNoiseUntil = actor.Location.Map.LocalTime.TurnCounter;
        }

        void AdvanceRadios(Map map)
        {
            if (map.LocalTime.TurnCounter % 30 != 0) return;
            foreach (MapObject obj in map.MapObjects)
            {
                RadioReceiver radio = obj as RadioReceiver;
                if (radio != null && radio.IsOn)
                    BroadcastRadio(radio.Station, map, radio.Location.Position, null);
            }
            foreach (Actor actor in map.Actors)
            {
                if (actor.IsDead || actor.IsSleeping || actor.Inventory == null) continue;
                foreach (Item item in actor.Inventory.Items)
                {
                    ItemRadio radio = item as ItemRadio;
                    if (radio == null || !radio.IsOn) continue;
                    if (radio.Batteries <= 0) { radio.IsOn = false; continue; }
                    --radio.Batteries;
                    BroadcastRadio(radio.Station, map, actor.Location.Position, actor);
                    if (radio.Batteries == 0) radio.IsOn = false;
                }
            }
        }

        void BroadcastRadio(int station, Map map, Point position, Actor owner)
        {
            map.RadioNoisePosition = position;
            map.RadioNoiseUntil = map.LocalTime.TurnCounter + 30;
            if (owner == null)
            {
                bool hasListener = false;
                foreach (Actor listener in map.Actors)
                    if (!listener.IsDead && !listener.IsSleeping && !listener.Model.Abilities.IsUndead &&
                        listener.Model.Abilities.IsIntelligent &&
                        m_Rules.GridDistance(position, listener.Location.Position) <= 5)
                    { hasListener = true; break; }
                if (!hasListener) { OnLoudNoise(map, position, "A radio"); return; }
            }
            RadioProgram program = GetRadioProgram(station, m_Session.WorldTime.TurnCounter / WorldTime.TURNS_PER_HOUR);
            if (station == 0 && m_Session.RadioHostId != Guid.Empty &&
                (program.HostId != m_Session.RadioHostId || program.Host == null || program.Host.IsDead))
            { if (owner == null) OnLoudNoise(map, position, "A radio"); return; }
            foreach (Actor listener in map.Actors)
            {
                if (listener.IsDead || listener.IsSleeping || listener.Model.Abilities.IsUndead ||
                    !listener.Model.Abilities.IsIntelligent || owner != null && listener != owner ||
                    owner == null && m_Rules.GridDistance(position, listener.Location.Position) > 5) continue;
                if (listener.Personality == null) listener.Personality = new PersonalityState();
                if (!listener.Personality.FirstRadioHearing(station, program.Slot)) continue;
                if (program.Facts != null)
                    foreach (NpcFact fact in program.Facts)
                        if (NpcKnowledgeSystem.Hear(this, listener, program.Source, fact))
                            ApplyRadioSanity(listener, fact);
                if (program.ForecastTurn > 0 && listener.Personality.LastRadioDropTurn != program.ForecastTurn)
                {
                    listener.Personality.LastRadioDropTurn = program.ForecastTurn;
                    if (listener.Model.Abilities.HasSanity && listener.Model.Abilities.HasToEat &&
                        listener.FoodPoints < m_Rules.ActorMaxFood(listener) / 4)
                        RegenActorSanity(listener, 1);
                }
                if (listener.IsPlayer && program.Text != null)
                {
                    bool heard = false;
                    foreach (HeardJournalEntry entry in listener.Personality.HeardJournal)
                        if (entry.Kind == "radio" && entry.Speaker == RadioStationNames[station] &&
                            entry.CauseId == program.Slot) { heard = true; break; }
                    if (!heard)
                    {
                        listener.Personality.HearSpeech(new HeardJournalEntry(map.LocalTime.TurnCounter,
                            "radio", RadioStationNames[station], program.Text, program.Slot));
                        AddMessage(new Message(RadioStationNames[station] + ": " + program.Text,
                            m_Session.WorldTime.TurnCounter, Color.LightGreen));
                    }
                }
            }
            if (owner == null) OnLoudNoise(map, position, "A radio");
        }

        RadioProgram GetRadioProgram(int station, int slot)
        {
            lock (m_Session)
            {
                RadioProgram[] programs = m_Session.RadioPrograms;
                RadioProgram program = programs[station];
                if (program == null || program.Slot != slot)
                    programs[station] = program = BuildRadioProgram(station, slot);
                return program;
            }
        }

        RadioProgram BuildRadioProgram(int station, int slot)
        {
            RadioProgram program = new RadioProgram { Slot = slot, HostId = m_Session.RadioHostId };
            World world = m_Session.World;
            int seed = m_Session.Seed;
            if (station == 0 && program.HostId != Guid.Empty)
                for (int x = 0; x < world.Size && program.Host == null; x++)
                    for (int y = 0; y < world.Size && program.Host == null; y++)
                    {
                        District district = world[x, y];
                        if (district == null) continue;
                        foreach (Map sourceMap in district.Maps)
                        {
                            foreach (Actor actor in sourceMap.Actors)
                                if (actor.PersonalityIdentity == program.HostId)
                                { program.Host = actor; break; }
                            if (program.Host != null) break;
                        }
                    }
            if (station == 0 && program.HostId != Guid.Empty &&
                (program.Host == null || program.Host.IsDead)) return program;

            int segment = (int)(((uint)seed + (uint)slot + (uint)(station * 2)) % 6u);
            if (segment == 0) { program.Text = RadioLore(station, slot); return program; }
            if (segment == 1) { program.Text = RadioBark(station, slot); return program; }

            int now = m_Session.WorldTime.TurnCounter;
            int remaining = m_Session.RadioDropTurn - now;
            if (station == 1 && m_Session.RadioDropTurn > 0 && remaining > 0 &&
                remaining <= 2 * WorldTime.TURNS_PER_DAY)
            {
                Point district = m_Session.RadioDropDistrict;
                program.ForecastTurn = m_Session.RadioDropTurn;
                program.Text = "A supply flight is scheduled for district " + World.CoordToString(district.X, district.Y) +
                    " in " + (remaining <= WorldTime.TURNS_PER_DAY ? "about a day." : "about two days.");
                return program;
            }

            Actor source = null;
            NpcFact headline = null;
            int best = Int32.MinValue;
            List<NpcFact> playerFacts = m_Player == null || m_Player.Personality == null ? null :
                m_Player.Personality.Knowledge.Facts;
            HashSet<long> knownIds = null;
            HashSet<string> knownStories = null;
            string lastKind = null;
            bool lastKindMatches = false;
            string lastStoryId = null;
            bool lastStoryKnown = false;
            if (playerFacts != null && playerFacts.Count > 0)
            {
                knownIds = new HashSet<long>();
                knownStories = new HashSet<string>(StringComparer.Ordinal);
                foreach (NpcFact known in playerFacts)
                {
                    knownIds.Add(known.EventId);
                    if (!String.IsNullOrEmpty(known.StoryId)) knownStories.Add(known.StoryId);
                }
            }
            for (int x = 0; x < world.Size; x++) for (int y = 0; y < world.Size; y++)
            {
                District district = world[x, y];
                if (district == null) continue;
                foreach (Map sourceMap in district.Maps)
                    foreach (Actor candidate in sourceMap.Actors)
                    {
                        if (candidate.IsDead || candidate.IsPlayer || candidate.Personality == null) continue;
                        List<NpcFact> facts = candidate.Personality.Knowledge.Facts;
                        for (int f = facts.Count - 1; f >= 0; f--)
                        {
                            NpcFact fact = facts[f];
                            int age = now - fact.EventTurn;
                            if (fact.Source != NpcKnowledgeSource.Told || fact.Place.Map == null ||
                                age > 2 * WorldTime.TURNS_PER_DAY || fact.Confidence < 40 || fact.Hops >= 3) continue;
                            if (station != 0)
                            {
                                if (lastKind == null || fact.Kind != lastKind)
                                { lastKind = fact.Kind; lastKindMatches = RadioMatches(station, lastKind); }
                                if (!lastKindMatches) continue;
                            }
                            int score = (2 * WorldTime.TURNS_PER_DAY - Math.Max(0, age)) /
                                (WorldTime.TURNS_PER_DAY / 24);
                            score += (int)((uint)(fact.EventId * 1103515245L + slot * 101L +
                                station * 37L + seed) % 64u);
                            // Player familiarity can add at most 32 points.
                            if (headline != null && score + 32 < best) continue;
                            if (knownIds != null)
                            {
                                bool knownEvent = false;
                                // One event ID can have several testimony kinds.
                                if (knownIds.Contains(fact.EventId))
                                    foreach (NpcFact known in playerFacts)
                                        if (known.EventId == fact.EventId && known.Kind == fact.Kind)
                                        { knownEvent = true; break; }
                                if (fact.StoryId != lastStoryId)
                                {
                                    lastStoryId = fact.StoryId;
                                    lastStoryKnown = !String.IsNullOrEmpty(lastStoryId) && knownStories.Contains(lastStoryId);
                                }
                                bool knownStory = lastStoryKnown;
                                if (!knownEvent) score += 12;
                                if (knownStory && !knownEvent) score += 20;
                            }
                            if ((headline == null || score > best ||
                                score == best && (fact.EventTurn > headline.EventTurn ||
                                fact.EventTurn == headline.EventTurn && fact.EventId > headline.EventId)) &&
                                NpcConversation.CanTell(this, candidate, fact))
                            { source = candidate; headline = fact; best = score; }
                        }
                    }
            }
            if (headline == null) { program.Text = RadioBark(station, slot); return program; }
            List<NpcFact> chapter = NpcConversation.Chapter(this, source, null, headline, now);
            List<string> lines = new List<string>();
            List<NpcFact> reported = new List<NpcFact>();
            HashSet<string> reports = new HashSet<string>(StringComparer.Ordinal);
            foreach (NpcFact fact in chapter)
            {
                string line = NpcConversation.ReportSentence(this, source, fact);
                if (!reports.Add(line)) continue;
                if (station == 3)
                {
                    switch (fact.Kind)
                    {
                        case "bikers_raid": line = line.Replace("there was a biker raid", "bikers arrived in the district on a raid"); break;
                        case "hells_souls_raid": line = line.Replace("there was a hell's souls raid", "Hell's Souls arrived in the district on a raid"); break;
                        case "free_angels_raid": line = line.Replace("there was a free angels raid", "Free Angels arrived in the district on a raid"); break;
                        case "gangstas_raid": line = line.Replace("there was a street gang raid", "a street gang arrived in the district on a raid"); break;
                        case "craps_raid": line = line.Replace("there was a craps raid", "the Craps arrived in the district on a raid"); break;
                        case "floods_raid": line = line.Replace("there was a floods raid", "the Floods arrived in the district on a raid"); break;
                    }
                }
                if (fact.Kind == "base_theft" || fact.Kind == "base_raid" || fact.Kind == "base_robbed" ||
                    fact.Kind == "supplies_lost" || fact.Resource == "base")
                {
                    string noun = RadioShelterNames[(int)((uint)(seed + slot + fact.EventId) % (uint)RadioShelterNames.Length)];
                    line = System.Text.RegularExpressions.Regex.Replace(line, @"\bbase\b", noun);
                }
                lines.Add(line);
                reported.Add(fact);
            }
            bool familiarStory = !String.IsNullOrEmpty(headline.StoryId) && knownStories != null &&
                knownStories.Contains(headline.StoryId);
            if (familiarStory)
            {
                for (int i = reported.Count - 1; i >= 0; i--)
                    if (playerFacts.Exists(known => known.EventId == reported[i].EventId && known.Kind == reported[i].Kind))
                    { reported.RemoveAt(i); lines.RemoveAt(i); }
            }
            program.Text = reported.Count == 0 ? RadioUpdateLeads[station] + "No new details on that story." :
                (familiarStory ? RadioUpdateLeads[station] : RadioStoryLeads[station]) +
                NpcConversation.StoryText(NpcContent, source, reported, lines);
            NpcStory activeStory = String.IsNullOrEmpty(headline.StoryId) ? null : m_Session.NpcDirector.Find(headline.StoryId);
            if (activeStory != null && !activeStory.Finished && headline.Place.Map.District != null)
            {
                Point district = headline.Place.Map.District.WorldPosition;
                program.Text += " The story is still unfolding in district " + World.CoordToString(district.X, district.Y) + ".";
            }
            program.Source = source;
            program.Facts = chapter.ToArray();
            program.EventId = headline.EventId;
            return program;
        }

        string RadioBark(int station, int slot)
        {
            string[] barks = RadioBarks[station];
            int index = (int)(((uint)m_Session.Seed + (uint)(slot / 6 + slot % 6)) % (uint)barks.Length);
            return barks[index];
        }

        string RadioLore(int station, int slot)
        {
            string city = RadioCities[(int)(((uint)m_Session.Seed + (uint)(slot / 6)) % (uint)RadioCities.Length)];
            string origin = RadioOrigins[(int)((uint)m_Session.Seed % (uint)RadioOrigins.Length)];
            switch (station)
            {
                case 0: return "A relay from " + city + " says shelters are still open. " + origin;
                case 1: return "Unconfirmed situation report from " + city + ": roads are restricted. " + origin;
                case 2: return "A caller from " + city + " asks for news of missing neighbors. " + origin;
                default: return "Pirated wire from " + city + ": evacuation passes are changing hands. " + origin;
            }
        }

        void ApplyRadioSanity(Actor listener, NpcFact fact)
        {
            if (!listener.Model.Abilities.HasSanity || listener.Personality == null) return;
            RelationshipRecord subject = fact.NamesSubject ? listener.Personality.Person(fact.SubjectId) : null;
            RelationshipRecord other = fact.NamesOther ? listener.Personality.Person(fact.OtherId) : null;
            bool closeSubject = subject != null && (subject.Attachment >= 20 || subject.Feeling >= 30);
            bool closeOther = other != null && (other.Attachment >= 20 || other.Feeling >= 30);
            int delta = 0;
            if (closeSubject && (fact.Kind == "attack" || fact.Kind == "death" || fact.Kind == "murder"))
                delta = listener.Personality.HasTrait("timid") ? -2 : -1;
            else if ((fact.Kind == "stolen_goods_found" || fact.Kind == "stolen_goods_passed") &&
                (closeOther || fact.OtherId == listener.PersonalityIdentity ||
                 listener.SocialGroup != null && listener.SocialGroup.Identity == fact.ClaimantGroupId))
                delta = listener.Personality.HasTrait("timid") ? -2 : -1;
            else if (fact.Kind == "psychopaths_arrival" && listener.Personality.HasTrait("timid"))
                delta = -1;
            else if ((closeSubject || closeOther) &&
                     (fact.Kind == "shared_food" || fact.Kind == "shared_medicine" || fact.Kind == "promise_kept"))
                delta = listener.Personality.HasTrait("compassionate") ? 2 : 1;
            if (delta < 0) SpendActorSanity(listener, -delta);
            else if (delta > 0) RegenActorSanity(listener, delta);
        }

        static bool RadioMatches(int station, string kind)
        {
            switch (station)
            {
                case 0: return true;
                case 1: return kind == "army_supplies" || kind == "national_guard_arrival" || kind == "blackops_raid";
                case 2: return kind == "shared_food" || kind.StartsWith("requested_", StringComparison.Ordinal) ||
                    kind.Contains("medicine") || kind.Contains("shelter") || kind.Contains("food");
                case 3: return kind.Contains("raid") || kind == "attack" || kind == "murder" ||
                    kind.Contains("theft") || kind.StartsWith("stolen_goods_", StringComparison.Ordinal) ||
                    kind == "supplies_lost" || kind == "base_robbed";
                default: return false;
            }
        }
    }
}
