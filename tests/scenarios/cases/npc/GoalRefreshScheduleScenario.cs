using System.Collections.Generic;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Gameplay.Personality;

static class GoalRefreshScheduleScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/goal-refresh-schedule", () => TownScenarioFactory.Arena(8473,
            ".........", ".........", "........."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            NpcIntentSupport.Player(world, 8, 2);
            Actor hungry = NpcIntentSupport.Actor(world, "hungry", 1, 1, "sociable");
            NpcIntentSupport.SetGoalRefreshSlot(hungry, 3);
            Actor helper = NpcIntentSupport.Actor(world, "helper", 2, 1, "generous");
            NpcIntentSupport.Food(world, helper, 3);
            hungry.FoodPoints = Session.Get.GamePreset.HungerPoints - 1;

            int slot = (int)((uint)hungry.PersonalityIdentity.GetHashCode() % 10u);
            world.Map.LocalTime.TurnCounter = slot == 0 ? 1 : slot - 1;
            NpcIntentSystem.Maintain(world.Game, hungry, new List<Actor>(), false, false);
            Check.Equal(null, NpcIntentSupport.Intent(hungry, "request_food"),
                "routine need does not create a long-term goal outside its turn group");

            world.Map.LocalTime.TurnCounter = slot == 0 ? 10 : slot;
            hungry.ActionPoints = Rules.BASE_ACTION_COST;
            Check.Equal(true, world.NpcTurn(hungry), "scheduled actor takes a legal action");
            Check.Equal(0, hungry.ActionPoints, "scheduled action costs one turn");
            NpcIntent request = NpcIntentSupport.Intent(hungry, "request_food");
            Check.Equal(true, request != null && request.Announced,
                "scheduled reevaluation creates and pursues the food request");
            Check.Equal(true, NpcIntentSupport.Intent(helper, "answer_food_request") != null,
                "the request event creates the listener's response immediately");
            NpcIntentSupport.Turn(world, helper);
            Check.Equal(1, NpcIntentSupport.FoodUnits(hungry),
                "event-driven response still transfers real food");
        });
    }
}
