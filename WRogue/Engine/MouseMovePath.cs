using System;
using System.Collections.Generic;
using System.Drawing;

namespace djack.RogueSurvivor.Engine
{
    // A shortest route measured in player actions. The starting tile is omitted.
    static class MouseMovePath
    {
        static readonly Point[] Neighbors = {
            new Point(0, -1), new Point(1, -1), new Point(1, 0),
            new Point(1, 1), new Point(0, 1), new Point(-1, 1),
            new Point(-1, 0), new Point(-1, -1)
        };

        public static List<Point> Find(Point start, Point goal, Rectangle bounds, Func<Point, bool> canEnter)
        {
            if (canEnter == null)
                throw new ArgumentNullException("canEnter");
            if (!bounds.Contains(start) || !bounds.Contains(goal))
                return null;
            if (start == goal)
                return new List<Point>();
            if (!canEnter(goal))
                return null;

            Queue<Point> frontier = new Queue<Point>();
            Dictionary<Point, Point> previous = new Dictionary<Point, Point>();
            frontier.Enqueue(start);
            previous[start] = start;

            while (frontier.Count > 0)
            {
                Point current = frontier.Dequeue();
                foreach (Point offset in Neighbors)
                {
                    Point next = new Point(current.X + offset.X, current.Y + offset.Y);
                    if (!bounds.Contains(next) || previous.ContainsKey(next) || !canEnter(next))
                        continue;
                    previous[next] = current;
                    if (next == goal)
                    {
                        List<Point> path = new List<Point>();
                        for (Point step = goal; step != start; step = previous[step])
                            path.Add(step);
                        path.Reverse();
                        return path;
                    }
                    frontier.Enqueue(next);
                }
            }
            return null;
        }
    }
}
