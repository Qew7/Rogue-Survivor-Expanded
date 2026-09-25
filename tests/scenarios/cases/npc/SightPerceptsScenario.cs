using System.Collections.Generic;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine.AI;
using djack.RogueSurvivor.Gameplay.AI.Sensors;

static class SightPerceptsScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/sight-percepts", () => TownScenarioFactory.Arena(7120,
            "............", "............", "............", "............",
            "............"), world =>
        {
            Actor observer = SkillScenario.Actor(world);
            Actor near = SkillScenario.Actor(world);
            Actor far = SkillScenario.Actor(world);
            world.Place(observer, 1, 2);
            world.Place(near, 3, 2);
            world.Place(far, 11, 2);
            LOSSensor sight = new LOSSensor(LOSSensor.SensingFilter.ACTORS);
            List<Percept> percepts = sight.Sense(world.Game, observer);
            Check.Equal(true, HasActor(percepts, near), "near actor is sensed");
            Check.Equal(false, HasActor(percepts, far), "distant actor is not sensed");
            world.SetTile(2, 2, '#');
            percepts = sight.Sense(world.Game, observer);
            Check.Equal(false, HasActor(percepts, near), "wall removes actor from sight");
            world.SetTile(2, 2, '.');
            percepts = sight.Sense(world.Game, observer);
            Check.Equal(true, HasActor(percepts, near), "opening wall restores sight");
        });
    }

    static bool HasActor(List<Percept> percepts, Actor actor)
    {
        foreach (Percept percept in percepts)
            if (percept.Percepted == actor) return true;
        return false;
    }
}
