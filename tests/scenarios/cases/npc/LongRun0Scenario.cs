using djack.RogueSurvivor.Engine;
static class LongRun0Scenario
{
    public static void Register()
    { ScenarioRunner.Add("npc/long-run-0", () => TownScenarioFactory.Arena(4811,
        "............", "............", "............", "............"), world => NpcLongRunSupport.Run(world, 0)); }
}
