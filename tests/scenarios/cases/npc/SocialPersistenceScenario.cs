using System;
using System.IO;
using System.Drawing;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Items;

static class SocialPersistenceScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/social-persistence", () => TownScenarioFactory.Arena(4668, ".........", ".........", "........."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD); NpcIntentSupport.Player(world, 8, 2);
            Actor recipient = NpcIntentSupport.Actor(world, "recipient", 2, 1, "sociable");
            Actor helper = NpcIntentSupport.Actor(world, "helper", 1, 1, "kind", "honest");
            recipient.FoodPoints = Session.Get.GamePreset.HungerPoints - 1;
            NpcIntentSupport.Turn(world, recipient); NpcIntentSupport.Turn(world, helper);
            NpcCommitment promise = helper.Personality.Commitments[0];
            var treasure = new ItemMeleeWeapon(world.Game.GameItems.BASEBALLBAT); helper.Inventory.AddAll(treasure);
            world.Game.DoEquipItem(helper, treasure);
            helper.Personality.Attach(new NpcAttachment { Kind = "item", ItemId = treasure.StoryIdentity, ModelId = treasure.Model.ID, Weight = 35 });
            helper.Personality.RememberDispute(new NpcResourceDispute { Other = recipient.PersonalityIdentity, Place = recipient.Location,
                Resource = "medicine", Response = "resource_refused", CauseId = promise.Id });
            string path = Path.Combine(Path.GetTempPath(), "npc-social-" + Guid.NewGuid().ToString("N"));
            try
            {
                BinarySaveStore.Save(path, Session.Get); Session loaded = BinarySaveStore.LoadExact<Session>(path);
                ScenarioWorld restored = NpcIntentSupport.Restore(world, loaded); Actor actor = NpcIntentSupport.Find(restored.Map, helper.PersonalityIdentity);
                Actor beneficiary = NpcIntentSupport.Find(restored.Map, recipient.PersonalityIdentity);
                NpcCommitment saved = actor.Personality.Commitments[0];
                Check.Equal(promise.Id, saved.Id, "promised identity survives compact saving");
                Check.Equal(promise.DueTurn, saved.DueTurn, "deadline is not reset by loading");
                Check.Equal(false, Object.ReferenceEquals(saved, beneficiary.Personality.Commitments[0]), "participants keep separate knowledge snapshots");
                Check.Same(restored.Map, actor.Personality.Disputes[0].Place.Map, "dispute shares the restored world map");
                Check.Equal(treasure.StoryIdentity, actor.Personality.Attachments[0].ItemId, "specific possession identity survives");
                restored.Map.DropItemAt(new ItemFood(restored.Game.GameItems.CANNED_FOOD) { Quantity = 3 }, new Point(1, 2));
                restored.Map.LocalTime.TurnCounter = 1; NpcIntentSupport.Turn(restored, actor);
                restored.Map.LocalTime.TurnCounter = 2; NpcIntentSupport.Turn(restored, actor);
                Check.Equal(NpcCommitmentStatus.Kept, saved.Status, "saved commitment resumes through real acquisition and delivery");
                loaded.WorldTime.TurnCounter = 2;
                BinarySaveStore.Save(path, loaded);
                RecordsSave archive = RecordsReader.Load(path);
                bool retainedCause = false;
                foreach (ResidentRecord resident in archive.Records.Residents)
                    if (resident.Identity == helper.PersonalityIdentity)
                        foreach (ResidentEntry entry in resident.Entries)
                            if (entry.SupportingCauses != null && Array.IndexOf(entry.SupportingCauses, promise.Id) >= 0) retainedCause = true;
                Check.Equal(true, retainedCause, "archive retains the supporting promise cause without loading the world");
                string text = String.Join(" ", RecordsReader.Lines(archive, null));
                Check.Equal(true, text.Contains("fulfilled their promise"), "archive-only reader exposes actual fulfilment");
                Check.Equal(false, text.Contains("promise_kept:"), "social events have readable chronicle text");
            }
            finally { if (File.Exists(path)) File.Delete(path); if (File.Exists(path + ".bak")) File.Delete(path + ".bak"); }
        });
    }
}
