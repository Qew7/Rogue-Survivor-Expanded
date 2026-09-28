using System;
using System.Drawing;
using System.IO;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Gameplay.Personality;

static class PlannerPersistenceScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/planner-persistence", () => TownScenarioFactory.Arena(4645, ".......", ".......", "......."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD); NpcIntentSupport.Player(world, 6, 2);
            Actor owner = NpcIntentSupport.Actor(world, "owner", 1, 1, "solitary", "scavenger");
            world.Map.DropItemAt(new ItemFood(world.Game.GameItems.CANNED_FOOD) { Quantity = 3 }, new Point(3, 1));
            owner.FoodPoints = Session.Get.GamePreset.HungerPoints - 1; NpcIntentSupport.Turn(world, owner);
            NpcIntent goal = NpcIntentSupport.Intent(owner, "obtain_food");
            Check.Equal(NpcPlanAction.PickupFood, goal.Plan.Current.Action, "save occurs between movement and pickup");
            string path = Path.Combine(Path.GetTempPath(), "npc-plan-" + Guid.NewGuid().ToString("N"));
            try
            {
                BinarySaveStore.Save(path, Session.Get); Session loaded = BinarySaveStore.LoadExact<Session>(path);
                ScenarioWorld restored = NpcIntentSupport.Restore(world, loaded); Actor actor = NpcIntentSupport.Find(restored.Map, owner.PersonalityIdentity);
                NpcIntent saved = NpcIntentSupport.Intent(actor, "obtain_food");
                Check.Equal(goal.Plan.Desired, saved.Plan.Desired, "desired state is saved separately from action method");
                Check.Equal(goal.Plan.Cursor, saved.Plan.Cursor, "actual execution cursor survives");
                Check.Same(restored.Map, saved.Plan.Current.Place.Map, "plan bindings share the loaded map");
                NpcIntentSupport.Turn(restored, actor);
                Check.Equal(3, NpcIntentSupport.FoodUnits(actor), "saved plan resumes with a single actual acquisition");
                Check.Equal(NpcIntentStatus.Completed, saved.Status, "restored goal finishes");
                Check.Equal(0, saved.Plan.Steps.Count, "terminal plan releases all bound map references");
                Check.Equal(true, saved.Plan.LastEventId > 0, "real execution saves its causal continuation");
                loaded.WorldTime.TurnCounter = restored.Map.LocalTime.TurnCounter; BinarySaveStore.Save(path, loaded);
                Check.Equal(true, String.Join(" ", RecordsReader.Lines(RecordsReader.Load(path), null, null, saved.StoryId, RecordsEventFilter.Intentions)).Contains("Plan:"), "archive-only records show generated plans");
            }
            finally { if (File.Exists(path)) File.Delete(path); if (File.Exists(path + ".bak")) File.Delete(path + ".bak"); }
        });
    }
}
