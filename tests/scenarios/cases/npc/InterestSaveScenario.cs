using System;
using System.Drawing;
using System.IO;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Gameplay.Personality;
static class InterestSaveScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/interest-save", () => TownScenarioFactory.Arena(4809,
            ".......", ".......", "......."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD); NpcIntentSupport.Player(world, 6, 2);
            Actor actor = NpcIntentSupport.Actor(world, "planner", 1, 1, "organized", "disciplined");
            world.Map.DropItemAt(new ItemFood(world.Game.GameItems.CANNED_FOOD) { Quantity = 4 }, new Point(2, 1));
            NpcGoalGenerator.Refresh(world.Game, actor);
            NpcInterest before = actor.Personality.Interest("food_reserve", actor.PersonalityIdentity);
            Check.Equal(true, before != null && before.ExpiresTurn > WorldTime.TURNS_PER_DAY,
                "real trait creates a lasting interest before saving");
            string path = Path.Combine(Path.GetTempPath(), "npc-interest-" + Guid.NewGuid().ToString("N") + ".dat");
            try
            {
                BinarySaveStore.Save(path, Session.Get); Session loaded = BinarySaveStore.Load<Session>(path);
                ScenarioWorld restored = NpcIntentSupport.Restore(world, loaded);
                Actor kept = NpcIntentSupport.Find(restored.Map, actor.PersonalityIdentity);
                NpcInterest after = kept.Personality.Interest("food_reserve", kept.PersonalityIdentity);
                Check.Equal(before.ExpiresTurn, after.ExpiresTurn, "duration survives full session serialization");
                for (int t = 1; t < 8 && NpcIntentSupport.FoodUnits(kept) < 3; t++)
                { restored.Map.LocalTime.TurnCounter = t; NpcIntentSupport.Turn(restored, kept); }
                Check.Equal(true, NpcIntentSupport.FoodUnits(kept) >= 3, "restored interest continues through real actions");
            }
            finally { if (File.Exists(path)) File.Delete(path); }
        });
    }
}
