using System.Collections.Generic;
using System.Drawing;
using System.Reflection;
using djack.RogueSurvivor.Data;

static class MouseEnemyStopScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("movement/mouse-enemy-stop", () => TownScenarioFactory.Arena(4543,
            ".......", ".......", "......."), world =>
        {
            Actor player = SkillScenario.Actor(world);
            world.Place(player, 0, 1);
            world.SetPlayer(player);
            world.Game.ComputeViewRect(player.Location.Position);
            Actor friend = new Actor(world.Game.GameActors.MaleCivilian,
                world.Game.GameFactions.TheCivilians, "friend", false, false, 0);
            world.Place(friend, 1, 1);
            Check.Call(world.Game, "UpdatePlayerFOV", player);
            Check.Equal(false, Check.Call(world.Game, "HasVisibleMouseMoveEnemy", player),
                "visible friendly actor permits travel");
            Actor enemy = new Actor(world.Game.GameActors.Zombie,
                world.Game.GameFactions.TheUndeads, "enemy", false, false, 0);
            world.Place(enemy, 2, 1);
            Check.Call(world.Game, "UpdatePlayerFOV", player);
            Check.Equal(true, Check.Call(world.Game, "HasVisibleMouseMoveEnemy", player),
                "visible enemy interrupts travel");
            FieldInfo steps = world.Game.GetType().GetField("m_MouseMoveSteps",
                BindingFlags.Instance | BindingFlags.NonPublic);
            steps.SetValue(world.Game, new List<Point> { new Point(0, 2) });
            Check.Equal(false, Check.Call(world.Game, "ContinueMouseMove", player),
                "automatic travel yields control when enemy appears");
            Check.Equal(null, steps.GetValue(world.Game), "remaining route is canceled");
            Check.Equal(new Point(0, 1), player.Location.Position, "player does not take another step");
            world.Map.PlaceActorAt(friend, new Point(1, 2));
            world.Map.SetTileModelAt(1, 1, world.Game.GameTiles.WALL_BRICK);
            Check.Call(world.Game, "UpdatePlayerFOV", player);
            Check.Equal(false, Check.Call(world.Game, "HasVisibleMouseMoveEnemy", player),
                "enemy behind wall does not interrupt travel");
        });
    }
}
