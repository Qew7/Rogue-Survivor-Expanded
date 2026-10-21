using System;
using System.Drawing;
using System.IO;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Actions;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Engine.MapObjects;
using djack.RogueSurvivor.Gameplay.Personality;

static class CacheLossRumorScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/cache-loss-rumor", () => TownScenarioFactory.Arena(4774,
            "....#....", "....#....", "....#...."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            Actor owner = NpcIntentSupport.Actor(world, "Petr", 1, 1, "sociable");
            Actor thief = NpcIntentSupport.Actor(world, "Andrey", 3, 1);
            Actor listener = NpcIntentSupport.Actor(world, "listener", 6, 1);
            NpcIntentSupport.Player(world, 8, 2);
            Point at = new Point(2, 1);
            StorageContainer cache = new StorageContainer("drawer", "closed", "open", 40, 2);
            world.Map.PlaceMapObjectAt(cache, at);
            world.Try(new ActionUseStorageContainer(owner, world.Game, cache, true));
            ItemFood food = NpcIntentSupport.Food(world, owner, 1);
            world.Map.PlaceActorAt(owner, at);
            world.Try(new ActionDropItem(owner, world.Game, food));
            world.Map.PlaceActorAt(owner, new Point(5, 1));
            world.Try(new ActionGetFromContainer(thief, world.Game, at));
            Check.Equal(false, owner.Personality.Knowledge.Facts.Exists(f => f.Kind == "cache_theft"),
                "owner did not witness the original theft");
            world.Map.PlaceActorAt(thief, new Point(3, 2));
            world.Map.PlaceActorAt(owner, new Point(1, 1));
            Check.Equal(true, world.Try(new ActionInspectStorage(owner, world.Game, cache)),
                "owner discovers the missing food");
            NpcFact loss = owner.Personality.Knowledge.Facts.Find(f => f.Kind == "cache_loss_discovered");
            Check.Equal(true, loss != null && loss.OtherId == Guid.Empty,
                "discovery contains no guessed culprit");
            Check.Equal(false, listener.Personality.Knowledge.Facts.Exists(f => f.EventId == loss.EventId),
                "listener across a wall does not witness discovery");
            world.Map.PlaceActorAt(owner, new Point(5, 0));
            world.Map.PlaceActorAt(listener, new Point(6, 0));
            var tell = new ActionNpcTell(owner, world.Game, listener, loss);
            Check.Equal(true, tell.IsLegal(), "owner can tell someone about the loss");
            tell.Perform();
            NpcFact heard = listener.Personality.Knowledge.Facts.Find(f => f.EventId == loss.EventId);
            Check.Equal(NpcKnowledgeSource.Told, heard.Source, "listener learns a report");
            Check.Equal(Guid.Empty, heard.OtherId, "retelling does not invent a thief");
            Check.Equal(false, tell.IsLegal(), "same report is not repeated immediately");

            string path = Path.Combine(Path.GetTempPath(), "cache-loss-rumor-" + Guid.NewGuid().ToString("N"));
            try
            {
                BinarySaveStore.Save(path, world.Map);
                Map loaded = BinarySaveStore.Load<Map>(path);
                loaded.ReconstructAuxiliaryFields();
                Actor saved = NpcIntentSupport.Find(loaded, listener.PersonalityIdentity);
                NpcFact retained = saved.Personality.Knowledge.Facts.Find(f => f.EventId == loss.EventId);
                Check.Equal(Guid.Empty, retained.OtherId, "anonymous report survives save");
                Check.Equal(NpcKnowledgeSource.Told, retained.Source, "hearsay source survives save");
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
                if (File.Exists(path + ".bak")) File.Delete(path + ".bak");
            }
        });
    }
}
