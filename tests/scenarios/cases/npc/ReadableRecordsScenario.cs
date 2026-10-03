using System;
using System.IO;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Gameplay.Personality;

static class ReadableRecordsScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/readable-records", () => TownScenarioFactory.Arena(4942,
            ".......", ".......", "......."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            NpcIntentSupport.Player(world, 6, 2);
            Actor owner = NpcIntentSupport.Actor(world, "Mira", 1, 1, "kind");
            Actor neighbor = NpcIntentSupport.Actor(world, "neighbor", 2, 1);
            NpcGeneratedGoal need = NpcValues.Evaluate(world.Game.NpcContent, owner,
                world.Game.NpcContent.Value(NpcGoalValue.Care), neighbor.PersonalityIdentity,
                0, 100, 100, 100, 0);
            NpcKnownPerson target = new NpcKnownPerson { Id = neighbor.PersonalityIdentity,
                Name = neighbor.UnmodifiedName, Place = neighbor.Location, SeenTurn = 0 };
            NpcIntent intent = NpcGoalLifecycle.Start(world.Game.NpcContent, owner, target,
                world.Game.NpcContent.Capability("answer_food_request"), generated: need);
            Check.Equal(true, intent != null, "real generated goal starts");
            intent.Plan = new NpcPlan();
            intent.Plan.Steps.Add(new NpcPlanStep { Action = NpcPlanAction.Travel });
            intent.Plan.Steps.Add(new NpcPlanStep { Action = NpcPlanAction.GiveFood });
            Session.Get.ResidentRecords.PlanChanged(owner, intent);
            Session.Get.ResidentRecords.Register(owner).Add("old-plan", 0,
                "Plan: Travel → EnterShelter. [story old123]",
                new ObservedEvent("goal_plan", 0, owner.UnmodifiedName, null, true, storyId: "old123"));
            Session.Get.ResidentRecords.Register(owner).Add("old-hunger", 0,
                "Intent started: Have usable food; target Mira; Nutrition: 0 → 100; deficit 100, importance 70, confidence 100, utility 70. [story old-food]",
                new ObservedEvent("goal_started", 0, owner.UnmodifiedName, owner.UnmodifiedName, true, storyId: "old-food"));
            Session.Get.ResidentRecords.Register(owner).Add("old-shelter", 0,
                "Intent started: Return to my chosen shelter; target Mira; Residence: 0 → 1; deficit 100, importance 53, confidence 100, utility 53. [story old-shelter]",
                new ObservedEvent("goal_started", 0, owner.UnmodifiedName, owner.UnmodifiedName, true, storyId: "old-shelter"));
            Session.Get.ResidentRecords.Register(owner).Add("recent-shelter", 0,
                "Mira decided to return to my chosen shelter. Need unmet: 100% (current 0, desired 1); importance: 53/200; confidence: 100%. The Homebody trait raised its importance by 8 points.",
                new ObservedEvent("goal_started", 0, owner.UnmodifiedName, owner.UnmodifiedName, true, storyId: "recent-shelter"));
            Session.Get.ResidentRecords.Register(owner).Add("old-stage", 0,
                "Story supply_run: completed.", new ObservedEvent("story_stage", 0, owner.UnmodifiedName, null, true));
            Session.Get.ResidentRecords.Register(owner).Add("old-completion", 0,
                "Intent completed: Find food; target Mira; goal met.",
                new ObservedEvent("goal_completed", 0, owner.UnmodifiedName, owner.UnmodifiedName, true));
            string path = Path.Combine(Path.GetTempPath(), "readable-records-" + Guid.NewGuid().ToString("N"));
            try
            {
                BinarySaveStore.Save(path, Session.Get);
                string lines = String.Join(" ", RecordsReader.Lines(RecordsReader.Load(path), null));
                Check.Equal(true, lines.Contains("believed neighbor needed food"), "food need is explained only for its own goal");
                Check.Equal(true, lines.Contains("Kind") && lines.Contains("trait made this goal more compelling"),
                    "archive names the cause without a score");
                Check.Equal(true, lines.Contains("travel onward, then give food"), "unknown old route has no invented place");
                Check.Equal(false, lines.Contains("[story "), "archive hides story IDs");
                Check.Equal(false, lines.Contains("Intent started:"), "archive hides internal state labels");
                Check.Equal(false, lines.Contains("Need unmet:") || lines.Contains("importance:") || lines.Contains("confidence:"),
                    "neither new nor older entries expose goal scores");
                Check.Equal(true, lines.Contains("Planned route: travel onward, then enter the shelter."),
                    "older archived plans also read naturally");
                Check.Equal(true, lines.Contains("Food was running low"), "older hunger entry explains its own need");
                Check.Equal(true, lines.Contains("The effort to supply run succeeded."),
                    "old story stages retain readable outcomes");
                Check.Equal(true, lines.Contains("Achieved: Find food. goal met."),
                    "old completed goals retain their outcome text");
                string shelter = String.Join(" ", RecordsReader.Lines(RecordsReader.Load(path), null,
                    null, "old-shelter", RecordsEventFilter.Intentions));
                Check.Equal(true, shelter.Contains("return to my chosen shelter") && !shelter.Contains("food") &&
                    !shelter.Contains("hungry"), "shelter entry does not acquire an unrelated hunger explanation");
                string recentShelter = String.Join(" ", RecordsReader.Lines(RecordsReader.Load(path), null,
                    null, "recent-shelter", RecordsEventFilter.Intentions));
                Check.Equal(true, recentShelter.Contains("Homebody") && !recentShelter.Contains("importance:") &&
                    !recentShelter.Contains("food"), "recent saves lose scores without gaining an unrelated need");
                string search = String.Join(" ", RecordsReader.Lines(RecordsReader.Load(path), null,
                    null, intent.StoryId, RecordsEventFilter.Intentions));
                Check.Equal(true, search.Contains("needed food"), "story ID still works as a search key");
            }
            finally { if (File.Exists(path)) File.Delete(path); if (File.Exists(path + ".bak")) File.Delete(path + ".bak"); }
        });
    }
}
