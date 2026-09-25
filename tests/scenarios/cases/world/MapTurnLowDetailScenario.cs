using System;
using System.Reflection;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Tasks;

static class MapTurnLowDetailScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("world/map-turn-low-detail", () => TownScenarioFactory.Arena(4532,
            ".....", ".....", "....."), world =>
        {
            Actor player = SkillScenario.Actor(world);
            world.Place(player, 1, 1);
            world.SetPlayer(player);
            world.Map.GetTileAt(2, 1).AddDecoration("timer marker");
            world.Map.AddTimer(new TaskRemoveDecoration(1, 2, 1, "timer marker"));
            int ap = player.ActionPoints;
            int turn = world.Map.LocalTime.TurnCounter;
            Type flags = typeof(RogueGame).GetNestedType("SimFlags", BindingFlags.NonPublic);
            Check.Call(world.Game, "NextMapTurn", new[] { typeof(Map), flags },
                world.Map, Enum.Parse(flags, "LODETAIL_TURN"));
            Check.Equal(ap, player.ActionPoints, "low detail skips actor regeneration");
            Check.Equal(false, world.Map.GetTileAt(2, 1).HasDecoration("timer marker"),
                "timers still run in low detail");
            Check.Equal(0, world.Map.CountTimers, "completed timer is removed");
            Check.Equal(turn + 1, world.Map.LocalTime.TurnCounter, "low detail advances local time");
        });
    }
}
