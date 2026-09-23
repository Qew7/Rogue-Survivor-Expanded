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
        Console.WriteLine("All unit tests passed");
    }
}
