using System;
using System.IO;
using System.Windows.Forms;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;

static class PlayerHeardJournalScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/player-heard-journal", () => TownScenarioFactory.Arena(4820,
            "............", "............", "............"), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            Actor player = NpcIntentSupport.Player(world, 1, 1);
            Actor speaker = NpcIntentSupport.Actor(world, "speaker", 2, 1);
            Actor listener = NpcIntentSupport.Actor(world, "listener", 3, 1);
            RogueGame.KeyBindings.ResetToDefaults();
            Check.Equal(PlayerCommand.HEARD_JOURNAL, InputTranslator.KeyToCommand(new KeyEventArgs(Keys.J)),
                "J opens the heard journal");
            world.Game.DoSay(speaker, listener, "The bikers took supplies.",
                RogueGame.Sayflags.IS_STORY | RogueGame.Sayflags.IS_RUMOR | RogueGame.Sayflags.IS_FREE_ACTION);
            Check.Equal(1, player.Personality.HeardJournal.Count, "player hears NPC to NPC rumor");
            Check.Equal("heard_rumor", player.Personality.HeardJournal[0].Kind, "rumor is labeled");
            world.Game.DoSay(speaker, player, "Can you bring food?",
                RogueGame.Sayflags.IS_STORY | RogueGame.Sayflags.IS_REQUEST | RogueGame.Sayflags.IS_FREE_ACTION);
            Check.Equal(2, player.Personality.HeardJournal.Count, "direct request is retained");
            player.IsSleeping = true;
            world.Game.DoSay(speaker, listener, "Sleepers miss this.",
                RogueGame.Sayflags.IS_STORY | RogueGame.Sayflags.IS_RUMOR | RogueGame.Sayflags.IS_FREE_ACTION);
            Check.Equal(2, player.Personality.HeardJournal.Count, "sleeping player hears nothing");
            player.IsSleeping = false;
            string path = Path.Combine(Path.GetTempPath(), "heard-journal-" + Guid.NewGuid().ToString("N"));
            try
            {
                BinarySaveStore.Save(path, Session.Get);
                Session loaded = BinarySaveStore.Load<Session>(path);
                Actor saved = NpcIntentSupport.Find(loaded.World[0, 0].EntryMap, player.PersonalityIdentity);
                Check.Equal(2, saved.Personality.HeardJournal.Count, "heard speech survives saving");
                Check.Equal("Can you bring food?", saved.Personality.HeardJournal[1].Text,
                    "request text survives saving");
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
                if (File.Exists(path + ".bak")) File.Delete(path + ".bak");
            }
            ScenarioUI ui = (ScenarioUI)world.Game.UI;
            ui.QueueWaitKey(Keys.Escape);
            Check.Call(world.Game, "HandleHeardJournal", Type.EmptyTypes);
            Check.Equal(true, String.Join(" ", ui.DrawnStrings.ToArray()).Contains("The bikers took supplies."),
                "journal renders heard rumor");
        });
    }
}
