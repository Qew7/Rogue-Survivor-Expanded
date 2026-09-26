using System;
using System.Collections.Generic;
using System.Drawing;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine.Items;

namespace djack.RogueSurvivor.Engine
{
    // The map graph is defined by actual AI exits, including basement and district links.
    static class XpdSupplyRoutes
    {
        public static bool InRange(District home, Map map)
        {
            if (home == null || map.District == null) return false;
            Point a = home.WorldPosition, b = map.District.WorldPosition;
            return Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y) <= 1;
        }

        public static Exit NextExit(Map from, Map destination, District home)
        {
            if (from == destination) return null;
            Queue<Map> queue = new Queue<Map>();
            Dictionary<Map, Exit> first = new Dictionary<Map, Exit>();
            queue.Enqueue(from);
            first.Add(from, null);
            while (queue.Count > 0)
            {
                Map map = queue.Dequeue();
                foreach (Exit exit in map.Exits)
                {
                    Map next = exit.ToMap;
                    if (!exit.IsAnAIExit || next == null ||
                        (home != null && !InRange(home, next)) || first.ContainsKey(next)) continue;
                    Exit firstExit = map == from ? exit : first[map];
                    if (next == destination) return firstExit;
                    first.Add(next, firstExit);
                    queue.Enqueue(next);
                }
            }
            return null;
        }

        public static bool FindSupply(Map from, District home, out Location location, out Item item)
        {
            return FindSupply(from, home, null, out location, out item);
        }

        public static bool FindSupply(Map from, District home, Predicate<Item> accept,
            out Location location, out Item item)
        {
            Queue<Map> queue = new Queue<Map>();
            HashSet<Map> seen = new HashSet<Map>();
            queue.Enqueue(from);
            seen.Add(from);
            while (queue.Count > 0)
            {
                Map map = queue.Dequeue();
                foreach (Inventory inventory in map.GroundInventories)
                {
                    Point? position = map.GetGroundInventoryPosition(inventory);
                    if (position == null || !map.IsWalkable(position.Value) ||
                        map.XpdBaseAt(position.Value) != null) continue;
                    foreach (Item candidate in inventory.Items)
                        if ((candidate is ItemFood || candidate is ItemWeapon) &&
                            (accept == null || accept(candidate)))
                        {
                            location = new Location(map, position.Value);
                            item = candidate;
                            return true;
                        }
                }
                foreach (Exit exit in map.Exits)
                    if (exit.IsAnAIExit && exit.ToMap != null && InRange(home, exit.ToMap) && seen.Add(exit.ToMap))
                        queue.Enqueue(exit.ToMap);
            }
            location = default(Location);
            item = null;
            return false;
        }
    }
}
