using djack.RogueSurvivor.Gameplay;

static class FactionHostilityScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("factions/hostility", () => TownScenarioFactory.Create(4201, false), world =>
        {
            GameFactions f = world.Game.GameFactions;
            Check.Equal(true, f.TheCivilians.IsEnemyOf(f.TheUndeads), "civilians fear undead");
            Check.Equal(true, f.TheUndeads.IsEnemyOf(f.TheCivilians), "undead attack civilians");
            Check.Equal(false, f.TheCivilians.IsEnemyOf(f.ThePolice), "police protect civilians");
            Check.Equal(false, f.ThePolice.IsEnemyOf(f.TheCivilians), "police do not attack civilians");
            Check.Equal(true, f.ThePolice.IsEnemyOf(f.TheGangstas), "police oppose gangstas");
            Check.Equal(true, f.TheGangstas.IsEnemyOf(f.ThePolice), "gangstas oppose police");
        });
    }
}
