using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine.Actions;
using djack.RogueSurvivor.Engine.MapObjects;

namespace djack.RogueSurvivor.Engine
{
    enum MouseContextActionKind { Move, Bump, CloseDoor, UseExit, LeaveMap, Wait }

    sealed class MouseContextAction
    {
        public readonly MouseContextActionKind Kind;
        public readonly Point Target;
        public readonly string Label;

        public MouseContextAction(MouseContextActionKind kind, Point target, string label)
        {
            Kind = kind;
            Target = target;
            Label = label;
        }
    }

    static class MouseContextMenu
    {
        public static List<MouseContextAction> Actions(Actor player, RogueGame game,
            Point target, bool visible, bool hasRoute)
        {
            List<MouseContextAction> actions = new List<MouseContextAction>();
            Map map = player.Location.Map;
            Point here = player.Location.Position;

            if (!map.IsInBounds(target))
            {
                AddBoundaryExit(actions, player, game, target);
                return actions;
            }
            if (!visible && target != here) return actions;

            if (target == here)
            {
                if (game.Rules.CanActorUseExit(player, here))
                    actions.Add(new MouseContextAction(MouseContextActionKind.UseExit,
                        here, "Use exit"));
                foreach (Direction direction in Direction.COMPASS_4)
                    AddBoundaryExit(actions, player, game,
                        new Point(here.X + direction.Vector.X, here.Y + direction.Vector.Y));
                actions.Add(new MouseContextAction(MouseContextActionKind.Wait, here, "Wait"));
                return actions;
            }

            if (hasRoute)
                actions.Add(new MouseContextAction(MouseContextActionKind.Move,
                    target, "Move here"));
            if (!game.Rules.IsAdjacent(here, target)) return actions;

            Direction bumpDirection = Direction.FromVector(new Point(target.X - here.X,
                target.Y - here.Y));
            ActionBump bump = new ActionBump(player, game, bumpDirection);
            if (bump.IsLegal() && !(bump.ConcreteAction is ActionMoveStep))
                actions.Add(new MouseContextAction(MouseContextActionKind.Bump,
                    target, BumpLabel(bump.ConcreteAction)));

            DoorWindow door = map.GetMapObjectAt(target) as DoorWindow;
            if (door != null && new ActionCloseDoor(player, game, door).IsLegal())
                actions.Add(new MouseContextAction(MouseContextActionKind.CloseDoor,
                    target, "Close door"));
            return actions;
        }

        static void AddBoundaryExit(List<MouseContextAction> actions, Actor player,
            RogueGame game, Point target)
        {
            Map map = player.Location.Map;
            Point here = player.Location.Position;
            if (map.IsInBounds(target) || !game.Rules.IsAdjacent(here, target) ||
                map.GetExitAt(target) == null)
                return;
            string reason;
            if (!game.Rules.CanActorLeaveMap(player, out reason)) return;
            Direction direction = Direction.FromVector(new Point(target.X - here.X,
                target.Y - here.Y));
            Exit exit = map.GetExitAt(target);
            string label = exit.ToMap.District != map.District ? "Leave district " : "Use boundary exit ";
            actions.Add(new MouseContextAction(MouseContextActionKind.LeaveMap,
                target, label + direction.ToString()));
        }

        static string BumpLabel(ActorAction action)
        {
            if (action is ActionMeleeAttack) return "Attack";
            if (action is ActionOpenDoor) return "Open door";
            if (action is ActionBashDoor) return "Bash door";
            if (action is ActionBreak) return "Break object";
            if (action is ActionChat) return "Talk";
            return "Interact";
        }
    }

    partial class RogueGame
    {
        const int MOUSE_MENU_WIDTH = 220;
        const int MOUSE_MENU_ROW_HEIGHT = 22;
        List<MouseContextAction> m_MouseContextActions;
        Point m_MouseContextPosition;
        int m_MouseContextSelected;

        void CloseMouseContextMenu() { m_MouseContextActions = null; }

        bool HandleMouseContextMenu(Actor player, Point mousePos,
            MouseButtons? buttons, out bool keepLoop)
        {
            keepLoop = true;
            if (buttons == MouseButtons.Right)
            {
                Point target = MouseToMap(mousePos);
                if (!IsInViewRect(target))
                {
                    CloseMouseContextMenu();
                    return false;
                }
                List<Point> route = player.Location.Map.IsInBounds(target)
                    ? FindMouseMovePath(player, target) : null;
                m_MouseContextActions = MouseContextMenu.Actions(player, this, target,
                    IsVisibleToPlayer(player.Location.Map, target), route != null && route.Count > 0);
                if (m_MouseContextActions.Count == 0)
                {
                    CloseMouseContextMenu();
                    return true;
                }
                Point canvas = new Point((int)(mousePos.X / m_UI.UI_GetCanvasScaleX()),
                    (int)(mousePos.Y / m_UI.UI_GetCanvasScaleY()));
                m_MouseContextPosition = new Point(
                    Math.Max(0, Math.Min(RIGHTPANEL_X - MOUSE_MENU_WIDTH,
                        canvas.X)),
                    Math.Max(0, Math.Min(MESSAGES_Y - (m_MouseContextActions.Count + 1)
                        * MOUSE_MENU_ROW_HEIGHT, canvas.Y)));
                m_MouseContextSelected = 0;
                Logger.WriteLine(Logger.Stage.RUN_MAIN,
                    "mouse context menu opened: target=" + target.X + "," + target.Y +
                    " player=" + player.Location.Position.X + "," + player.Location.Position.Y +
                    " actions=" + m_MouseContextActions.Count);
                return true;
            }
            if (m_MouseContextActions == null) return false;

            Point click = new Point((int)(mousePos.X / m_UI.UI_GetCanvasScaleX()),
                (int)(mousePos.Y / m_UI.UI_GetCanvasScaleY()));
            Rectangle menu = new Rectangle(m_MouseContextPosition,
                new Size(MOUSE_MENU_WIDTH, (m_MouseContextActions.Count + 1) * MOUSE_MENU_ROW_HEIGHT));
            if (menu.Contains(click) && click.Y >= menu.Y + MOUSE_MENU_ROW_HEIGHT)
                m_MouseContextSelected = (click.Y - menu.Y) / MOUSE_MENU_ROW_HEIGHT - 1;
            if (buttons == MouseButtons.Left)
            {
                if (menu.Contains(click) && click.Y >= menu.Y + MOUSE_MENU_ROW_HEIGHT)
                    keepLoop = !ExecuteMouseContextAction(player,
                        m_MouseContextActions[m_MouseContextSelected]);
                CloseMouseContextMenu();
            }
            return true;
        }

        bool HandleMouseContextKey(Actor player, Keys key, out bool keepLoop)
        {
            keepLoop = true;
            if (m_MouseContextActions == null) return false;
            if (key == Keys.Escape) CloseMouseContextMenu();
            else if (key == Keys.Up)
                m_MouseContextSelected = (m_MouseContextSelected + m_MouseContextActions.Count - 1)
                    % m_MouseContextActions.Count;
            else if (key == Keys.Down)
                m_MouseContextSelected = (m_MouseContextSelected + 1) % m_MouseContextActions.Count;
            else if (key == Keys.Enter)
            {
                keepLoop = !ExecuteMouseContextAction(player,
                    m_MouseContextActions[m_MouseContextSelected]);
                CloseMouseContextMenu();
            }
            else CloseMouseContextMenu();
            return true;
        }

        bool ExecuteMouseContextAction(Actor player, MouseContextAction action)
        {
            Logger.WriteLine(Logger.Stage.RUN_MAIN, "mouse context action: " + action.Label);
            Point here = player.Location.Position;
            Map map = player.Location.Map;
            if (action.Kind == MouseContextActionKind.Move)
            {
                List<Point> route = FindMouseMovePath(player, action.Target);
                if (route == null || route.Count == 0) return false;
                m_MouseMoveSteps = route;
                return ContinueMouseMove(player);
            }
            if (action.Kind == MouseContextActionKind.Wait && action.Target == here)
            {
                DoWait(player);
                return true;
            }
            if (TryPlayerInsanity()) return true;
            if (action.Kind == MouseContextActionKind.UseExit && action.Target == here &&
                m_Rules.CanActorUseExit(player, here))
                return DoUseExit(player, here);
            if (action.Kind == MouseContextActionKind.LeaveMap &&
                !map.IsInBounds(action.Target) &&
                m_Rules.IsAdjacent(here, action.Target) &&
                map.GetExitAt(action.Target) != null)
            {
                string reason;
                if (m_Rules.CanActorLeaveMap(player, out reason))
                    return DoLeaveMap(player, action.Target, false);
            }
            if (!map.IsInBounds(action.Target) ||
                !m_Rules.IsAdjacent(here, action.Target)) return false;
            if (action.Kind == MouseContextActionKind.CloseDoor)
            {
                DoorWindow door = map.GetMapObjectAt(action.Target) as DoorWindow;
                ActionCloseDoor close = door == null ? null : new ActionCloseDoor(player, this, door);
                if (close == null || !close.IsLegal()) return false;
                close.Perform();
                return true;
            }
            if (action.Kind == MouseContextActionKind.Bump)
            {
                Direction direction = Direction.FromVector(new Point(action.Target.X - here.X,
                    action.Target.Y - here.Y));
                ActionBump bump = new ActionBump(player, this, direction);
                if (!bump.IsLegal() || bump.ConcreteAction is ActionMoveStep) return false;
                if ((bump.ConcreteAction is ActionBreak ||
                    bump.ConcreteAction is ActionBashDoor) && m_Rules.IsActorTired(player))
                    return false;
                // Selecting the menu item confirms potentially destructive bump actions.
                bump.Perform();
                return true;
            }
            return false;
        }

        void DrawMouseContextMenu()
        {
            if (m_MouseContextActions == null) return;
            int height = (m_MouseContextActions.Count + 1) * MOUSE_MENU_ROW_HEIGHT;
            m_UI.UI_FillRect(Color.FromArgb(235, 20, 20, 20),
                new Rectangle(m_MouseContextPosition, new Size(MOUSE_MENU_WIDTH, height)));
            m_UI.UI_DrawRect(Color.Yellow,
                new Rectangle(m_MouseContextPosition, new Size(MOUSE_MENU_WIDTH, height)));
            m_UI.UI_DrawStringBold(Color.Yellow, "Actions", m_MouseContextPosition.X + 5,
                m_MouseContextPosition.Y + 3);
            for (int index = 0; index < m_MouseContextActions.Count; index++)
            {
                int y = m_MouseContextPosition.Y + (index + 1) * MOUSE_MENU_ROW_HEIGHT;
                if (index == m_MouseContextSelected)
                    m_UI.UI_FillRect(Color.DarkSlateBlue,
                        new Rectangle(m_MouseContextPosition.X + 2, y,
                            MOUSE_MENU_WIDTH - 4, MOUSE_MENU_ROW_HEIGHT));
                m_UI.UI_DrawStringBold(Color.White, m_MouseContextActions[index].Label,
                    m_MouseContextPosition.X + 5, y + 3);
            }
        }
    }
}
