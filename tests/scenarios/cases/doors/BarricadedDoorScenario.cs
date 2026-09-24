using System.Drawing;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine.Actions;
using djack.RogueSurvivor.Engine.MapObjects;

static class BarricadedDoorScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("doors/barricaded", () => TownScenarioFactory.Arena(4310,
            ".....", ".....", "....."), world =>
        {
            Actor actor = SkillScenario.Actor(world);
            world.Map.PlaceActorAt(actor, new Point(1, 1));
            DoorWindow door = new DoorWindow("door", "closed", "open", "broken", 40);
            world.Map.PlaceMapObjectAt(door, new Point(2, 1));
            door.BarricadePoints = 10;
            Check.Equal(false, world.Try(new ActionOpenDoor(actor, world.Game, door)),
                "barricade prevents opening");
            Check.Equal(true, door.IsClosed, "blocked attempt leaves door closed");
            door.BarricadePoints = 0;
            Check.Equal(true, world.Try(new ActionOpenDoor(actor, world.Game, door)),
                "removed barricade permits opening");
            Check.Equal(true, door.IsOpen, "door opens after barricade removal");
        });
    }
}
