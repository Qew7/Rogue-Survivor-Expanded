using System.Drawing;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Actions;

static class NpcObstacleScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/obstacle", () => new ScenarioWorld(103,
            ".....", "..#..", "....."), world =>
        {
            Actor npc = world.Place("npc", 1, 1);
            npc.Controller = new AvoidWallController();
            Check.Equal(true, world.NpcTurn(npc), "NPC chooses a legal action");
            Check.Equal(new Point(1, 2), npc.Location.Position, "NPC goes around wall");
            Check.Equal(0, npc.ActionPoints, "NPC action consumes AP");
        });
    }

    sealed class AvoidWallController : ActorController
    {
        public override ActorAction GetAction(RogueGame game)
        {
            ActorAction east = new ActionBump(ControlledActor, game, Direction.E);
            return east.IsLegal() ? east : new ActionBump(ControlledActor, game, Direction.S);
        }
    }
}
