using System.Drawing;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Gameplay.AI;

static class PlannedEscapeRunningScenario
{
    static ScenarioWorld World(int seed, int stamina, out Actor civilian)
    {
        ScenarioWorld world = TownScenarioFactory.Arena(seed,
            "############.", "####.#######.", "####.#######.", "...........#.",
            "############.", "############.", "############.");
        Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
        NpcIntentSupport.Player(world, 12, 6);
        civilian = NpcIntentSupport.Actor(world, "civilian", 5, 3, "organized");
        civilian.HitPoints = 1;
        civilian.StaminaPoints = stamina;
        Actor zombie = new Actor(world.Game.GameActors.Zombie, world.Game.GameFactions.TheUndeads,
            "zombie", false, false, 0);
        world.Place(zombie, 7, 3);
        Map refuge = new Map(seed + 100000, "refuge", 3, 3);
        for (int y = 0; y < 3; y++) for (int x = 0; x < 3; x++)
            refuge.SetTileModelAt(x, y, world.Game.GameTiles.FLOOR_ASPHALT);
        world.Map.District.AddUniqueMap(refuge);
        world.Map.SetExitAt(new Point(4, 1), new Exit(refuge, new Point(1, 1)) { IsAnAIExit = true });
        return world;
    }

    public static void Register()
    {
        ScenarioRunner.Add("npc/planned-escape-running", () =>
        {
            Actor ignored;
            return World(7202, 100, out ignored);
        }, world =>
        {
            Actor runner = world.Map.GetActorAt(5, 3);
            int before = runner.StaminaPoints;
            Check.Equal(true, world.NpcTurn(runner), "planned retreat performs a legal move");
            Check.Equal(new Point(4, 2), runner.Location.Position, "runner follows the exit plan");
            Check.Equal(true, runner.IsRunning, "planned retreat enables running when possible");
            Check.Equal(true, runner.StaminaPoints < before, "running uses stamina");

            Actor tired;
            ScenarioWorld exhausted = World(7202, 0, out tired);
            Check.Equal(true, exhausted.NpcTurn(tired), "tired NPC still performs a legal retreat");
            Check.Equal(false, tired.IsRunning, "insufficient stamina prevents running");
        });
    }
}
