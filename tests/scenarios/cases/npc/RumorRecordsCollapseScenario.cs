using System;
using System.Collections.Generic;
using System.IO;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;

static class RumorRecordsCollapseScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/rumor-records-collapse", () => TownScenarioFactory.Arena(4782,
            ".....", ".....", "....."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            Actor speaker = NpcIntentSupport.Actor(world, "speaker", 1, 1);
            Actor other = NpcIntentSupport.Actor(world, "other", 1, 2);
            Actor listener = NpcIntentSupport.Actor(world, "listener", 2, 1);
            const string report = "Raid at the station.";
            RogueGame.Sayflags flags = RogueGame.Sayflags.IS_STORY | RogueGame.Sayflags.IS_RUMOR |
                RogueGame.Sayflags.IS_FREE_ACTION;
            world.Game.DoSay(speaker, listener, report, flags, 123, "raid-story");
            world.Map.LocalTime.TurnCounter = Session.Get.WorldTime.TurnCounter = 5;
            world.Game.DoSay(speaker, listener, report, flags, 123, "raid-story");
            world.Map.LocalTime.TurnCounter = Session.Get.WorldTime.TurnCounter = 6;
            world.Game.DoSay(other, listener, report, flags, 123, "raid-story");
            world.Game.DoSay(speaker, listener, report, flags, 124, "raid-story");
            ResidentRecord record = Session.Get.ResidentRecords.Register(listener);
            Check.Equal(4, Heard(record), "the underlying archive retains every speech event");

            string path = Path.Combine(Path.GetTempPath(), "rumor-collapse-" + Guid.NewGuid().ToString("N") + ".dat");
            try
            {
                BinarySaveStore.Save(path, Session.Get);
                RecordsSave archive = RecordsReader.Load(path);
                ResidentRecord saved = null;
                foreach (ResidentRecord resident in archive.Records.Residents)
                    if (resident.Identity == listener.PersonalityIdentity) saved = resident;
                IList<string> lines = RecordsReader.Lines(archive, saved, null, report, RecordsEventFilter.Encounters);
                Check.Equal(3, Count(lines, report), "another speaker or underlying fact remains distinct");
                Check.Equal(1, Count(lines, "heard 2 times"), "collapsed line reports the repeat count");
                Check.Equal(1, Count(lines, "last at"), "collapsed line retains the last hearing time");
                Check.Equal(4, Heard(saved), "display grouping never deletes archived events");
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
                if (File.Exists(path + ".bak")) File.Delete(path + ".bak");
            }
        });
    }

    static int Heard(ResidentRecord record)
    {
        int count = 0;
        foreach (ResidentEntry entry in record.Entries) if (entry.Kind == "heard_rumor") count++;
        return count;
    }
    static int Count(IList<string> lines, string text)
    {
        int count = 0;
        foreach (string line in lines) if (line.Contains(text)) count++;
        return count;
    }
}
