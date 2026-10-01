using System;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Gameplay.Personality;

static class GoalOfferValidationScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/goal-offer-validation", () => TownScenarioFactory.Arena(4827,
            ".....", ".....", "....."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            Actor owner = NpcIntentSupport.Actor(world, "owner", 1, 1);
            var context = new NpcGoalContext(world.Game, owner, world.Game.NpcContent);
            var offers = new NpcGoalOffers(context);
            Check.Throws<ArgumentException>(() => offers.Add(context.Self, (NpcGoalValue)999,
                world.Game.NpcContent.Capability(NutritionModule.ObtainFoodId), 0, 100, 100, 100),
                "an unregistered value reports a content error at the offer boundary");
            Check.Throws<ArgumentException>(() => offers.Add(context.Self, NpcGoalValue.Nutrition,
                null, 0, 100, 100, 100),
                "an unregistered capability reports a content error at the offer boundary");
            Check.Equal(0, offers.Candidates.Count, "invalid offers do not mutate the candidate list");
        });
    }
}
