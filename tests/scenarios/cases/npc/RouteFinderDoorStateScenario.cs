using System.Drawing;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine.Actions;
using djack.RogueSurvivor.Engine.MapObjects;

static class RouteFinderDoorStateScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/route-finder-door-state", () => TownScenarioFactory.Arena(7129,
            "#####", "#...#", "#####"), world =>
        {
            Actor npc = SkillScenario.Actor(world);
            DoorWindow door = new DoorWindow("door", "closed", "open", "broken", 40);
            world.Map.PlaceMapObjectAt(door, new Point(2, 1));
            RouteFinderProbe route = new RouteFinderProbe(world.Game, npc);
            Point target = new Point(3, 1);
            Check.Equal(false, route.CanReach(target),
                "closed door blocks walking route");
            Check.Equal(true, route.CanReach(target, 1 << 0),
                "door action makes the destination reachable");
            Check.Equal(true, world.Try(new ActionOpenDoor(npc, world.Game, door)),
                "actor opens the door");
            Check.Equal(true, route.CanReach(target),
                "open door changes route immediately");
            Check.Equal(true, world.Try(new ActionCloseDoor(npc, world.Game, door)),
                "actor closes the door");
            Check.Equal(false, route.CanReach(target),
                "closed door blocks the route again");
        });
    }
}
