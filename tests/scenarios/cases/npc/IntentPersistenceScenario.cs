using System;
using System.Collections.Generic;
using System.IO;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Gameplay.Personality;

static class IntentPersistenceScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/intent-persistence", () => TownScenarioFactory.Arena(4606, ".....", ".....", "....."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            Actor helper = NpcIntentSupport.Player(world, 2, 1);
            Actor grateful = NpcIntentSupport.Actor(world, "grateful", 1, 1, "generous");
            NpcIntentSupport.Food(world, grateful, 3);
            SignificantEvent source = new SignificantEvent("helped", grateful, helper, world.Map, grateful.Location.Position, 0);
            PersonalitySystem.Report(world.Game, source);
            NpcIntent intent = grateful.Personality.Intents[0];
            NpcIntentSystem.Maintain(world.Game, grateful, new[] { helper }, true, false);
            Check.Equal(NpcIntentStatus.Paused, intent.Status, "danger pauses repayment without removing it");
            string path = Path.Combine(Path.GetTempPath(), "intent-" + Guid.NewGuid().ToString("N"));
            try
            {
                BinarySaveStore.Save(path, Session.Get);
                Session loaded = BinarySaveStore.LoadExact<Session>(path);
                ScenarioWorld restored = NpcIntentSupport.Restore(world, loaded);
                Actor actor = NpcIntentSupport.Find(restored.Map, grateful.PersonalityIdentity);
                Actor player = NpcIntentSupport.Find(restored.Map, helper.PersonalityIdentity);
                NpcIntent saved = actor.Personality.Intents[0];
                Check.Equal(intent.StoryId, saved.StoryId, "story identity survives");
                Check.Equal(source.Id, saved.CauseId, "source event identity survives");
                Check.Equal(NpcIntentStatus.Paused, saved.Status, "paused intention survives");
                Check.Same(restored.Map, saved.LastKnown.Map, "known position shares the loaded world map");
                Check.Equal(1, actor.Personality.Reactions.Count, "pending reaction survives");
                NpcIntentSupport.Turn(restored, actor); NpcIntentSupport.Turn(restored, actor);
                Check.Equal(1, NpcIntentSupport.FoodUnits(player), "restored AI resumes and transfers one item");
                Check.Equal(NpcIntentStatus.Completed, saved.Status, "resumed intention completes");
                BinarySaveStore.Save(path, loaded);
                RecordsSave records = RecordsReader.Load(path);
                ResidentRecord record = RecordsReader.Residents(records).Find(r => r.Identity == actor.PersonalityIdentity);
                RecordsProfile profile = new RecordsProfile(record, records.Turn);
                Check.Equal(1, profile.GoalsStarted, "creation is not repeated across saves");
                Check.Equal(1, profile.GoalsCompleted, "completion is recorded once");
                string lines = String.Join(" ", new List<string>(RecordsReader.Lines(records, record, null, "", RecordsEventFilter.Intentions)).ToArray());
                Check.Equal(true, lines.Contains("Intent completed") && lines.Contains(saved.StoryId), "archive-only reader exposes the linked episode");
                SignificantEvent replay = new SignificantEvent("helped", actor, player, restored.Map, actor.Location.Position, 0); replay.Id = source.Id;
                PersonalitySystem.Report(restored.Game, replay);
                Check.Equal(1, actor.Personality.Intents.Count, "old source cannot restart a completed intention after load");
                SignificantEvent next = NpcEvents.Publish(restored.Game, "raid", actor, null);
                Check.Equal(true, next.Id > source.Id, "event sequence continues after loading");
            }
            finally { if (File.Exists(path)) File.Delete(path); if (File.Exists(path + ".bak")) File.Delete(path + ".bak"); }
        });
    }
}
