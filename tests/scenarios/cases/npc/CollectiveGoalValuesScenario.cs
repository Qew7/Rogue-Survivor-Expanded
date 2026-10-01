using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Gameplay.Personality;

static class CollectiveGoalValuesScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/collective-goal-values", () => TownScenarioFactory.Arena(4669, ".........", ".........", "........."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD); NpcIntentSupport.Player(world, 8, 2);
            Actor recipient = NpcIntentSupport.Actor(world, "recipient", 2, 1);
            Actor leader = NpcIntentSupport.Actor(world, "leader", 3, 1); leader.AddFollower(recipient);
            recipient.Faction = world.Game.GameFactions.TheArmy;
            Actor helper = NpcIntentSupport.Actor(world, "helper", 1, 1);
            helper.Personality.Knowledge.See(recipient, 0);
            NpcKnownPerson known = helper.Personality.Knowledge.Person(recipient.PersonalityIdentity);
            known.FoodNeed = known.FoodConfidence = 100;
            Check.Equal(10, NpcValues.Importance(world.Game.NpcContent, helper, NpcGoalValue.Care, recipient.PersonalityIdentity), "neutral actor has little autonomous inclination to donate");
            var memory = new MemoryInstance("collective-test", 0, 100, "Army");
            helper.Personality.RememberFaction(recipient.Faction.ID, recipient.Faction.Name, memory, 80);
            NpcGoalGenerator.Refresh(world.Game, helper);
            Check.Equal(true, NpcIntentSupport.Intent(helper, "answer_food_request") != null, "remembered faction sympathy affects goal admission");
            Check.Equal(30, NpcValues.Importance(world.Game.NpcContent, helper, NpcGoalValue.Care, recipient.PersonalityIdentity), "collective history is included in perceived personal value");
            helper.Personality.RememberGroup(recipient.SocialGroup.Identity, leader.UnmodifiedName, new MemoryInstance("group-test", 0, 100, "group"), -80);
            Check.Equal(10, NpcValues.Importance(world.Game.NpcContent, helper, NpcGoalValue.Care, recipient.PersonalityIdentity), "known group reputation can offset faction sympathy");
            Actor stranger = NpcIntentSupport.Actor(world, "stranger", 4, 1);
            helper.Personality.Knowledge.See(stranger, 0);
            Check.Equal(10, NpcValues.Importance(world.Game.NpcContent, helper, NpcGoalValue.Care, stranger.PersonalityIdentity), "unrelated faction and group are unaffected");
        });
    }
}
