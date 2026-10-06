using System.Drawing;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Gameplay.AI.Sensors;
using djack.RogueSurvivor.Gameplay.Personality;

static class ExitPerceptionVisibilityScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/exit-perception-visibility", () =>
        {
            string[] rows = new string[30];
            for (int y = 0; y < rows.Length; y++) rows[y] = new string('.', 30);
            return TownScenarioFactory.Arena(4850, rows);
        }, world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            world.Map.Lighting = Lighting.LIT;
            Actor observer = NpcIntentSupport.Actor(world, "observer", 1, 1);
            Map destination = new ScenarioWorld(4851, "....").Map;
            Point near = new Point(1, 2), blocked = new Point(5, 1);
            int range = world.Game.Rules.ActorFOV(observer, world.Map.LocalTime, world.Game.Session.World.Weather);
            Check.Equal(true, range >= 4 && range < 28, "fixture has a usable sight boundary");
            Point distant = new Point(range + 2, 1), diagonal = new Point(range + 1, range + 1);
            world.Map.SetExitAt(near, new Exit(destination, new Point(0, 0)) { IsAnAIExit = true });
            world.Map.SetExitAt(blocked, new Exit(destination, new Point(1, 0)) { IsAnAIExit = true });
            world.Map.SetExitAt(distant, new Exit(destination, new Point(2, 0)) { IsAnAIExit = true });
            world.Map.SetExitAt(diagonal, new Exit(destination, new Point(3, 0)) { IsAnAIExit = true });
            world.SetTile(3, 1, '#');

            var sensor = new LOSSensor(LOSSensor.SensingFilter.ACTORS);
            NpcKnowledgeSystem.Perceive(world.Game, observer, sensor.Sense(world.Game, observer), sensor.FOV);
            Check.Equal(false, sensor.FOV.Contains(diagonal), "diagonal exit lies outside the sensor's round FOV");
            Check.Equal(true, Knows(observer, near), "near visible exit enters NPC knowledge");
            Check.Equal(true, Knows(observer, diagonal), "grid-distance visibility still reveals the diagonal exit");
            Check.Equal(false, Knows(observer, blocked), "wall hides an exit in range");
            Check.Equal(false, Knows(observer, distant), "exit beyond grid sight range stays unknown");

            world.SetTile(3, 1, '.');
            NpcKnowledgeSystem.Perceive(world.Game, observer, sensor.Sense(world.Game, observer), sensor.FOV);
            Check.Equal(true, Knows(observer, blocked), "opening the sight line reveals the exit immediately");
            Check.Equal(false, Knows(observer, distant), "open sight line does not bypass the range limit");
        });
    }

    static bool Knows(Actor observer, Point position)
    { return observer.Personality.Knowledge.Exits.Exists(exit => exit.From.Position == position); }
}
