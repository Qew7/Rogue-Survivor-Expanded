using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Actions;
using djack.RogueSurvivor.Gameplay;

static class MovementScenarioTests
{
    public static void Run()
    {
        GameTiles tiles = new GameTiles();
        Map map = new Map(17, "test street", 5, 5);
        for (int x = 0; x < 5; x++)
            for (int y = 0; y < 5; y++)
                map.SetTileModelAt(x, y, tiles[GameTiles.IDs.FLOOR_ASPHALT]);

        Actor actor = (Actor)FormatterServices.GetUninitializedObject(typeof(Actor));
        actor.ActionPoints = Rules.BASE_ACTION_COST;
        map.PlaceActorAt(actor, new Point(2, 2));
        RogueGame game = (RogueGame)FormatterServices.GetUninitializedObject(typeof(RogueGame));
        typeof(RogueGame).GetField("m_Rules", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(game, new Rules(new DiceRoller(123)));

        ActionBump walk = new ActionBump(actor, game, Direction.E);
        Check.Equal(true, walk.IsLegal(), "east step is legal");
        walk.Perform();
        Check.Equal(new Point(3, 2), actor.Location.Position, "bump moves one tile");
        Check.Equal(0, actor.ActionPoints, "movement spends one action");

        map.SetTileModelAt(4, 2, tiles[GameTiles.IDs.WALL_BRICK]);
        ActionBump wall = new ActionBump(actor, game, Direction.E);
        Check.Equal(false, wall.IsLegal(), "wall blocks bump");
        Check.Equal(new Point(3, 2), actor.Location.Position, "blocked bump stays put");

        string path = Path.Combine(Path.GetTempPath(), "rogue-map-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            BinarySaveStore.Save(path, map);
            Map loaded = (Map)BinarySaveStore.Load(path, null);
            loaded.ReconstructAuxiliaryFields();
            Check.Equal(true, loaded.GetActorAt(new Point(3, 2)) != null, "save restores actor position");
            Check.Equal(false, loaded.GetTileAt(4, 2).Model.IsWalkable, "save restores wall");
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
            if (File.Exists(path + ".bak")) File.Delete(path + ".bak");
        }

        World world = new World(1);
        District district = new District(new Point(0, 0), DistrictKind.RESIDENTIAL);
        district.EntryMap = map;
        world[0, 0] = district;
        Session session = (Session)FormatterServices.GetUninitializedObject(typeof(Session));
        session.Seed = 123;
        session.World = world;
        session.CurrentMap = map;
        session.GameDiceRoller.Roll(0, 100);
        path = Path.Combine(Path.GetTempPath(), "rogue-session-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            djack.RogueSurvivor.Logger.CreateFile();
            Session.Save(session, path, Session.SaveFormat.FORMAT_BIN);
            int expected = session.GameDiceRoller.Roll(0, 100);
            Check.Equal(true, Session.Load(path, Session.SaveFormat.FORMAT_BIN), "session loads");
            Check.Equal(true, Session.Get.CurrentMap.GetActorAt(new Point(3, 2)) != null,
                "session restores map actor");
            Check.Equal(true, Object.ReferenceEquals(Session.Get.CurrentMap,
                Session.Get.World[0, 0].EntryMap), "shared map reference survives save");
            Check.Equal(true, Object.ReferenceEquals(Session.Get.World[0, 0],
                Session.Get.CurrentMap.District), "district cycle survives save");
            Check.Equal(expected, Session.Get.GameDiceRoller.Roll(0, 100),
                "session resumes random sequence");

            using (FileStream legacy = File.Create(path))
                new BinaryFormatter().Serialize(legacy, session);
            Check.Equal(true, Session.Load(path, Session.SaveFormat.FORMAT_BIN),
                "legacy session format loads");
            Check.Equal(true, Session.Get.CurrentMap.GetActorAt(new Point(3, 2)) != null,
                "legacy session restores map actor");
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
            if (File.Exists(path + ".bak")) File.Delete(path + ".bak");
        }
    }
}
