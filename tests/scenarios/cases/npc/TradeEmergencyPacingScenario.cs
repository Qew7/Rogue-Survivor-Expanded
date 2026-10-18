using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Gameplay.AI;

static class TradeEmergencyPacingScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/trade-emergency-pacing", () => TownScenarioFactory.Arena(8204,
            ".........", ".........", "........."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            Actor hungry = NpcIntentSupport.Actor(world, "hungry", 1, 1);
            Actor wounded = NpcIntentSupport.Actor(world, "wounded", 5, 1);
            Actor firstRefuser = NpcIntentSupport.Actor(world, "first refuser", 2, 1);
            Actor secondRefuser = NpcIntentSupport.Actor(world, "second refuser", 6, 1);
            Actor foodSeller = NpcIntentSupport.Actor(world, "food seller", 0, 1);
            Actor medicineSeller = NpcIntentSupport.Actor(world, "medicine seller", 4, 1);
            hungry.Inventory.AddAll(new ItemMedicine(world.Game.GameItems.BANDAGE));
            wounded.Inventory.AddAll(new ItemFood(world.Game.GameItems.CANNED_FOOD));
            firstRefuser.Inventory.AddAll(new ItemFood(world.Game.GameItems.GROCERIES));
            secondRefuser.Inventory.AddAll(new ItemMedicine(world.Game.GameItems.MEDIKIT));
            foodSeller.Inventory.AddAll(new ItemFood(world.Game.GameItems.GROCERIES));
            medicineSeller.Inventory.AddAll(new ItemMedicine(world.Game.GameItems.MEDIKIT));
            ((CivilianAI)firstRefuser.Controller).Directives.CanTrade = false;
            ((CivilianAI)secondRefuser.Controller).Directives.CanTrade = false;

            world.Game.DoTrade(hungry, firstRefuser);
            world.Game.DoTrade(wounded, secondRefuser);
            Check.Equal(false, world.Game.Rules.CanActorInitiateTradeWith(hungry, foodSeller),
                "a healthy NPC cannot trade during the general pause");
            Check.Equal(false, world.Game.Rules.CanActorInitiateTradeWith(wounded, medicineSeller),
                "an uninjured NPC cannot trade during the general pause");

            hungry.FoodPoints = 0;
            wounded.HitPoints = 1;
            Check.Equal(true, world.Game.Rules.CanActorInitiateTradeWith(hungry, foodSeller),
                "starvation without food permits an early food trade");
            Check.Equal(false, world.Game.Rules.CanActorInitiateTradeWith(hungry, medicineSeller),
                "starvation does not permit an unrelated trade");
            Check.Equal(false, world.Game.Rules.CanActorInitiateTradeWith(hungry, firstRefuser),
                "survival does not bypass the six-hour pause with the refuser");
            Check.Equal(true, world.Game.Rules.CanActorInitiateTradeWith(wounded, medicineSeller),
                "critical injury without medicine permits an early medicine trade");
            Check.Equal(false, world.Game.Rules.CanActorInitiateTradeWith(wounded, foodSeller),
                "critical injury does not permit an unrelated trade");
            Check.Equal(false, world.Game.Rules.CanActorInitiateTradeWith(wounded, secondRefuser),
                "critical injury does not bypass the pause with the refuser");

            hungry.Inventory.AddAll(new ItemFood(world.Game.GameItems.CANNED_FOOD));
            wounded.Inventory.AddAll(new ItemMedicine(world.Game.GameItems.MEDIKIT));
            Check.Equal(false, world.Game.Rules.CanActorInitiateTradeWith(hungry, foodSeller),
                "available food ends the starvation exception");
            Check.Equal(false, world.Game.Rules.CanActorInitiateTradeWith(wounded, medicineSeller),
                "available medicine ends the injury exception");
        });
    }
}
