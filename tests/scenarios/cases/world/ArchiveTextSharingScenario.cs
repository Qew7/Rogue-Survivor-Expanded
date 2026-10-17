using System;
using System.Collections.Generic;
using System.IO;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;

static class ArchiveTextSharingScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("storage/archive-text-sharing", () => TownScenarioFactory.Arena(4583,
            "....", "...."), world =>
        {
            Actor first = new Actor(world.Game.GameActors.MaleCivilian,
                world.Game.GameFactions.TheCivilians, "first", true, false, 0);
            Actor second = new Actor(world.Game.GameActors.FemaleCivilian,
                world.Game.GameFactions.TheCivilians, "second", true, false, 0);
            world.Place(first, 1, 1);
            world.Place(second, 2, 1);
            ResidentRecord a = Session.Get.ResidentRecords.Register(first);
            ResidentRecord b = Session.Get.ResidentRecords.Register(second);
            a.Add("note:one", 1, new string("The same report.".ToCharArray()));
            b.Add("note:two", 1, new string("The same report.".ToCharArray()));
            Check.Equal(true, Object.ReferenceEquals(a.Entries[a.EntryCount - 1].Text,
                b.Entries[b.EntryCount - 1].Text), "equal archive text shares one string in memory");

            string path = Path.Combine(Path.GetTempPath(), "archive-text-" + Guid.NewGuid().ToString("N"));
            try
            {
                BinarySaveStore.Save(path, Session.Get);
                ResidentRecords loaded = BinarySaveStore.LoadExact<Session>(path).ResidentRecords;
                var records = new List<ResidentRecord>(loaded.Residents);
                ResidentRecord restoredA = records.Find(r => r.Identity == first.PersonalityIdentity);
                ResidentRecord restoredB = records.Find(r => r.Identity == second.PersonalityIdentity);
                Check.Equal(true, Object.ReferenceEquals(restoredA.Entries[restoredA.EntryCount - 1].Text,
                    restoredB.Entries[restoredB.EntryCount - 1].Text),
                    "loaded archive restores text sharing");
                restoredA.Add("note:three", 2, new string("The same report.".ToCharArray()));
                Check.Equal(true, Object.ReferenceEquals(restoredA.Entries[restoredA.EntryCount - 1].Text,
                    restoredB.Entries[restoredB.EntryCount - 1].Text),
                    "new entries reuse loaded archive text");
                Check.Equal("The same report.", restoredB.Entries[restoredB.EntryCount - 1].Text,
                    "sharing preserves the report text");
            }
            finally { if (File.Exists(path)) File.Delete(path); }
        });
    }
}
