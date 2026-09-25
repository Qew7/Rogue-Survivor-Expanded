using System.Text;
using djack.RogueSurvivor.Data;

static class SeedDeterminismScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("generation/seed-determinism",
            () => TownScenarioFactory.Create(7122, false), world =>
            {
                string first = Signature(world.Map);
                ScenarioWorld again = TownScenarioFactory.Create(7122, false);
                Check.Equal(first, Signature(again.Map),
                    "same seed reproduces tiles, interiors and zones");
                ScenarioWorld different = TownScenarioFactory.Create(7123, false);
                Check.Equal(false, first == Signature(different.Map),
                    "different seed produces another layout");
            });
    }

    static string Signature(Map map)
    {
        StringBuilder result = new StringBuilder();
        for (int y = 0; y < map.Height; y++)
            for (int x = 0; x < map.Width; x++)
            {
                Tile tile = map.GetTileAt(x, y);
                result.Append(tile.Model.ID).Append(tile.IsInside ? 'i' : 'o').Append(';');
            }
        foreach (Zone zone in map.Zones)
            result.Append(zone.Name).Append(zone.Bounds.ToString());
        return result.ToString();
    }
}
