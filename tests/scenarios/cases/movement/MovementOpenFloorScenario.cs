using System.Drawing;
using djack.RogueSurvivor.Data;

static class MovementOpenFloorScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("movement/open-floor", () => new ScenarioWorld(101,
            ".....", ".....", "....."), world =>
        {
            Actor player = world.Place("player", 1, 1);
            Check.Equal(true, world.Bump(player, Direction.E), "east bump is legal");
            Check.Equal(new Point(2, 1), player.Location.Position, "player moved");
            Check.Same(player, world.Map.GetActorAt(2, 1), "map records player");
            Check.Equal(0, player.ActionPoints, "movement spent one action");
        });
    }
}
