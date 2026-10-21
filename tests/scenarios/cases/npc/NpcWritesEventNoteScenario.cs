using System.Drawing;
using System;
using System.IO;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Actions;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Engine.MapObjects;

static class NpcWritesEventNoteScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/writes-event-note", () => TownScenarioFactory.Arena(4875,
            "........", "........", "........"), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            Actor owner = NpcIntentSupport.Actor(world, "owner", 1, 1, "kind");
            Actor thief = NpcIntentSupport.Actor(world, "thief", 3, 1);
            owner.Personality.Opinion(thief.PersonalityIdentity, thief.UnmodifiedName);
            NpcIntentSupport.Player(world, 7, 2);
            StorageContainer cache = new StorageContainer("drawer", "closed", "open", 40, 3);
            cache.IsMaterialTransparent = true;
            world.Map.PlaceMapObjectAt(cache, new Point(2, 1));
            world.Try(new ActionUseStorageContainer(owner, world.Game, cache, true));
            world.Map.PlaceActorAt(owner, cache.Location.Position);
            world.Try(new ActionDropItem(owner, world.Game, NpcIntentSupport.Food(world, owner, 1)));
            world.Map.PlaceActorAt(owner, new Point(1, 1));
            world.Try(new ActionGetFromContainer(thief, world.Game, cache.Location.Position));
            NpcFact witnessed = owner.Personality.Knowledge.Facts.Find(f => f.Kind == "cache_theft");
            Check.Equal(true, witnessed != null, "author has a real witnessed event");
            world.Map.PlaceActorAt(thief, new Point(7, 0));
            ItemNote paper = new ItemNote(world.Game.GameItems.NOTE);
            owner.Inventory.AddAll(paper);
            for (int turn = 0; turn < 15 && paper.IsBlank; turn++)
            {
                owner.ActionPoints = Rules.BASE_ACTION_COST;
                world.NpcTurn(owner);
                world.Map.LocalTime.TurnCounter++;
            }
            Check.Equal(false, paper.IsBlank, "NPC chooses to write a witnessed event");
            Check.Equal(NoteTopic.Warning, paper.Topic, "known theft becomes a warning");
            Check.Equal(witnessed.EventId, paper.Claim.EventId, "note cites the real event");
            Check.Equal(true, owner.Personality.Knowledge.HasWrittenNote("cache_theft:" + witnessed.EventId),
                "author remembers this written event");
            string path = Path.Combine(Path.GetTempPath(), "npc-event-written-" + Guid.NewGuid().ToString("N"));
            try
            {
                BinarySaveStore.Save(path, world.Map);
                Map loaded = BinarySaveStore.Load<Map>(path);
                loaded.ReconstructAuxiliaryFields();
                Actor saved = NpcIntentSupport.Find(loaded, owner.PersonalityIdentity);
                Check.Equal(true, saved.Personality.Knowledge.HasWrittenNote("cache_theft:" + witnessed.EventId),
                    "written event key survives save and load");
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
                if (File.Exists(path + ".bak")) File.Delete(path + ".bak");
            }
        });
    }
}
