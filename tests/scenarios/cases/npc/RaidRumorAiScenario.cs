using System.Drawing;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Gameplay.Personality;

static class RaidRumorAiScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/raid-rumor-ai", () => TownScenarioFactory.Arena(4910,
            "...#.........", "...#.........", "...#........."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            Actor witness = NpcIntentSupport.Actor(world, "witness", 0, 1, "sociable");
            Actor listener = NpcIntentSupport.Actor(world, "listener", 5, 1);
            NpcIntentSupport.Player(world, 8, 2);
            PersonalitySystem.Report(world.Game, new SignificantEvent("raid", null, null,
                world.Map, new Point(1, 1), 0));
            NpcFact fact = witness.Personality.Knowledge.Facts.Find(f => f.Kind == "raid");
            Check.Equal(true, fact != null, "witness has the leaderless raid stimulus");
            Check.Equal(false, listener.Personality.Knowledge.Facts.Exists(f => f.EventId == fact.EventId),
                "wall prevents firsthand knowledge");
            world.Place(witness, 5, 0); world.Place(listener, 6, 0);
            NpcIntentSupport.Turn(world, witness);
            NpcFact heard = listener.Personality.Knowledge.Facts.Find(f => f.EventId == fact.EventId);
            Check.Equal(true, heard != null && heard.Source == NpcKnowledgeSource.Told,
                "production NPC controller chooses to tell a subjectless raid rumor");
            Check.Equal(true, witness.Personality.Knowledge.WasTold(fact.EventId, listener.PersonalityIdentity),
                "AI delivery records its actual recipient to prevent repetition");
        });
    }
}
