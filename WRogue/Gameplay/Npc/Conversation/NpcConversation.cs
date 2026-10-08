using System;
using System.Collections.Generic;
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
            game.DoSay(target, player, yes ? "Thank you. I'll remember." : "I understand.", RogueGame.Sayflags.IS_FREE_ACTION);
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
            Guid id = speaker.PersonalityIdentity;
            if (fact.SubjectId != id && fact.OtherId != id) return true;
            NpcEventDefinition definition = game.NpcContent.Event(fact.Kind);
            if (definition == null) return true;
            NpcReportActorRole role = definition.ReportActorRole;
            bool ownAction = ((role & NpcReportActorRole.Subject) != 0 && fact.SubjectId == id) ||
                ((role & NpcReportActorRole.Other) != 0 && fact.OtherId == id);
            if (!ownAction) return true;
            switch (definition.SelfReportTone)
            {
                case NpcSelfReportTone.Harmful:
                    return speaker.Personality.HasTrait("cruel") || speaker.Personality.HasTrait("rebellious") ||
                        speaker.Personality.HasTrait("hotheaded") || speaker.Personality.HasTrait("vindictive") ||
                        PersonalitySystem.Bias(speaker, DecisionKind.Law, registry: game.NpcContent.Personalities) <= -15 &&
                        PersonalitySystem.Bias(speaker, DecisionKind.Courage, registry: game.NpcContent.Personalities) >= 10;
                case NpcSelfReportTone.Helpful:
                    return PersonalitySystem.Bias(speaker, DecisionKind.Compassion, registry: game.NpcContent.Personalities) >= 15 ||
                        PersonalitySystem.Bias(speaker, DecisionKind.Group, registry: game.NpcContent.Personalities) >= 15 ||
                        PersonalitySystem.Bias(speaker, DecisionKind.Trade, registry: game.NpcContent.Personalities) >= 15;
                default:
                    return PersonalitySystem.Bias(speaker, DecisionKind.Group, registry: game.NpcContent.Personalities) >= 15 ||
                        PersonalitySystem.Bias(speaker, DecisionKind.Trade, registry: game.NpcContent.Personalities) >= 15;
            }
        }

        public static NpcFact Rumor(RogueGame game, Actor speaker, Actor listener)
        {
            if (speaker.Personality == null || !Session.Get.GamePreset.NpcPersonalitiesEnabled) return null;
            int turn = speaker.Location.Map.LocalTime.TurnCounter;
            NpcFact chosen = null;
            foreach (NpcFact fact in speaker.Personality.Knowledge.Facts)
                if (EligibleFact(game, speaker, fact, turn) && EligibleListener(speaker, listener, fact) &&
                    (chosen == null || fact.EventTurn > chosen.EventTurn)) chosen = fact;
            return chosen;
        }

        public static bool EligibleFact(RogueGame game, Actor speaker, NpcFact fact, int turn)
        { return fact != null && fact.Place.Map != null && fact.Confidence >= 40 && fact.Hops < 3 &&
            turn - fact.EventTurn <= 2 * WorldTime.TURNS_PER_DAY && CanTell(game, speaker, fact); }

        public static bool EligibleListener(Actor speaker, Actor listener, NpcFact fact)
        { return listener != null && listener != speaker && listener.PersonalityIdentity != fact.SourceId &&
            !speaker.Personality.Knowledge.WasTold(fact.EventId, listener.PersonalityIdentity) &&
            (listener.Personality == null || !listener.Personality.Knowledge.Facts.Exists(known =>
                known.EventId == fact.EventId && known.Kind == fact.Kind &&
                known.Confidence >= NpcKnowledgeSystem.ReportConfidence(listener, speaker, fact))); }

        public static void ShareRumor(RogueGame game, Actor speaker, Actor listener, NpcFact fact, bool free)
        {
            if (!CanTell(game, speaker, fact)) return;
            int turn = speaker.Location.Map.LocalTime.TurnCounter;
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
            game.DoSay(speaker, listener, StoryText(game.NpcContent, speaker, reported, lines), RogueGame.Sayflags.IS_STORY | RogueGame.Sayflags.IS_RUMOR |
                (free ? RogueGame.Sayflags.IS_FREE_ACTION : RogueGame.Sayflags.NONE), fact.EventId, fact.StoryId);
            List<Actor> hearers = new List<Actor>();
            foreach (Actor hearer in speaker.Location.Map.Actors)
            {
                if (hearer == speaker || hearer.IsDead || hearer.IsSleeping || !hearer.Model.Abilities.IsIntelligent ||
                    hearer.Model.Abilities.IsUndead || game.Rules.StdDistance(hearer.Location.Position, speaker.Location.Position) > hearer.AudioRange) continue;
                if (hearer.Personality == null) hearer.Personality = new PersonalityState();
                hearers.Add(hearer);
            }
            foreach (NpcFact part in chapter)
            {
                foreach (Actor hearer in hearers)
                {
                    NpcKnowledgeSystem.Hear(game, hearer, speaker, part);
                    speaker.Personality.Knowledge.Told(part.EventId, hearer.PersonalityIdentity, turn);
                }
                speaker.Personality.Knowledge.Told(part.EventId, listener.PersonalityIdentity, turn);
            }
            speaker.Personality.Knowledge.NextTalkTurn = turn + 30;
            NpcEvents.Publish(game, "rumor_shared", speaker, listener, fact.EventId, fact.StoryId);
        }

        public static List<NpcFact> Chapter(RogueGame game, Actor speaker, Actor listener, NpcFact fact, int turn = -1)
        {
            if (turn < 0) turn = speaker.Location.Map.LocalTime.TurnCounter;
            List<NpcFact> chapter = new List<NpcFact> { fact };
            if (!String.IsNullOrEmpty(fact.StoryId))
                foreach (NpcFact related in speaker.Personality.Knowledge.Facts)
                    if (related != fact && related.StoryId == fact.StoryId &&
                        EligibleFact(game, speaker, related, turn) &&
                        (listener == null || EligibleListener(speaker, listener, related)))
                        chapter.Add(related);
            chapter.Sort((a, b) => a.EventTurn != b.EventTurn ? a.EventTurn.CompareTo(b.EventTurn) : a.EventId.CompareTo(b.EventId));
            while (chapter.Count > 10) chapter.RemoveAt(chapter[0] == fact ? 1 : 0);
            return chapter;
        }

        public static string ReportSentence(RogueGame game, Actor speaker, NpcFact fact)
        {
            return ReportLead(speaker, fact) + NpcRecordDescriptions.Report(game.NpcContent, fact) + " " + ReportPlace(fact) + ".";
        }

        struct StoryPart
        {
            public NpcFact First, Last;
            public string Text;
            public StoryPart(NpcFact first, NpcFact last, string text)
            { First = first; Last = last; Text = text; }
        }

        static bool CanGroup(Actor speaker, IList<NpcFact> run, NpcFact next)
        {
            NpcFact first = run[0];
            if (first.SubjectId == Guid.Empty || first.OtherId == Guid.Empty || next.OtherId == Guid.Empty ||
                first.CauseId != 0 || next.CauseId != 0 || first.Kind != next.Kind ||
                first.SubjectId != next.SubjectId || first.ReportSubject != next.ReportSubject ||
                String.IsNullOrEmpty(first.ReportOther) || String.IsNullOrEmpty(next.ReportOther) ||
                ReportLead(speaker, first) != ReportLead(speaker, next) || ReportPlace(first) != ReportPlace(next)) return false;
            foreach (NpcFact prior in run)
                if (prior.OtherId == next.OtherId || prior.ReportOther == next.ReportOther) return false;
            return true;
        }

        public static string StoryText(NpcContentCatalog catalog, Actor speaker, IList<NpcFact> facts, IList<string> lines)
        {
            if (lines.Count == 0) return "";
            var parts = new List<StoryPart>();
            for (int i = 0; i < lines.Count;)
            {
                NpcEventDefinition definition = catalog.Event(facts[i].Kind);
                var run = new List<NpcFact> { facts[i] };
                if (definition != null && definition.SummarizeReports != null)
                    while (i + run.Count < facts.Count && CanGroup(speaker, run, facts[i + run.Count]))
                        run.Add(facts[i + run.Count]);
                if (run.Count > 1)
                {
                    string summary = definition.SummarizeReports(run);
                    if (!String.IsNullOrEmpty(summary))
                    {
                        parts.Add(new StoryPart(run[0], run[run.Count - 1],
                            ReportLead(speaker, run[0]) + summary + " " + ReportPlace(run[0]) + "."));
                        i += run.Count;
                        continue;
                    }
                }
                parts.Add(new StoryPart(facts[i], facts[i], lines[i]));
                i++;
            }
            string story = parts[0].Text;
            string previousPlace = ReportPlace(parts[0].Last);
            for (int i = 1; i < parts.Count; i++)
            {
                NpcFact fact = parts[i].First, previous = parts[i - 1].Last;
                string line = parts[i].Text;
                string lead = ReportLead(speaker, fact);
                bool sameLead = lead == ReportLead(speaker, previous);
                if (sameLead && line.StartsWith(lead, StringComparison.Ordinal))
                    line = line.Substring(lead.Length);
                string currentPlace = ReportPlace(fact);
                string placeSuffix = " " + currentPlace + ".";
                if (currentPlace == previousPlace && line.EndsWith(placeSuffix, StringComparison.Ordinal))
                    line = line.Substring(0, line.Length - placeSuffix.Length) + ".";
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

        static string ReportPlace(NpcFact fact)
        {
            District district = fact.Place.Map.District;
            string place = district == null ? "near " + fact.Place.Map.Name :
                "in district " + World.CoordToString(district.WorldPosition.X, district.WorldPosition.Y);
            Zone building = Zone.BuildingAt(fact.Place);
            if (building != null) place += ", at the " + Zone.BuildingLabel(building.BuildingKind);
            return place;
        }

        static string ReportLead(Actor speaker, NpcFact fact)
        {
            return fact.Kind == "claimed_permission" && fact.SubjectId == speaker.PersonalityIdentity ? "I say that " :
                fact.Source == NpcKnowledgeSource.Told ? "I was told that " :
                fact.Source == NpcKnowledgeSource.Inferred ? "As far as I know, " :
                fact.Source == NpcKnowledgeSource.Participant ? "I was involved when " : "I saw that ";
        }
    }
}
