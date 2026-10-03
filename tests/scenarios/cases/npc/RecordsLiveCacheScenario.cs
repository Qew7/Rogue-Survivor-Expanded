using System;
using System.Collections.Generic;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Gameplay.Personality;

static class RecordsLiveCacheScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/records-live-cache", () => TownScenarioFactory.Arena(4842,
            ".....", ".....", "....."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            Actor actor = NpcIntentSupport.Actor(world, "resident", 2, 1);
            ResidentRecord record = Session.Get.ResidentRecords.Register(actor);
            var live = new RecordsSave("live", Session.Get);
            string initial = String.Join(" ", new List<string>(RecordsReader.Lines(live, record)).ToArray());
            Check.Equal(false, initial.Contains("prepared cause"), "viewer starts from the current archive");
            record.Add("cause:test", 0, "prepared cause.", new ObservedEvent("note", 0,
                actor.UnmodifiedName, null, true, eventId: 4842));
            record.Add("effect:test", 0, "prepared effect.", new ObservedEvent("note", 0,
                actor.UnmodifiedName, null, true, causeId: 4842));
            string linked = String.Join(" ", new List<string>(RecordsReader.Lines(live, record, null,
                "prepared cause", RecordsEventFilter.All)).ToArray());
            Check.Equal(true, linked.Contains("prepared effect") && linked.Contains("Connected to: prepared cause"),
                "new live entries refresh the searchable cause index");
            Session.Get.WorldTime.TurnCounter = 1;
            record.Add("next:test", 1, "next turn entry.");
            string advanced = String.Join(" ", new List<string>(RecordsReader.Lines(live, record)).ToArray());
            Check.Equal(true, advanced.Contains("next turn entry"), "live viewer follows the session turn");
        });
    }
}
