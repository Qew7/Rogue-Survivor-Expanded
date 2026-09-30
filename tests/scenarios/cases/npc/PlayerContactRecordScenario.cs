using System;
using System.Collections.Generic;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Items;

static class PlayerContactRecordScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/player-contact-record", () => TownScenarioFactory.Arena(4821,
            ".....", ".....", "....."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            Actor player = NpcIntentSupport.Player(world, 1, 1);
            Actor neighbor = NpcIntentSupport.Actor(world, "neighbor", 2, 1);
            world.Game.DoChat(player, neighbor);
            Check.Equal(true, player.Personality.Person(neighbor.PersonalityIdentity) != null,
                "chat creates a neutral but visible contact");
            IList<string> lines = (IList<string>)Check.Call(world.Game, "RelationshipLines",
                new[] { typeof(Actor) }, player);
            Check.Equal(true, String.Join(" ", new List<string>(lines).ToArray()).Contains("neighbor: neutral"),
                "contact appears in Shift+I");
            ItemFood food = NpcIntentSupport.Food(world, player, 1);
            ItemMedicine medicine = new ItemMedicine(world.Game.GameItems.BANDAGE);
            Check.Equal(true, neighbor.Inventory.AddAll(medicine), "neighbor carries exchange item");
            Check.Call(world.Game, "SwapActorItems",
                new[] { typeof(Actor), typeof(Item), typeof(Actor), typeof(Item) },
                player, food, neighbor, medicine);
            Check.Equal(true, player.Inventory.Contains(medicine) && neighbor.Inventory.Contains(food),
                "real trade swaps the items");
            Check.Equal(2, player.Personality.Person(neighbor.PersonalityIdentity).Trust,
                "completed trade adds trust");
            Check.Equal(true, Session.Get.ResidentRecords.Register(neighbor).Entries.Count > 1,
                "neighbor archive records the social exchange");
        });
    }
}
