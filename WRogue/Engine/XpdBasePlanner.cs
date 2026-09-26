using System;
using System.Collections.Generic;
using System.Drawing;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine.MapObjects;

namespace djack.RogueSurvivor.Engine
{
    // Treat doors as seals when finding outdoor space, then as links between sealed rooms.
    static class XpdBasePlanner
    {
        static readonly Point[] Neighbors = {
            new Point(0, -1), new Point(1, 0), new Point(0, 1), new Point(-1, 0)
        };

        public static List<Point> Preview(Map map, Actor claimant, Rules rules, out string reason)
        {
            reason = null;
            if (map == null || claimant == null || claimant.Location.Map != map ||
                claimant.Model.Abilities.IsUndead)
            {
                reason = "Only a living actor standing on this map may claim a base.";
                return null;
            }
            Point start = claimant.Location.Position;
            if (!map.GetTileAt(start).IsInside)
            {
                reason = "Stand inside a building to claim a base.";
                return null;
            }
            HashSet<Point> exterior = Exterior(map);
            if (exterior.Contains(start))
            {
                reason = "This building is open to the outside.";
                return null;
            }

            List<Point> cells = new List<Point>();
            HashSet<Point> seen = new HashSet<Point>();
            Queue<Point> queue = new Queue<Point>();
            queue.Enqueue(start);
            seen.Add(start);
            while (queue.Count > 0)
            {
                Point point = queue.Dequeue();
                cells.Add(point);
                foreach (Point delta in Neighbors)
                {
                    Point next = new Point(point.X + delta.X, point.Y + delta.Y);
                    if (seen.Contains(next) || !map.IsInBounds(next) || exterior.Contains(next) ||
                        !map.GetTileAt(next).Model.IsWalkable || map.GetMapObjectAt(next) is Fortification)
                        continue;
                    seen.Add(next);
                    queue.Enqueue(next);
                }
            }

            HashSet<Point> blocked = new HashSet<Point>();
            foreach (Point cell in cells)
            {
                XpdBase existing = map.XpdBaseAt(cell);
                if (existing != null)
                {
                    blocked.Add(cell);
                    continue;
                }
                Actor occupant = map.GetActorAt(cell);
                if (occupant != null && occupant != claimant &&
                    (occupant.Model.Abilities.IsUndead || rules.AreEnemies(claimant, occupant)))
                    BlockOccupiedRoom(map, cell, blocked);
            }
            if (blocked.Contains(start))
            {
                reason = map.XpdBaseAt(start) != null ? "This area is already claimed." :
                    "Clear hostile actors and undead from this room first.";
                return null;
            }
            HashSet<Point> available = new HashSet<Point>(cells);
            available.ExceptWith(blocked);
            List<Point> claimable = new List<Point>();
            seen.Clear();
            queue.Enqueue(start);
            seen.Add(start);
            while (queue.Count > 0)
            {
                Point point = queue.Dequeue();
                claimable.Add(point);
                foreach (Point delta in Neighbors)
                {
                    Point next = new Point(point.X + delta.X, point.Y + delta.Y);
                    if (available.Contains(next) && seen.Add(next)) queue.Enqueue(next);
                }
            }
            return claimable;
        }

        static void BlockOccupiedRoom(Map map, Point start, HashSet<Point> blocked)
        {
            if (!map.GetTileAt(start).IsInside || map.GetMapObjectAt(start) is DoorWindow)
            {
                blocked.Add(start);
                return;
            }
            Queue<Point> queue = new Queue<Point>();
            queue.Enqueue(start);
            blocked.Add(start);
            while (queue.Count > 0)
            {
                Point point = queue.Dequeue();
                foreach (Point delta in Neighbors)
                {
                    Point next = new Point(point.X + delta.X, point.Y + delta.Y);
                    if (!map.IsInBounds(next) || !map.GetTileAt(next).IsInside ||
                        !map.GetTileAt(next).Model.IsWalkable ||
                        map.GetMapObjectAt(next) is DoorWindow || !blocked.Add(next)) continue;
                    queue.Enqueue(next);
                }
            }
        }

        public static Rectangle RoomAt(Map map, XpdBase baseClaim, Point start)
        {
            if (baseClaim == null || !baseClaim.Contains(start) || !map.GetTileAt(start).IsInside)
                throw new ArgumentException("Stand in an indoor room of your base");
            HashSet<Point> seen = new HashSet<Point>();
            Queue<Point> queue = new Queue<Point>();
            queue.Enqueue(start);
            seen.Add(start);
            int left = start.X, right = start.X, top = start.Y, bottom = start.Y;
            while (queue.Count > 0)
            {
                Point point = queue.Dequeue();
                left = Math.Min(left, point.X); right = Math.Max(right, point.X);
                top = Math.Min(top, point.Y); bottom = Math.Max(bottom, point.Y);
                foreach (Point delta in Neighbors)
                {
                    Point next = new Point(point.X + delta.X, point.Y + delta.Y);
                    if (!map.IsInBounds(next) || seen.Contains(next) || !baseClaim.Contains(next) ||
                        !map.GetTileAt(next).IsInside || !map.GetTileAt(next).Model.IsWalkable ||
                        map.GetMapObjectAt(next) is DoorWindow) continue;
                    seen.Add(next);
                    queue.Enqueue(next);
                }
            }
            return Rectangle.FromLTRB(left, top, right + 1, bottom + 1);
        }

        static HashSet<Point> Exterior(Map map)
        {
            HashSet<Point> exterior = new HashSet<Point>();
            Queue<Point> queue = new Queue<Point>();
            for (int y = 0; y < map.Height; y++)
                for (int x = 0; x < map.Width; x++)
                {
                    if (x != 0 && y != 0 && x != map.Width - 1 && y != map.Height - 1) continue;
                    Point edge = new Point(x, y);
                    if (!PassesOutdoorFlood(map, edge) || !exterior.Add(edge)) continue;
                    queue.Enqueue(edge);
                }
            while (queue.Count > 0)
            {
                Point point = queue.Dequeue();
                foreach (Point delta in Neighbors)
                {
                    Point next = new Point(point.X + delta.X, point.Y + delta.Y);
                    if (!map.IsInBounds(next) || !PassesOutdoorFlood(map, next) || !exterior.Add(next)) continue;
                    queue.Enqueue(next);
                }
            }
            return exterior;
        }

        static bool PassesOutdoorFlood(Map map, Point point)
        {
            if (!map.GetTileAt(point).Model.IsWalkable) return false;
            MapObject obj = map.GetMapObjectAt(point);
            return !(obj is DoorWindow) && !(obj is Fortification);
        }
    }
}
