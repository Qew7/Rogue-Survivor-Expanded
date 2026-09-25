using System.Collections.Generic;
using System.Drawing;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine.Actions;
using djack.RogueSurvivor.Engine.AI;
using djack.RogueSurvivor.Engine.MapObjects;
using djack.RogueSurvivor.Gameplay.AI.Sensors;

static class SightDoorStateScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/sight-door-state", () => TownScenarioFactory.Arena(7127,
            "#######", "#.....#", "#######"), world =>
        {
            Actor observer = SkillScenario.Actor(world);
            Actor target = SkillScenario.Actor(world);
            world.Map.PlaceActorAt(target, new Point(5, 1));
            world.Map.PlaceActorAt(observer, new Point(2, 1));
            world.Map.Lighting = Lighting.LIT;
            DoorWindow door = new DoorWindow("door", "closed", "open", "broken", 40);
            world.Map.PlaceMapObjectAt(door, new Point(3, 1));
            LOSSensor sight = new LOSSensor(LOSSensor.SensingFilter.ACTORS);
            Check.Equal(false, CanSee(sight.Sense(world.Game, observer), target),
                "closed door blocks sight");
            Check.Equal(true, world.Try(new ActionOpenDoor(observer, world.Game, door)),
                "door can open without moving the observer");
            Check.Equal(true, CanSee(sight.Sense(world.Game, observer), target),
                "open door changes sight in the same turn");
            Check.Equal(true, world.Try(new ActionCloseDoor(observer, world.Game, door)),
                "door can close again");
            Check.Equal(false, CanSee(sight.Sense(world.Game, observer), target),
                "closed door hides target again");
        });
    }

    static bool CanSee(List<Percept> percepts, Actor actor)
    {
        foreach (Percept percept in percepts)
            if (percept.Percepted == actor) return true;
        return false;
    }
}
