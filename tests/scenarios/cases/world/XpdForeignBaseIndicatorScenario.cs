using System.Drawing;
using System.Reflection;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;

static class XpdForeignBaseIndicatorScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("xpd/foreign-base-indicator", () => TownScenarioFactory.Arena(4428,
            ".......", ".......", ".......", ".......", ".......", ".......", "......."), world =>
        {
            Session.Get.GameMode = GameMode.GM_XPD;
            Actor player = new Actor(world.Game.GameActors.MaleCivilian,
                world.Game.GameFactions.TheCivilians, "player", false, false, 0);
            player.Controller = new PlayerController();
            world.Map.PlaceActorAt(player, new Point(2, 2));
            world.SetPlayer(player);
            Actor owner = new Actor(world.Game.GameActors.MaleCivilian,
                world.Game.GameFactions.TheCivilians, "owner", false, false, 0);
            world.Map.PlaceActorAt(owner, new Point(0, 0));
            XpdBase foreign = new XpdBase(owner, new[] {
                new Point(2, 2), new Point(2, 3), new Point(3, 2), new Point(3, 3) });
            world.Map.AddXpdBase(foreign);
            XpdBase own = new XpdBase(player, new[] { new Point(5, 5) });
            world.Map.AddXpdBase(own);

            ScenarioUI ui = (ScenarioUI)world.Game.UI;
            GameOptions before = RogueGame.Options;
            GameOptions options = before;
            options.IsMinimapOn = true;
            FieldInfo field = typeof(RogueGame).GetField("s_Options",
                BindingFlags.Static | BindingFlags.NonPublic);
            try
            {
                field.SetValue(null, options);
                int index = ui.DrawnStrings.Count;
                world.Game.DrawActorStatus(player, 0, 0);
                Check.Equal(true, ui.DrawnStrings[index].Contains("[FOREIGN BASE]"),
                    "status identifies current foreign base");
                Check.Equal(true, ui.DrawnStrings[index].Contains("[Base "),
                    "own base coordinate remains visible");
                world.Game.DrawMiniMap(world.Map);
                Check.Equal(false, ui.MinimapColors.ContainsKey(new Point(2, 2)),
                    "foreign base is not marked on minimap");
                Check.Equal(Color.LimeGreen, ui.MinimapColors[new Point(5, 5)],
                    "own base boundary remains green");

                world.Map.PlaceActorAt(player, new Point(5, 5));
                index = ui.DrawnStrings.Count;
                world.Game.DrawActorStatus(player, 0, 0);
                Check.Equal(false, ui.DrawnStrings[index].Contains("[FOREIGN BASE]"),
                    "foreign marker clears on own base");
                world.Map.PlaceActorAt(player, new Point(4, 4));
                index = ui.DrawnStrings.Count;
                world.Game.DrawActorStatus(player, 0, 0);
                Check.Equal(false, ui.DrawnStrings[index].Contains("[FOREIGN BASE]"),
                    "foreign marker stays hidden on unclaimed ground");
            }
            finally { field.SetValue(null, before); }
        });
    }
}
