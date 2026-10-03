using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Gameplay.Personality;

static class CustomCatalogGoalRecordScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/custom-catalog-goal-record", () => TownScenarioFactory.Arena(4826,
            ".....", ".....", "....."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            world.Game.NpcContent = PersonalityContent.Create(new NpcRestContent()).Content;
            Actor owner = NpcIntentSupport.Actor(world, "resting", 1, 1, "restful");
            owner.StaminaPoints = Rules.STAMINA_MIN_FOR_ACTIVITY;

            NpcGoalGenerator.Refresh(world.Game, owner);
            NpcIntent goal = NpcIntentSupport.Intent(owner, "take_breath");
            Check.Equal(true, goal != null, "the custom module starts a real goal");
            bool attributed = false;
            foreach (ResidentEntry entry in Session.Get.ResidentRecords.Register(owner).Entries)
                if (entry.Kind == "goal_started" && entry.Text.Contains("recover stamina") &&
                    entry.Text.Contains("Restful trait made this goal more compelling")) attributed = true;
            Check.Equal(true, attributed, "records resolve custom goal prose and trait influence from the active catalog");

            Actor assigned = NpcIntentSupport.Actor(world, "assigned", 2, 1);
            NpcKnownPerson assignedSelf = new NpcKnownPerson { Id = assigned.PersonalityIdentity,
                Name = assigned.UnmodifiedName, Place = assigned.Location };
            NpcIntent assignedGoal = NpcStorySystem.StartKnown(world.Game.NpcContent, assigned, assignedSelf,
                world.Game.NpcContent.Capability("take_breath"));
            Check.Equal(true, assignedGoal != null, "the custom capability also starts an assigned goal");
            assignedGoal.Plan = new NpcPlan();
            assignedGoal.Plan.Steps.Add(new NpcPlanStep { OperatorId = "rest" });
            Session.Get.ResidentRecords.PlanChanged(assigned, assignedGoal, world.Game.NpcContent);
            NpcIntentSystem.Finish(world.Game.NpcContent, assigned, assignedGoal,
                NpcIntentStatus.Completed, "rested after taking a breath");
            bool namedFinish = false;
            bool namedStory = false;
            bool namedPlan = false;
            foreach (ResidentEntry entry in Session.Get.ResidentRecords.Register(assigned).Entries)
            {
                if (entry.Kind == "goal_completed" && entry.Text.Contains("take a breath")) namedFinish = true;
                if (entry.Kind == "story_stage" && entry.Text.Contains("take a breath")) namedStory = true;
                if (entry.Kind == "goal_plan" && entry.Text.Contains("To take a breath")) namedPlan = true;
            }
            Check.Equal(true, namedFinish, "finished assigned goals retain custom capability names in Read Records");
            Check.Equal(true, namedStory, "finished stories use the active catalog's capability name");
            Check.Equal(true, namedPlan, "plans use the active catalog's capability name");

            NpcKnownPerson self = new NpcKnownPerson { Id = owner.PersonalityIdentity,
                Name = owner.UnmodifiedName, Place = owner.Location };
            Check.Equal(null, NpcStorySystem.StartKnown(world.Game.NpcContent, owner, self, null),
                "a removed capability cannot create an assigned intention");
        });
    }
}
