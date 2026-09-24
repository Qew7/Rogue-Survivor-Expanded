using System.Drawing;
using System.Reflection;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Actions;
using djack.RogueSurvivor.Engine.MapObjects;

static class MouseContextMenuScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("movement/mouse-context-exit",
            () => TownScenarioFactory.Arena(4411, ".....", ".....", "....."),
            world =>
            {
                Actor player = SkillScenario.Actor(world);
                player.Controller = new PlayerController();
                world.Map.PlaceActorAt(player, new Point(4, 1));
                world.SetPlayer(player);
                Point nextTile = new Point(3, 1);
                Check.Equal(MouseContextActionKind.Move,
                    MouseContextMenu.Actions(player, world.Game, nextTile, true, true)[0].Kind,
                    "reachable tile offers movement");
                DoorWindow door = new DoorWindow("door", "closed", "open", "broken", 40);
                world.Map.PlaceMapObjectAt(door, nextTile);
                Check.Equal(1, MouseContextMenu.Actions(player, world.Game, nextTile,
                    true, false).FindAll(action => action.Label == "Open door").Count,
                    "closed adjacent door offers opening");
                Check.Equal(true, world.Try(new ActionOpenDoor(player, world.Game, door)),
                    "door opens using existing action");
                Check.Equal(1, MouseContextMenu.Actions(player, world.Game, nextTile,
                    true, true).FindAll(action => action.Kind == MouseContextActionKind.CloseDoor).Count,
                    "open adjacent door offers closing");
                Point boundary = new Point(5, 1);
                Check.Equal(0, MouseContextMenu.Actions(player, world.Game, boundary,
                    false, false).Count, "unlinked boundary has no travel action");

                Map nextMap = new Map(4412, "neighbor", 5, 3);
                for (int x = 0; x < nextMap.Width; x++)
                    for (int y = 0; y < nextMap.Height; y++)
                        nextMap.SetTileModelAt(x, y, world.Game.GameTiles.FLOOR_ASPHALT);
                District nextDistrict = new District(new Point(1, 0), DistrictKind.RESIDENTIAL);
                nextDistrict.EntryMap = nextMap;
                world.Map.SetExitAt(boundary, new Exit(nextMap, new Point(0, 1)));

                MouseContextAction travel = MouseContextMenu.Actions(player, world.Game,
                    boundary, false, false)[0];
                Check.Equal(MouseContextActionKind.LeaveMap, travel.Kind,
                    "clicking the edge exit offers district travel");
                Check.Equal(true, travel.Label.StartsWith("Leave district"),
                    "travel label names the district transition");
                Check.Equal(1, MouseContextMenu.Actions(player, world.Game,
                    player.Location.Position, true, false).FindAll(
                        action => action.Kind == MouseContextActionKind.LeaveMap).Count,
                    "player tile also offers the nearby border exit");
                Check.Equal(0, MouseContextMenu.Actions(player, world.Game,
                    new Point(6, 1), false, false).Count,
                    "nonadjacent boundary cannot be used");

                GameOptions originalOptions = RogueGame.Options;
                GameOptions withoutThread = originalOptions;
                withoutThread.SimThread = false;
                FieldInfo optionsField = typeof(RogueGame).GetField("s_Options",
                    BindingFlags.Static | BindingFlags.NonPublic);
                try
                {
                    optionsField.SetValue(null, withoutThread);
                    Check.Equal(true, (bool)Check.Call(world.Game, "ExecuteMouseContextAction",
                        player, travel), "menu action performs travel without a keyboard prompt");
                }
                finally { optionsField.SetValue(null, originalOptions); }
                Check.Same(nextMap, player.Location.Map, "player entered the next district");
                Check.Equal(new Point(0, 1), player.Location.Position,
                    "player reached the linked arrival tile");
            });
    }
}
