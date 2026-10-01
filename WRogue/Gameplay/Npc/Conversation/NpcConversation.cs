using System;
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

        public static NpcFact Rumor(Actor speaker, Actor listener)
        {
            if (speaker.Personality == null || !Session.Get.GamePreset.NpcPersonalitiesEnabled) return null;
            int turn = speaker.Location.Map.LocalTime.TurnCounter;
            NpcFact chosen = null;
            foreach (NpcFact fact in speaker.Personality.Knowledge.Facts)
                if (fact.Place.Map != null && fact.Confidence >= 40 && fact.Hops < 3 &&
                    turn - fact.EventTurn <= 2 * WorldTime.TURNS_PER_DAY &&
                    !speaker.Personality.Knowledge.WasTold(fact.EventId, listener.PersonalityIdentity) &&
                    (listener.Personality == null || !listener.Personality.Knowledge.Facts.Exists(known =>
                        known.EventId == fact.EventId && known.Kind == fact.Kind)) &&
                    (chosen == null || fact.EventTurn > chosen.EventTurn)) chosen = fact;
            return chosen;
        }

        public static void ShareRumor(RogueGame game, Actor speaker, Actor listener, NpcFact fact, bool free)
        {
            string report = NpcRecordDescriptions.Report(game.NpcContent, fact);
            District district = fact.Place.Map.District;
            string place = district == null ? "near " + fact.Place.Map.Name :
                "in district " + World.CoordToString(district.WorldPosition.X, district.WorldPosition.Y);
            Zone building = Zone.BuildingAt(fact.Place);
            if (building != null) place += ", at the " + Zone.BuildingLabel(building.BuildingKind);
            game.DoSay(speaker, listener, (fact.Source == NpcKnowledgeSource.Told ? "I was told that " :
                fact.Source == NpcKnowledgeSource.Inferred ? "As far as I know, " : "I saw that ") + report +
                " " + place + ".", RogueGame.Sayflags.IS_STORY | RogueGame.Sayflags.IS_RUMOR |
                (free ? RogueGame.Sayflags.IS_FREE_ACTION : RogueGame.Sayflags.NONE), fact.EventId, fact.StoryId);
            foreach (Actor hearer in speaker.Location.Map.Actors)
            {
                if (hearer == speaker || hearer.IsDead || hearer.IsSleeping || !hearer.Model.Abilities.IsIntelligent ||
                    hearer.Model.Abilities.IsUndead || game.Rules.StdDistance(hearer.Location.Position, speaker.Location.Position) > hearer.AudioRange) continue;
                if (hearer.Personality == null) hearer.Personality = new PersonalityState();
                NpcKnowledgeSystem.Hear(game, hearer, speaker, fact);
            }
            int turn = speaker.Location.Map.LocalTime.TurnCounter;
            speaker.Personality.Knowledge.Told(fact.EventId, listener.PersonalityIdentity, turn);
            speaker.Personality.Knowledge.NextTalkTurn = turn + 30;
            NpcEvents.Publish(game, "rumor_shared", speaker, listener, fact.EventId, fact.StoryId);
        }
    }
}
