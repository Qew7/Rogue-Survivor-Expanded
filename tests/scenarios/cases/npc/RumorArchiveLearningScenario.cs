using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Gameplay.Personality;

static class RumorArchiveLearningScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/rumor-archive-learning", () => TownScenarioFactory.Arena(4781,
            "............", "............", "............"), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            Actor speaker = NpcIntentSupport.Actor(world, "speaker", 1, 1);
            Actor victim = NpcIntentSupport.Actor(world, "victim", 0, 0);
            Actor attacker = NpcIntentSupport.Actor(world, "attacker", 0, 1);
            NpcEvents.Publish(world.Game, "attack", victim, attacker, story: "two-attacks");
            NpcEvents.Publish(world.Game, "attack", victim, attacker, story: "two-attacks");
            List<NpcFact> chapter = speaker.Personality.Knowledge.Facts.FindAll(f => f.Kind == "attack" && f.StoryId == "two-attacks");
            Check.Equal(2, chapter.Count, "speaker knows both linked facts");

            Actor target = NpcIntentSupport.Actor(world, "target", 2, 1);
            Actor stale = NpcIntentSupport.Actor(world, "stale", 3, 1);
            Actor partial = NpcIntentSupport.Actor(world, "partial", 3, 0);
            Actor fresh = NpcIntentSupport.Actor(world, "fresh", 2, 2);
            Actor player = NpcIntentSupport.Player(world, 4, 1);
            foreach (NpcFact fact in chapter) NpcKnowledgeSystem.Hear(world.Game, stale, speaker, fact);
            NpcKnowledgeSystem.Hear(world.Game, partial, speaker, chapter[0]);

            NpcConversation.ShareRumor(world.Game, speaker, target, chapter[0], true);
            Check.Equal(0, Heard(stale), "listener who learned no fact gets no duplicate archive line");
            Check.Equal(1, Heard(partial), "new fact later in the chapter keeps an overheard archive line");
            Check.Equal(1, Heard(fresh), "new listener archives the spoken story");
            Check.Equal(1, Heard(target), "addressed listener archives the story");
            Check.Equal(2, target.Personality.Knowledge.Facts.FindAll(f => f.Kind == "attack" && f.StoryId == "two-attacks").Count,
                "both facts still reach the addressee");
            int playerHeard = 0;
            foreach (HeardJournalEntry entry in player.Personality.HeardJournal)
                if (entry.Kind == "heard_rumor" && entry.CauseId == chapter[0].EventId) playerHeard++;
            Check.Equal(1, playerHeard, "player hears each spoken rumor once");

            ResidentRecord freshRecord = Session.Get.ResidentRecords.Register(fresh);
            string path = Path.Combine(Path.GetTempPath(), "rumor-archive-" + Guid.NewGuid().ToString("N") + ".dat");
            try
            {
                // Simulate an old save's retained speech key; the next save must discard it.
                HashSet<string> keys = (HashSet<string>)typeof(ResidentRecord).GetField("m_KeySet",
                    BindingFlags.Instance | BindingFlags.NonPublic).GetValue(freshRecord);
                keys.Add("heard_speech:12345");
                typeof(ResidentRecord).GetField("m_SpeechKeysCompacted", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(freshRecord, false);
                BinarySaveStore.Save(path, Session.Get);
                RecordsSave archive = RecordsReader.Load(path);
                ResidentRecord loaded = null;
                foreach (ResidentRecord record in archive.Records.Residents)
                    if (record.Identity == fresh.PersonalityIdentity) loaded = record;
                Check.Equal(true, loaded != null && Heard(loaded) == 1,
                    "heard story survives archive-only save and load");
                HashSet<string> savedKeys = (HashSet<string>)typeof(ResidentRecord).GetField("m_KeySet",
                    BindingFlags.Instance | BindingFlags.NonPublic).GetValue(loaded);
                foreach (string key in savedKeys)
                    Check.Equal(false, key.StartsWith("heard_speech:", StringComparison.Ordinal),
                        "speech keys do not persist, including old-save keys");
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
                if (File.Exists(path + ".bak")) File.Delete(path + ".bak");
            }
        });
    }

    static int Heard(Actor actor) { return Heard(Session.Get.ResidentRecords.Register(actor)); }
    static int Heard(ResidentRecord record)
    {
        int count = 0;
        foreach (ResidentEntry entry in record.Entries) if (entry.Kind == "heard_rumor") count++;
        return count;
    }
}
