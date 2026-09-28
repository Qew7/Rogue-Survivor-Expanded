using System;
using System.Windows.Forms;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;

static class PersonalityRelationshipsScreenScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/personality-relationships-screen", () => TownScenarioFactory.Arena(4558,
            "...", "...", "..."), world =>
        {
            Actor player = new Actor(world.Game.GameActors.MaleCivilian,
                world.Game.GameFactions.TheCivilians, "player", true, false, 0);
            player.Controller = new PlayerController();
            world.Place(player, 1, 1);
            world.SetPlayer(player);
            ScenarioUI ui = (ScenarioUI)world.Game.UI;
            int turn = world.Map.LocalTime.TurnCounter;
            ui.QueueWaitKey(Keys.PageDown);
            ui.QueueWaitKey(Keys.Escape);
            Check.Call(world.Game, "HandleRelationships", Type.EmptyTypes);
            Check.Equal(true, ui.DrawnStrings.Contains("No relationships recorded yet."),
                "empty journal renders and ignores moving past its last page");
            player.Personality = new PersonalityState();
            for (int i = 0; i < 50; i++)
                player.Personality.RememberPerson(Guid.NewGuid(), "person " + i.ToString("D2"),
                    new MemoryInstance("received_help", 0, 1, "private episode"), 30);
            ui.DrawnStrings.Clear();
            ui.QueueWaitKey(Keys.PageDown);
            ui.QueueWaitKey(Keys.PageDown);
            ui.QueueWaitKey(Keys.PageUp);
            ui.QueueWaitKey(Keys.Escape);
            Check.Call(world.Game, "HandleRelationships", Type.EmptyTypes);
            Check.Equal(true, ui.DrawnStrings.Contains("  person 49: friendly"),
                "later page reaches the last relationship");
            Check.Equal(true, ui.DrawnStrings.Contains("  person 00: friendly"),
                "first page contains the sorted beginning");
            Check.Equal(false, String.Join(" ", ui.DrawnStrings.ToArray()).Contains("private episode"),
                "screen never renders memory content");
            Check.Equal(turn, world.Map.LocalTime.TurnCounter, "viewing relationships costs no turn");
        });
    }
}
