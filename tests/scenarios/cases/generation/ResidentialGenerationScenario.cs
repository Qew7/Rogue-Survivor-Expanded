static class ResidentialGenerationScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("generation/residential", () => TownScenarioFactory.Create(4201, false), world =>
        {
            Check.Equal(40, world.Map.Width, "town width");
            Check.Equal(40, world.Map.Height, "town height");
            int walkable = 0;
            int walls = 0;
            for (int y = 0; y < world.Map.Height; y++)
                for (int x = 0; x < world.Map.Width; x++)
                {
                    if (world.Map.GetTileAt(x, y).Model.IsWalkable) walkable++;
                    else walls++;
                }
            Check.Equal(true, walkable > 0, "town has walkable tiles");
            Check.Equal(true, walls > 0, "town has buildings");
        });
    }
}
