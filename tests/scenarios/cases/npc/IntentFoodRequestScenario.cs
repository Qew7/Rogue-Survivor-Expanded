using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;

static class IntentFoodRequestScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/intent-food-request", () => TownScenarioFactory.Arena(4602, ".......", ".......", "......."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            NpcIntentSupport.Player(world, 6, 2);
            Actor hungry = NpcIntentSupport.Actor(world, "hungry", 1, 1, "sociable");
            Actor helper = NpcIntentSupport.Actor(world, "helper", 2, 1, "generous");
            NpcIntentSupport.Food(world, helper, 3); hungry.FoodPoints = Session.Get.GamePreset.HungerPoints - 1;
            NpcIntentSupport.Turn(world, hungry);
            NpcIntent request = NpcIntentSupport.Intent(hungry, "request_food");
            Check.Equal(true, request != null && request.Announced, "sociable NPC makes a real food request");
            Check.Equal(helper.PersonalityIdentity, request.TargetId, "request picks the nearby person");
            NpcIntent answer = NpcIntentSupport.Intent(helper, "answer_food_request");
            Check.Equal(true, answer != null, "generous listener forms a response goal");
            Check.Equal(request.StoryId, answer.StoryId, "both intentions belong to the same story");
            NpcIntentSupport.Turn(world, helper);
            Check.Equal(1, NpcIntentSupport.FoodUnits(hungry), "requested food is actually delivered");
            Check.Equal(2, NpcIntentSupport.FoodUnits(helper), "donor retains its reserve");
            Check.Equal(NpcIntentStatus.Completed, request.Status, "received help satisfies the request");
            Check.Equal(NpcIntentStatus.Completed, answer.Status, "giver records the successful response");
            Check.Equal(null, NpcIntentSupport.Intent(hungry, "repay_aid"), "story help does not create a gift loop");
            Check.Equal(true, NpcIntentSupport.HasEvent(hungry, "helped"), "existing memory system sees successful help");
            Check.Equal(true, answer.CauseId > 0, "response retains the request event's identity");
            Check.Equal(request.StoryId, hungry.Personality.Reactions[0].StoryId, "acknowledgement remains linked to the request episode");
            Actor solitary = NpcIntentSupport.Actor(world, "solitary", 4, 1, "solitary");
            solitary.FoodPoints = Session.Get.GamePreset.HungerPoints - 1;
            NpcIntentSupport.Turn(world, solitary);
            Check.Equal(null, NpcIntentSupport.Intent(solitary, "request_food"), "solitary trait changes choice under the same need");
        });
    }
}
