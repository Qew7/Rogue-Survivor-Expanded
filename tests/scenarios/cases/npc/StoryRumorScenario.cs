using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Actions;
using djack.RogueSurvivor.Gameplay.Personality;

static class StoryRumorScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/story-rumor", () => TownScenarioFactory.Arena(4620, "...#.....", "...#.....", "...#....."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            NpcIntentSupport.Player(world, 8, 2);
            Actor witness = NpcIntentSupport.Actor(world, "witness", 0, 1, "sociable");
            Actor victim = NpcIntentSupport.Actor(world, "victim", 1, 1);
            Actor attacker = NpcIntentSupport.Actor(world, "Alex", 2, 1);
            Actor listener = NpcIntentSupport.Actor(world, "Alex", 5, 1, "timid");
            PersonalitySystem.Report(world.Game, new SignificantEvent("attack", victim, attacker, world.Map, victim.Location.Position, 0));
            NpcFact fact = witness.Personality.Knowledge.Facts[0];
            Check.Equal(NpcKnowledgeSource.Witness, fact.Source, "event is witnessed rather than heard");
            Check.Equal(false, listener.Personality.HasKnowledge, "wall prevents original knowledge");
            world.Place(victim, 5, 0); world.Place(attacker, 6, 0); world.Place(listener, 2, 2);
            NpcIntentSupport.Turn(world, witness);
            NpcFact heard = listener.Personality.Knowledge.Facts.Find(f => f.EventId == fact.EventId);
            Check.Equal(NpcKnowledgeSource.Told, heard.Source, "real spoken action transmits hearsay");
            Check.Equal(70, heard.Confidence, "hearsay loses confidence");
            Check.Equal(attacker.PersonalityIdentity, heard.OtherId, "namesake does not replace the reported aggressor");
            Check.Equal(witness.PersonalityIdentity, heard.SourceId, "listener retains the actual source");
            Check.Equal(true, NpcIntentSupport.HasEvent(listener, "rumor_shared"), "conversation has its own observed event");
            Check.Equal(false, NpcIntentSupport.HasEvent(listener, "attack"), "hearsay is not invented eyewitness evidence");
            var repeated = new ActionNpcTell(witness, world.Game, listener, fact);
            int ap = witness.ActionPoints; repeated.Perform();
            Check.Equal(false, repeated.IsLegal(), "same report is not repeated to the same listener");
            Check.Equal(ap, witness.ActionPoints, "rejected retelling spends no AP");
            NpcIntentSupport.Turn(world, listener);
            Check.Equal(true, NpcIntentSupport.Intent(listener, "avoid_reported_threat") != null, "timid listener generates a response from heard facts");
        });
    }
}
