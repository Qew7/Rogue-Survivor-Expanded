using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;

namespace djack.RogueSurvivor.Gameplay.Personality
{
    static class NpcConversation
    {
        public static void BroadcastReport(RogueGame game, SignificantEvent source, NpcEventDefinition definition)
        {
            if (definition == null || !definition.AudibleReport || source.Subject == null ||
                source.Subject.Personality == null) return;
            NpcFact fact = source.Subject.Personality.Knowledge.Facts.Find(f => f.EventId == source.Id && f.Kind == source.Kind);
            if (fact == null) return;
            foreach (Actor hearer in source.Map.Actors)
            {
                if (hearer == source.Subject || hearer.IsDead || hearer.IsSleeping ||
                    !hearer.Model.Abilities.IsIntelligent || hearer.Model.Abilities.IsUndead ||
                    game.Rules.StdDistance(hearer.Location.Position, source.Position) > hearer.AudioRange) continue;
                if (hearer.Personality == null) hearer.Personality = new PersonalityState();
                NpcKnowledgeSystem.Hear(game, hearer, source.Subject, fact);
            }
        }

        public static void OfferPlayerReply(RogueGame game, SignificantEvent source, NpcEventDefinition definition)
        {
            Actor player = source.Other != null && source.Other.IsPlayer ? source.Other : game.Player;
            if (definition == null || definition.PlayerReply == null || player == null || !player.IsPlayer ||
                player.IsSleeping || source.Subject == null || source.Subject == player || player.Location.Map != source.Map)
                return;
            bool overheard = player != source.Other;
            if (overheard && definition.PlayerReply.DirectOnly) return;
            if (overheard && (!definition.AudibleReport ||
                game.Rules.StdDistance(player.Location.Position, source.Position) > player.AudioRange)) return;
            if (player.Personality == null) player.Personality = new PersonalityState();
            player.Personality.Reactions.RemoveAll(r => r.Deadline < source.Turn);
            if (player.Personality.Reactions.Count >= 4 ||
                player.Personality.Reactions.Exists(r => r.CauseId == source.Id)) return;
            player.Personality.Reactions.Add(new NpcReaction(source.Subject, definition.PlayerReply.Prompt,
                source.Id, source.Turn, source.Kind, source.StoryId, overheard: overheard));
        }

        public static NpcReaction Pending(Actor player, Actor target)
        {
            if (player == null || player.Personality == null || target == null ||
                !Session.Get.GamePreset.NpcPersonalitiesEnabled) return null;
            int turn = player.Location.Map.LocalTime.TurnCounter;
            player.Personality.Reactions.RemoveAll(r => r.Deadline < turn);
            return player.Personality.Reactions.Find(r => r.TargetId == target.PersonalityIdentity);
        }

        public static void Reply(RogueGame game, Actor player, Actor target, NpcReaction pending, bool yes)
        {
            NpcEventDefinition definition = game.NpcContent.Event(pending.Kind);
            NpcPlayerReply reply = definition == null ? null : definition.PlayerReply;
            player.Personality.Reactions.Remove(pending);
            if (reply == null) return;
            game.DoSay(player, target, yes ? reply.YesText : reply.NoText, RogueGame.Sayflags.IS_STORY,
                pending.CauseId, pending.StoryId);
            NpcEvents.Publish(game, yes ? reply.YesKind : reply.NoKind, player, target, pending.CauseId, pending.StoryId);
            game.DoSay(target, player, yes ? reply.YesReply : reply.NoReply, RogueGame.Sayflags.IS_FREE_ACTION);
        }

        public static bool CanTell(RogueGame game, Actor speaker, NpcFact fact)
        {
            if (speaker == null || fact == null || speaker.Personality == null) return false;
            if (fact.Kind == "claimed_permission")
            {
                if (fact.Source == NpcKnowledgeSource.Told)
                {
                    foreach (NpcFact known in speaker.Personality.Knowledge.Facts)
                        if (known.EventId == fact.EventId &&
                            (known.Kind == "base_theft" && known.Source != NpcKnowledgeSource.Told ||
                             known.Kind == "false_testimony_exposed")) return false;
                    return true;
                }
                return speaker.Personality.HasTrait("deceptive") &&
                    fact.SubjectId == speaker.PersonalityIdentity && fact.SourceId == speaker.PersonalityIdentity;
            }
            if (fact.Kind == "theft_accusation")
            {
                if (speaker.Personality.Knowledge.Facts.Exists(f =>
                    f.EventId == fact.EventId && f.Kind == "false_testimony_exposed")) return false;
                if (fact.OtherId == speaker.PersonalityIdentity) return false;
                return fact.SubjectId != speaker.PersonalityIdentity ||
                    speaker.Personality.HasTrait("deceptive") && fact.Source == NpcKnowledgeSource.Participant;
            }
            if (fact.Kind == "theft_accusation_denied" && fact.SubjectId == speaker.PersonalityIdentity)
                return fact.Source == NpcKnowledgeSource.Participant;
            if ((fact.Kind == "base_theft" || fact.Kind == "cache_theft") &&
                speaker.Personality.Knowledge.Facts.Exists(f => f.Kind == "theft_accusation" &&
                    f.CauseId == fact.EventId && f.SubjectId == speaker.PersonalityIdentity &&
                    f.Source == NpcKnowledgeSource.Participant)) return false;
            Guid id = speaker.PersonalityIdentity;
            if (fact.SubjectId != id && fact.OtherId != id) return true;
            NpcEventDefinition definition = game.NpcContent.Event(fact.Kind);
            if (definition == null) return true;
            bool ownAction = OwnAction(speaker, fact, definition);
            if (!ownAction) return true;
            switch (definition.SelfReportTone)
            {
                case NpcSelfReportTone.Harmful:
                    return speaker.Personality.HasTrait("cruel") || speaker.Personality.HasTrait("rebellious") ||
                        speaker.Personality.HasTrait("hotheaded") || speaker.Personality.HasTrait("vindictive") ||
                        PersonalitySystem.Bias(speaker, NpcPersonalityAxis.Law, registry: game.NpcContent.Personalities) <= -15 &&
                        PersonalitySystem.Bias(speaker, NpcPersonalityAxis.Courage, registry: game.NpcContent.Personalities) >= 10;
                case NpcSelfReportTone.Helpful:
                    return PersonalitySystem.Bias(speaker, NpcPersonalityAxis.Compassion, registry: game.NpcContent.Personalities) >= 15 ||
                        PersonalitySystem.Bias(speaker, NpcPersonalityAxis.Group, registry: game.NpcContent.Personalities) >= 15 ||
                        PersonalitySystem.Bias(speaker, NpcPersonalityAxis.Trade, registry: game.NpcContent.Personalities) >= 15;
                default:
                    return PersonalitySystem.Bias(speaker, NpcPersonalityAxis.Group, registry: game.NpcContent.Personalities) >= 15 ||
                        PersonalitySystem.Bias(speaker, NpcPersonalityAxis.Trade, registry: game.NpcContent.Personalities) >= 15;
            }
        }

        public static NpcFact Rumor(RogueGame game, Actor speaker, Actor listener)
        {
            if (speaker.Personality == null || !Session.Get.GamePreset.NpcPersonalitiesEnabled) return null;
            int turn = speaker.Location.Map.LocalTime.TurnCounter;
            var recentRadioStories = new HashSet<string>(StringComparer.Ordinal);
            if (listener != null && listener.IsPlayer && listener.Personality != null)
            {
                var radioTurns = new HashSet<int>();
                for (int i = listener.Personality.HeardJournal.Count - 1; i >= 0; i--)
                {
                    HeardJournalEntry heard = listener.Personality.HeardJournal[i];
                    if (heard.Kind == "radio" && turn >= heard.Turn &&
                        turn - heard.Turn <= 6 * WorldTime.TURNS_PER_HOUR)
                        radioTurns.Add(heard.Turn);
                }
                foreach (NpcFact known in listener.Personality.Knowledge.Facts)
                    if (radioTurns.Contains(known.LearnedTurn) && !String.IsNullOrEmpty(known.StoryId))
                        recentRadioStories.Add(known.StoryId);
            }
            NpcFact chosen = null; int best = Int32.MinValue;
            foreach (NpcFact fact in speaker.Personality.Knowledge.Facts)
                if (EligibleFact(game, speaker, fact, turn) && EligibleListener(game, speaker, listener, fact))
                {
                    int score = fact.EventTurn + SelfReportPreference(game, speaker, fact);
                    if (fact.StoryId != null && recentRadioStories.Contains(fact.StoryId))
                        score += 2 * WorldTime.TURNS_PER_DAY;
                    if (score > best) { chosen = fact; best = score; }
                }
            return chosen;
        }

        static bool OwnAction(Actor speaker, NpcFact fact, NpcEventDefinition definition)
        {
            NpcReportActorRole role = definition.ReportActorRole;
            Guid id = speaker.PersonalityIdentity;
            return ((role & NpcReportActorRole.Subject) != 0 && fact.SubjectId == id) ||
                ((role & NpcReportActorRole.Other) != 0 && fact.OtherId == id);
        }

        public static int SelfReportPreference(RogueGame game, Actor speaker, NpcFact fact)
        {
            if (fact.Source != NpcKnowledgeSource.Participant) return 0;
            NpcEventDefinition definition = game.NpcContent.Event(fact.Kind);
            return definition != null && definition.SelfReportTone != NpcSelfReportTone.Neutral &&
                OwnAction(speaker, fact, definition) ? WorldTime.TURNS_PER_HOUR : 0;
        }

        public static bool EligibleFact(RogueGame game, Actor speaker, NpcFact fact, int turn, bool radio = false)
        { return fact != null && fact.Place.Map != null && fact.Confidence >= 40 && fact.Hops < 3 &&
            turn - fact.EventTurn <= NpcKnowledge.FactMaxAge(fact) &&
            (radio || turn - fact.LearnedTurn <= WorldTime.TURNS_PER_DAY) && CanTell(game, speaker, fact); }

        public static bool EligibleListener(RogueGame game, Actor speaker, Actor listener, NpcFact fact)
        { return listener != null && listener != speaker && listener.PersonalityIdentity != fact.SourceId &&
            !speaker.Personality.Knowledge.WasToldRecently(fact.EventId, listener.PersonalityIdentity,
                speaker.Location.Map.LocalTime.TurnCounter) &&
            (!speaker.Personality.Knowledge.HasRecentRumor(listener.PersonalityIdentity, fact.Kind,
                speaker.Location.Map.LocalTime.TurnCounter) ||
                !speaker.Personality.Knowledge.WasToldRumor(fact, listener.PersonalityIdentity,
                    ReportSentence(game, speaker, fact), speaker.Location.Map.LocalTime.TurnCounter)) &&
            (listener.Personality == null || !listener.Personality.Knowledge.Facts.Exists(known =>
                known.EventId == fact.EventId && known.Kind == fact.Kind &&
                known.Confidence >= NpcKnowledgeSystem.ReportConfidence(listener, speaker, fact))); }

        public static void ShareRumor(RogueGame game, Actor speaker, Actor listener, NpcFact fact, bool free)
        {
            int turn = speaker.Location.Map.LocalTime.TurnCounter;
            if (!EligibleFact(game, speaker, fact, turn) || !EligibleListener(game, speaker, listener, fact)) return;
            Action<string, long> profile = RogueGame.ProfileSimulationPhase;
            long started = profile == null ? 0 : System.Diagnostics.Stopwatch.GetTimestamp();
            List<NpcFact> chapter = Chapter(game, speaker, listener, fact);
            List<string> lines = new List<string>();
            List<NpcFact> reported = new List<NpcFact>();
            foreach (NpcFact part in chapter)
            {
                string line = ReportSentence(game, speaker, part);
                if (lines.Contains(line)) continue;
                lines.Add(line);
                reported.Add(part);
            }
            string speech = StoryText(game.NpcContent, speaker, reported, lines);
            long speechId = game.DoSay(speaker, listener, speech, RogueGame.Sayflags.IS_STORY | RogueGame.Sayflags.IS_RUMOR |
                (free ? RogueGame.Sayflags.IS_FREE_ACTION : RogueGame.Sayflags.NONE), fact.EventId, fact.StoryId,
                deferNpcArchive: true);
            List<Actor> hearers = new List<Actor>();
            foreach (Actor hearer in speaker.Location.Map.Actors)
            {
                if (hearer == speaker || hearer.IsDead || hearer.IsSleeping || !hearer.Model.Abilities.IsIntelligent ||
                    hearer.Model.Abilities.IsUndead || game.Rules.StdDistance(hearer.Location.Position, speaker.Location.Position) > hearer.AudioRange) continue;
                if (hearer.Personality == null) hearer.Personality = new PersonalityState();
                hearers.Add(hearer);
            }
            bool[] learned = new bool[hearers.Count];
            foreach (NpcFact part in chapter)
            {
                string report = ReportSentence(game, speaker, part);
                for (int i = 0; i < hearers.Count; i++)
                {
                    Actor hearer = hearers[i];
                    if (NpcKnowledgeSystem.Hear(game, hearer, speaker, part)) learned[i] = true;
                    speaker.Personality.Knowledge.Told(part.EventId, hearer.PersonalityIdentity, turn);
                    speaker.Personality.Knowledge.ToldRumor(part, hearer.PersonalityIdentity, report, turn);
                }
                speaker.Personality.Knowledge.Told(part.EventId, listener.PersonalityIdentity, turn);
                speaker.Personality.Knowledge.ToldRumor(part, listener.PersonalityIdentity, report, turn);
            }
            for (int i = 0; i < hearers.Count; i++)
                if (!hearers[i].IsPlayer && (learned[i] || hearers[i] == listener))
                    game.RecordHeardSpeech(speaker, listener, hearers[i], speech, "heard_rumor",
                        speechId, fact.EventId, fact.StoryId);
            speaker.Personality.Knowledge.NextTalkTurn = turn + 30;
            NpcEvents.Publish(game, "rumor_shared", speaker, listener, fact.EventId, fact.StoryId);
            if (profile != null) profile("rumor share", System.Diagnostics.Stopwatch.GetTimestamp() - started);
        }

        public static List<NpcFact> Chapter(RogueGame game, Actor speaker, Actor listener, NpcFact fact, int turn = -1,
            IList<NpcFact> sourceFacts = null, bool radio = false)
        {
            if (turn < 0) turn = speaker.Location.Map.LocalTime.TurnCounter;
            List<NpcFact> chapter = new List<NpcFact> { fact };
            if (!String.IsNullOrEmpty(fact.StoryId))
                foreach (NpcFact related in sourceFacts ?? (IList<NpcFact>)speaker.Personality.Knowledge.Facts)
                    if (related != fact && related.StoryId == fact.StoryId &&
                        EligibleFact(game, speaker, related, turn, radio) &&
                        (listener == null || EligibleListener(game, speaker, listener, related)))
                        chapter.Add(related);
            chapter.Sort((a, b) => a.EventTurn != b.EventTurn ? a.EventTurn.CompareTo(b.EventTurn) : a.EventId.CompareTo(b.EventId));
            while (chapter.Count > 10) chapter.RemoveAt(chapter[0] == fact ? 1 : 0);
            return chapter;
        }

        public static string ReportSentence(RogueGame game, Actor speaker, NpcFact fact, bool radio = false)
        {
            if (!radio && fact.Source == NpcKnowledgeSource.Participant && speaker != null &&
                (fact.SubjectId == speaker.PersonalityIdentity || fact.OtherId == speaker.PersonalityIdentity))
            {
                NpcEventDefinition definition = game.NpcContent.Event(fact.Kind);
                string own = definition == null || definition.DescribeSelfReport == null ? null :
                    definition.DescribeSelfReport(fact, speaker);
                if (!String.IsNullOrEmpty(own)) return own + " " + ReportPlace(fact) + ".";
            }
            return SpokenReport(speaker, fact, NpcRecordDescriptions.Report(game.NpcContent, fact), radio);
        }

        static string SpokenReport(Actor speaker, NpcFact fact, string clause, bool radio)
        {
            if (!radio && fact.Source == NpcKnowledgeSource.Participant && speaker != null)
            {
                Guid id = speaker.PersonalityIdentity;
                string name = fact.SubjectId == id ? fact.ReportSubject : fact.OtherId == id ? fact.ReportOther : null;
                // When two different people share a display name, the prose alone
                // cannot identify which occurrence refers to the speaker.
                bool namesake = fact.SubjectId != fact.OtherId && fact.SubjectId != Guid.Empty && fact.OtherId != Guid.Empty &&
                    fact.ReportSubject == fact.ReportOther;
                if (!namesake && !String.IsNullOrEmpty(name))
                {
                    if (clause.StartsWith(name + " ", StringComparison.Ordinal))
                        return "I" + clause.Substring(name.Length) + " " + ReportPlace(fact) + ".";
                    if (clause.StartsWith(name + "'s ", StringComparison.Ordinal))
                        return "My" + clause.Substring(name.Length + 2) + " " + ReportPlace(fact) + ".";
                    string escaped = Regex.Escape(name);
                    clause = Regex.Replace(clause, @"(?<!\w)" + escaped + @"'s(?!\w)", "my");
                    clause = Regex.Replace(clause, @"(?<!\w)" + escaped + @"(?!\w)", "me");
                }
            }
            return ReportLead(speaker, fact, radio) + clause + " " + ReportPlace(fact) + ".";
        }

        struct StoryPart
        {
            public NpcFact First, Last;
            public string Text;
            public StoryPart(NpcFact first, NpcFact last, string text)
            { First = first; Last = last; Text = text; }
        }

        static bool CanGroup(Actor speaker, IList<NpcFact> run, NpcFact next, bool radio)
        {
            NpcFact first = run[0];
            if (first.SubjectId == Guid.Empty || first.OtherId == Guid.Empty || next.OtherId == Guid.Empty ||
                first.CauseId != 0 || next.CauseId != 0 || first.Kind != next.Kind ||
                first.SubjectId != next.SubjectId || first.ReportSubject != next.ReportSubject ||
                String.IsNullOrEmpty(first.ReportOther) || String.IsNullOrEmpty(next.ReportOther) ||
                ReportLead(speaker, first, radio) != ReportLead(speaker, next, radio) || ReportPlace(first) != ReportPlace(next)) return false;
            foreach (NpcFact prior in run)
                if (prior.OtherId == next.OtherId || prior.ReportOther == next.ReportOther) return false;
            return true;
        }

        public static string StoryText(NpcContentCatalog catalog, Actor speaker, IList<NpcFact> facts, IList<string> lines,
            bool radio = false)
        {
            if (lines.Count == 0) return "";
            var parts = new List<StoryPart>();
            for (int i = 0; i < lines.Count;)
            {
                NpcEventDefinition definition = catalog.Event(facts[i].Kind);
                var run = new List<NpcFact> { facts[i] };
                if (definition != null && definition.SummarizeReports != null)
                    while (i + run.Count < facts.Count && CanGroup(speaker, run, facts[i + run.Count], radio))
                        run.Add(facts[i + run.Count]);
                if (run.Count > 1)
                {
                    string summary = definition.SummarizeReports(run);
                    if (!String.IsNullOrEmpty(summary))
                    {
                        parts.Add(new StoryPart(run[0], run[run.Count - 1],
                            SpokenReport(speaker, run[0], summary, radio)));
                        i += run.Count;
                        continue;
                    }
                }
                parts.Add(new StoryPart(facts[i], facts[i], lines[i]));
                i++;
            }
            if (parts.Count > 4)
            {
                bool[] keep = new bool[parts.Count];
                keep[0] = keep[parts.Count - 1] = true;
                for (int chosen = 2; chosen < 4; chosen++)
                {
                    int best = -1, bestScore = Int32.MinValue;
                    for (int i = 1; i < parts.Count - 1; i++)
                    {
                        if (keep[i]) continue;
                        int score = ReportBeatScore(catalog, speaker, parts, i);
                        if (score > bestScore || score == bestScore && i > best)
                        { best = i; bestScore = score; }
                    }
                    keep[best] = true;
                }
                for (int i = parts.Count - 1; i >= 0; i--) if (!keep[i]) parts.RemoveAt(i);
            }
            string story = parts[0].Text;
            string previousPlace = ReportPlace(parts[0].Last);
            for (int i = 1; i < parts.Count; i++)
            {
                NpcFact fact = parts[i].First, previous = parts[i - 1].Last;
                string line = parts[i].Text;
                string lead = ReportLead(speaker, fact, radio);
                bool sameLead = lead == ReportLead(speaker, previous, radio);
                if (sameLead && line.StartsWith(lead, StringComparison.Ordinal))
                    line = line.Substring(lead.Length);
                string currentPlace = ReportPlace(fact);
                string placeSuffix = " " + currentPlace + ".";
                if (currentPlace == previousPlace && line.EndsWith(placeSuffix, StringComparison.Ordinal))
                    line = line.Substring(0, line.Length - placeSuffix.Length) + ".";
                if (sameLead && fact.NamesSubject && previous.NamesSubject &&
                    fact.SubjectId == previous.SubjectId &&
                    line.StartsWith(fact.ReportSubject + " ", StringComparison.Ordinal))
                    line = "they" + line.Substring(fact.ReportSubject.Length);
                NpcEventDefinition definition = catalog.Event(fact.Kind);
                string transition = definition != null && definition.ReportDisputesKinds != null &&
                    Array.IndexOf(definition.ReportDisputesKinds, previous.Kind) >= 0 &&
                    fact.EventId == previous.EventId ? " However, " :
                    i == parts.Count - 1 && definition != null && definition.ReportConclusion ? " In the end, " :
                    fact.CauseId > 0 && fact.CauseId == previous.EventId && sameLead ? " As a result, " :
                    fact.EventTurn == previous.EventTurn
                        ? (i % 2 == 0 ? " Also, " : " Around the same time, ")
                        : (i % 2 == 0 ? " Later, " : " After that, ");
                story += transition + line;
                previousPlace = ReportPlace(parts[i].Last);
            }
            return story;
        }

        static int ReportBeatScore(NpcContentCatalog catalog, Actor speaker, IList<StoryPart> parts, int index)
        {
            NpcFact fact = parts[index].First;
            NpcEventDefinition definition = catalog.Event(fact.Kind);
            int score = definition == null ? 0 : definition.ReportPriority;
            if (definition != null && definition.ReportConclusion) score += 8;
            if (fact.Units > 0 || !String.IsNullOrEmpty(fact.Resource)) score += 3;
            if (parts[index - 1].Last.Kind != fact.Kind) score += 2;
            for (int i = 0; i < parts.Count; i++)
            {
                if (i == index) continue;
                if (fact.CauseId > 0 && fact.CauseId == parts[i].Last.EventId ||
                    parts[i].First.CauseId > 0 && parts[i].First.CauseId == parts[index].Last.EventId)
                    score += 5;
                if (definition != null && definition.ReportDisputesKinds != null &&
                    fact.EventId == parts[i].Last.EventId &&
                    Array.IndexOf(definition.ReportDisputesKinds, parts[i].Last.Kind) >= 0)
                    score += 8;
            }
            if (speaker.Personality != null && definition != null)
            {
                if (speaker.Personality.HasTrait("timid") &&
                    (definition.Categories & NpcRecordCategory.Combat) != 0) score += 3;
                if (PersonalitySystem.Bias(speaker, NpcPersonalityAxis.Compassion,
                    registry: catalog.Personalities) > 10 &&
                    (definition.Categories & NpcRecordCategory.Help) != 0) score += 3;
            }
            return score;
        }

        static string ReportPlace(NpcFact fact)
        {
            District district = fact.Place.Map.District;
            string place = district == null ? "near " + fact.Place.Map.Name :
                "in district " + World.CoordToString(district.WorldPosition.X, district.WorldPosition.Y);
            Zone building = Zone.BuildingAt(fact.Place);
            if (building != null) place += ", at the " + Zone.BuildingLabel(building.BuildingKind);
            return place;
        }

        static string ReportLead(Actor speaker, NpcFact fact, bool radio = false)
        {
            if (radio)
                return fact.Kind == "claimed_permission" && fact.SubjectId == speaker.PersonalityIdentity ?
                    "The person involved claims that " :
                    fact.Source == NpcKnowledgeSource.Told ? "A secondhand report says that " :
                    fact.Source == NpcKnowledgeSource.Inferred ? "A source believes that " :
                    fact.Source == NpcKnowledgeSource.Participant ? "Someone involved says that " :
                    "A witness says that ";
            return fact.Kind == "claimed_permission" && fact.SubjectId == speaker.PersonalityIdentity ? "I say that " :
                fact.Source == NpcKnowledgeSource.Told ? "I was told that " :
                fact.Source == NpcKnowledgeSource.Inferred ? "As far as I know, " :
                fact.Source == NpcKnowledgeSource.Participant ? "I was involved when " : "I saw that ";
        }
    }
}
