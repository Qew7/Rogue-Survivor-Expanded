using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;

static class PlannerChainScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/planner-chain", () => TownScenarioFactory.Arena(4642, ".......", ".......", "......."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD); NpcIntentSupport.Player(world, 6, 2);
            Actor hungry = NpcIntentSupport.Actor(world, "hungry", 1, 1, "sociable");
            Actor helper = NpcIntentSupport.Actor(world, "helper", 2, 1, "generous");
            Actor donor = NpcIntentSupport.Actor(world, "donor", 3, 1, "generous");
            NpcIntentSupport.Food(world, helper, 1); NpcIntentSupport.Food(world, donor, 3);
            hungry.FoodPoints = Session.Get.GamePreset.HungerPoints - 1;
            NpcIntentSupport.Turn(world, hungry); NpcIntentSupport.Turn(world, helper);
            NpcIntent first = NpcIntentSupport.Intent(helper, "answer_food_request");
            Check.Equal(true, NpcIntentSupport.HasEvent(donor, "requested_food"), "helper creates a real request to another independently controlled person");
            NpcIntent second = NpcIntentSupport.Intent(donor, "answer_food_request");
            Check.Equal(first.StoryId, second.StoryId, "causal links connect independent plans");
            Check.Equal(0, NpcIntentSupport.FoodUnits(hungry), "planning and requests deliver nothing");
            NpcIntentSupport.Turn(world, donor);
            Check.Equal(2, NpcIntentSupport.FoodUnits(helper), "donor actually supplies the helper");
            Check.Equal(false, first.Finished, "intermediate help does not finish the beneficiary's task");
            world.Map.LocalTime.TurnCounter = 8; NpcIntentSupport.Turn(world, helper);
            Check.Equal(1, NpcIntentSupport.FoodUnits(hungry), "helper achieves the original goal using the received aid");
            Check.Equal(NpcIntentStatus.Completed, first.Status, "first independently planned goal completes");
            Check.Equal(NpcIntentStatus.Completed, second.Status, "second independent goal completes");
            Check.Equal(1, NpcIntentSupport.FoodUnits(helper), "helper's own reserve is preserved");
            Check.Equal("completed", Session.Get.NpcDirector.Find(first.StoryId).Stage, "story finishes from actual outcomes");
            long latest = 0; foreach (ObservedEvent e in helper.Personality.Events) if (e.Kind == "shared_food" && e.SubjectId == donor.PersonalityIdentity) latest = e.EventId;
            Check.Equal(latest, hungry.Personality.Events[hungry.Personality.Events.Count - 2].CauseId, "delivery follows the actual intermediate event");
        });
    }
}
