using System;
using System.IO;
using System.Drawing;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Items;

static class RecordsLifetimeItemsScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/records-lifetime-items", () => TownScenarioFactory.Arena(4581, ".....", "....."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            Session.Get.UniqueActors.TheSewersThing = new UniqueActor();
            Actor player = new Actor(world.Game.GameActors.MaleCivilian, world.Game.GameFactions.TheCivilians, "player", true, false, 0);
            player.Controller = new PlayerController(); world.Place(player, 4, 0); world.SetPlayer(player);
            Actor actor = SkillScenario.Actor(world); actor.Name = "collector"; actor.Personality = new PersonalityState(); world.Place(actor, 1, 1);
            actor.Inventory.MaxCapacity = 1;
            int limit = world.Game.GameItems.CANNED_FOOD.StackingLimit;
            ItemFood initial = new ItemFood(world.Game.GameItems.CANNED_FOOD); initial.Quantity = limit - 1;
            actor.Inventory.AddAll(initial);
            ItemFood pickup = new ItemFood(world.Game.GameItems.CANNED_FOOD); pickup.Quantity = limit + 3;
            world.Map.DropItemAt(pickup, actor.Location.Position);
            world.Game.DoTakeItem(actor, actor.Location.Position, pickup);
            Check.Equal((long)limit, actor.Inventory.TotalReceived, "partial pickup counts only transferred unit plus starting items");
            world.Game.DoTakeItem(actor, actor.Location.Position, pickup);
            Check.Equal((long)limit, actor.Inventory.TotalReceived, "failed pickup does not increase lifetime count");
            actor.Inventory.Consume(initial);
            world.Game.DoTakeItem(actor, actor.Location.Position, pickup);
            Check.Equal((long)limit + 1, actor.Inventory.TotalReceived, "consumption does not subtract; later refill counts");
            actor.Inventory.Consume(initial);
            ItemFood gift = new ItemFood(world.Game.GameItems.CANNED_FOOD); player.Inventory.AddAll(gift);
            world.Game.DoGiveItemTo(player, actor, gift);
            Check.Equal((long)limit + 2, actor.Inventory.TotalReceived, "real gift is counted once through pickup");
            ItemRangedWeapon pistol = new ItemRangedWeapon(world.Game.GameItems.PISTOL); player.Inventory.AddAll(pistol);
            Check.Call(world.Game, "SwapActorItems", new[] { typeof(Actor), typeof(Item), typeof(Actor), typeof(Item) },
                actor, initial, player, pistol);
            Check.Equal((long)limit + 3, actor.Inventory.TotalReceived, "successful trade counts incoming item units");
            world.Game.KillActor(null, actor, "scenario", false);
            ResidentRecord record = Session.Get.ResidentRecords.Register(actor);
            Check.Equal((long)limit + 3, record.ItemsReceived, "death snapshot keeps lifetime acquisitions");
            string path = Path.Combine(Path.GetTempPath(), "items-records-" + Guid.NewGuid().ToString("N"));
            try
            {
                BinarySaveStore.Save(path, Session.Get);
                RecordsSave saved = RecordsReader.Load(path);
                Check.Equal((long)limit + 3, RecordsReader.Residents(saved).Find(r => r.Identity == actor.PersonalityIdentity).ItemsReceived,
                    "removed NPC acquisitions survive archive-only read");
                Session loaded = BinarySaveStore.LoadExact<Session>(path);
                Check.Equal((long)limit + 3, new System.Collections.Generic.List<ResidentRecord>(loaded.ResidentRecords.Residents)
                    .Find(r => r.Identity == actor.PersonalityIdentity).ItemsReceived, "full load restores same archive");
            }
            finally { if (File.Exists(path)) File.Delete(path); }
        });
    }
}
