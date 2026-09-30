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
            return Find(start, goal, bounds, canEnter, point => false);
        }

        public static List<Point> Find(Point start, Point goal, Rectangle bounds,
            Func<Point, bool> canEnter, Func<Point, bool> isHazard)
        {
            if (canEnter == null) throw new ArgumentNullException("canEnter");
            if (isHazard == null) throw new ArgumentNullException("isHazard");
            // Prefer any safe route. If hazards cannot be avoided, choose the
            // shortest route with as few hazardous tiles as possible.
            List<Point> safe = FindCore(start, goal, bounds,
                point => canEnter(point) && !isHazard(point), isHazard);
            return safe ?? FindCore(start, goal, bounds, canEnter, isHazard);
        }

        static List<Point> FindCore(Point start, Point goal, Rectangle bounds,
            Func<Point, bool> canEnter, Func<Point, bool> isHazard)
        {
            if (canEnter == null)
                throw new ArgumentNullException("canEnter");
            if (isHazard == null)
                throw new ArgumentNullException("isHazard");
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
            int[] distance = new int[capacity];
            int[] hazards = new int[capacity];
            long[] deviation = new long[capacity];
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
                    if (distance[nextIndex] == 0 && nextIndex != startIndex && !canEnter(next))
                        continue;
                    int nextDistance = distance[currentIndex] + 1;
                    if (nextIndex == startIndex ||
                        (distance[nextIndex] != 0 && distance[nextIndex] < nextDistance))
                        continue;
                    // Compare routes only within the shortest-path layer.
                    int nextHazards = hazards[currentIndex] + (isHazard(next) ? 1 : 0);
                    long cross = (long)(next.X - start.X) * (goal.Y - start.Y) -
                        (long)(next.Y - start.Y) * (goal.X - start.X);
                    long nextDeviation = deviation[currentIndex] + cross * cross;
                    if (distance[nextIndex] == nextDistance &&
                        (hazards[nextIndex] < nextHazards ||
                        (hazards[nextIndex] == nextHazards && deviation[nextIndex] <= nextDeviation)))
                        continue;
                    previous[nextIndex] = currentIndex + 1;
                    hazards[nextIndex] = nextHazards;
                    deviation[nextIndex] = nextDeviation;
                    if (distance[nextIndex] == 0)
                    {
                        distance[nextIndex] = nextDistance;
                        frontier[tail++] = nextIndex;
                    }
                }
            }
            if (previous[goalIndex] == 0) return null;
            List<Point> path = new List<Point>();
            for (int step = goalIndex; step != startIndex; step = previous[step] - 1)
                path.Add(new Point(bounds.Left + step % width, bounds.Top + step / width));
            path.Reverse();
            return path;
        }
    }
}
