using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Gameplay.AI;

static class PersonalityNeutralTradeScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/personality-neutral-trade", () => TownScenarioFactory.Arena(4541,
            ".....", ".....", "....."), world =>
        {
            GamePreset preset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            preset.NpcPersonalitiesEnabled = false;
            Session.Get.GamePreset = preset;
            Actor speaker = world.Game.GameActors.MaleCivilian.CreateNumberedName(
                world.Game.GameFactions.TheCivilians, 0);
            Actor partner = world.Game.GameActors.FemaleCivilian.CreateNumberedName(
                world.Game.GameFactions.TheCivilians, 0);
            speaker.Inventory.AddAll(new ItemFood(world.Game.GameItems.CANNED_FOOD));
            partner.Inventory.AddAll(new ItemFood(world.Game.GameItems.GROCERIES));
            speaker.FoodPoints = world.Game.Rules.ActorMaxFood(speaker);
            partner.FoodPoints = world.Game.Rules.ActorMaxFood(partner);
            world.Place(speaker, 1, 1);
            world.Place(partner, 2, 1);
            Check.Equal(null, speaker.Personality,
                "disabled preset creates a civilian without personality state");

            for (int turn = 0; turn < 12; turn++)
            {
                speaker.Controller = new CivilianAI();
                speaker.ActionPoints = Rules.BASE_ACTION_COST;
                Check.Equal(true, world.NpcTurn(speaker), "civilian performs a legal turn");
                Check.Equal(true, Check.CallOn(typeof(BaseAI), speaker.Controller,
                    "IsActorTabooTrade", partner),
                    "neutral trade bias keeps every eligible trade attempt");
            }

            world.Map.RemoveActor(partner);
            speaker.Controller = new CivilianAI();
            speaker.ActionPoints = Rules.BASE_ACTION_COST;
            Check.Equal(true, world.NpcTurn(speaker), "civilian acts without a trading partner");
            Check.Equal(false, Check.CallOn(typeof(BaseAI), speaker.Controller,
                "IsActorTabooTrade", partner),
                "no trade attempt is recorded when the partner is absent");
        });
    }
}
