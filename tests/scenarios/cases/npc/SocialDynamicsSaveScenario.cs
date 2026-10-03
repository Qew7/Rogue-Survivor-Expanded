using System;
using System.Drawing;
using System.IO;
using System.Linq;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Gameplay.Personality;

static class SocialDynamicsSaveScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/social-dynamics-save", () => TownScenarioFactory.Arena(4697,
            "..........", "..........", ".........."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            NpcIntentSupport.Player(world, 9, 2);
            Actor owner = NpcIntentSupport.Actor(world, "owner", 2, 1, "kind", "loyal");
            Actor asker = NpcIntentSupport.Actor(world, "asker", 1, 1, "lawful");
            Actor member = NpcIntentSupport.Actor(world, "member", 3, 1, "loyal");
            owner.AddFollower(member);
            Point at = new Point(1, 2);
            world.Map.AddXpdBase(new XpdBase(owner, new[] { at }));
            world.Map.DropItemAt(new ItemFood(world.Game.GameItems.CANNED_FOOD), at);
            PersonalitySystem.Report(world.Game, new SignificantEvent("resource_contested", asker, owner,
                world.Map, asker.Location.Position, 0) { ResourcePlace = new Location(world.Map, at), Resource = "food" });
            NpcIntentSupport.Turn(world, owner);
            Check.Equal(1, asker.Personality.Permissions.Count, "spoken concession creates saved permission");
            owner.SocialGroup.SupplyRule = 40;
            PersonalitySystem.Report(world.Game, new SignificantEvent("group_supply_rule_strict", owner, null,
                world.Map, owner.Location.Position, 0));
            Check.Equal(40, member.Personality.KnownSupplyRule(owner.SocialGroup.Identity),
                "present member learns the announced rule before saving");

            for (int x = 7; x <= 8; x++) for (int y = 1; y <= 2; y++) world.Map.GetTileAt(x, y).IsInside = true;
            Actor provider = NpcIntentSupport.Actor(world, "provider", 5, 1, "sociable", "honest", "humble");
            Actor patient = NpcIntentSupport.Actor(world, "patient", 6, 1, "sociable", "trusting");
            provider.Inventory.AddAll(new ItemMedicine(world.Game.GameItems.MEDIKIT)); patient.HitPoints = 1;
            provider.Personality.Knowledge.RememberPlace(new NpcKnownPlace(new Location(world.Map, new Point(7, 1)), "shelter", 0));
            PersonalitySystem.Report(world.Game, new SignificantEvent("requested_medicine", patient, provider,
                world.Map, patient.Location.Position, 0));
            NpcIntentSupport.Turn(world, provider);
            world.Map.LocalTime.TurnCounter = 1; NpcIntentSupport.Turn(world, patient);
            Check.Equal(NpcServiceStatus.Accepted, provider.Personality.ServiceAgreements[0].Status,
                "spoken agreement is active before saving");

            string path = Path.Combine(Path.GetTempPath(), "npc-social-dynamics-" + Guid.NewGuid().ToString("N"));
            try
            {
                BinarySaveStore.Save(path, Session.Get);
                Session loaded = BinarySaveStore.LoadExact<Session>(path);
                ScenarioWorld restored = NpcIntentSupport.Restore(world, loaded);
                Actor savedOwner = NpcIntentSupport.Find(restored.Map, owner.PersonalityIdentity);
                Actor savedAsker = NpcIntentSupport.Find(restored.Map, asker.PersonalityIdentity);
                Actor savedMember = NpcIntentSupport.Find(restored.Map, member.PersonalityIdentity);
                Actor savedProvider = NpcIntentSupport.Find(restored.Map, provider.PersonalityIdentity);
                Actor savedPatient = NpcIntentSupport.Find(restored.Map, patient.PersonalityIdentity);
                Check.Equal(40, savedOwner.SocialGroup.SupplyRule, "group rule survives save and load");
                Check.Equal(40, savedMember.Personality.KnownSupplyRule(savedOwner.SocialGroup.Identity),
                    "member's learned group rule survives save and load");
                Check.Same(restored.Map, savedAsker.Personality.Permissions[0].Place.Map,
                    "permission still names the restored map");
                Check.Equal(NpcServiceStatus.Accepted, savedProvider.Personality.ServiceAgreements[0].Status,
                    "provider retains the exchange obligation");
                Check.Equal(NpcServiceStatus.Accepted, savedPatient.Personality.ServiceAgreements[0].Status,
                    "patient retains a separate exchange obligation");
                Check.Equal(false, Object.ReferenceEquals(savedProvider.Personality.ServiceAgreements[0],
                    savedPatient.Personality.ServiceAgreements[0]), "participant knowledge has separate saved copies");
                Item food = restored.Map.GetItemsAt(at).Items.First();
                restored.Game.DoTakeItem(savedAsker, at, food);
                Check.Equal(false, NpcIntentSupport.HasEvent(savedOwner, "base_theft"),
                    "restored permission still authorizes a real pickup");
                string text = String.Join(" ", RecordsReader.Lines(RecordsReader.Load(path), null));
                Check.Equal(true, text.Contains("offered medicine in exchange for help reaching shelter"),
                    "readable records retain the spoken agreement");
            }
            finally { if (File.Exists(path)) File.Delete(path); if (File.Exists(path + ".bak")) File.Delete(path + ".bak"); }
        });
    }
}
