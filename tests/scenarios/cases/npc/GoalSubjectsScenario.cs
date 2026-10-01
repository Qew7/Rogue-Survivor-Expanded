using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Gameplay.Personality;

static class GoalSubjectsScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/goal-subjects", () => TownScenarioFactory.Arena(4654, ".........", ".........", "........."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD); NpcIntentSupport.Player(world, 8, 2);
            Actor helper = NpcIntentSupport.Actor(world, "helper", 1, 1, "kind"); NpcIntentSupport.Food(world, helper, 4);
            Actor first = NpcIntentSupport.Actor(world, "Alex", 2, 1), second = NpcIntentSupport.Actor(world, "Alex", 1, 2);
            foreach (Actor person in new[] { first, second })
            {
                helper.Personality.Knowledge.See(person, 0); NpcKnownPerson need = helper.Personality.Knowledge.Person(person.PersonalityIdentity);
                need.FoodNeed = need.FoodConfidence = 100;
            }
            NpcGoalGenerator.Refresh(world.Game, helper);
            Check.Equal(2, helper.Personality.Intents.Count, "same outcome can bind different residents instead of one template slot");
            Check.Equal(false, helper.Personality.Intents[0].Generated.Key == helper.Personality.Intents[1].Generated.Key, "namesakes retain distinct state goals");
            NpcIntentSupport.Turn(world, helper); world.Map.LocalTime.TurnCounter = 1; NpcIntentSupport.Turn(world, helper);
            Check.Equal(1, NpcIntentSupport.FoodUnits(first), "first personal deficit receives a real item");
            Check.Equal(1, NpcIntentSupport.FoodUnits(second), "second personal deficit receives a different item");
            Check.Equal(2, NpcIntentSupport.FoodUnits(helper), "both actions preserve actual accounting");
        });
    }
}
