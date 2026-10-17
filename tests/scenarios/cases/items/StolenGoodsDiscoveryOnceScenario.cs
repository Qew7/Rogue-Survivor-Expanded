using System;
using System.Drawing;
using System.IO;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.AI;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Gameplay.Personality;

static class StolenGoodsDiscoveryOnceScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("items/stolen-goods-discovery-once", () => TownScenarioFactory.Arena(8192,
            ".........", ".........", "........."), world =>
        {
            Session original = Session.Get;
            original.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            Actor owner = NpcIntentSupport.Actor(world, "owner", 4, 1);
            Actor thief = NpcIntentSupport.Actor(world, "thief", 2, 1);
            Actor friend = NpcIntentSupport.Actor(world, "friend", 6, 1);
            friend.Personality.Knowledge.See(owner, 0);
            world.Map.AddXpdBase(new XpdBase(owner, new[] { new Point(2, 1), new Point(3, 1), new Point(4, 1) }));
            ItemFood loot = new ItemFood(world.Game.GameItems.CANNED_FOOD);
            world.Map.DropItemAt(loot, new Point(2, 1));
            world.Game.DoTakeItem(thief, new Point(2, 1), loot);
            Check.Equal(true, loot.IsStolen && !loot.StolenGoodsFound,
                "theft alone is not a discovery");
            world.Game.DoDropItem(thief, loot);
            Point first = thief.Location.Position;
            NpcKnowledgeSystem.Perceive(world.Game, owner, new[] {
                new Percept(world.Map.GetItemsAt(first), 0, new Location(world.Map, first)) });
            Check.Equal(true, loot.StolenGoodsFound,
                "the first informed sighting marks this physical stack as found");
            Check.Equal(1, owner.Personality.Knowledge.Facts.FindAll(f => f.Kind == "stolen_goods_found").Count,
                "the first discovery produces one fact");
            int friendFinds = friend.Personality.Knowledge.Facts.FindAll(f => f.Kind == "stolen_goods_found").Count;
            world.Map.RemoveItemAt(loot, first);
            Point moved = new Point(5, 1);
            world.Map.DropItemAt(loot, moved);
            NpcKnowledgeSystem.Perceive(world.Game, friend, new[] {
                new Percept(world.Map.GetItemsAt(moved), 1, new Location(world.Map, moved)) });
            Check.Equal(friendFinds, friend.Personality.Knowledge.Facts.FindAll(f => f.Kind == "stolen_goods_found").Count,
                "a different informed person at a new location does not rediscover the same supplies");
            world.Game.DoTakeItem(friend, moved, loot);
            Check.Equal(friendFinds, friend.Personality.Knowledge.Facts.FindAll(f => f.Kind == "stolen_goods_found").Count,
                "pickup does not repeat the already reported discovery");
            Check.Equal(true, loot.IsStolen,
                "finding supplies preserves their provenance for a later handoff");
            ItemFood carried = new ItemFood(world.Game.GameItems.CANNED_FOOD);
            carried.MarkStolen(owner.PersonalityIdentity, owner.PersonalityIdentity, owner.UnmodifiedName,
                "second-theft", 1234);
            friend.Inventory.AddAll(carried);
            ItemFood merging = new ItemFood(world.Game.GameItems.CANNED_FOOD);
            merging.CopyTheftFrom(carried);
            world.Map.DropItemAt(merging, moved);
            int beforeMergeFinds = friend.Personality.Knowledge.Facts.FindAll(f => f.Kind == "stolen_goods_found").Count;
            world.Game.DoTakeItem(friend, moved, merging);
            Check.Equal(true, carried.StolenGoodsFound && merging.StolenGoodsFound,
                "a discovery follows supplies even when pickup merges the ground stack into an inventory stack");
            Check.Equal(beforeMergeFinds + 1,
                friend.Personality.Knowledge.Facts.FindAll(f => f.Kind == "stolen_goods_found").Count,
                "the merged stack produces one discovery");
            world.Game.ReportStolenGoods(friend, null, carried, "stolen_goods_found");
            Check.Equal(beforeMergeFinds + 1,
                friend.Personality.Knowledge.Facts.FindAll(f => f.Kind == "stolen_goods_found").Count,
                "the merged carried stack cannot be reported as newly found again");
            Guid friendId = friend.PersonalityIdentity;
            string path = Path.Combine(Path.GetTempPath(), "stolen-found-" + Guid.NewGuid().ToString("N"));
            try
            {
                BinarySaveStore.Save(path, original);
                Session loaded = BinarySaveStore.Load<Session>(path);
                Map map = loaded.World[0, 0].EntryMap; map.ReconstructAuxiliaryFields();
                Actor restored = NpcIntentSupport.Find(map, friendId);
                Item kept = restored.Inventory.GetFirstByModel(world.Game.GameItems.CANNED_FOOD);
                Check.Equal(true, kept.IsStolen && kept.StolenGoodsFound,
                    "the discovered state and provenance survive save and load");
            }
            finally
            {
                Session.Restore(original);
                if (File.Exists(path)) File.Delete(path);
                if (File.Exists(path + ".bak")) File.Delete(path + ".bak");
            }
        });
    }
}
