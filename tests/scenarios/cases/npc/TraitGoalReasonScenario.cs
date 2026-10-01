using System;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Gameplay.Personality;

static class TraitGoalReasonScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/trait-goal-reason", () => TownScenarioFactory.Arena(4824,
            ".....", ".....", "....."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            Actor helper = NpcIntentSupport.Actor(world, "helper", 1, 1, "kind");
            Actor neighbor = NpcIntentSupport.Actor(world, "neighbor", 2, 1);
            NpcGeneratedGoal goal = NpcValues.Evaluate(helper, world.Game.NpcContent.Value(NpcGoalValue.Care),
                neighbor.PersonalityIdentity, 0, 100, 100, 100, 0, world.Game.NpcContent.Personalities,
                world.Game.NpcContent);
            string reason = NpcValues.TraitInfluence(helper, goal, world.Game.NpcContent);
            Check.Equal(true, reason != null && reason.Contains("trait Kind") && reason.Contains("25"),
                "reason reports the measured importance contribution of Kind");
            NpcGeneratedGoal unchanged = NpcValues.Evaluate(helper, world.Game.NpcContent.Value(NpcGoalValue.Nutrition),
                helper.PersonalityIdentity, 0, 100, 100, 100, 0, world.Game.NpcContent.Personalities,
                world.Game.NpcContent);
            Check.Equal(null, NpcValues.TraitInfluence(helper, unchanged, world.Game.NpcContent),
                "unrelated nutrition goal gets no invented trait cause");
            NpcKnownPerson target = new NpcKnownPerson { Id = neighbor.PersonalityIdentity,
                Name = neighbor.UnmodifiedName, Place = neighbor.Location, SeenTurn = 0 };
            NpcIntent started = NpcGoalLifecycle.Start(world.Game.NpcContent, helper, target,
                world.Game.NpcContent.Capability("answer_food_request"), generated: goal);
            Check.Equal(true, started != null, "goal starts with adequate utility");
            bool recorded = false;
            foreach (ResidentEntry entry in Session.Get.ResidentRecords.Register(helper).Entries)
                if (entry.Kind == "goal_started" && entry.Text.Contains("because trait Kind")) recorded = true;
            Check.Equal(true, recorded, "Read Records includes the causal trait contribution at intent start");
        });
    }
}
