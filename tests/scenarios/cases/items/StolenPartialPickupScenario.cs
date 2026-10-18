using System;
using System.Drawing;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Items;

static class StolenPartialPickupScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("items/stolen-partial-pickup", () => TownScenarioFactory.Arena(5943,
            "......", "......", "......"), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            NpcIntentSupport.Player(world, 0, 0);
            Actor owner = NpcIntentSupport.Actor(world, "owner", 4, 1);
            Actor thief = NpcIntentSupport.Actor(world, "thief", 2, 1);
            world.Map.AddXpdBase(new XpdBase(owner, new[] { new Point(2, 1), new Point(4, 1) }));

            string story = "base-theft:" + owner.PersonalityIdentity.ToString("N") + ":0";
            ItemFood carried = new ItemFood(world.Game.GameItems.CANNED_FOOD);
            carried.Quantity = carried.Model.StackingLimit - 1;
            carried.MarkStolen(owner.PersonalityIdentity, owner.PersonalityIdentity, owner.UnmodifiedName, story, 0);
            thief.Inventory.AddAll(carried);
            thief.Inventory.MaxCapacity = 1;

            ItemFood ground = new ItemFood(world.Game.GameItems.CANNED_FOOD) { Quantity = 3 };
            world.Map.DropItemAt(ground, new Point(2, 1));
            world.Game.DoTakeItem(thief, new Point(2, 1), ground);
            Check.Equal(carried.Model.StackingLimit, carried.Quantity, "one unit joins the matching stolen stack");
            Check.Equal(2, ground.Quantity, "untaken units remain at the base");
            Check.Equal(false, ground.IsStolen, "untaken units keep the owner's clean provenance");
            Check.Equal(true, carried.IsStolen, "taken units remain marked as stolen");
            Check.Equal(1, thief.Personality.Knowledge.Facts.Find(f => f.Kind == "base_theft").Units,
                "theft reports only the unit actually taken");

            world.Game.DoTakeItem(owner, new Point(2, 1), ground);
            Check.Equal(false, owner.Personality.Knowledge.Facts.Exists(f => f.Kind == "stolen_goods_found"),
                "owner cannot discover their own untaken supplies as stolen goods");
        });
    }
}
