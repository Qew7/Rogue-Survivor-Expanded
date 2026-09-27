using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Gameplay.AI;
using djack.RogueSurvivor.Gameplay.Personality;

static class PersonalityRelationshipsAiScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/personality-relationships-ai", () => TownScenarioFactory.Arena(4554,
            ".....", ".....", "....."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            Actor speaker = world.Game.GameActors.MaleCivilian.CreateNumberedName(
                world.Game.GameFactions.TheCivilians, 0);
            Actor formerLeader = world.Game.GameActors.FemaleCivilian.CreateNumberedName(
                world.Game.GameFactions.TheCivilians, 0);
            Actor stranger = world.Game.GameActors.FemaleCivilian.CreateNumberedName(
                world.Game.GameFactions.TheCivilians, 1);
            speaker.Personality = new PersonalityState();
            speaker.Inventory.AddAll(new ItemFood(world.Game.GameItems.CANNED_FOOD));
            formerLeader.Inventory.AddAll(new ItemFood(world.Game.GameItems.GROCERIES));
            stranger.Inventory.AddAll(new ItemFood(world.Game.GameItems.GROCERIES));
            speaker.FoodPoints = world.Game.Rules.ActorMaxFood(speaker);
            formerLeader.FoodPoints = world.Game.Rules.ActorMaxFood(formerLeader);
            stranger.FoodPoints = world.Game.Rules.ActorMaxFood(stranger);
            world.Place(speaker, 2, 1);
            world.Place(formerLeader, 1, 1);
            world.Place(stranger, 3, 1);
            formerLeader.AddFollower(speaker);
            world.Game.DoCancelLead(formerLeader, speaker);
            Check.Equal(-35, PersonalitySystem.Attitude(speaker, formerLeader),
                "dismissal produces a personal grievance");

            bool triedStranger = false;
            for (int turn = 0; turn < 12; turn++)
            {
                speaker.Controller = new CivilianAI();
                speaker.ActionPoints = Rules.BASE_ACTION_COST;
                Check.Equal(true, world.NpcTurn(speaker), "civilian performs a legal AI turn");
                Check.Equal(false, Check.CallOn(typeof(BaseAI), speaker.Controller,
                    "IsActorTabooTrade", formerLeader),
                    "civilian does not offer a trade to the person who abandoned them");
                if ((bool)Check.CallOn(typeof(BaseAI), speaker.Controller,
                    "IsActorTabooTrade", stranger)) triedStranger = true;
            }
            Check.Equal(true, triedStranger,
                "civilian still attempts a trade with an available neutral NPC");

            world.Map.RemoveActor(stranger);
            speaker.Controller = new CivilianAI();
            speaker.ActionPoints = Rules.BASE_ACTION_COST;
            Check.Equal(true, world.NpcTurn(speaker), "civilian acts when neutral partner is absent");
            Check.Equal(false, Check.CallOn(typeof(BaseAI), speaker.Controller,
                "IsActorTabooTrade", formerLeader),
                "absence of a neutral partner does not force trade with a disliked NPC");
        });
    }
}
