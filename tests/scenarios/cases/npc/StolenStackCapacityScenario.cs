using System;
using System.Collections.Generic;
using System.Drawing;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Actions;
using djack.RogueSurvivor.Engine.AI;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Gameplay.AI;

static class StolenStackCapacityScenario
{
    sealed class PickupProbe : CivilianAI
    {
        public ActorAction ChooseGroundItem(RogueGame game, Percept percept)
        {
            Percept last = null;
            return BehaviorGoGetInterestingItems(game, new List<Percept> { percept },
                false, false, "cannot get it", false, ref last);
        }
        public bool Avoids(Point point) { return IsTileTaboo(point); }
    }

    public static void Register()
    {
        ScenarioRunner.Add("npc/stolen-stack-capacity", () => TownScenarioFactory.Arena(5945,
            ".....", ".....", "....."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            Actor npc = NpcIntentSupport.Actor(world, "collector", 1, 1);
            var ai = new PickupProbe();
            npc.Controller = ai;
            ItemFood clean = new ItemFood(world.Game.GameItems.CANNED_FOOD);
            Check.Equal(true, clean.CanStackMore, "fixture has a partial stack");
            npc.Inventory.AddAll(clean);
            npc.Inventory.MaxCapacity = 1;
            Point at = new Point(2, 1);
            ItemFood stolen = new ItemFood(world.Game.GameItems.CANNED_FOOD);
            stolen.MarkStolen(Guid.NewGuid(), Guid.NewGuid(), "victim", "theft", 1);
            world.Map.DropItemAt(stolen, at);
            Percept sight = new Percept(world.Map.GetItemsAt(at), world.Map.LocalTime.TurnCounter,
                new Location(world.Map, at));
            Check.Equal(false, npc.Inventory.CanAddAtLeastOne(stolen),
                "the clean partial stack cannot receive stolen food");
            Check.Equal(false, world.Game.Rules.CanActorGetItem(npc, stolen),
                "the real pickup rule rejects the stolen stack with no free slot");
            Check.Equal(null, ai.ChooseGroundItem(world.Game, sight),
                "NPC does not plan a pickup it cannot complete");
            Check.Equal(false, ai.Avoids(at),
                "impossible stolen pickup does not mark a useful tile as taboo");

            world.Map.RemoveItemAt(stolen, at);
            ItemFood moreClean = new ItemFood(world.Game.GameItems.CANNED_FOOD);
            world.Map.DropItemAt(moreClean, at);
            sight = new Percept(world.Map.GetItemsAt(at), world.Map.LocalTime.TurnCounter,
                new Location(world.Map, at));
            ActorAction approach = ai.ChooseGroundItem(world.Game, sight);
            Check.Equal(true, approach != null && world.Try(approach) && npc.Location.Position == at,
                "NPC approaches matching clean food");
            ActorAction pickup = ai.ChooseGroundItem(world.Game, sight);
            Check.Equal(true, pickup is ActionTakeItem && world.Try(pickup),
                "NPC takes matching clean food into the partial stack");
            Check.Equal(2, clean.Quantity, "legal pickup increases the existing stack");

            Actor hungry = NpcIntentSupport.Actor(world, "hungry", 3, 1);
            var hungryAi = new PickupProbe();
            hungry.Controller = hungryAi;
            int capacity = world.Game.Rules.ActorMaxInv(hungry);
            for (int i = 0; i < capacity; i++)
                Check.Equal(true, hungry.Inventory.AddAll(new ItemMedicine(world.Game.GameItems.BANDAGE)
                    { Quantity = world.Game.GameItems.BANDAGE.StackingLimit }), "fixture fills each slot");
            Check.Equal(true, hungry.Inventory.IsFull, "hungry NPC starts with a full inventory");
            hungry.FoodPoints = 0;
            Point foodAt = hungry.Location.Position;
            ItemFood dinner = new ItemFood(world.Game.GameItems.CANNED_FOOD);
            world.Map.DropItemAt(dinner, foodAt);
            Percept foodSight = new Percept(world.Map.GetItemsAt(foodAt), world.Map.LocalTime.TurnCounter,
                new Location(world.Map, foodAt));
            Check.Equal(true, hungryAi.IsInterestingItemToOwn(world.Game, dinner, BaseAI.ItemSource.GROUND_STACK),
                "hungry NPC wants the visible food");
            ActorAction makeRoom = hungryAi.ChooseGroundItem(world.Game, foodSight);
            Check.Equal(true, makeRoom is ActionDropItem && world.Try(makeRoom),
                "NPC without food can still free a slot for food");
            ActorAction takeFood = hungryAi.ChooseGroundItem(world.Game, foodSight);
            Check.Equal(true, takeFood is ActionTakeItem && world.Try(takeFood),
                "NPC takes the food after freeing a slot");
            Check.Equal(true, hungry.Inventory.Contains(dinner), "food reaches the inventory");
        });
    }
}
