using System;
using System.IO;
using System.Linq;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Gameplay.Personality;

static class CausalStoryRumorScenario
{
    static int Count(string text, string phrase)
    {
        int count = 0, from = 0, at;
        while ((at = text.IndexOf(phrase, from, StringComparison.Ordinal)) >= 0)
        { count++; from = at + phrase.Length; }
        return count;
    }

    public static void Register()
    {
        ScenarioRunner.Add("npc/causal-story-rumor", () => TownScenarioFactory.Arena(4638,
            "...#....", "...#....", "...#...."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            Session.Get.Seed = 4638;
            World city = new World(1);
            city[0, 0] = world.Map.District;
            Session.Get.World = city;
            Actor player = NpcIntentSupport.Player(world, 7, 2);
            Actor teller = NpcIntentSupport.Actor(world, "teller", 0, 1);
            Actor giver = NpcIntentSupport.Actor(world, "giver", 1, 1);
            Actor recipient = NpcIntentSupport.Actor(world, "recipient", 2, 1);
            Actor listener = NpcIntentSupport.Actor(world, "listener", 6, 1);

            SignificantEvent promise = NpcEvents.Publish(world.Game, "food_promised", giver, recipient,
                story: "promise-chapter");
            world.Map.LocalTime.TurnCounter = 1;
            SignificantEvent kept = NpcEvents.Publish(world.Game, "promise_kept", giver, recipient,
                promise.Id, "promise-chapter");
            world.Map.LocalTime.TurnCounter = 2;
            SignificantEvent unrelated = NpcEvents.Publish(world.Game, "shared_food", giver, recipient,
                99999, "promise-chapter");
            NpcFact promiseFact = teller.Personality.Knowledge.Facts.First(f => f.EventId == promise.Id);
            NpcFact keptFact = teller.Personality.Knowledge.Facts.First(f => f.EventId == kept.Id);
            Check.Equal(promise.Id, keptFact.CauseId, "witness retains the real event link");

            world.Place(teller, 5, 1);
            NpcConversation.ShareRumor(world.Game, teller, listener, promiseFact, true);
            string story = player.Personality.HeardJournal.Last().Text;
            Check.Equal(1, Count(story, " As a result, "), "only the linked next event has causal wording");
            Check.Equal(true, story.Contains(" Later, ") || story.Contains(" After that, "),
                "missing antecedent uses a neutral transition");
            Check.Equal(true, listener.Personality.Knowledge.Facts.Any(f => f.EventId == promise.Id) &&
                listener.Personality.Knowledge.Facts.Any(f => f.EventId == kept.Id) &&
                listener.Personality.Knowledge.Facts.Any(f => f.EventId == unrelated.Id),
                "every reported event survives condensed speech");
            NpcFact heard = listener.Personality.Knowledge.Facts.First(f => f.EventId == kept.Id);
            Check.Equal(promise.Id, heard.CauseId, "retelling preserves the causal link");
            Check.Equal(promise.Id, heard.Retell(listener.PersonalityIdentity, 3, 50).CauseId,
                "another retelling preserves the link");

            player.Personality.Knowledge.Facts.Clear();
            Session.Get.WorldTime.TurnCounter = 26 * WorldTime.TURNS_PER_HOUR;
            RadioProgram radio = (RadioProgram)Check.Call(world.Game, "GetRadioProgram", 0, 26);
            Check.Equal(true, radio.Facts.Any(f => f.EventId == kept.Id),
                "radio includes the linked event");
            Check.Equal(1, Count(radio.Text, " As a result, "),
                "radio uses the same supported causal transition");

            string path = Path.Combine(Path.GetTempPath(), "causal-rumor-" + Guid.NewGuid().ToString("N"));
            try
            {
                BinarySaveStore.Save(path, Session.Get);
                Session loaded = BinarySaveStore.LoadExact<Session>(path);
                Actor saved = NpcIntentSupport.Find(loaded.World[0, 0].EntryMap, listener.PersonalityIdentity);
                Check.Equal(promise.Id, saved.Personality.Knowledge.Facts.First(f => f.EventId == kept.Id).CauseId,
                    "causal link survives save and load");
                Check.Equal(99999L, saved.Personality.Knowledge.Facts.First(f => f.EventId == unrelated.Id).CauseId,
                    "unresolved link remains available without claiming a cause in speech");
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
                if (File.Exists(path + ".bak")) File.Delete(path + ".bak");
            }
        });
    }
}
