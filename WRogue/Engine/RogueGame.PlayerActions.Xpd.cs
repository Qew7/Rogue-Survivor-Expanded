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
            actor.Location.Map.AddXpdBase(new XpdBase(actor, cells));
            SpendActorActionPoints(actor, Rules.BASE_ACTION_COST);
            return true;
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
                AddMessage(new Message("Claim highlighted base? Y confirms, any other key cancels.",
                    m_Session.WorldTime.TurnCounter, Color.Yellow));
                RedrawPlayScreen();
                KeyEventArgs key = m_UI.UI_WaitKey();
                ClearOverlays();
                if (key.KeyCode != Keys.Y) return false;
                // Actors may have moved while the confirmation screen was open.
                cells = XpdBasePlanner.Preview(map, player, m_Rules, out reason);
                if (cells == null)
                {
                    AddMessage(MakeErrorMessage(reason));
                    return false;
                }
                if (!TryClaimXpdBase(player, out reason))
                {
                    AddMessage(MakeErrorMessage(reason));
                    return false;
                }
                AddMessage(new Message("Base claimed. Stand in a room and press Ctrl+B to assign storage.",
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
