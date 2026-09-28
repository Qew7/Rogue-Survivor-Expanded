using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Gameplay.Personality;

static class PlannerObservedOutcomeScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/planner-observed-outcome", () => TownScenarioFactory.Arena(4649, "...............................", "...............................", "..............................."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD); NpcIntentSupport.Player(world, 30, 2);
            Actor recipient = NpcIntentSupport.Actor(world, "recipient", 1, 1);
            Actor donor = NpcIntentSupport.Actor(world, "donor", 2, 1, "generous");
            Actor watcher = NpcIntentSupport.Actor(world, "watcher", 3, 1, "generous");
            Actor collector = NpcIntentSupport.Actor(world, "collector", 4, 1, "generous", "scavenger");
            Actor absent = NpcIntentSupport.Actor(world, "absent", 25, 1, "generous");
            NpcIntentSupport.Food(world, donor, 3);
            var known = new NpcKnownPerson { Id = recipient.PersonalityIdentity, Name = recipient.UnmodifiedName, Place = recipient.Location };
            NpcIntent gift = NpcStorySystem.StartKnown(donor, known, NpcIntentContent.Help);
            NpcIntent waiting = NpcStorySystem.StartKnown(watcher, known, NpcIntentContent.Help, storyId: gift.StoryId);
            NpcIntent unseen = NpcStorySystem.StartKnown(absent, known, NpcIntentContent.Help, storyId: gift.StoryId);
            NpcIntent task = NpcStorySystem.StartKnown(collector, known, NpcIntentContent.Gather, storyId: gift.StoryId);
            task.CoordinatorId = watcher.PersonalityIdentity; task.CoordinatorPlace = watcher.Location;
            NpcIntentSupport.Turn(world, donor);
            Check.Equal(NpcIntentStatus.Completed, waiting.Status, "observed actual aid satisfies another helper's desired result");
            Check.Equal(false, unseen.Finished, "unseen acquisition cannot remotely satisfy an NPC's goal");
            Check.Equal(false, task.Finished, "a collector still owes its promised report");
            Check.Equal(2, task.Progress, "witnessed aid establishes delivery without inventing a pickup");
            NpcIntentSupport.Turn(world, collector);
            Check.Equal(NpcIntentStatus.Completed, task.Status, "collector replans to report the observed result");
            Check.Equal(1, NpcIntentSupport.FoodUnits(recipient), "success does not require duplicate deliveries");
            Check.Equal(0, NpcIntentSupport.FoodUnits(watcher), "observing aid transfers no possessions to the observer");
            Check.Equal(false, NpcIntentSupport.HasEvent(collector, "supplies_acquired"), "reporting another person's success creates no fictional acquisition");
            Check.Equal(true, NpcIntentSupport.HasEvent(watcher, "supplies_delivered"), "report is an actual spoken event");
        });
    }
}
