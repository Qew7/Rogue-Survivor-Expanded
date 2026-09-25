using System;
using System.Drawing;
using System.Reflection;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;

static class MapTurnFullDetailScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("world/map-turn-full-detail", () => TownScenarioFactory.Arena(4531,
            ".....", ".....", "....."), world =>
        {
            Actor player = SkillScenario.Actor(world);
            world.Place(player, 1, 1);
            world.SetPlayer(player);
            Point scentTile = new Point(4, 1);
            world.Map.ModifyScentAt(Odor.LIVING, 100, scentTile);
            int ap = player.ActionPoints;
            int turn = world.Map.LocalTime.TurnCounter;
            Type flags = typeof(RogueGame).GetNestedType("SimFlags", BindingFlags.NonPublic);
            Check.Call(world.Game, "NextMapTurn", new[] { typeof(Map), flags },
                world.Map, Enum.Parse(flags, "NOT_SIMULATING"));
            Check.Equal(true, player.ActionPoints > ap, "full turn regenerates action points");
            Check.Equal(true, world.Map.GetScentByOdorAt(Odor.LIVING, scentTile) < 100,
                "full turn decays map scents");
            Check.Equal(turn + 1, world.Map.LocalTime.TurnCounter, "full turn advances local time");
        });
    }
}
