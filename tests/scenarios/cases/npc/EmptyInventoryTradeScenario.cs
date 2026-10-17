using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Gameplay.AI;

static class EmptyInventoryTradeScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/empty-inventory-trade", () => TownScenarioFactory.Arena(8152,
            ".....", ".....", "....."), world =>
        {
            GamePreset preset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            preset.NpcPersonalitiesEnabled = false;
            Session.Get.GamePreset = preset;
            Actor speaker = world.Game.GameActors.MaleCivilian.CreateNumberedName(
                world.Game.GameFactions.TheCivilians, 0);
            Actor partner = world.Game.GameActors.FemaleCivilian.CreateNumberedName(
                world.Game.GameFactions.TheCivilians, 0);
            partner.Inventory.AddAll(new ItemFood(world.Game.GameItems.GROCERIES));
            speaker.FoodPoints = world.Game.Rules.ActorMaxFood(speaker);
            partner.FoodPoints = world.Game.Rules.ActorMaxFood(partner);
            world.Place(speaker, 1, 1);
            world.Place(partner, 2, 1);

            speaker.Controller = new CivilianAI();
            speaker.ActionPoints = Rules.BASE_ACTION_COST;
            Check.Equal(true, world.NpcTurn(speaker), "empty-handed civilian still takes a legal turn");
            Check.Equal(false, Check.CallOn(typeof(BaseAI), speaker.Controller,
                "IsActorTabooTrade", partner), "empty-handed civilian does not start an impossible trade");

            world.Map.RemoveActor(speaker);
            world.Place(speaker, 1, 1);
            speaker.Inventory.AddAll(new ItemFood(world.Game.GameItems.CANNED_FOOD));
            speaker.Controller = new CivilianAI();
            speaker.ActionPoints = Rules.BASE_ACTION_COST;
            Check.Equal(true, world.NpcTurn(speaker), "civilian with an offer takes a legal turn");
            Check.Equal(true, Check.CallOn(typeof(BaseAI), speaker.Controller,
                "IsActorTabooTrade", partner), "trade remains available once both sides carry items");
        });
    }
}
