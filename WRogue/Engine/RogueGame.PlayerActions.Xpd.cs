using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using djack.RogueSurvivor.Data;
using Message = djack.RogueSurvivor.Data.Message;

namespace djack.RogueSurvivor.Engine
{
    partial class RogueGame
    {
        public bool TryClaimXpdBase(Actor actor, out string reason)
        {
            if (!m_Session.GamePreset.Bases)
            {
                reason = "Bases are disabled in this game preset.";
                return false;
            }
            List<Point> cells = XpdBasePlanner.Preview(actor.Location.Map, actor, m_Rules, out reason);
            if (cells == null) return false;
            XpdBase linkedBase = FindLinkedXpdBase(actor, actor.Location.Map, cells);
            XpdBase newBase = new XpdBase(actor, cells, linkedBase);
            actor.Location.Map.AddXpdBase(newBase);
            if (actor.IsPlayer) ReleaseGroupBases(actor, newBase);
            SpendActorActionPoints(actor, Rules.BASE_ACTION_COST);
            return true;
        }

        void ReleaseGroupBases(Actor leader, XpdBase except = null)
        {
            foreach (Map map in WorldMaps())
            {
                List<XpdBase> released = new List<XpdBase>();
                foreach (XpdBase baseClaim in map.XpdBases)
                    if (baseClaim.GroupLeader == leader &&
                        (except == null || !baseClaim.IsPartOf(except)))
                        released.Add(baseClaim);
                foreach (XpdBase baseClaim in released) map.RemoveXpdBase(baseClaim);
            }
        }

        XpdBase FindLinkedXpdBase(Actor actor, Map map, IEnumerable<Point> cells)
        {
            foreach (Point cell in cells)
            {
                Exit exit = map.GetExitAt(cell);
                if (exit == null || exit.ToMap == null || exit.ToMap == map ||
                    exit.ToMap.District != map.District) continue;
                XpdBase candidate = exit.ToMap.XpdBaseAt(exit.ToPosition);
                if (candidate == null || !candidate.Owns(actor)) continue;
                Exit returnExit = exit.ToMap.GetExitAt(exit.ToPosition);
                if (returnExit != null && returnExit.ToMap == map &&
                    returnExit.ToPosition == cell) return candidate;
            }
            return null;
        }

        IEnumerable<Map> WorldMaps()
        {
            if (m_Session.World == null) yield break;
            for (int x = 0; x < m_Session.World.Size; x++)
                for (int y = 0; y < m_Session.World.Size; y++)
                {
                    District district = m_Session.World[x, y];
                    if (district == null) continue;
                    foreach (Map map in district.Maps) yield return map;
                }
        }

        XpdBase FindPlayerXpdBase(Actor player, out Map baseMap)
        {
            XpdBase fallback = null;
            Map fallbackMap = null;
            foreach (Map map in WorldMaps())
                foreach (XpdBase baseClaim in map.XpdBases)
                    if (baseClaim.GroupLeader == player)
                    {
                        if (baseClaim.Root == baseClaim)
                        {
                            baseMap = map;
                            return baseClaim;
                        }
                        fallback = baseClaim;
                        fallbackMap = map;
                    }
            baseMap = fallbackMap;
            return fallback;
        }

        public bool TryFindOwnedXpdBase(Actor actor, out Location location, out XpdBase baseClaim)
        {
            foreach (Map map in WorldMaps())
                foreach (XpdBase candidate in map.XpdBases)
                    if (candidate.Owns(actor))
                        foreach (Point point in candidate.Cells)
                            if (map.IsWalkable(point.X, point.Y) && map.GetActorAt(point) == null)
                            {
                                location = new Location(map, point);
                                baseClaim = candidate;
                                return true;
                            }
            location = default(Location);
            baseClaim = null;
            return false;
        }

        static IEnumerable<Point> XpdBaseBoundary(XpdBase baseClaim)
        {
            HashSet<Point> cells = new HashSet<Point>(baseClaim.Cells);
            foreach (Point point in cells)
                if (!cells.Contains(new Point(point.X - 1, point.Y)) ||
                    !cells.Contains(new Point(point.X + 1, point.Y)) ||
                    !cells.Contains(new Point(point.X, point.Y - 1)) ||
                    !cells.Contains(new Point(point.X, point.Y + 1)))
                    yield return point;
        }

        bool HandlePlayerXpdBase(Actor player)
        {
            if (!m_Session.GamePreset.Bases)
            {
                AddMessage(MakeErrorMessage("Bases are disabled in this game preset."));
                return false;
            }
            Map map = player.Location.Map;
            XpdBase baseClaim = map.XpdBaseAt(player.Location.Position);
            if (baseClaim == null)
            {
                string reason;
                List<Point> cells = XpdBasePlanner.Preview(map, player, m_Rules, out reason);
                if (cells == null)
                {
                    AddMessage(MakeErrorMessage(reason));
                    return false;
                }
                DrawXpdBoundary(cells, Color.Lime);
                bool linked = FindLinkedXpdBase(player, map, cells) != null;
                AddMessage(new Message(linked ?
                    "Add highlighted level to your base? Y confirms, any other key cancels." :
                    "Claim highlighted base? Y confirms, any other key cancels.",
                    m_Session.WorldTime.TurnCounter, Color.Yellow));
                RedrawPlayScreen();
                KeyEventArgs key = m_UI.UI_WaitKey();
                ClearOverlays();
                if (key.KeyCode != Keys.Y) return false;
                // TryClaimXpdBase validates the current map and actor position again.
                if (!TryClaimXpdBase(player, out reason))
                {
                    AddMessage(MakeErrorMessage(reason));
                    return false;
                }
                AddMessage(new Message(linked ? "Level added to base. Press Ctrl+B in a room to assign storage." :
                    "Base claimed. Stand in a room and press Ctrl+B to assign storage.",
                    m_Session.WorldTime.TurnCounter, Color.LightGreen));
                return true;
            }
            if (!baseClaim.Owns(player))
            {
                AddMessage(MakeErrorMessage("This base belongs to another group."));
                return false;
            }
            Rectangle room;
            try { room = XpdBasePlanner.RoomAt(map, baseClaim, player.Location.Position); }
            catch (ArgumentException error)
            {
                AddMessage(MakeErrorMessage(error.Message));
                return false;
            }
            DrawXpdBoundary(RoomCells(map, baseClaim, room), Color.Cyan);
            AddMessage(new Message("Assign highlighted room: F food, W weapons, Esc cancel.",
                m_Session.WorldTime.TurnCounter, Color.Yellow));
            RedrawPlayScreen();
            KeyEventArgs storageKey = m_UI.UI_WaitKey();
            ClearOverlays();
            if (storageKey.KeyCode == Keys.F) baseClaim.SetFoodRoom(room);
            else if (storageKey.KeyCode == Keys.W) baseClaim.SetWeaponRoom(room);
            else return false;
            SpendActorActionPoints(player, Rules.BASE_ACTION_COST);
            AddMessage(new Message(storageKey.KeyCode == Keys.F ? "Food storage assigned." : "Weapon storage assigned.",
                m_Session.WorldTime.TurnCounter, Color.LightGreen));
            return true;
        }

        static List<Point> RoomCells(Map map, XpdBase baseClaim, Rectangle room)
        {
            List<Point> cells = new List<Point>();
            foreach (Point point in baseClaim.Cells)
                if (room.Contains(point) && map.GetTileAt(point).IsInside) cells.Add(point);
            return cells;
        }

        void DrawXpdBoundary(IEnumerable<Point> cells, Color color)
        {
            HashSet<Point> area = new HashSet<Point>(cells);
            foreach (Point point in area)
            {
                if (!IsInViewRect(point)) continue;
                if (area.Contains(new Point(point.X - 1, point.Y)) &&
                    area.Contains(new Point(point.X + 1, point.Y)) &&
                    area.Contains(new Point(point.X, point.Y - 1)) &&
                    area.Contains(new Point(point.X, point.Y + 1))) continue;
                AddOverlay(new OverlayRect(color, new Rectangle(MapToScreen(point), new Size(TILE_SIZE, TILE_SIZE))));
            }
        }
    }
}
