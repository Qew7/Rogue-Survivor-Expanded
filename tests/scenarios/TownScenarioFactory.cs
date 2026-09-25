using System.Drawing;
using System.Reflection;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Gameplay.Generators;

static class TownScenarioFactory
{
    public static ScenarioWorld Arena(int seed, params string[] rows)
    {
        ScenarioWorld town = Create(seed, false);
        ScenarioWorld floor = new ScenarioWorld(seed, rows);
        District district = Session.Get.World[0, 0];
        district.EntryMap = floor.Map;
        Session.Get.CurrentMap = floor.Map;
        return new ScenarioWorld(seed, floor.Map, town.Game);
    }

    public static ScenarioWorld Create(int seed, bool populate)
    {
        typeof(Session).GetField("s_TheSession", BindingFlags.Static | BindingFlags.NonPublic)
            .SetValue(null, null);
        Session.Get.Seed = seed;
        typeof(Session).GetField("m_GameDiceRoller", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(Session.Get, new DiceRoller(seed));
        Session.Get.NextAutoSaveTime = int.MaxValue;
        foreach (PropertyInfo property in typeof(UniqueMaps).GetProperties())
            if (property.PropertyType == typeof(UniqueMap))
                property.SetValue(Session.Get.UniqueMaps, new UniqueMap(), null);
        djack.RogueSurvivor.Logger.CreateFile();
        RogueGame game = new RogueGame(new ScenarioUI());
        Check.Call(game, "LoadData");
        GameOptions options = RogueGame.Options;
        options.MaxCivilians = populate ? 1 : 0;
        options.MaxUndeads = 0;
        typeof(RogueGame).GetField("s_Options", BindingFlags.Static | BindingFlags.NonPublic)
            .SetValue(null, options);
        BaseTownGenerator.Parameters parameters = BaseTownGenerator.DEFAULT_PARAMS;
        parameters.MapWidth = 40;
        parameters.MapHeight = 40;
        parameters.District = new District(new Point(0, 0), DistrictKind.RESIDENTIAL);
        parameters.GeneratePoliceStation = false;
        parameters.GenerateHospital = false;
        Map map = populate ? new StdTownGenerator(game, parameters).Generate(seed) :
            new BaseTownGenerator(game, parameters).Generate(seed);
        parameters.District.EntryMap = map;
        World world = new World(1);
        world[0, 0] = parameters.District;
        Session.Get.World = world;
        Session.Get.CurrentMap = map;
        return new ScenarioWorld(seed, map, game);
    }
}
