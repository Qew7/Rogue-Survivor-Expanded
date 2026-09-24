using System;

class Program
{
    static int Main(string[] args)
    {
        ScenarioRunner.RegisterAll();
        SkillScenario.AssertCoverage();
        if (args.Length == 1)
        {
            if (args[0] == "--list") { ScenarioRunner.List(); return 0; }
            return ScenarioRunner.Run(args[0]);
        }
        if (args.Length != 0)
        {
            Console.Error.WriteLine("Usage: UnitTests.exe [--list|--all|scenario-name]");
            return 2;
        }
        GameTests.Run();
        MouseMoveTests.Run();
        AITests.Run();
        RulesTests.Run();
        GenerationTests.Run();
        CatalogTests.Run();
        ModelIdTests.Run();
        CommandCatalogTests.Run();
        RandomStateTests.Run();
        SaveStoreTests.Run();
        HintsSaveTests.Run();
        InputReaderTests.Run();
        SimulationWorkerTests.Run();
        ManualNavigatorTests.Run();
        MovementScenarioTests.Run();
        if (ScenarioRunner.Run("--all") != 0) return 1;
        Console.WriteLine("All unit tests passed");
        return 0;
    }
}
