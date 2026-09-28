using System.Drawing;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Gameplay.Personality;

static class StorySearchScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/story-search", () => TownScenarioFactory.Arena(4622, ".....", ".....", "....."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            NpcIntentSupport.Player(world, 4, 2);
            Actor seeker = NpcIntentSupport.Actor(world, "seeker", 1, 1, "loyal", "protective");
            Actor companion = NpcIntentSupport.Actor(world, "companion", 2, 1);
            Actor informant = NpcIntentSupport.Actor(world, "informant", 0, 0);
            seeker.AddFollower(companion);
            Map annex = new ScenarioWorld(4623, "....", "....", "....").Map;
            Session.Get.World[0, 0].AddUniqueMap(annex);
            world.Map.SetExitAt(new Point(3, 1), new Exit(annex, new Point(0, 1)) { IsAnAIExit = true });
            annex.SetExitAt(new Point(0, 1), new Exit(world.Map, new Point(3, 1)) { IsAnAIExit = true });
            NpcIntentSupport.Turn(world, seeker);
            world.Map.RemoveActor(companion); annex.PlaceActorAt(companion, new Point(1, 1));
            if (seeker.Location.Position != new Point(1, 1)) world.Place(seeker, 1, 1);
            world.Map.RemoveActor(informant); annex.PlaceActorAt(informant, new Point(2, 1)); annex.LocalTime.TurnCounter = 10;
            NpcIntentSupport.Turn(new ScenarioWorld(4623, annex, world.Game), informant);
            annex.RemoveActor(informant); world.Place(informant, 2, 1); world.Map.LocalTime.TurnCounter = 40;
            NpcIntentSupport.Turn(world, seeker);
            NpcIntent goal = NpcIntentSupport.Intent(seeker, "seek_companion");
            Check.Equal(true, goal != null && NpcIntentSupport.HasEvent(seeker, "asked_location"), "missing contact creates a real search and question");
            Check.Same(world.Map, goal.LastKnown.Map, "question alone does not reveal another NPC's knowledge");
            NpcIntentSupport.Turn(world, informant);
            Check.Same(annex, seeker.Personality.Knowledge.Person(companion.PersonalityIdentity).Place.Map, "spoken answer supplies the remembered map");
            for (int i = 0; i < 12 && !goal.Finished; i++)
            { seeker.Location.Map.LocalTime.TurnCounter = 100 + i; NpcIntentSupport.Turn(new ScenarioWorld(4622, seeker.Location.Map, world.Game), seeker); }
            Check.Equal(NpcIntentStatus.Completed, goal.Status, "real controller follows a known exit and reunites");
            Check.Same(annex, seeker.Location.Map, "search actually changes maps");
            Check.Equal(true, NpcIntentSupport.HasEvent(companion, "reunited"), "found person experiences the encounter");
            MemoryInstance memory = null;
            foreach (MemoryInstance pending in seeker.Personality.Memories) if (pending.Id == "found_a_companion") memory = pending;
            annex.LocalTime.TurnCounter = memory.ResolveTurn; PersonalitySystem.ResolveDue(world.Game, annex);
            Check.Equal(true, seeker.Personality.HasTrait("protector"), "protective searcher develops the memory outcome");
        });
    }
}
