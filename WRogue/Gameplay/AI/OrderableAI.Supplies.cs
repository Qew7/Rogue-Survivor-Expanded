using System;
using System.Drawing;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Actions;
using djack.RogueSurvivor.Engine.AI;
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

        protected ActorAction DefendXpdBaseWithTrap(RogueGame game)
        {
            if (!Session.Get.GamePreset.Bases || m_Actor.IsPlayer ||
                m_Actor.Inventory == null ||
                (m_Actor.Leader != null && m_Actor.Leader.IsPlayer)) return null;
            ItemTrap trap = m_Actor.Inventory.GetFirstByType(typeof(ItemTrap)) as ItemTrap;
            if (trap == null) return null;
            Map map = m_Actor.Location.Map;
            XpdBase claim = map.XpdBaseAt(m_Actor.Location.Position);
            if (claim == null || !claim.Owns(m_Actor)) return null;

            Point? best = null;
            int bestDistance = Int32.MaxValue;
            foreach (Point cell in claim.Cells)
            {
                if (!map.IsWalkable(cell.X, cell.Y) ||
                    (map.GetActorAt(cell) != null && map.GetActorAt(cell) != m_Actor) ||
                    (claim.FoodRoom != null && claim.FoodRoom.Value.Contains(cell)) ||
                    (claim.WeaponRoom != null && claim.WeaponRoom.Value.Contains(cell))) continue;
                bool boundary = false;
                foreach (Point step in new[] { new Point(-1, 0), new Point(1, 0),
                    new Point(0, -1), new Point(0, 1) })
                {
                    Point neighbor = new Point(cell.X + step.X, cell.Y + step.Y);
                    if (map.IsInBounds(neighbor) && map.IsWalkable(neighbor.X, neighbor.Y) &&
                        !claim.Contains(neighbor)) { boundary = true; break; }
                }
                if (!boundary) continue;
                Inventory onGround = map.GetItemsAt(cell);
                if (onGround != null && onGround.HasItemMatching(item =>
                    item is ItemTrap && ((ItemTrap)item).IsActivated)) continue;
                int distance = game.Rules.GridDistance(m_Actor.Location.Position, cell);
                if (distance < bestDistance) { best = cell; bestDistance = distance; }
            }
            if (best == null) return null;
            if (best.Value != m_Actor.Location.Position)
                return BehaviorIntelligentBumpToward(game, best.Value, false, false);
            if (!trap.IsActivated && !trap.TrapModel.ActivatesWhenDropped)
            {
                ActorAction use = new ActionUseItem(m_Actor, game, trap);
                return use.IsLegal() ? use : null;
            }
            ActorAction drop = new ActionDropItem(m_Actor, game, trap);
            return drop.IsLegal() ? drop : null;
        }

        protected ActorAction TryStartAutonomousScavenge(RogueGame game,
            System.Collections.Generic.List<Percept> percepts, ExplorationData exploration)
        {
            if (!Session.Get.GamePreset.Bases || m_Actor.IsPlayer ||
                (m_Actor.Leader != null && m_Actor.Leader.IsPlayer) ||
                m_Actor.Inventory == null || m_Actor.Inventory.IsFull) return null;
            Location home = m_Actor.Location;
            if ((home.Map.LocalTime.TurnCounter + home.Position.X + home.Position.Y) % 30 != 0)
                return null;
            XpdBase baseClaim = home.Map.XpdBaseAt(home.Position);
            if (baseClaim == null || !baseClaim.Owns(m_Actor)) return null;
            Location supply;
            Item item;
            if (!XpdSupplyRoutes.FindSupply(home.Map, home.Map.District, out supply, out item))
                return null;
            SetOrder(new ActorOrder(ActorTasks.SCAVENGE_SUPPLIES, home));
            ActorAction action = ExecuteOrder(game, Order, percepts, exploration);
            if (action != null) return action;
            SetOrder(null);
            return null;
        }

        ActorAction ExecuteScavengeSupplies(RogueGame game, ActorOrder order)
        {
            if (!Session.Get.GamePreset.Bases || m_Actor.Model.Abilities.IsUndead ||
                m_Actor.Inventory == null || order.Location.Map == null ||
                (m_Actor.Leader != null && m_Actor.Leader.IsDead)) return null;
            bool autonomous = m_Actor.Leader == null || !m_Actor.Leader.IsPlayer;
            Map home = order.Location.Map;
            XpdBase baseClaim = home.XpdBaseAt(order.Location.Position);
            if (baseClaim == null || !baseClaim.Owns(m_Actor))
            {
                Location destination;
                if (!game.TryFindOwnedXpdBase(m_Actor, out destination, out baseClaim)) return null;
                order.Retarget(destination);
                home = destination.Map;
            }

            if (autonomous && m_XpdSupplyStage < 2) m_XpdSupplyStage = 2;

            // A relocated base may be far from the current scavenging area.
            if (m_XpdSupplyStage == 2 && !XpdSupplyRoutes.InRange(home.District, m_Actor.Location.Map))
            {
                ActorAction returnHome = GoTo(game, order.Location);
                if (returnHome != null) return returnHome;
                if (m_Actor.Location.Map != home ||
                    m_Actor.Location.Position != order.Location.Position) return null;
            }

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
                        ActorAction approach = GoTo(game, supply);
                        if (approach != null) return approach;
                        if (m_Actor.Location.Map != home || m_Actor.Location.Position != supply.Position)
                        {
                            m_XpdSupplyStage++;
                            continue;
                        }
                        ActorAction take = new ActionTakeItem(m_Actor, game, supply.Position, item);
                        if (!take.IsLegal())
                        {
                            m_XpdSupplyStage++;
                            continue;
                        }
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
                while (XpdSupplyRoutes.FindSupply(m_Actor.Location.Map, home.District,
                    candidate => !IsItemTaboo(candidate), out supply, out item))
                {
                    ActorAction approach = GoTo(game, supply);
                    if (approach != null) return approach;
                    if (m_Actor.Location.Map == supply.Map && m_Actor.Location.Position == supply.Position)
                    {
                        ActorAction take = new ActionTakeItem(m_Actor, game, supply.Position, item);
                        if (take.IsLegal())
                        {
                            m_XpdLoot = item;
                            m_XpdSupplyStage = 3;
                            return take;
                        }
                    }
                    MarkItemAsTaboo(item);
                }
                return null;
            }

            if (m_XpdLoot == null || !m_Actor.Inventory.Contains(m_XpdLoot)) return null;
            Rectangle? storage = m_XpdLoot is ItemFood ? baseClaim.FoodRoom : baseClaim.WeaponRoom;
            Point? storageCell = storage == null ? null :
                FirstWalkableStorageCell(home, baseClaim, storage.Value);
            Point dropPoint = storageCell ?? order.Location.Position;
            if (!home.IsWalkable(dropPoint.X, dropPoint.Y) ||
                (home.GetActorAt(dropPoint) != null && home.GetActorAt(dropPoint) != m_Actor))
            {
                Point? fallback = FirstWalkableBaseCell(home, baseClaim);
                if (fallback == null) return null;
                dropPoint = fallback.Value;
            }
            Location drop = new Location(home, dropPoint);
            ActorAction travel = GoTo(game, drop);
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

        ActorAction GoTo(RogueGame game, Location target)
        {
            if (m_Actor.Location.Map == target.Map)
                return m_Actor.Location.Position == target.Position ? null :
                    BehaviorIntelligentBumpToward(game, target.Position, false, false);
            Exit exit = XpdSupplyRoutes.NextExit(m_Actor.Location.Map, target.Map, null);
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

        Point? FirstWalkableStorageCell(Map map, XpdBase baseClaim, Rectangle room)
        {
            for (int y = room.Top; y < room.Bottom; y++)
                for (int x = room.Left; x < room.Right; x++)
                {
                    Point point = new Point(x, y);
                    if (map.IsInBounds(point) && baseClaim.Contains(point) && map.IsWalkable(x, y) &&
                        (map.GetActorAt(point) == null || map.GetActorAt(point) == m_Actor))
                        return point;
                }
            return null;
        }

        Point? FirstWalkableBaseCell(Map map, XpdBase baseClaim)
        {
            foreach (Point point in baseClaim.Cells)
                if (map.IsWalkable(point.X, point.Y) &&
                    (map.GetActorAt(point) == null || map.GetActorAt(point) == m_Actor))
                    return point;
            return null;
        }
    }
}
