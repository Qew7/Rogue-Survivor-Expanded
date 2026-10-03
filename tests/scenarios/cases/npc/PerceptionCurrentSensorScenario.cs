using System.Collections.Generic;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.AI;
using djack.RogueSurvivor.Gameplay.AI.Sensors;
using djack.RogueSurvivor.Gameplay.Personality;

static class PerceptionCurrentSensorScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/perception-current-sensor", () => TownScenarioFactory.Arena(4845,
            "......", "......", "......"), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            Actor observer = NpcIntentSupport.Actor(world, "observer", 1, 1);
            Actor target = NpcIntentSupport.Actor(world, "target", 2, 1);
            var sensor = new LOSSensor(LOSSensor.SensingFilter.ACTORS);
            List<Percept> old = sensor.Sense(world.Game, observer);
            world.Map.RemoveActor(target); world.Place(target, 4, 1);
            NpcKnowledgeSystem.Perceive(world.Game, observer, old, sensor.FOV);
            Check.Equal(null, observer.Personality.Knowledge.Person(target.PersonalityIdentity),
                "a moved actor is not accepted at a stale sensor location");
            NpcKnowledgeSystem.Perceive(world.Game, observer, sensor.Sense(world.Game, observer), sensor.FOV);
            Check.Equal(true, observer.Personality.Knowledge.Person(target.PersonalityIdentity) != null,
                "current LOS sensor percept enters NPC knowledge");
            world.Map.LocalTime.TurnCounter = 1;
            NpcKnowledgeSystem.Perceive(world.Game, observer, old, sensor.FOV);
            Check.Equal(0, observer.Personality.Knowledge.Person(target.PersonalityIdentity).SeenTurn,
                "memorized percepts do not become fresh sightings");
        });
    }
}
