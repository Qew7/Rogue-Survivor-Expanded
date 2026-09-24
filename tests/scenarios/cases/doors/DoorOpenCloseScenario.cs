using System.Drawing;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine.Actions;
using djack.RogueSurvivor.Engine.MapObjects;

static class DoorOpenCloseScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("doors/open-close", () => TownScenarioFactory.Arena(4301,
            ".....", ".....", "....."), world =>
        {
            Actor actor = SkillScenario.Actor(world);
            world.Map.PlaceActorAt(actor, new Point(1, 1));
            DoorWindow door = new DoorWindow("door", "closed", "open", "broken", 40);
            world.Map.PlaceMapObjectAt(door, new Point(2, 1));
            Check.Equal(true, door.IsClosed, "door starts closed");
            Check.Equal(false, door.IsWalkable, "closed door blocks path");
            Check.Equal(true, world.Try(new ActionOpenDoor(actor, world.Game, door)), "open action legal");
            Check.Equal(true, door.IsOpen, "door opens");
            Check.Equal(true, door.IsWalkable, "open door permits movement");
            Check.Equal(true, world.Try(new ActionCloseDoor(actor, world.Game, door)), "close action legal");
            Check.Equal(true, door.IsClosed, "door closes again");
            Check.Equal(false, door.IsWalkable, "closed door blocks path again");
        });
    }
}
