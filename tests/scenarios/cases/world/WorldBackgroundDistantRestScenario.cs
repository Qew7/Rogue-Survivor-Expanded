using System;
using System.Drawing;
using System.Reflection;
using System.Threading;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;

static class WorldBackgroundDistantRestScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("world/background-distant-rest", () => TownScenarioFactory.Arena(5017,
            ".....", ".....", "....."), world =>
        {
            GamePreset preset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            Session.Get.GamePreset = preset;
            Actor player = NpcIntentSupport.Player(world, 2, 1);
            World city = new World(4);
            AddDistrict(world, city, 1, 1, 5017, world.Map);
            var neighbors = new Map[8];
            int next = 0;
            for (int x = 0; x < 3; x++) for (int y = 0; y < 3; y++)
                if (x != 1 || y != 1)
                    neighbors[next++] = AddDistrict(world, city, x, y, 5020 + x * 3 + y);
            Map distant = AddDistrict(world, city, 3, 3, 5019);
            Session.Get.World = city;
            Type flags = typeof(RogueGame).GetNestedType("SimFlags", BindingFlags.NonPublic);
            object active = Enum.Parse(flags, "NOT_SIMULATING");

            player.ActionPoints = Rules.BASE_ACTION_COST;
            world.Game.DoWait(player);
            Check.Call(world.Game, "AdvancePlay", new[] { typeof(District), flags }, world.Map.District, active);
            foreach (Map neighbor in neighbors)
                Check.Equal(1, neighbor.LocalTime.TurnCounter, "all eight neighbors advance on the live turn");
            Check.Equal(0, distant.LocalTime.TurnCounter, "far district waits for the worker");
            Check.Call(world.Game, "StartSimThread");
            Check.Equal(true, SpinWait.SpinUntil(() => distant.LocalTime.TurnCounter == 1, 5000),
                "worker catches the far district up without another player turn");
            Check.Call(world.Game, "StopSimThread", false);

            player.ActionPoints = Rules.BASE_ACTION_COST;
            world.Game.DoMoveActor(player, new Location(world.Map, new Point(3, 1)));
            Check.Call(world.Game, "AdvancePlay", new[] { typeof(District), flags }, world.Map.District, active);
            foreach (Map neighbor in neighbors)
                Check.Equal(2, neighbor.LocalTime.TurnCounter, "neighbors stay synchronous during ordinary play");
            Check.Equal(1, distant.LocalTime.TurnCounter, "far district remains outside the live square");
            Check.Call(world.Game, "StartSimThread");
            Check.Equal(true, SpinWait.SpinUntil(() => distant.LocalTime.TurnCounter == 2, 5000),
                "background work continues after the first wait and an ordinary move");
            Check.Call(world.Game, "StopSimThread", false);

            preset.DisableDistantSimulationDuringRest = true;
            Session.Get.GamePreset = preset;
            player.ActionPoints = Rules.BASE_ACTION_COST;
            world.Game.DoWait(player);
            Check.Call(world.Game, "AdvancePlay", new[] { typeof(District), flags }, world.Map.District, active);
            Check.Call(world.Game, "StartSimThread");
            Check.Call(world.Game, "StopSimThread", false);
            foreach (Map neighbor in neighbors)
                Check.Equal(3, neighbor.LocalTime.TurnCounter, "neighbors still advance when the option is off");
            Check.Equal(2, distant.LocalTime.TurnCounter, "disabled option does not advance far districts");
        });
    }

    static Map AddDistrict(ScenarioWorld world, World city, int x, int y, int seed, Map entry = null)
    {
        District district = new District(new Point(x, y), DistrictKind.RESIDENTIAL);
        Map map = entry ?? new Map(seed, "district", 5, 3);
        Map sewers = new Map(seed + 100, "sewers", 5, 3);
        for (int row = 0; row < 3; row++) for (int col = 0; col < 5; col++)
        {
            map.SetTileModelAt(col, row, world.Game.GameTiles.FLOOR_ASPHALT);
            sewers.SetTileModelAt(col, row, world.Game.GameTiles.FLOOR_ASPHALT);
        }
        district.EntryMap = map;
        district.SewersMap = sewers;
        city[x, y] = district;
        return map;
    }
}
