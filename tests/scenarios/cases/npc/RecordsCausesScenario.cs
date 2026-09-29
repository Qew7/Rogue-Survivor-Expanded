using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text.RegularExpressions;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Items;

static class RecordsCausesScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/records-causes", () => TownScenarioFactory.Arena(4824,
            ".........", ".........", "........."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            NpcIntentSupport.Player(world, 8, 2);
            Actor owner = NpcIntentSupport.Actor(world, "owner", 4, 1, "lawful", "frugal", "organized");
            Actor thief = NpcIntentSupport.Actor(world, "thief", 2, 1, "selfish", "rebellious");
            var baseArea = new XpdBase(owner, new[] { new Point(3, 1) });
            baseArea.SetFoodRoom(new Rectangle(3, 1, 1, 1)); world.Map.AddXpdBase(baseArea);
            var food = new ItemFood(world.Game.GameItems.CANNED_FOOD) { Quantity = 3 };
            world.Map.DropItemAt(food, new Point(3, 1)); world.Game.DoTakeItem(thief, new Point(3, 1), food);
            Check.Equal(true, NpcIntentSupport.HasEvent(owner, "base_theft"), "physical theft supplies a real archived cause");
            thief.RemoveAggressorOf(owner); owner.RemoveSelfDefenceFrom(thief);
            world.Map.RemoveActor(owner); world.Place(owner, 3, 0);
            NpcIntentSupport.Turn(world, owner);
            Check.Equal(true, NpcIntentSupport.HasEvent(thief, "restitution_requested"), "owner actually acts on the loss");
            ResidentRecord record = Session.Get.ResidentRecords.Register(owner);
            record.Add("note:orphan", 0, "orphan note", new ObservedEvent("note", 0, owner.UnmodifiedName, null, true, causeId: 999999));
            Session.Get.WorldTime.TurnCounter = world.Map.LocalTime.TurnCounter;
            string path = Path.Combine(Path.GetTempPath(), "npc-causes-" + Guid.NewGuid().ToString("N") + ".dat");
            try
            {
                BinarySaveStore.Save(path, Session.Get);
                RecordsSave archive = RecordsReader.Load(path);
                ResidentRecord saved = RecordsReader.Residents(archive).Find(r => r.Identity == owner.PersonalityIdentity);
                string story = Regex.Replace(String.Join(" ", new List<string>(RecordsReader.Lines(archive, saved)).ToArray()), @"\s+", " ");
                Check.Equal(true, story.Contains("Prompted by: thief stole from owner's base"),
                    "archive-only reader explains a goal from its actual cause");
                Check.Equal(true, story.Contains("Connected to: thief stole from owner's base"),
                    "the resulting action points to the same archived incident");
                string found = String.Join(" ", new List<string>(RecordsReader.Lines(archive, saved, null,
                    "stole from owner's base", RecordsEventFilter.Intentions)).ToArray());
                Check.Equal(true, found.Contains("Intent started"), "cause prose is searchable in the intended category");
                string orphan = String.Join(" ", new List<string>(RecordsReader.Lines(archive, saved, null,
                    "orphan note", RecordsEventFilter.All)).ToArray());
                Check.Equal(false, orphan.Contains("Connected to:"), "missing evidence never creates an invented reason");
            }
            finally { if (File.Exists(path)) File.Delete(path); if (File.Exists(path + ".bak")) File.Delete(path + ".bak"); }
        });
    }
}
