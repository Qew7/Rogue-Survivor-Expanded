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

            // The search is confined to the visible rectangle, so dense arrays avoid
            // hashing every explored tile during mouse hover.
            int width = bounds.Width;
            int capacity = width * bounds.Height;
            int[] frontier = new int[capacity];
            int[] previous = new int[capacity]; // parent index + 1; zero means unseen.
            int head = 0;
            int tail = 0;
            int startIndex = (start.Y - bounds.Top) * width + start.X - bounds.Left;
            int goalIndex = (goal.Y - bounds.Top) * width + goal.X - bounds.Left;
            frontier[tail++] = startIndex;
            previous[startIndex] = startIndex + 1;

            while (head < tail)
            {
                int currentIndex = frontier[head++];
                Point current = new Point(bounds.Left + currentIndex % width,
                    bounds.Top + currentIndex / width);
                foreach (Point offset in Neighbors)
                {
                    Point next = new Point(current.X + offset.X, current.Y + offset.Y);
                    if (!bounds.Contains(next))
                        continue;
                    int nextIndex = (next.Y - bounds.Top) * width + next.X - bounds.Left;
                    if (previous[nextIndex] != 0 || !canEnter(next))
                        continue;
                    previous[nextIndex] = currentIndex + 1;
                    if (nextIndex == goalIndex)
                    {
                        List<Point> path = new List<Point>();
                        for (int step = goalIndex; step != startIndex; step = previous[step] - 1)
                            path.Add(new Point(bounds.Left + step % width,
                                bounds.Top + step / width));
                        path.Reverse();
                        return path;
                    }
                    frontier[tail++] = nextIndex;
                }
            }
            return null;
        }
    }
}
