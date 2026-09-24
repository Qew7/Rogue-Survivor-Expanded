using System;
using System.Drawing;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Actions;
using djack.RogueSurvivor.Gameplay;

// A small, deterministic world for exercising real game actions without a UI.
sealed class ScenarioWorld
{
    readonly GameTiles tiles = new GameTiles();
    public readonly Map Map;
    public readonly RogueGame Game;
    public readonly int Seed;

    public ScenarioWorld(int seed, params string[] rows)
    {
        if (rows == null || rows.Length == 0 || rows[0].Length == 0)
            throw new ArgumentException("A scenario needs a nonempty map");
        Seed = seed;
        Map = new Map(seed, "scenario", rows[0].Length, rows.Length);
        for (int y = 0; y < rows.Length; y++)
        {
            if (rows[y].Length != Map.Width) throw new ArgumentException("Map rows must have equal length");
            for (int x = 0; x < Map.Width; x++)
                SetTile(x, y, rows[y][x]);
        }
        Game = (RogueGame)FormatterServices.GetUninitializedObject(typeof(RogueGame));
        typeof(RogueGame).GetField("m_Rules", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(Game, new Rules(new DiceRoller(seed)));
    }

    public ScenarioWorld(int seed, Map map, RogueGame game)
    {
        if (map == null || game == null) throw new ArgumentNullException(map == null ? "map" : "game");
        Seed = seed;
        Map = map;
        Game = game;
    }

    public void SetTile(int x, int y, char tile)
    {
        GameTiles.IDs id;
        if (tile == '.') id = GameTiles.IDs.FLOOR_ASPHALT;
        else if (tile == '#') id = GameTiles.IDs.WALL_BRICK;
        else throw new ArgumentException("Unknown tile '" + tile + "' at " + x + "," + y);
        Map.SetTileModelAt(x, y, tiles[id]);
    }

    public Actor Place(string name, int x, int y)
    {
        Actor actor = (Actor)FormatterServices.GetUninitializedObject(typeof(Actor));
        actor.Name = name;
        actor.ActionPoints = Rules.BASE_ACTION_COST;
        Place(actor, x, y);
        return actor;
    }

    public void Place(Actor actor, int x, int y)
    {
        if (actor == null) throw new ArgumentNullException("actor");
        if (!Map.IsInBounds(x, y) || !Map.GetTileAt(x, y).Model.IsWalkable)
            throw new ArgumentException("Actor must start on a walkable tile");
        Map.PlaceActorAt(actor, new Point(x, y));
    }

    public void SetPlayer(Actor actor)
    {
        if (actor == null || actor.Location.Map != Map)
            throw new ArgumentException("Player must be on this map");
        typeof(RogueGame).GetField("m_Player", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(Game, actor);
    }

    public bool Try(ActorAction action)
    {
        if (!action.IsLegal()) return false;
        action.Perform();
        return true;
    }

    public bool Bump(Actor actor, Direction direction)
    {
        return Try(new ActionBump(actor, Game, direction));
    }

    public bool NpcTurn(Actor actor)
    {
        if (actor.Controller == null) throw new ArgumentException("NPC has no controller");
        return Try(actor.Controller.GetAction(Game));
    }

    public string Draw()
    {
        StringBuilder result = new StringBuilder();
        for (int y = 0; y < Map.Height; y++)
        {
            for (int x = 0; x < Map.Width; x++)
            {
                Actor actor = Map.GetActorAt(x, y);
                result.Append(actor != null ? 'A' : Map.GetTileAt(x, y).Model.IsWalkable ? '.' : '#');
            }
            result.AppendLine();
        }
        return result.ToString();
    }
}
