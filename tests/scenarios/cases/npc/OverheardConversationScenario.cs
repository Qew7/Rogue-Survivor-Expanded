using System;
using System.IO;
using System.Reflection;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Gameplay.Personality;

static class OverheardConversationScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/overheard-conversation", () => TownScenarioFactory.Arena(4704,
            "...#.............", "...#.............", "...#............."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            Actor player = NpcIntentSupport.Player(world, 6, 1);
            Actor speaker = NpcIntentSupport.Actor(world, "speaker", 2, 1);
            Actor target = NpcIntentSupport.Actor(world, "target", 1, 1);
            Actor hidden = NpcIntentSupport.Actor(world, "hidden", 5, 0);
            Actor far = NpcIntentSupport.Actor(world, "far", 16, 1);
            far.AudioRangeMod = -far.AudioRange;
            speaker.ActionPoints = Rules.BASE_ACTION_COST;
            world.Game.DoSay(speaker, target, "Could you find food for our camp?", RogueGame.Sayflags.IS_STORY | RogueGame.Sayflags.IS_REQUEST);
            MessageManager messages = (MessageManager)typeof(RogueGame).GetField("m_MessageManager",
                BindingFlags.Instance | BindingFlags.NonPublic).GetValue(world.Game);
            bool playerHeard = false;
            foreach (Message message in messages.History)
                if (message.Text.Contains("You overhear") && message.Text.Contains("Could you find food") &&
                    !message.Text.Contains("speaker")) playerHeard = true;
            Check.Equal(true, playerHeard, "player hears the request through a wall without learning the speaker's identity");
            SignificantEvent request = NpcEvents.Publish(world.Game, "requested_food", speaker, target);
            Check.Equal(true, hidden.Personality.Knowledge.Facts.Exists(f => f.EventId == request.Id &&
                f.Source == NpcKnowledgeSource.Told), "heard request becomes hearsay in NPC knowledge");
            Check.Equal(false, NpcIntentSupport.HasEvent(hidden, "requested_food"),
                "hearing does not invent a witnessed request event");
            ResidentRecord heard = Session.Get.ResidentRecords.Register(target);
            Check.Equal(true, HasHeard(heard, "Could you find food"),
                "the addressed NPC archives the actual spoken request");
            ResidentRecord behindWall = Session.Get.ResidentRecords.Register(hidden);
            Check.Equal(true, HasHeard(behindWall, "Could you find food"), "an awake NPC hears through the wall");
            Check.Equal(true, HasHeard(behindWall, "Heard someone say"), "hidden speaker's name is not revealed");
            ResidentRecord distant = Session.Get.ResidentRecords.Register(far);
            Check.Equal(false, HasHeard(distant, "Could you find food"), "outside hearing range does not invent a conversation");
            Check.Equal(false, NpcIntentSupport.HasEvent(player, "requested_food"), "hearing alone does not imply eyewitness knowledge");
            string path = Path.Combine(Path.GetTempPath(), "overheard-" + Guid.NewGuid().ToString("N"));
            try
            {
                BinarySaveStore.Save(path, Session.Get);
                RecordsSave archive = RecordsReader.Load(path);
                ResidentRecord saved = null;
                foreach (ResidentRecord resident in archive.Records.Residents)
                    if (resident.Identity == hidden.PersonalityIdentity) saved = resident;
                Check.Equal(true, saved != null && HasHeard(saved, "Could you find food"),
                    "Read Records loads the NPC's heard request from the save");
                bool searchable = false;
                foreach (string line in RecordsReader.Lines(archive, saved, null, "Could you find food", RecordsEventFilter.Encounters))
                    if (line.Contains("Could you find food")) searchable = true;
                Check.Equal(true, searchable, "heard request is searchable in Read Records");
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
                if (File.Exists(path + ".bak")) File.Delete(path + ".bak");
            }
            target.IsSleeping = true;
            int before = heard.Entries.Count;
            world.Game.DoSay(speaker, target, "A second request", RogueGame.Sayflags.IS_STORY);
            Check.Equal(before, heard.Entries.Count, "sleeping NPC does not archive unheard speech");
        });
    }

    static bool HasHeard(ResidentRecord record, string phrase)
    { foreach (ResidentEntry entry in record.Entries) if (entry.Text.Contains(phrase)) return true; return false; }
}
