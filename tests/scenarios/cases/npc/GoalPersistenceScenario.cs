using System;
using System.Drawing;
using System.IO;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Gameplay.Personality;

static class GoalPersistenceScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/goal-persistence", () => TownScenarioFactory.Arena(4655, ".......", ".......", "......."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD); NpcIntentSupport.Player(world, 6, 2);
            Actor owner = NpcIntentSupport.Actor(world, "owner", 1, 1, "solitary", "scavenger");
            owner.FoodPoints = Session.Get.GamePreset.HungerPoints - 1;
            world.Map.DropItemAt(new ItemFood(world.Game.GameItems.CANNED_FOOD) { Quantity = 3 }, new Point(3, 1));
            NpcIntentSupport.Turn(world, owner); NpcIntent goal = NpcIntentSupport.Intent(owner, "obtain_food");
            string path = Path.Combine(Path.GetTempPath(), "npc-value-" + Guid.NewGuid().ToString("N"));
            try
            {
                BinarySaveStore.Save(path, Session.Get); Session loaded = BinarySaveStore.LoadExact<Session>(path);
                ScenarioWorld restored = NpcIntentSupport.Restore(world, loaded); Actor actor = NpcIntentSupport.Find(restored.Map, owner.PersonalityIdentity);
                NpcIntent saved = NpcIntentSupport.Intent(actor, "obtain_food");
                Check.Equal(goal.Generated.Key, saved.Generated.Key, "desired state retains its permanent subject identity");
                Check.Equal(goal.Generated.Explanation, saved.Generated.Explanation, "deficit, values, confidence and utility survive");
                Check.Same(restored.Map, saved.Plan.Current.Place.Map, "generated plan shares restored world identity");
                NpcIntentSupport.Turn(restored, actor);
                Check.Equal(NpcIntentStatus.Completed, saved.Status, "saved state goal resumes through a real action");
                Check.Equal(3, NpcIntentSupport.FoodUnits(actor), "loading creates no duplicate supplies");
                BinarySaveStore.Save(path, loaded);
                Check.Equal(true, String.Join(" ", RecordsReader.Lines(RecordsReader.Load(path), null, null, saved.StoryId, RecordsEventFilter.Intentions)).Contains("Need unmet:"), "archive-only reader explains the reason for generation");
                var food = new System.Collections.Generic.List<Item>(actor.Inventory.Items);
                foreach (Item item in food) actor.Inventory.RemoveAllQuantity(item);
                restored.Map.LocalTime.TurnCounter = 1; NpcGoalGenerator.Refresh(restored.Game, actor);
                Check.Equal(1, actor.Personality.Intents.Count, "per-state cooldown survives a renewed deficit after loading");
            }
            finally { if (File.Exists(path)) File.Delete(path); if (File.Exists(path + ".bak")) File.Delete(path + ".bak"); }
        });
    }
}
