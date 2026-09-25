using System.Drawing;
using System.Reflection;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Gameplay;

static class MinimapRenderingScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("world/minimap-rendering", () => TownScenarioFactory.Arena(4535,
            "....", "....", "...."), world =>
        {
            Actor player = SkillScenario.Actor(world);
            world.Place(player, 1, 1);
            world.SetPlayer(player);
            world.Map.GetTileAt(1, 1).IsVisited = true;
            world.Map.GetTileAt(1, 1).AddDecoration(GameImages.DECO_PLAYER_TAG1);
            ScenarioUI ui = (ScenarioUI)world.Game.UI;
            GameOptions before = RogueGame.Options;
            GameOptions options = before;
            options.IsMinimapOn = true;
            options.ShowPlayerTagsOnMinimap = true;
            FieldInfo field = typeof(RogueGame).GetField("s_Options",
                BindingFlags.Static | BindingFlags.NonPublic);
            try
            {
                field.SetValue(null, options);
                world.Game.DrawMiniMap(world.Map);
                Check.Equal(world.Map.GetTileAt(1, 1).Model.MinimapColor,
                    ui.MinimapColors[new Point(1, 1)], "visited tile receives minimap color");
                Check.Equal(false, ui.MinimapColors.ContainsKey(new Point(2, 1)),
                    "unvisited tile stays hidden");
                Check.Equal(true, ui.DrawnImages.Contains(GameImages.MINI_PLAYER_TAG1),
                    "player tag remains visible above minimap");
                world.Map.GetTileAt(2, 1).IsVisited = true;
                world.Game.DrawMiniMap(world.Map);
                Check.Equal(true, ui.MinimapColors.ContainsKey(new Point(2, 1)),
                    "newly visited tile appears on next draw");
            }
            finally { field.SetValue(null, before); }
        });
    }
}
