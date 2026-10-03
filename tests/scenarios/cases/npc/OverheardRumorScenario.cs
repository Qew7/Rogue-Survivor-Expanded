using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Actions;
using djack.RogueSurvivor.Gameplay.Personality;

static class OverheardRumorScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/overheard-rumor", () => TownScenarioFactory.Arena(4705,
            "...#.........", "...#.........", "...#........."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            NpcIntentSupport.Player(world, 5, 1);
            Actor speaker = NpcIntentSupport.Actor(world, "speaker", 2, 1);
            Actor victim = NpcIntentSupport.Actor(world, "victim", 1, 1);
            Actor attacker = NpcIntentSupport.Actor(world, "attacker", 1, 0);
            Actor hidden = NpcIntentSupport.Actor(world, "hidden", 6, 1);
            PersonalitySystem.Report(world.Game, new SignificantEvent("attack", victim, attacker,
                world.Map, victim.Location.Position, 0));
            Check.Equal(false, NpcIntentSupport.HasEvent(hidden, "attack"), "the wall blocks the original event");
            NpcFact fact = speaker.Personality.Knowledge.Facts.Find(f => f.Kind == "attack");
            Check.Equal(true, fact != null, "speaker has a witnessed fact to report");
            Actor recipient = NpcIntentSupport.Actor(world, "recipient", 2, 2);
            var tell = new ActionNpcTell(speaker, world.Game, recipient, fact);
            Check.Equal(true, tell.IsLegal(), "speaker can tell an adjacent person");
            speaker.ActionPoints = Rules.BASE_ACTION_COST;
            tell.Perform();
            Check.Equal(0, speaker.ActionPoints, "spoken rumor costs a turn");
            Check.Equal(true, hidden.Personality.Knowledge.Facts.Exists(f => f.EventId == fact.EventId && f.Source == NpcKnowledgeSource.Told),
                "NPC behind the wall learns actual hearsay rather than becoming an eyewitness");
            ResidentRecord record = Session.Get.ResidentRecords.Register(hidden);
            bool heard = false;
            foreach (ResidentEntry entry in record.Entries)
                if (entry.Kind == "heard_rumor" && entry.CauseId == fact.EventId && entry.Text.Contains("I saw that")) heard = true;
            Check.Equal(true, heard, "Read Records stores the heard rumor and its source event");
        });
    }
}
