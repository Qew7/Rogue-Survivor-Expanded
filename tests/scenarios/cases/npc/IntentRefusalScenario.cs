using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;

static class IntentRefusalScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/intent-refusal", () => TownScenarioFactory.Arena(4607, ".....", ".....", "....."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            NpcIntentSupport.Player(world, 4, 2);
            Actor hungry = NpcIntentSupport.Actor(world, "hungry", 1, 1, "sociable");
            Actor selfish = NpcIntentSupport.Actor(world, "selfish", 2, 1, "selfish");
            NpcIntentSupport.Food(world, selfish, 3); hungry.FoodPoints = Session.Get.GamePreset.HungerPoints - 1;
            NpcIntentSupport.Turn(world, hungry);
            NpcIntent request = NpcIntentSupport.Intent(hungry, "request_food");
            Check.Equal(null, NpcIntentSupport.Intent(selfish, "answer_food_request"), "selfish listener declines the goal");
            NpcIntentSupport.Turn(world, selfish);
            Check.Equal(true, NpcIntentSupport.HasEvent(hungry, "request_refused"), "refusal is actually spoken and observed");
            Check.Equal(3, NpcIntentSupport.FoodUnits(selfish), "refusal transfers no items");
            Check.Equal(0, NpcIntentSupport.FoodUnits(hungry), "request alone cannot fabricate supplies");
            Check.Equal(NpcIntentStatus.Failed, request.Status, "explicit refusal ends the matching request");
            Check.Equal(true, hungry.Personality.Person(selfish.PersonalityIdentity) != null, "refusal becomes a personal experience");
            world.Map.LocalTime.TurnCounter = request.Deadline;
            djack.RogueSurvivor.Gameplay.Personality.NpcIntentSystem.AdvanceClock(world.Game, world.Map);
            Check.Equal("request was declined", request.Outcome, "deadline does not rewrite an already known outcome");
            int count = hungry.Personality.Intents.Count;
            NpcIntentSupport.Turn(world, hungry);
            Check.Equal(count, hungry.Personality.Intents.Count, "saved cooldown prevents immediate repeated requests");
        });
    }
}
