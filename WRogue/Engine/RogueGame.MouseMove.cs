using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine.Actions;
using Message = djack.RogueSurvivor.Data.Message;

namespace djack.RogueSurvivor.Engine
{
    partial class RogueGame
    {
        bool m_IsMouseMoveMode;
        Point? m_MouseMoveHover;
        List<Point> m_MouseMovePreview;
        List<Point> m_MouseMoveSteps;
        KeyEventArgs m_MouseMoveInterruptedKey;
        bool m_MouseMoveCanBump;

        void ToggleMouseMoveMode()
        {
            m_IsMouseMoveMode = !m_IsMouseMoveMode;
            m_MouseMoveHover = null;
            m_MouseMovePreview = null;
            m_MouseMoveSteps = null;
            m_MouseMoveCanBump = false;
            CloseMouseContextMenu();
            ClearOverlays();
            AddMessage(new Message(m_IsMouseMoveMode ?
                "Mouse movement ON: left-click to move, right-click for actions; M to turn off." :
                "Mouse movement OFF.", m_Session.WorldTime.TurnCounter, Color.Yellow));
        }

        List<Point> FindMouseMovePath(Actor player, Point goal)
        {
            Map map = player.Location.Map;
            return MouseMovePath.Find(player.Location.Position, goal, m_MapViewRect,
                delegate(Point point)
                {
                    return IsVisibleToPlayer(map, point) &&
                        m_Rules.IsWalkableFor(player, map, point.X, point.Y);
                });
        }

        bool IsAdjacentMouseBump(Actor player, Point goal)
        {
            Point from = player.Location.Position;
            Map map = player.Location.Map;
            return goal != from && m_Rules.IsAdjacent(from, goal) &&
                map.IsInBounds(goal) && IsVisibleToPlayer(map, goal) &&
                !m_Rules.IsWalkableFor(player, map, goal.X, goal.Y);
        }

        bool CanMouseBump(Actor player, Point goal)
        {
            if (!IsAdjacentMouseBump(player, goal))
                return false;
            Point from = player.Location.Position;
            Direction direction = Direction.FromVector(new Point(goal.X - from.X, goal.Y - from.Y));
            return direction != null && new ActionBump(player, this, direction).IsLegal();
        }

        bool HandleMouseMove(Actor player, Point mousePos, MouseButtons? buttons, out bool keepLoop)
        {
            keepLoop = true;
            Point goal = MouseToMap(mousePos);
            if (!IsInViewRect(goal))
            {
                m_MouseMoveHover = null;
                m_MouseMovePreview = null;
                m_MouseMoveCanBump = false;
                return false;
            }

            if (!m_MouseMoveHover.HasValue || m_MouseMoveHover.Value != goal)
            {
                m_MouseMoveHover = goal;
                m_MouseMovePreview = FindMouseMovePath(player, goal);
                m_MouseMoveCanBump = m_MouseMovePreview == null && CanMouseBump(player, goal);
            }

            if (buttons == MouseButtons.Left && IsAdjacentMouseBump(player, goal))
            {
                Point from = player.Location.Position;
                Direction direction = Direction.FromVector(new Point(goal.X - from.X, goal.Y - from.Y));
                keepLoop = TryPlayerInsanity() ? false : !DoPlayerBump(player, direction);
                m_MouseMoveHover = null;
                m_MouseMovePreview = null;
                m_MouseMoveCanBump = false;
            }
            else if (buttons == MouseButtons.Left && m_MouseMovePreview != null && m_MouseMovePreview.Count > 0)
            {
                m_MouseMoveSteps = new List<Point>(m_MouseMovePreview);
                keepLoop = !ContinueMouseMove(player);
            }
            return true;
        }

        bool CancelMouseMoveOnDamage(Actor actor, int damage)
        {
            if (damage <= 0 || actor != m_Player || m_MouseMoveSteps == null)
                return false;
            m_MouseMoveSteps = null;
            m_MouseMoveHover = null;
            m_MouseMovePreview = null;
            m_MouseMoveCanBump = false;
            return true;
        }

        bool ContinueMouseMove(Actor player)
        {
            if (m_MouseMoveSteps == null || m_MouseMoveSteps.Count == 0)
                return false;
            KeyEventArgs interruption = m_UI.UI_PeekKey();
            if (interruption != null)
            {
                m_MouseMoveSteps = null;
                if (InputTranslator.KeyToCommand(interruption) == PlayerCommand.MOUSE_MOVE_MODE)
                    ToggleMouseMoveMode();
                else
                    m_MouseMoveInterruptedKey = interruption;
                return false;
            }
            Point next = m_MouseMoveSteps[0];
            Point position = player.Location.Position;
            Map map = player.Location.Map;
            Point delta = new Point(next.X - position.X, next.Y - position.Y);
            Direction direction = Direction.FromVector(delta);
            if (direction == null || !IsVisibleToPlayer(player.Location.Map, next) ||
                !m_Rules.IsWalkableFor(player, player.Location.Map, next.X, next.Y))
            {
                m_MouseMoveSteps = null;
                m_MouseMovePreview = null;
                AddMessage(MakeErrorMessage("Mouse route is blocked."));
                return false;
            }
            if (TryPlayerInsanity())
            {
                m_MouseMoveSteps = null;
                return true;
            }
            if (!DoPlayerBump(player, direction))
            {
                m_MouseMoveSteps = null;
                return false;
            }
            if (m_MouseMoveSteps == null)
            {
                FinishMouseMoveAction(player);
                return true;
            }
            if (player.Location.Map != map || player.Location.Position != next)
            {
                m_MouseMoveSteps = null;
                FinishMouseMoveAction(player);
                return true;
            }
            m_MouseMoveSteps.RemoveAt(0);
            if (m_MouseMoveSteps.Count == 0)
                m_MouseMoveSteps = null;
            m_MouseMovePreview = null;
            m_MouseMoveHover = null;
            FinishMouseMoveAction(player);
            return true;
        }

        void FinishMouseMoveAction(Actor player)
        {
            UpdatePlayerFOV(player);
            ComputeViewRect(player.Location.Position);
            m_Session.LastTurnPlayerActed = m_Session.WorldTime.TurnCounter;
            RedrawPlayScreen();
        }

        void DrawMouseMovePreview()
        {
            if (!m_IsMouseMoveMode || m_Player == null)
                return;

            m_UI.UI_DrawStringBold(Color.Yellow, "MOUSE MOVE  [M: off, RMB: actions]", 6, 5);
            if (m_MouseMoveSteps == null && !m_MouseMoveHover.HasValue)
                return;

            List<Point> path = m_MouseMoveSteps ?? m_MouseMovePreview;
            Point goal = m_MouseMoveSteps != null ?
                m_MouseMoveSteps[m_MouseMoveSteps.Count - 1] : m_MouseMoveHover.Value;
            if (path == null || path.Count == 0)
            {
                Map map = m_Player.Location.Map;
                if (!map.IsInBounds(goal) && map.GetExitAt(goal) != null)
                {
                    string label = m_Rules.IsAdjacent(m_Player.Location.Position, goal)
                        ? "Right-click: leave district" : "Stand beside exit";
                    DrawMouseMoveLabel(goal, label, Color.Yellow);
                    return;
                }
                if (goal == m_Player.Location.Position && map.GetExitAt(goal) != null)
                {
                    DrawMouseMoveLabel(goal, "Right-click: use exit", Color.Yellow);
                    return;
                }
                if (m_MouseMoveCanBump)
                {
                    Point bumpFrom = TileCenter(m_Player.Location.Position);
                    Point bumpTo = TileCenter(goal);
                    m_UI.UI_DrawLine(Color.Orange, bumpFrom.X, bumpFrom.Y, bumpTo.X, bumpTo.Y);
                    double bumpAngle = Math.Atan2(bumpTo.Y - bumpFrom.Y, bumpTo.X - bumpFrom.X);
                    DrawArrowWing(bumpTo, bumpAngle + 2.55, Color.Orange);
                    DrawArrowWing(bumpTo, bumpAngle - 2.55, Color.Orange);
                    DrawMouseMoveLabel(goal, "Bump (1 action)", Color.Orange);
                    return;
                }
                if (goal != m_Player.Location.Position)
                    DrawMouseMoveLabel(goal, "No route", Color.OrangeRed);
                return;
            }

            Point from = TileCenter(m_Player.Location.Position);
            Point before = from;
            foreach (Point tile in path)
            {
                Point to = TileCenter(tile);
                m_UI.UI_DrawLine(Color.Lime, from.X, from.Y, to.X, to.Y);
                before = from;
                from = to;
            }
            double angle = Math.Atan2(from.Y - before.Y, from.X - before.X);
            DrawArrowWing(from, angle + 2.55);
            DrawArrowWing(from, angle - 2.55);
            DrawMouseMoveLabel(goal, path.Count + (path.Count == 1 ? " step" : " steps"), Color.Lime);
        }

        Point TileCenter(Point tile)
        {
            Point corner = MapToScreen(tile);
            return new Point(corner.X + TILE_SIZE / 2, corner.Y + TILE_SIZE / 2);
        }

        void DrawArrowWing(Point tip, double angle)
        {
            DrawArrowWing(tip, angle, Color.Lime);
        }

        void DrawArrowWing(Point tip, double angle, Color color)
        {
            m_UI.UI_DrawLine(color, tip.X, tip.Y,
                tip.X + (int)(Math.Cos(angle) * 12),
                tip.Y + (int)(Math.Sin(angle) * 12));
        }

        void DrawMouseMoveLabel(Point tile, string label, Color color)
        {
            Point center = TileCenter(tile);
            int x = Math.Max(2, Math.Min(RIGHTPANEL_X - label.Length * 9, center.X + 15));
            int y = Math.Max(20, Math.Min(MESSAGES_Y - 20, center.Y + 10));
            m_UI.UI_DrawStringBold(Color.Black, label, x + 1, y + 1);
            m_UI.UI_DrawStringBold(color, label, x, y);
        }
    }
}
