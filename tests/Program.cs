using System;

class Program
{
    static void Main()
    {
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
        Console.WriteLine("All unit tests passed");
    }
}
