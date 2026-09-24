using djack.RogueSurvivor.Data;

static class CivilianDecisionScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/civilian-decision", () => TownScenarioFactory.Create(4201, true), world =>
        {
            Check.Equal(true, world.Map.CountActors > 0, "inhabitants generated");
            Actor civilian = null;
            foreach (Actor actor in world.Map.Actors) { civilian = actor; break; }
            Check.Equal(true, civilian.Controller != null, "civilian has a real AI controller");
            int before = civilian.ActionPoints;
            Check.Equal(true, world.NpcTurn(civilian), "civilian chose a legal action");
            Check.Equal(true, civilian.ActionPoints < before, "civilian acted and spent AP");
            Check.Same(civilian, world.Map.GetActorAt(civilian.Location.Position),
                "civilian remains registered on map after its decision");
        });
    }
}
