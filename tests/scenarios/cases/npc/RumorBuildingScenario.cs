using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Gameplay.Personality;

static class RumorBuildingScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/rumor-building", () => TownScenarioFactory.Arena(4832,
            "........", "........", "........"), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            Actor player = NpcIntentSupport.Player(world, 5, 1);
            Actor witness = NpcIntentSupport.Actor(world, "witness", 0, 1);
            Actor victim = NpcIntentSupport.Actor(world, "victim", 1, 1);
            Actor attacker = NpcIntentSupport.Actor(world, "attacker", 2, 1);
            Actor listener = NpcIntentSupport.Actor(world, "listener", 4, 1);
            var grocery = new Zone("Grocery@1-1", new Rectangle(1, 1, 2, 1))
                { BuildingKind = BuildingKind.Grocery };
            world.Map.AddZone(grocery);
            PersonalitySystem.Report(world.Game, new SignificantEvent("attack", victim, attacker,
                world.Map, victim.Location.Position, 0));
            NpcFact fact = witness.Personality.Knowledge.Facts.Find(f => f.Kind == "attack");
            Check.Equal(true, fact != null, "witness retains the event's actual location");
            NpcConversation.ShareRumor(world.Game, witness, listener, fact, true);
            string place = "in district " + World.CoordToString(0, 0) + ", at the grocery store";
            bool heardBuilding = false;
            foreach (HeardJournalEntry entry in player.Personality.HeardJournal)
                if (entry.Text.Contains(place)) heardBuilding = true;
            Check.Equal(true, heardBuilding, "the spoken rumor and heard journal include the observed building");
            var colors = new RecordsTextColors(Session.Get.ResidentRecords.Residents);
            bool colored = false;
            string line = player.Personality.HeardJournal[0].Text;
            foreach (RecordsTextRun run in colors.Runs(line, Color.White))
                if (line.Substring(run.Start, run.Length) == "grocery store" &&
                    run.Color == Zone.FoodStoreColor) colored = true;
            Check.Equal(true, colored, "building color comes from the zone type");

            ScenarioUI ui = (ScenarioUI)world.Game.UI;
            ui.QueueWaitKey(Keys.Escape);
            Check.Call(world.Game, "HandleHeardJournal", Type.EmptyTypes);
            Check.Equal(true, ui.DrawnBold.Exists(draw => draw.Item1 == Zone.FoodStoreColor &&
                draw.Item2 == "grocery store"), "the player's heard journal colors the building");

            string path = Path.Combine(Path.GetTempPath(), "rogue-building-" + Guid.NewGuid().ToString("N"));
            try
            {
                BinarySaveStore.Save(path, world.Map);
                Map loaded = (Map)BinarySaveStore.Load(path, null);
                loaded.ReconstructAuxiliaryFields();
                Check.Equal(BuildingKind.Grocery,
                    Zone.BuildingAt(new Location(loaded, new Point(1, 1))).BuildingKind,
                    "the typed building survives save and load");
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
                if (File.Exists(path + ".bak")) File.Delete(path + ".bak");
            }

            world.Map.RemoveZone(grocery);
            Check.Equal(null, Zone.BuildingAt(fact.Place), "streets have no invented building type");
            Actor another = NpcIntentSupport.Actor(world, "another listener", 3, 2);
            NpcConversation.ShareRumor(world.Game, witness, another, fact, true);
            string fallback = player.Personality.HeardJournal[player.Personality.HeardJournal.Count - 1].Text;
            Check.Equal(true, fallback.Contains("in district " + World.CoordToString(0, 0)) &&
                !fallback.Contains("grocery store"), "without a building, the rumor names only the district");
        });
    }
}
