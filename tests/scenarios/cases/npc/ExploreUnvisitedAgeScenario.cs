using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Actions;
using djack.RogueSurvivor.Gameplay.AI;

static class ExploreUnvisitedAgeScenario
{
    sealed class ExploreProbe : CivilianAI
    {
        public ActionBump Explore(RogueGame game, ExplorationData data)
        { return BehaviorExplore(game, data) as ActionBump; }
    }

    public static void Register()
    {
        ScenarioRunner.Add("npc/explore-unvisited-age", () => TownScenarioFactory.Arena(8147,
            ".......", ".......", ".......", ".......", ".......", ".......", "......."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            Actor owner = NpcIntentSupport.Actor(world, "explorer", 3, 3);
            var probe = new ExploreProbe(); probe.TakeControl(owner);
            var explored = new ExplorationData(30, 6);
            explored.AddExplored(owner.Location + Direction.N);
            explored.AddExplored(owner.Location + Direction.E);
            ActionBump action = probe.Explore(world.Game, explored);
            Check.Equal(true, action != null && action.IsLegal(), "exploration chooses a legal move");
            Check.Equal(false, explored.HasExplored(owner.Location + action.Direction), "exploration avoids visited cells");
            Check.Equal(Direction.SE, action.Direction, "unvisited location scoring keeps the seeded choice");
            foreach (Direction direction in Direction.COMPASS)
                explored.AddExplored(owner.Location + direction);
            Check.Equal(null, probe.Explore(world.Game, explored), "all visited neighbors leave no exploration move");
        });
    }
}
