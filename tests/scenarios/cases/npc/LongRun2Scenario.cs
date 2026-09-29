using djack.RogueSurvivor.Engine;
static class LongRun2Scenario
{
    public static void Register()
    { ScenarioRunner.Add("npc/long-run-2", () => TownScenarioFactory.Arena(4813,
        "............", "............", "............", "............"), world => NpcLongRunSupport.Run(world, 2)); }
}
