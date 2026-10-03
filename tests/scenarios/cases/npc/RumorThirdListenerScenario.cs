using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Actions;
using djack.RogueSurvivor.Gameplay.Personality;

static class RumorThirdListenerScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/rumor-third-listener", () => TownScenarioFactory.Arena(4841,
            "...#.....", "...#.....", "...#....."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            NpcIntentSupport.Player(world, 8, 2);
            Actor speaker = NpcIntentSupport.Actor(world, "speaker", 2, 1);
            Actor victim = NpcIntentSupport.Actor(world, "victim", 1, 1);
            Actor attacker = NpcIntentSupport.Actor(world, "attacker", 1, 0);
            Actor witness = NpcIntentSupport.Actor(world, "witness", 6, 1);
            PersonalitySystem.Report(world.Game, new SignificantEvent("attack", victim, attacker,
                world.Map, victim.Location.Position, 0));
            NpcFact fact = speaker.Personality.Knowledge.Facts.Find(f => f.Kind == "attack");
            Check.Equal(true, fact != null, "speaker witnessed the original attack");
            Actor recipient = NpcIntentSupport.Actor(world, "recipient", 2, 2);
            var tell = new ActionNpcTell(speaker, world.Game, recipient, fact);
            Check.Equal(true, tell.IsLegal(), "new recipient can hear the rumor");
            tell.Perform();
            Check.Equal(true, witness.Personality.Knowledge.Facts.Exists(f => f.EventId == fact.EventId &&
                f.Source == NpcKnowledgeSource.Told), "third listener learned the spoken fact");
            Check.Equal(true, speaker.Personality.Knowledge.WasTold(fact.EventId, witness.PersonalityIdentity),
                "speaker remembers the third listener as an audience member");
            world.Map.RemoveActor(witness); world.Place(witness, 1, 2);
            var repeat = new ActionNpcTell(speaker, world.Game, witness, fact);
            Check.Equal(false, repeat.IsLegal(), "NPC cannot address the same rumor to an overhearing witness");
            int archived = Session.Get.ResidentRecords.Register(speaker).Entries.Count;
            Check.Equal(false, world.Try(repeat), "repeated address cannot perform speech");
            Check.Equal(archived, Session.Get.ResidentRecords.Register(speaker).Entries.Count,
                "rejected repetition adds no speech to the archive");
            Check.Equal(null, NpcConversation.Rumor(world.Game, speaker, witness),
                "player conversation uses the same audience rule");
        });
    }
}
