using djack.RogueSurvivor.Engine;
static class LongRun1Scenario
{
    public static void Register()
    { ScenarioRunner.Add("npc/long-run-1", () => TownScenarioFactory.Arena(4812,
        "............", "............", "............", "............"), world => NpcLongRunSupport.Run(world, 1)); }
}
