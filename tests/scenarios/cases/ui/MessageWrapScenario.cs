using System;
using System.Collections.Generic;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using Message = djack.RogueSurvivor.Data.Message;

static class MessageWrapScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("ui/message-wrap", () => TownScenarioFactory.Arena(4837,
            "...", "...", "..."), world =>
        {
            ScenarioUI ui = (ScenarioUI)world.Game.UI;
            MessageManager manager = (MessageManager)typeof(RogueGame).GetField("m_MessageManager",
                BindingFlags.NonPublic | BindingFlags.Instance).GetValue(world.Game);
            string token = new string('X', 180);
            manager.Add(new Message("Short message.", 1, Color.White));
            manager.Add(new Message("A caller asks for news of missing neighbors. " + token +
                " A coastal freighter is blamed.", 2, Color.LightGreen));
            ui.DrawnText.Clear();
            ui.DrawnBold.Clear();
            manager.Draw(ui, 2, 4, 676, 700, 7);
            Check.Equal(true, ui.DrawnText.Count > 2, "long message uses several rows");
            Check.Equal(true, ui.DrawnText.Count <= 7, "message panel stays within its row budget");
            foreach (Tuple<string, int, int> draw in ui.DrawnText)
            {
                Check.Equal(true, ui.UI_BoldTextWidth(draw.Item1) <= 700,
                    "each panel line fits its measured width");
                Check.Equal(true, draw.Item3 < RogueGame.CANVAS_HEIGHT,
                    "each panel line stays on canvas");
            }
            foreach (Tuple<Color, string> draw in ui.DrawnBold)
                Check.Equal(Color.LightGreen.ToArgb(), draw.Item1.ToArgb(),
                    "continuation rows retain their message color");
            List<Message> history = manager.WrappedHistory(ui, 700);
            string joined = "";
            foreach (Message line in history)
            {
                Check.Equal(true, ui.UI_BoldTextWidth(line.Text) <= 700,
                    "history line fits its measured width");
                joined += line.Text.Replace(" ", "");
            }
            Check.Equal(true, joined.Contains(token), "hard wrapped token remains complete");

            for (int i = 0; i < 35; i++)
                manager.Add(new Message("Bulletin " + i + " " + new string('W', 130), 3, Color.White));
            manager.Add(new Message("Latest bulletin.", 4, Color.Yellow));
            Type[] mouseInput = { typeof(Point), typeof(MouseButtons?), typeof(int) };
            int turn = Session.Get.WorldTime.TurnCounter;
            Check.Equal(false, Check.Call(world.Game, "HandleMessagePanelInput", mouseInput,
                new Point(700, 700), null, 120), "wheel outside message panel is ignored");
            Check.Equal(true, Check.Call(world.Game, "HandleMessagePanelInput", mouseInput,
                new Point(100, 700), null, 120), "wheel over messages is handled");
            Check.Equal(3, manager.ScrollOffset, "wheel scrolls message lines upward");
            Check.Call(world.Game, "HandleMessagePanelInput", mouseInput,
                new Point(645, 721), MouseButtons.Left, 0);
            Check.Equal(0, manager.ScrollOffset, "down button returns to latest messages");
            ui.DrawnText.Clear();
            ui.QueueWaitKey(Keys.Home);
            ui.QueueWaitKey(Keys.End);
            ui.QueueWaitKey(Keys.Escape);
            Check.Call(world.Game, "HandleMessagePanelInput", mouseInput,
                new Point(100, 700), MouseButtons.Left, 0);
            Check.Equal(true, ui.DrawnText.Exists(draw => draw.Item1.Contains("Short message.")),
                "log can return to oldest line");
            Check.Equal(true, ui.DrawnText.Exists(draw => draw.Item1.Contains("Latest bulletin.")),
                "log can return to newest line");
            Check.Equal(turn, Session.Get.WorldTime.TurnCounter,
                "scrolling and opening the log do not spend a turn");
        });
    }
}
