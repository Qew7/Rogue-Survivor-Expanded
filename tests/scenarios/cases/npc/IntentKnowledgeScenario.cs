using System.Drawing;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Gameplay.Personality;

static class IntentKnowledgeScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/intent-knowledge", () => TownScenarioFactory.Arena(4605, "...#...", "...#...", "...#..."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            Session.Get.UniqueActors.TheSewersThing = new UniqueActor();
            NpcIntentSupport.Player(world, 2, 2);
            Actor seeker = NpcIntentSupport.Actor(world, "seeker", 1, 1, "generous");
            Actor target = NpcIntentSupport.Actor(world, "Alex", 2, 1);
            Actor namesake = NpcIntentSupport.Actor(world, "Alex", 0, 1);
            NpcIntentSupport.Food(world, seeker, 3);
            PersonalitySystem.Report(world.Game, new SignificantEvent("helped", seeker, target, world.Map, seeker.Location.Position, 0));
            NpcIntent intent = seeker.Personality.Intents[0]; Location known = intent.LastKnown;
            world.Place(target, 5, 1);
            world.Map.LocalTime.TurnCounter = 31; // old acknowledgment expires before searching
            NpcIntentSupport.Turn(world, seeker);
            Check.Equal(known, intent.LastKnown, "unseen movement does not reveal current target location");
            Check.Equal(0, NpcIntentSupport.FoodUnits(namesake), "same name does not redirect gratitude");
            Check.Equal(0, NpcIntentSupport.FoodUnits(target), "gift is not delivered through a wall");
            world.Game.KillActor(null, target, "scenario", false);
            Check.Equal(false, intent.Finished, "unseen death is not learned remotely");
            world.Map.LocalTime.TurnCounter = intent.Deadline;
            NpcIntentSystem.AdvanceClock(world.Game, world.Map);
            Check.Equal(NpcIntentStatus.Failed, intent.Status, "unresolved search ends at its deadline");
            Check.Equal("deadline expired", intent.Outcome, "failure records the actual known reason");
            Check.Equal(null, intent.LastKnown.Map, "finished intention releases its map reference");
        });
    }
}
