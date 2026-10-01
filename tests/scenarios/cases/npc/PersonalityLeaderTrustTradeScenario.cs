using System;
using System.Reflection;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Gameplay.AI;
using djack.RogueSurvivor.Gameplay.Personality;

static class PersonalityLeaderTrustTradeScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/personality-leader-trust-trade", () => TownScenarioFactory.Arena(4578,
            ".....", ".....", "....."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            Actor follower = world.Game.GameActors.MaleCivilian.CreateNumberedName(
                world.Game.GameFactions.TheCivilians, 0);
            Actor leader = world.Game.GameActors.MaleCivilian.CreateNumberedName(
                world.Game.GameFactions.TheCivilians, 1);
            Actor stranger = world.Game.GameActors.MaleCivilian.CreateNumberedName(
                world.Game.GameFactions.TheCivilians, 2);
            follower.Personality = new PersonalityState();
            leader.Personality = new PersonalityState();
            world.Place(follower, 2, 1);
            world.Place(leader, 1, 1);
            world.Place(stranger, 3, 1);
            leader.AddFollower(follower);
            follower.TrustInLeader = Rules.TRUST_TRUSTING_THRESHOLD;
            PersonalitySystem.Report(world.Game, new SignificantEvent("attack", follower, leader,
                world.Map, follower.Location.Position, 0));
            PersonalitySystem.Report(world.Game, new SignificantEvent("attack", follower, stranger,
                world.Map, follower.Location.Position, 0));
            Check.Equal(true, PersonalitySystem.Attitude(follower, leader) <= -30,
                "memories create negative attitude despite previously accumulated trust");
            ItemFood offered = new ItemFood(world.Game.GameItems.GROCERIES);
            ItemFood asked = new ItemFood(world.Game.GameItems.CANNED_FOOD);
            BaseAI ai = (BaseAI)follower.Controller;
            Check.Equal(BaseAI.TradeRating.ACCEPT, ai.RateTradeOffer(world.Game, leader, offered, asked),
                "trusted leader takes precedence over negative attitude");
            Check.Equal(BaseAI.TradeRating.REFUSE, ai.RateTradeOffer(world.Game, stranger, offered, asked),
                "trust exception does not apply to another disliked trader");
            follower.Inventory.AddAll(asked);
            leader.Inventory.AddAll(offered);
            stranger.Inventory.AddAll(new ItemFood(world.Game.GameItems.GROCERIES));
            foreach (Actor actor in world.Map.Actors) actor.FoodPoints = world.Game.Rules.ActorMaxFood(actor);
            bool triedLeader = false;
            for (int turn = 0; turn < 12; turn++)
            {
                follower.Controller = new CivilianAI();
                follower.ActionPoints = Rules.BASE_ACTION_COST;
                Check.Equal(true, world.NpcTurn(follower), "follower performs a real AI turn");
                if ((bool)Check.CallOn(typeof(BaseAI), follower.Controller, "IsActorTabooTrade", leader))
                    triedLeader = true;
                Check.Equal(false, Check.CallOn(typeof(BaseAI), follower.Controller, "IsActorTabooTrade", stranger),
                    "autonomous trade still excludes the disliked stranger");
            }
            Check.Equal(true, triedLeader, "autonomous trade uses the same trusted-leader precedence");
            int trustBefore = follower.TrustInLeader;
            int change = world.Game.Rules.ActorTrustIncrease(leader, follower);
            Check.Equal(true, change < 0, "negative memories reduce accumulated trust");
            Type flags = typeof(RogueGame).GetNestedType("SimFlags", BindingFlags.NonPublic);
            Check.Call(world.Game, "NextMapTurn", new[] { typeof(Map), flags },
                world.Map, Enum.Parse(flags, "NOT_SIMULATING"));
            Check.Equal(trustBefore + change, follower.TrustInLeader, "real map turn applies trust decay");
            Check.Equal(false, world.Game.Rules.IsActorTrustingLeader(follower), "trust falls below its threshold");
            ai = (BaseAI)follower.Controller;
            Check.Equal(BaseAI.TradeRating.REFUSE, ai.RateTradeOffer(world.Game, leader, offered, asked),
                "untrusted leader follows normal attitude refusal");
            follower.Controller = new CivilianAI();
            follower.ActionPoints = Rules.BASE_ACTION_COST;
            Check.Equal(true, world.NpcTurn(follower), "untrusted follower still acts");
            Check.Equal(false, Check.CallOn(typeof(BaseAI), follower.Controller, "IsActorTabooTrade", leader),
                "autonomous trade loses the exception when trust decays");
            GamePreset disabled = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            disabled.NpcPersonalitiesEnabled = false;
            Session.Get.GamePreset = disabled;
            Check.Equal(Rules.TRUST_BASE_INCREASE, world.Game.Rules.ActorTrustIncrease(leader, follower),
                "disabled personalities retain ordinary trust growth");
            follower.TrustInLeader = Rules.TRUST_TRUSTING_THRESHOLD;
            Check.Equal(BaseAI.TradeRating.ACCEPT, ((BaseAI)follower.Controller)
                .RateTradeOffer(world.Game, leader, offered, asked), "disabled personalities retain leader acceptance");
        });
    }
}
