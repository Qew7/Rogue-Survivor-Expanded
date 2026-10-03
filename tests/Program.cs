using System;

class Program
{
    static int Main(string[] args)
    {
        if (args.Length == 3 && args[0] == "--save-budget-worker") return SaveBudgetRunner.RunWorker(args[1], args[2]);
        if (args.Length == 2 && args[0] == "--check-save-budget") { SaveBudgetRunner.Run(args[1]); return 0; }
        ScenarioRunner.RegisterAll();
        SkillScenario.AssertCoverage();
        if (args.Length == 2 && args[0] == "--audit-save")
        {
            SaveStructureAudit.Run(args[1]);
            return 0;
        }
        if (args.Length == 2 && args[0] == "--bench-save")
        {
            SavePerformanceBenchmarks.Run(args[1]);
            return 0;
        }
        if (args.Length == 1 && args[0] == "--bench")
        {
            PerformanceBenchmarks.Run();
            return 0;
        }
        if (args.Length == 1 && args[0] == "--bench-ai")
        {
            AIAndGenerationBenchmarks.Run();
            return 0;
        }
        if (args.Length == 1 && args[0] == "--bench-npc-turn")
        {
            NpcTurnBenchmarks.Run();
            return 0;
        }
        if (args.Length == 1 && args[0] == "--bench-post-0d243e")
        {
            PostBaselineBenchmarks.Run();
            return 0;
        }
        if (args.Length == 1 && args[0] == "--bench-npc-safety")
        {
            NpcSafetyBenchmarks.Run();
            return 0;
        }
        if (args.Length == 1 && args[0] == "--bench-xpd")
        {
            XpdBaseBenchmarks.Run();
            return 0;
        }
        if (args.Length == 1)
        {
            if (args[0] == "--list") { ScenarioRunner.List(); return 0; }
            return ScenarioRunner.Run(args[0]);
        }
        if (args.Length != 0)
        {
            Console.Error.WriteLine("Usage: UnitTests.exe [--list|--all|--bench|--bench-ai|--bench-npc-turn|--bench-post-0d243e|--bench-npc-safety|--bench-xpd|--bench-save copied-save-path|--check-save-budget copied-save-path|--audit-save copied-save-path|scenario-name]");
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
        SaveBudgetTests.Run();
        SaveStructureAuditTests.Run();
        SaveGameVersionTests.Run();
        XpdFoodOrderMigrationTests.Run();
        HintsSaveTests.Run();
        InputReaderTests.Run();
        SimulationWorkerTests.Run();
        GDIPlusCanvasResourceTests.Run();
        GameImagesGrayLevelTests.Run();
        GameImagesRealAssetTests.Run();
        FovRadiusEquivalenceTests.Run();
        OverlayCollectionTests.Run();
        ManualNavigatorTests.Run();
        MovementScenarioTests.Run();
        if (ScenarioRunner.Run("--all") != 0) return 1;
        Console.WriteLine("All unit tests passed");
        return 0;
    }
}
