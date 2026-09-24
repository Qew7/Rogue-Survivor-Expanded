using System.Drawing;
using djack.RogueSurvivor.Data;

static class MovementWallScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("movement/wall", () => new ScenarioWorld(102,
            ".....", "..#..", "....."), world =>
        {
            Actor player = world.Place("player", 1, 1);
            Check.Equal(false, world.Bump(player, Direction.E), "wall rejects bump");
            Check.Equal(new Point(1, 1), player.Location.Position, "player stays put");
            world.SetTile(2, 1, '.');
            Check.Equal(true, world.Bump(player, Direction.E), "opening path permits bump");
            Check.Equal(new Point(2, 1), player.Location.Position, "player enters opened tile");
        });
    }
}
