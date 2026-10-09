using System;
using System.Reflection;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;

static class RestProgressScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("ui/rest-progress", () => TownScenarioFactory.Arena(4980,
            ".....", ".....", "....."), world =>
        {
            Actor player = NpcIntentSupport.Player(world, 2, 1);
            ScenarioUI ui = (ScenarioUI)world.Game.UI;
            Type game = typeof(RogueGame);
            const BindingFlags fields = BindingFlags.Instance | BindingFlags.NonPublic;
            game.GetField("m_RestSimulationProgress", fields).SetValue(world.Game,
                "Simulating city: 2/8 district turns, ~3s left");
            world.Game.RedrawPlayScreen();
            Check.Equal(true, ui.DrawnStrings.Exists(s => s.Contains("2/8 district turns")),
                "simulation progress is drawn over the game");

            game.GetField("m_RestSimulationProgress", fields).SetValue(world.Game, null);
            game.GetField("m_IsPlayerLongWait", fields).SetValue(world.Game, true);
            game.GetField("m_PlayerLongWaitEnd", fields).SetValue(world.Game, new WorldTime(20));
            ui.DrawnStrings.Clear();
            world.Game.RedrawPlayScreen();
            Check.Equal(true, ui.DrawnStrings.Exists(s => s.Contains("20 turns left")),
                "long wait displays remaining turns");
            game.GetField("m_RestStartTurn", fields).SetValue(world.Game, 0);
            game.GetField("m_RestStartTicks", fields).SetValue(world.Game,
                System.Diagnostics.Stopwatch.GetTimestamp() - System.Diagnostics.Stopwatch.Frequency);
            Session.Get.WorldTime.TurnCounter = 1;
            ui.DrawnStrings.Clear();
            world.Game.RedrawPlayScreen();
            Check.Equal(true, ui.DrawnStrings.Exists(s => s.Contains("19 turns left") && s.Contains("s")),
                "long wait countdown and time estimate advance after a turn");

            game.GetField("m_IsPlayerLongWait", fields).SetValue(world.Game, false);
            player.SleepPoints = 0;
            world.Game.DoStartSleeping(player);
            ui.DrawnStrings.Clear();
            world.Game.RedrawPlayScreen();
            Check.Equal(true, ui.DrawnStrings.Exists(s => s.StartsWith("Sleeping: ~")),
                "sleep keeps a visible status between simulation updates");
            game.GetField("m_RestProgressLastDraw", fields).SetValue(world.Game,
                System.Diagnostics.Stopwatch.GetTimestamp());
            Type flags = game.GetNestedType("SimFlags", BindingFlags.NonPublic);
            object active = Enum.Parse(flags, "NOT_SIMULATING");
            int before = ui.RepaintCount;
            for (int i = 0; i < 10; i++)
            {
                player.SleepPoints = 0;
                Check.Call(world.Game, "NextMapTurn", new[] { typeof(Map), flags }, world.Map, active);
            }
            Check.Equal(true, ui.RepaintCount - before < 10,
                "sleep does not redraw the whole screen on every turn");
        });
    }
}
