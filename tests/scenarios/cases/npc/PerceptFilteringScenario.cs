using System.Collections.Generic;
using System.Drawing;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine.AI;

static class PerceptFilteringScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/percept-filtering", () => TownScenarioFactory.Arena(7125,
            "....", "...."), world =>
        {
            Actor observer = SkillScenario.Actor(world);
            Actor friend = SkillScenario.Actor(world);
            ChoiceProbeAI ai = new ChoiceProbeAI();
            observer.Controller = ai;
            Percept local = new Percept(friend, 0, friend.Location);
            ScenarioWorld other = new ScenarioWorld(7126, "....", "....");
            Actor remote = new Actor(world.Game.GameActors.MaleCivilian,
                world.Game.GameFactions.TheCivilians, "remote", false, false, 0);
            other.Place(remote, 1, 0);
            Percept distant = new Percept(remote, 0, remote.Location);
            Check.Equal(1, ai.Classify(world.Game, new List<Percept> { local }),
                "same-map actor is classified");
            Check.Equal(1, ai.Classify(world.Game, new List<Percept> { distant, local }),
                "other map actor is excluded");
            Check.Equal(0, ai.Classify(world.Game, new List<Percept> { distant }),
                "other map alone produces no candidates");
        });
    }
}
