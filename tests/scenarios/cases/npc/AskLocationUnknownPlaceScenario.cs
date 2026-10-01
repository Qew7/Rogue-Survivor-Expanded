using System.Drawing;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Actions;
using djack.RogueSurvivor.Gameplay.Personality;

static class AskLocationUnknownPlaceScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/ask-location-unknown-place", () => TownScenarioFactory.Arena(4825,
            ".........", ".........", "........."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            NpcIntentSupport.Player(world, 8, 2);
            Actor seeker = NpcIntentSupport.Actor(world, "seeker", 1, 1, "loyal", "protective");
            Actor missing = NpcIntentSupport.Actor(world, "missing", 5, 1);
            Actor listener = NpcIntentSupport.Actor(world, "listener", 2, 1);
            NpcKnownPerson target = new NpcKnownPerson { Id = missing.PersonalityIdentity, Name = missing.UnmodifiedName,
                Place = new Location(world.Map, new Point(5, 1)) };
            NpcIntent goal = NpcStorySystem.StartKnown(seeker, target, world.Game.NpcContent.Capability("seek_companion"));
            Check.Equal(true, goal != null, "a genuine search goal starts for a known person");
            listener.Personality.Knowledge.People.Add(new NpcKnownPerson { Id = missing.PersonalityIdentity,
                Name = missing.UnmodifiedName, Place = default(Location), Confidence = 60 });
            var ask = new ActionNpcAskLocation(seeker, world.Game, listener, goal);
            Check.Equal(true, ask.IsLegal(), "nearby listener can be asked for the missing person");
            ask.Perform();
            Check.Equal("I don't know where they are.", listener.Personality.Reactions[0].Text,
                "unmapped knowledge produces the honest unknown-location reply");
            Check.Equal(null, listener.Personality.Reactions[0].ReportedPerson,
                "an unknown location cannot overwrite the seeker's earlier knowledge");
            Check.Equal(true, NpcIntentSupport.HasEvent(listener, "asked_location"), "the real question is recorded");
            Check.Equal(false, new ActionNpcAskLocation(seeker, world.Game, missing, goal).IsLegal(),
                "the sought person is not an eligible informant");
        });
    }
}
