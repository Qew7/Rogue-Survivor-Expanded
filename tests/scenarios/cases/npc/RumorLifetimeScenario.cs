using System;
using System.IO;
using System.Linq;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Actions;
using djack.RogueSurvivor.Gameplay.Personality;

static class RumorLifetimeScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/rumor-lifetime", () => TownScenarioFactory.Arena(8205,
            "...#....", "...#....", "...#...."), world =>
        {
            Session original = Session.Get;
            original.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            var city = new World(1);
            city[0, 0] = world.Map.District;
            original.World = city;
            Actor player = NpcIntentSupport.Player(world, 6, 1);
            Actor speaker = NpcIntentSupport.Actor(world, "speaker", 0, 1);
            Actor victim = NpcIntentSupport.Actor(world, "victim", 1, 1);
            Actor attacker = NpcIntentSupport.Actor(world, "attacker", 2, 1);
            speaker.Personality.Opinion(victim.PersonalityIdentity, victim.UnmodifiedName);
            speaker.Personality.Opinion(attacker.PersonalityIdentity, attacker.UnmodifiedName);
            PersonalitySystem.Report(world.Game, new SignificantEvent("attack", victim, attacker,
                world.Map, victim.Location.Position, 0));
            NpcFact first = speaker.Personality.Knowledge.Facts.Single(f => f.Kind == "attack");
            world.Place(speaker, 5, 1);
            new ActionPlayerTalk(player, world.Game, speaker, null).Perform();
            Check.Equal(true, player.Personality.Knowledge.Facts.Any(f => f.EventId == first.EventId),
                "a real player conversation conveys the witnessed attack");

            world.Map.LocalTime.TurnCounter = 1;
            world.Place(speaker, 0, 1);
            PersonalitySystem.Report(world.Game, new SignificantEvent("attack", victim, attacker,
                world.Map, victim.Location.Position, 1));
            NpcFact repeated = speaker.Personality.Knowledge.Facts.Last(f => f.Kind == "attack");
            Check.Equal(true, repeated.EventId != first.EventId, "another incident has a new event ID");
            Check.Equal(NpcConversation.ReportSentence(world.Game, speaker, first),
                NpcConversation.ReportSentence(world.Game, speaker, repeated),
                "the two occurrences have the same spoken report");
            world.Place(speaker, 5, 1);
            Check.Equal(false, NpcConversation.EligibleListener(world.Game, speaker, player, repeated),
                "the same spoken report is not repeated to the player that day");
            int journalCount = player.Personality.HeardJournal.Count;
            NpcConversation.ShareRumor(world.Game, speaker, player, repeated, true);
            Check.Equal(journalCount, player.Personality.HeardJournal.Count,
                "the shared speech path also rejects a repeated report");
            Actor relay = NpcIntentSupport.Actor(world, "relay", 4, 1);
            Check.Equal(true, new ActionNpcTell(speaker, world.Game, relay, repeated).IsLegal(),
                "a different actor can hear the new occurrence");
            NpcFact distinct = new NpcFact { Kind = "shared_food", EventId = Session.Get.NextPersonalityEventId(),
                EventTurn = 1, LearnedTurn = 1, Confidence = 90, Source = NpcKnowledgeSource.Witness,
                SourceId = speaker.PersonalityIdentity, Place = first.Place };
            speaker.Personality.Knowledge.Learn(distinct);
            Check.Equal(true, NpcConversation.EligibleListener(world.Game, speaker, player, distinct),
                "a different report remains available to the same listener");

            world.Map.LocalTime.TurnCounter = 500;
            Check.Equal(true, NpcKnowledgeSystem.Hear(world.Game, relay, speaker, first),
                "another NPC learns the old event later");
            NpcFact learned = relay.Personality.Knowledge.Facts.Single(f => f.EventId == first.EventId);
            Check.Equal(500, learned.LearnedTurn, "the relay's clock starts when they hear it");
            relay.Personality.Knowledge.Learn(learned.Retell(speaker.PersonalityIdentity, 600, 95));
            Check.Equal(500, relay.Personality.Knowledge.Facts.Single(f => f.EventId == first.EventId).LearnedTurn,
                "better evidence does not renew an already learned rumor");

            Guid speakerId = speaker.PersonalityIdentity, playerId = player.PersonalityIdentity;
            Guid relayId = relay.PersonalityIdentity;
            string path = Path.Combine(Path.GetTempPath(), "rumor-lifetime-" + Guid.NewGuid().ToString("N"));
            try
            {
                BinarySaveStore.Save(path, original);
                Session loaded = BinarySaveStore.Load<Session>(path);
                Map map = loaded.World[0, 0].EntryMap;
                map.ReconstructAuxiliaryFields();
                Actor savedSpeaker = NpcIntentSupport.Find(map, speakerId);
                Actor savedPlayer = NpcIntentSupport.Find(map, playerId);
                Actor savedRelay = NpcIntentSupport.Find(map, relayId);
                NpcFact savedFirst = savedSpeaker.Personality.Knowledge.Facts.Single(f => f.EventId == first.EventId);
                NpcFact savedRepeated = savedSpeaker.Personality.Knowledge.Facts.Single(f => f.EventId == repeated.EventId);
                NpcFact savedLearned = savedRelay.Personality.Knowledge.Facts.Single(f => f.EventId == first.EventId);
                Check.Equal(false, NpcConversation.EligibleListener(world.Game, savedSpeaker, savedPlayer, savedRepeated),
                    "the generic recipient history survives save and load");
                savedPlayer.Personality.Knowledge.Facts.Clear();
                map.LocalTime.TurnCounter = WorldTime.TURNS_PER_DAY - 1;
                Check.Equal(false, NpcConversation.EligibleListener(world.Game, savedSpeaker, savedPlayer, savedFirst),
                    "the same recipient remains blocked before 720 turns");
                map.LocalTime.TurnCounter = WorldTime.TURNS_PER_DAY;
                Check.Equal(true, NpcConversation.EligibleListener(world.Game, savedSpeaker, savedPlayer, savedFirst),
                    "a forgotten report may be told again at the one-day boundary");
                map.LocalTime.TurnCounter = 800;
                Check.Equal(false, NpcConversation.EligibleFact(world.Game, savedSpeaker, savedFirst, 800),
                    "the original eyewitness stops spreading the event after one day");
                Check.Equal(true, NpcConversation.EligibleFact(world.Game, savedRelay, savedLearned, 800),
                    "a later listener may still spread it during their own day");
                map.LocalTime.TurnCounter = 1220;
                Check.Equal(true, NpcConversation.EligibleFact(world.Game, savedRelay, savedLearned, 1220),
                    "the later listener can still report exactly one day after learning");
                map.LocalTime.TurnCounter = 1221;
                Check.Equal(false, NpcConversation.EligibleFact(world.Game, savedRelay, savedLearned, 1221),
                    "the later listener's day also ends");
            }
            finally
            {
                Session.Restore(original);
                if (File.Exists(path)) File.Delete(path);
                if (File.Exists(path + ".bak")) File.Delete(path + ".bak");
            }
        });
    }
}
