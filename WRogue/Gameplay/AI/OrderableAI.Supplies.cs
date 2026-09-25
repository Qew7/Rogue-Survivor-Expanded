using System;
using System.Drawing;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Actions;
using djack.RogueSurvivor.Engine.Items;

namespace djack.RogueSurvivor.Gameplay.AI
{
    abstract partial class OrderableAI
    {
        // 0: provision food, 1: provision weapon, 2: collect, 3: return.
        int m_XpdSupplyStage;
        Item m_XpdLoot;
        protected bool IsReturningXpdLoot
        {
            get { return Order != null && Order.Task == ActorTasks.SCAVENGE_SUPPLIES &&
                m_XpdSupplyStage == 3; }
        }

        ActorAction ExecuteScavengeSupplies(RogueGame game, ActorOrder order)
        {
            if (!Session.Get.GamePreset.Bases || m_Actor.Model.Abilities.IsUndead ||
                m_Actor.Inventory == null || order.Location.Map == null ||
                m_Actor.Leader == null || m_Actor.Leader.IsDead) return null;
            Map home = order.Location.Map;
            XpdBase baseClaim = home.XpdBaseAt(order.Location.Position);
            if (baseClaim == null || !baseClaim.Owns(m_Actor)) return null;

            while (m_XpdSupplyStage < 2)
            {
                bool food = m_XpdSupplyStage == 0;
                Rectangle? room = food ? baseClaim.FoodRoom : baseClaim.WeaponRoom;
                if (room != null && !m_Actor.Inventory.HasItemMatching(it =>
                    food ? it is ItemFood : it is ItemWeapon))
                {
                    Location supply;
                    Item item;
                    if (FindStoredItem(home, baseClaim, room.Value, food, out supply, out item))
                    {
                        ActorAction approach = GoTo(game, supply, home.District);
                        if (approach != null) return approach;
                        if (m_Actor.Location.Map != home || m_Actor.Location.Position != supply.Position)
                            return null;
                        ActorAction take = new ActionTakeItem(m_Actor, game, supply.Position, item);
                        if (!take.IsLegal()) return null;
                        m_XpdSupplyStage++;
                        return take;
                    }
                }
                m_XpdSupplyStage++;
            }

            if (m_XpdSupplyStage == 2)
            {
                if (m_Actor.Inventory.IsFull) return null;
                Location supply;
                Item item;
                if (!XpdSupplyRoutes.FindSupply(m_Actor.Location.Map, home.District, out supply, out item))
                    return null;
                ActorAction approach = GoTo(game, supply, home.District);
                if (approach != null) return approach;
                if (m_Actor.Location.Map != supply.Map || m_Actor.Location.Position != supply.Position)
                    return null;
                ActorAction take = new ActionTakeItem(m_Actor, game, supply.Position, item);
                if (!take.IsLegal()) return null;
                m_XpdLoot = item;
                m_XpdSupplyStage = 3;
                return take;
            }

            if (m_XpdLoot == null || !m_Actor.Inventory.Contains(m_XpdLoot)) return null;
            Rectangle? storage = m_XpdLoot is ItemFood ? baseClaim.FoodRoom : baseClaim.WeaponRoom;
            Location drop = new Location(home, storage == null ? order.Location.Position :
                FirstWalkableStorageCell(home, baseClaim, storage.Value));
            ActorAction travel = GoTo(game, drop, home.District);
            if (travel != null) return travel;
            if (m_Actor.Location.Map != home || m_Actor.Location.Position != drop.Position)
                return null;
            if (m_XpdLoot.IsEquipped)
                return new ActionUnequipItem(m_Actor, game, m_XpdLoot);
            ActorAction dropAction = new ActionDropItem(m_Actor, game, m_XpdLoot);
            if (!dropAction.IsLegal()) return null;
            SetOrder(null);
            return dropAction;
        }

        ActorAction GoTo(RogueGame game, Location target, District home)
        {
            if (m_Actor.Location.Map == target.Map)
                return m_Actor.Location.Position == target.Position ? null :
                    BehaviorIntelligentBumpToward(game, target.Position, false, false);
            Exit exit = XpdSupplyRoutes.NextExit(m_Actor.Location.Map, target.Map, home);
            if (exit == null) return null;
            Point? point = m_Actor.Location.Map.GetExitPos(exit);
            if (point == null) return null;
            if (m_Actor.Location.Position != point.Value)
                return BehaviorIntelligentBumpToward(game, point.Value, false, false);
            ActorAction use = new ActionUseExit(m_Actor, point.Value, game);
            return use.IsLegal() ? use : null;
        }

        static bool FindStoredItem(Map map, XpdBase baseClaim, Rectangle room, bool food,
            out Location location, out Item item)
        {
            for (int y = room.Top; y < room.Bottom; y++)
                for (int x = room.Left; x < room.Right; x++)
                {
                    Point point = new Point(x, y);
                    if (!map.IsInBounds(point) || !baseClaim.Contains(point) || !map.IsWalkable(x, y)) continue;
                    Inventory inventory = map.GetItemsAt(point);
                    if (inventory == null) continue;
                    foreach (Item candidate in inventory.Items)
                        if (food ? candidate is ItemFood : candidate is ItemWeapon)
                        {
                            location = new Location(map, point);
                            item = candidate;
                            return true;
                        }
                }
            location = default(Location);
            item = null;
            return false;
        }

        static Point FirstWalkableStorageCell(Map map, XpdBase baseClaim, Rectangle room)
        {
            for (int y = room.Top; y < room.Bottom; y++)
                for (int x = room.Left; x < room.Right; x++)
                {
                    Point point = new Point(x, y);
                    if (map.IsInBounds(point) && baseClaim.Contains(point) && map.IsWalkable(x, y))
                        return point;
                }
            throw new InvalidOperationException("The assigned storage room has no walkable base tile");
        }
    }
}
