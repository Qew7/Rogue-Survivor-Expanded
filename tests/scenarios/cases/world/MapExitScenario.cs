using System.Drawing;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine.Actions;

static class MapExitScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("world/map-exit", () => TownScenarioFactory.Arena(4318,
            ".....", ".....", "....."), world =>
        {
            Actor actor = SkillScenario.Actor(world);
            actor.Controller = new PlayerController();
            world.Map.PlaceActorAt(actor, new Point(1, 1));
            world.SetPlayer(actor);
            Point exitPoint = actor.Location.Position;
            Check.Equal(false, world.Game.Rules.CanActorUseExit(actor, exitPoint),
                "no exit means no transition");
            Map basement = new Map(4319, "basement", 3, 3);
            for (int x = 0; x < 3; x++)
                for (int y = 0; y < 3; y++)
                    basement.SetTileModelAt(x, y, world.Game.GameTiles.FLOOR_ASPHALT);
            world.Map.District.SewersMap = basement;
            world.Map.SetExitAt(exitPoint, new Exit(basement, new Point(1, 1)));
            Check.Equal(true, world.Try(new ActionUseExit(actor, exitPoint, world.Game)),
                "exit action legal");
            Check.Same(basement, actor.Location.Map, "actor entered destination map");
            Check.Equal(new Point(1, 1), actor.Location.Position, "actor reached destination tile");
        });
    }
}
