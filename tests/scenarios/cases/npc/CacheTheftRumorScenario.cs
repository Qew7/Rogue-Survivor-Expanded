using System;
using System.Drawing;
using System.IO;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Actions;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Engine.MapObjects;
using djack.RogueSurvivor.Gameplay.Personality;

static class CacheTheftRumorScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/cache-theft-rumor", () => TownScenarioFactory.Arena(4775,
            "....#....", "....#....", "....#...."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            world.Map.Lighting = Lighting.LIT;
            Actor owner = NpcIntentSupport.Actor(world, "Petr", 1, 1, "sociable");
            Actor thief = NpcIntentSupport.Actor(world, "Vasily", 3, 2);
            owner.Personality.Opinion(thief.PersonalityIdentity, thief.UnmodifiedName);
            Actor listener = NpcIntentSupport.Actor(world, "listener", 6, 1);
            NpcIntentSupport.Player(world, 8, 2);
            Point at = new Point(2, 1);
            StorageContainer cache = new StorageContainer("drawer", "closed", "open", 40, 2);
            cache.IsMaterialTransparent = true;
            world.Map.PlaceMapObjectAt(cache, at);
            world.Try(new ActionUseStorageContainer(owner, world.Game, cache, true));
            ItemFood food = NpcIntentSupport.Food(world, owner, 1);
            world.Map.PlaceActorAt(owner, at);
            world.Try(new ActionDropItem(owner, world.Game, food));
            world.Map.PlaceActorAt(owner, new Point(1, 1));
            Check.Equal(true, world.Try(new ActionGetFromContainer(thief, world.Game, at)),
                "Vasily takes food in front of Petr");
            NpcFact witnessed = owner.Personality.Knowledge.Facts.Find(f => f.Kind == "cache_theft");
            Check.Equal(true, witnessed != null && witnessed.NamesSubject,
                "eyewitness can name the person they saw");
            Check.Equal(false, listener.Personality.Knowledge.Facts.Exists(f => f.EventId == witnessed.EventId),
                "listener across the wall lacks firsthand knowledge");
            world.Map.PlaceActorAt(owner, new Point(5, 0));
            world.Map.PlaceActorAt(listener, new Point(6, 0));
            var tell = new ActionNpcTell(owner, world.Game, listener, witnessed);
            Check.Equal(true, tell.IsLegal(), "owner can describe the witnessed theft");
            tell.Perform();
            NpcFact heard = listener.Personality.Knowledge.Facts.Find(f => f.EventId == witnessed.EventId);
            Check.Equal(NpcKnowledgeSource.Told, heard.Source, "listener holds hearsay");
            Check.Equal(thief.PersonalityIdentity, heard.SubjectId, "report names the witnessed thief");
            RelationshipRecord opinion = listener.Personality.Person(thief.PersonalityIdentity);
            int grievance = opinion == null ? 0 : opinion.Grievance;
            Check.Equal(false, tell.IsLegal(), "same story cannot be immediately retold");
            Check.Equal(grievance, listener.Personality.Person(thief.PersonalityIdentity).Grievance,
                "repeating the same fact does not amplify reputation");

            string path = Path.Combine(Path.GetTempPath(), "cache-theft-rumor-" + Guid.NewGuid().ToString("N"));
            try
            {
                BinarySaveStore.Save(path, world.Map);
                Map loaded = BinarySaveStore.Load<Map>(path);
                loaded.ReconstructAuxiliaryFields();
                Actor saved = NpcIntentSupport.Find(loaded, listener.PersonalityIdentity);
                NpcFact retained = saved.Personality.Knowledge.Facts.Find(f => f.EventId == witnessed.EventId);
                Check.Equal(NpcKnowledgeSource.Told, retained.Source, "report source survives save");
                Check.Equal(thief.PersonalityIdentity, retained.SubjectId, "named witness account survives save");
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
                if (File.Exists(path + ".bak")) File.Delete(path + ".bak");
            }
        });
    }
}
