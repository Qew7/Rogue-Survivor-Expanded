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
            string path = Path.Combine(Path.GetTempPath(), "readable-records-" + Guid.NewGuid().ToString("N"));
            try
            {
                BinarySaveStore.Save(path, Session.Get);
                string lines = String.Join(" ", RecordsReader.Lines(RecordsReader.Load(path), null));
                Check.Equal(true, lines.Contains("Need unmet: 100%"), "archive explains deficit");
                Check.Equal(true, lines.Contains("importance: " + need.Importance + "/200"), "archive explains importance");
                Check.Equal(true, lines.Contains("Kind trait raised its importance"), "archive names the cause");
                Check.Equal(true, lines.Contains("travel to the destination, then give food"), "archive explains the plan");
                Check.Equal(false, lines.Contains("[story "), "archive hides story IDs");
                Check.Equal(false, lines.Contains("Intent started:"), "archive hides internal state labels");
                Check.Equal(true, lines.Contains("Planned route: travel to the destination, then enter the shelter."),
                    "older archived plans also read naturally");
                string search = String.Join(" ", RecordsReader.Lines(RecordsReader.Load(path), null,
                    null, intent.StoryId, RecordsEventFilter.Intentions));
                Check.Equal(true, search.Contains("Need unmet:"), "story ID still works as a search key");
            }
            finally { if (File.Exists(path)) File.Delete(path); if (File.Exists(path + ".bak")) File.Delete(path + ".bak"); }
        });
    }
}
