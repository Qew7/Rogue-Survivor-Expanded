static class SaveBudgetScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("storage/save-budget", () => new ScenarioWorld(SaveBudgetFixture.Seed, "."), world => SaveBudgetRunner.Run());
    }
}
