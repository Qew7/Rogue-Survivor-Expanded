using System;
using System.Drawing;
using System.IO;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Gameplay.Personality;

static class SupplyLossRumorSaveScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/supply-loss-rumor-save", () => TownScenarioFactory.Arena(4904,
            ".....", ".....", "....."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            Actor witness = NpcIntentSupport.Actor(world, "witness", 0, 1);
            Actor owner = NpcIntentSupport.Actor(world, "owner", 1, 1);
            Actor thief = NpcIntentSupport.Actor(world, "thief", 2, 1);
            thief.Faction = world.Game.GameFactions.TheBikers;
            var claim = new XpdBase(owner, new[] { new Point(2, 1) });
            claim.SetFoodRoom(new Rectangle(2, 1, 1, 1)); world.Map.AddXpdBase(claim);
            var food = new ItemFood(world.Game.GameItems.CANNED_FOOD) { Quantity = 3 };
            world.Map.DropItemAt(food, new Point(2, 1));
            world.Game.DoTakeItem(thief, new Point(2, 1), food);
            NpcFact fact = witness.Personality.Knowledge.Facts.Find(f => f.Kind == "supplies_lost");
            string path = Path.Combine(Path.GetTempPath(), "supply-rumor-" + Guid.NewGuid().ToString("N"));
            try
            {
                BinarySaveStore.Save(path, Session.Get);
                Session loaded = BinarySaveStore.Load<Session>(path);
                Map map = loaded.World[0, 0].EntryMap; map.ReconstructAuxiliaryFields();
                Actor saved = NpcIntentSupport.Find(map, witness.PersonalityIdentity);
                NpcFact retained = saved.Personality.Knowledge.Facts.Find(f => f.EventId == fact.EventId);
                Check.Equal("food", retained.Resource, "saved rumor retains the actual resource");
                Check.Equal(3, retained.Units, "saved rumor retains the quantity");
                Check.Equal("a biker", retained.Retell(saved.PersonalityIdentity, 1, 70).ReportOther,
                    "retelling after load preserves the original faction description");
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
                if (File.Exists(path + ".bak")) File.Delete(path + ".bak");
            }
        });
    }
}
