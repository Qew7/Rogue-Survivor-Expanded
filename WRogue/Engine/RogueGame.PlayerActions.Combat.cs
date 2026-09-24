using System;
using System.Collections.Generic;
using System.Text;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using System.IO;

using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine.Actions;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Engine.MapObjects;
using djack.RogueSurvivor.Gameplay;
using djack.RogueSurvivor.Gameplay.AI;
using djack.RogueSurvivor.Gameplay.Generators;

using Message = djack.RogueSurvivor.Data.Message;
using djack.RogueSurvivor.Engine.Tasks;
using ItemRating = djack.RogueSurvivor.Gameplay.AI.BaseAI.ItemRating;
using TradeRating = djack.RogueSurvivor.Gameplay.AI.BaseAI.TradeRating;

namespace djack.RogueSurvivor.Engine
{
    partial class RogueGame
    {
        void HandlePlayerRunToggle(Actor player)
        {
            string reason;
            if (!m_Rules.CanActorRun(player, out reason))
            {
                AddMessage(MakeErrorMessage(String.Format("Cannot run now : {0}.", reason)));
                return;
            }

            // ok.
            player.IsRunning = !player.IsRunning;
            AddMessage(MakeMessage(player, String.Format("{0} running.", Conjugate(player, player.IsRunning ? VERB_START : VERB_STOP))));
        }

        bool HandlePlayerCloseDoor(Actor player)
        {
            bool loop = true;
            bool actionDone = false;

            ClearOverlays();
            AddOverlay(new OverlayPopup(CLOSE_DOOR_MODE_TEXT, MODE_TEXTCOLOR, MODE_BORDERCOLOR, MODE_FILLCOLOR, new Point(0, 0)));

            do
            {
                ///////////////////
                // 1. Redraw
                // 2. Get input.
                // 3. Handle input
                ///////////////////

                // 1. Redraw
                RedrawPlayScreen();

                // 2. Get input.
                Direction dir = WaitDirectionOrCancel();

                // 3. Handle input
                if (dir == null)
                {
                    loop = false;
                }
                else if (dir != Direction.NEUTRAL)
                {
                    Point pos = player.Location.Position + dir;
                    if (player.Location.Map.IsInBounds(pos))
                    {
                        MapObject mapObj = player.Location.Map.GetMapObjectAt(pos);
                        if (mapObj != null && mapObj is DoorWindow)
                        {
                            DoorWindow door = mapObj as DoorWindow;
                            string reason;
                            if (m_Rules.IsClosableFor(player, door, out reason))
                            {
                                DoCloseDoor(player, door);
                                RedrawPlayScreen();
                                loop = false;
                                actionDone = true;
                            }
                            else
                            {
                                AddMessage(MakeErrorMessage(String.Format("Can't close {0} : {1}.", door.TheName, reason)));
                            }
                        }
                        else
                            AddMessage(MakeErrorMessage("Nothing to close there."));
                    }
                }
            }
            while (loop);

            // cleanup.
            ClearOverlays();

            // return if we did an action.
            return actionDone;
        }

        bool HandlePlayerBarricade(Actor player)
        {
            bool loop = true;
            bool actionDone = false;

            ClearOverlays();
            AddOverlay(new OverlayPopup(BARRICADE_MODE_TEXT, MODE_TEXTCOLOR, MODE_BORDERCOLOR, MODE_FILLCOLOR, new Point(0, 0)));

            do
            {
                ///////////////////
                // 1. Redraw
                // 2. Get input.
                // 3. Handle input
                ///////////////////

                // 1. Redraw
                RedrawPlayScreen();

                // 2. Get input.
                Direction dir = WaitDirectionOrCancel();

                // 3. Handle input
                if (dir == null)
                {
                    loop = false;
                }
                else if (dir != Direction.NEUTRAL)
                {
                    Point pos = player.Location.Position + dir;
                    if (player.Location.Map.IsInBounds(pos))
                    {
                        MapObject mapObj = player.Location.Map.GetMapObjectAt(pos);
                        if (mapObj != null)
                        {
                            // barricading a door.
                            if (mapObj is DoorWindow)
                            {
                                DoorWindow door = mapObj as DoorWindow;
                                string reason;
                                if (m_Rules.CanActorBarricadeDoor(player, door, out reason))
                                {
                                    DoBarricadeDoor(player, door);
                                    RedrawPlayScreen();
                                    loop = false;
                                    actionDone = true;
                                }
                                else
                                {
                                    AddMessage(MakeErrorMessage(String.Format("Cannot barricade {0} : {1}.", door.TheName, reason)));
                                }
                            }
                            // repairing a fortification.
                            else if (mapObj is Fortification)
                            {
                                Fortification fort = mapObj as Fortification;
                                string reason;
                                if (m_Rules.CanActorRepairFortification(player, fort, out reason))
                                {
                                    DoRepairFortification(player, fort);
                                    RedrawPlayScreen();
                                    loop = false;
                                    actionDone = true;
                                }
                                else
                                {
                                    AddMessage(MakeErrorMessage(String.Format("Cannot repair {0} : {1}.", fort.TheName, reason)));
                                }
                            }
                            else
                                AddMessage(MakeErrorMessage(String.Format("{0} cannot be repaired or barricaded.", mapObj.TheName)));
                        }
                        else
                            AddMessage(MakeErrorMessage("Nothing to barricade there."));
                    }
                }
            }
            while (loop);

            // cleanup.
            ClearOverlays();

            // return if we did an action.
            return actionDone;
        }

        bool HandlePlayerBreak(Actor player)
        {
            bool loop = true;
            bool actionDone = false;

            ClearOverlays();
            AddOverlay(new OverlayPopup(BREAK_MODE_TEXT, MODE_TEXTCOLOR, MODE_BORDERCOLOR, MODE_FILLCOLOR, new Point(0, 0)));

            do
            {
                ///////////////////
                // 1. Redraw
                // 2. Get input.
                // 3. Handle input
                ///////////////////

                // 1. Redraw
                RedrawPlayScreen();

                // 2. Get input.
                Direction dir = WaitDirectionOrCancel();

                // 3. Handle input
                if (dir == null)
                {
                    loop = false;
                }
                else
                {
                    // handle neutral direction = through exit.
                    if (dir == Direction.NEUTRAL)
                    {
                        Exit exitThere = player.Location.Map.GetExitAt(player.Location.Position);
                        if (exitThere == null)
                            AddMessage(MakeErrorMessage("No exit there."));
                        else
                        {
                            // attack/break?
                            string reason;
                            Map mapTo = exitThere.ToMap;
                            Actor actorTo = mapTo.GetActorAt(exitThere.ToPosition);
                            if (actorTo != null)
                            {
                                // only if enemy.
                                if (m_Rules.AreEnemies(player, actorTo))
                                {
                                    // check melee rule.
                                    if (m_Rules.CanActorMeleeAttack(player, actorTo, out reason))
                                    {
                                        DoMeleeAttack(player, actorTo);
                                        loop = false;
                                        actionDone = true;
                                    }
                                    else
                                        AddMessage(MakeErrorMessage(String.Format("Cannot attack {0} : {1}.", actorTo.Name, reason)));
                                }
                                else
                                    AddMessage(MakeErrorMessage(String.Format("{0} is not your enemy.", actorTo.Name)));
                            }
                            else
                            {
                                // break?
                                MapObject objTo = mapTo.GetMapObjectAt(exitThere.ToPosition);
                                if (objTo != null)
                                {
                                    // check break rule.
                                    if (m_Rules.IsBreakableFor(player, objTo, out reason))
                                    {
                                        DoBreak(player, objTo);
                                        loop = false;
                                        actionDone = true;
                                    }
                                    else
                                        AddMessage(MakeErrorMessage(String.Format("Cannot break {0} : {1}.", objTo.TheName, reason)));
                                }
                                else
                                    AddMessage(MakeErrorMessage("Nothing to break or attack on the other side."));
                            }

                        }
                    }
                    else
                    {
                        // adjacent direction.
                        Point pos = player.Location.Position + dir;
                        if (player.Location.Map.IsInBounds(pos))
                        {
                            MapObject mapObj = player.Location.Map.GetMapObjectAt(pos);
                            if (mapObj != null)
                            {
                                string reason;
                                if (m_Rules.IsBreakableFor(player, mapObj, out reason))
                                {
                                    DoBreak(player, mapObj);
                                    RedrawPlayScreen();
                                    loop = false;
                                    actionDone = true;
                                }
                                else
                                {
                                    AddMessage(MakeErrorMessage(String.Format("Cannot break {0} : {1}.", mapObj.TheName, reason)));
                                }
                            }
                            else
                                AddMessage(MakeErrorMessage("Nothing to break there."));
                        }
                    }
                }
            }
            while (loop);

            // cleanup.
            ClearOverlays();

            // return if we did an action.
            return actionDone;
        }

        bool HandlePlayerBuildFortification(Actor player, bool isLarge)
        {
            /////////////////////////////////////
            // Check skill & has enough material.
            /////////////////////////////////////
            if (player.Sheet.SkillTable.GetSkillLevel((int)Skills.IDs.CARPENTRY) == 0)
            {
                AddMessage(MakeErrorMessage("need carpentry skill."));
                return false;
            }
            int need = m_Rules.ActorBarricadingMaterialNeedForFortification(player, isLarge);
            if (m_Rules.CountBarricadingMaterial(player) < need)
            {
                AddMessage(MakeErrorMessage(string.Format("not enough barricading material, need {0}.", need)));
                return false;
            }

            bool loop = true;
            bool actionDone = false;

            ClearOverlays();
            AddOverlay(new OverlayPopup(isLarge ? BUILD_LARGE_FORT_MODE_TEXT : BUILD_SMALL_FORT_MODE_TEXT, MODE_TEXTCOLOR, MODE_BORDERCOLOR, MODE_FILLCOLOR, new Point(0, 0)));

            do
            {
                ///////////////////
                // 1. Redraw
                // 2. Get input.
                // 3. Handle input
                ///////////////////

                // 1. Redraw
                RedrawPlayScreen();

                // 2. Get input.
                Direction dir = WaitDirectionOrCancel();

                // 3. Handle input
                if (dir == null)
                {
                    loop = false;
                }
                else if (dir != Direction.NEUTRAL)
                {
                    Point pos = player.Location.Position + dir;
                    if (player.Location.Map.IsInBounds(pos))
                    {
                        string reason;
                        if (m_Rules.CanActorBuildFortification(player, pos, isLarge, out reason))
                        {
                            DoBuildFortification(player, pos, isLarge);
                            RedrawPlayScreen();
                            loop = false;
                            actionDone = true;
                        }
                        else
                        {
                            AddMessage(MakeErrorMessage(String.Format("Cannot build here : {0}.", reason)));
                        }
                    }
                }
            }
            while (loop);

            // cleanup.
            ClearOverlays();

            // return if we did an action.
            return actionDone;
        }

        bool HandlePlayerFireMode(Actor player)
        {
            bool loop = true;
            bool actionDone = false;

            // If grenade equipped, redirected to HandlePlayerThrowGrenade.
            ItemGrenade grenade = player.GetEquippedWeapon() as ItemGrenade;
            ItemGrenadePrimed primedGrenade = player.GetEquippedWeapon() as ItemGrenadePrimed;
            if (grenade != null || primedGrenade != null)
                return HandlePlayerThrowGrenade(player);

            // Check if weapon to fire.
            ItemRangedWeapon rangedWeapon = player.GetEquippedWeapon() as ItemRangedWeapon;
            if (rangedWeapon == null)
            {
                AddMessage(MakeErrorMessage("No weapon ready to fire."));
                RedrawPlayScreen();
                return false;
            }
            if (rangedWeapon.Ammo <= 0)
            {
                AddMessage(MakeErrorMessage("No ammo left."));
                RedrawPlayScreen();
                return false;
            }

            // Get targeting data.
            HashSet<Point> fov = LOS.ComputeFOVFor(m_Rules, player, m_Session.WorldTime, m_Session.World.Weather);
            List<Actor> potentialTargets = m_Rules.GetEnemiesInFov(player, fov);

            if (potentialTargets == null || potentialTargets.Count == 0)
            {
                AddMessage(MakeErrorMessage("No targets to fire at."));
                RedrawPlayScreen();
                return false;
            }

            // Loop.
            Attack rangedAttack = m_Rules.ActorRangedAttack(player, player.CurrentRangedAttack, 0, null);
            int iCurrentTarget = 0;
            List<Point> LoF = new List<Point>(rangedAttack.Range);
            FireMode mode = m_Session.Player_CurrentFireMode;  // alpha10
            do
            {
                Actor currentTarget = potentialTargets[iCurrentTarget];
                LoF.Clear();
                string reason;
                bool canFireAtTarget = m_Rules.CanActorFireAt(player, currentTarget, LoF, out reason);
                int dToTarget = m_Rules.GridDistance(player.Location.Position, currentTarget.Location.Position);

                string modeDesc;
                if (mode == FireMode.RAPID)
                    modeDesc = string.Format("RAPID fire average hit chances {0}% {1}%", m_Rules.ComputeChancesRangedHit(player, currentTarget, 1), m_Rules.ComputeChancesRangedHit(player, currentTarget, 2));
                else
                    modeDesc = string.Format("Normal fire average hit chance {0}%", m_Rules.ComputeChancesRangedHit(player, currentTarget, 0));

                ///////////////////
                // 1. Redraw
                // 2. Get input.
                // 3. Handle input
                ///////////////////

                // 1. Redraw
                List<string> overlayPopupText = new List<string>();
                overlayPopupText.AddRange(FIRE_MODE_TEXT);
                overlayPopupText.Add(modeDesc);
                ClearOverlays();
                AddOverlay(new OverlayPopup(overlayPopupText.ToArray(), MODE_TEXTCOLOR, MODE_BORDERCOLOR, MODE_FILLCOLOR, new Point(0, 0)));
                Point targetScreen = MapToScreen(currentTarget.Location.Position);
                AddOverlay(new OverlayImage(targetScreen, GameImages.ICON_TARGET));
                string lineImage = canFireAtTarget ? (dToTarget <= rangedAttack.EfficientRange ? GameImages.ICON_LINE_CLEAR : GameImages.ICON_LINE_BAD) : GameImages.ICON_LINE_BLOCKED;
                foreach (Point pt in LoF)
                {
                    Point screenPt = MapToScreen(pt);
                    AddOverlay(new OverlayImage(screenPt, lineImage));
                }
                RedrawPlayScreen();

                // 2. Get input.
                KeyEventArgs key = m_UI.UI_WaitKey();
                PlayerCommand command = InputTranslator.KeyToCommand(key);

                // 3. Handle input
                if (key.KeyCode == Keys.Escape) //command == PlayerCommand.EXIT_OR_CANCEL)
                {
                    loop = false;
                }
                else if (key.KeyCode == Keys.T)  // next target
                {
                    iCurrentTarget = (iCurrentTarget + 1) % potentialTargets.Count;
                }
                else if (key.KeyCode == Keys.M)    // next mode
                {
                    // switch.
                    mode = (FireMode)(((int)mode + 1) % (int)FireMode._COUNT);
                    // tell.
                    AddMessage(new Message(String.Format("Switched to {0} fire mode.", mode.ToString()), m_Session.WorldTime.TurnCounter, Color.Yellow));
                    // alpha10
                    // save preference to session
                    m_Session.Player_CurrentFireMode = mode;
                }
                else if (key.KeyCode == Keys.F) // do fire
                {
                    if (canFireAtTarget)
                    {
                        DoRangedAttack(player, currentTarget, LoF, mode);
                        RedrawPlayScreen();
                        loop = false;
                        actionDone = true;
                    }
                    else
                    {
                        AddMessage(MakeErrorMessage(String.Format("Can't fire at {0} : {1}.", currentTarget.TheName, reason)));
                    }
                }
            }
            while (loop);

            // cleanup.
            ClearOverlays();

            // return if we did an action.
            return actionDone;
        }

        void HandlePlayerMarkEnemies(Actor player)
        {
            // Pre-conditions.
            // FIXME put all that into a rule Rule.CanMakeEnemyOf()
            if (player.Model.Abilities.IsUndead)
            {
                AddMessage(MakeErrorMessage("Undeads can't have personal enemies."));
                return;
            }

            // List all visible actors.
            #region
            Map map = player.Location.Map;
            List<Actor> visibleActors = new List<Actor>();
            foreach (Point p in m_PlayerFOV)
            {
                Actor a = map.GetActorAt(p);
                if (a == null || a.IsPlayer)
                    continue;
                visibleActors.Add(a);
            }
            if (visibleActors.Count == 0)
            {
                AddMessage(MakeErrorMessage("No visible actors to mark."));
                RedrawPlayScreen();
                return;
            }
            #endregion

            // Loop.
            bool loop = true;
            int iCurrentActor = 0;
            do
            {
                Actor currentActor = visibleActors[iCurrentActor];

                ///////////////////
                // 1. Redraw
                // 2. Get input.
                // 3. Handle input
                ///////////////////

                // 1. Redraw
                ClearOverlays();
                AddOverlay(new OverlayPopup(MARK_ENEMIES_MODE, MODE_TEXTCOLOR, MODE_BORDERCOLOR, MODE_FILLCOLOR, new Point(0, 0)));
                Point targetScreen = MapToScreen(currentActor.Location.Position);
                AddOverlay(new OverlayImage(targetScreen, GameImages.ICON_TARGET));
                RedrawPlayScreen();

                // 2. Get input.
                KeyEventArgs key = m_UI.UI_WaitKey();
                PlayerCommand command = InputTranslator.KeyToCommand(key);

                // 3. Handle input
                if (key.KeyCode == Keys.Escape)// command == PlayerCommand.EXIT_OR_CANCEL)
                {
                    loop = false;
                }
                else if (key.KeyCode == Keys.T)  // next actor
                {
                    iCurrentActor = (iCurrentActor + 1) % visibleActors.Count;
                }

                else if (key.KeyCode == Keys.E) // toggle.
                {
                    // never make enemies of leader/follower/enemy faction.
                    // FIXME put all that into a rule Rule.CanMakeEnemyOf()
                    bool allowed = true;
                    if (currentActor.Leader == player)
                    {
                        AddMessage(MakeErrorMessage("Can't make a follower your enemy."));
                        allowed = false;
                    }
                    else if (player.Leader == currentActor)
                    {
                        AddMessage(MakeErrorMessage("Can't make your leader your enemy."));
                        allowed = false;
                    }
                    else if (m_Rules.AreEnemies(m_Player, currentActor))
                    {
                        AddMessage(MakeErrorMessage("Already enemies."));
                        allowed = false;
                    }

                    // do it?
                    if (allowed)
                    {
                        AddMessage(new Message(String.Format("{0} is now a personal enemy.", currentActor.TheName), m_Session.WorldTime.TurnCounter, Color.Orange));
                        DoMakeAggression(player, currentActor);
                    }
                }
            }
            while (loop);

            // cleanup.
            ClearOverlays();
        }

        bool HandlePlayerThrowGrenade(Actor player)
        {
            bool loop = true;
            bool actionDone = false;

            // Get grenade equipped.
            ItemGrenade unprimedGrenade = player.GetEquippedWeapon() as ItemGrenade;
            ItemGrenadePrimed primedGrenade = player.GetEquippedWeapon() as ItemGrenadePrimed;
            if (unprimedGrenade == null && primedGrenade == null)
            {
                AddMessage(MakeErrorMessage("No grenade to throw."));
                RedrawPlayScreen();
                return false;
            }
            ItemGrenadeModel grenadeModel;
            if (unprimedGrenade != null)
                grenadeModel = unprimedGrenade.Model as ItemGrenadeModel;
            else
                grenadeModel = (primedGrenade.Model as ItemGrenadePrimedModel).GrenadeModel;

            // Get data.
            Map map = player.Location.Map;
            Point targetThrow = player.Location.Position;
            int maxThrowDist = m_Rules.ActorMaxThrowRange(player, grenadeModel.MaxThrowDistance);

            // Loop.
            List<Point> LoT = new List<Point>();
            do
            {
                // get LoT.
                LoT.Clear();
                string reason;
                bool canThrowAtTarget = m_Rules.CanActorThrowTo(player, targetThrow, LoT, out reason);

                ///////////////////
                // 1. Redraw
                // 2. Get input.
                // 3. Handle input
                ///////////////////

                // 1. Redraw
                ClearOverlays();
                AddOverlay(new OverlayPopup(THROW_GRENADE_MODE_TEXT, MODE_TEXTCOLOR, MODE_BORDERCOLOR, MODE_FILLCOLOR, new Point(0, 0)));
                string lineImage = canThrowAtTarget ? GameImages.ICON_LINE_CLEAR : GameImages.ICON_LINE_BLOCKED;
                foreach (Point pt in LoT)
                {
                    Point screenPt = MapToScreen(pt);
                    AddOverlay(new OverlayImage(screenPt, lineImage));
                }
                RedrawPlayScreen();

                // 2. Get input.
                KeyEventArgs key;
                Point mousePos;
                MouseButtons? mouseButtons;
                WaitKeyOrMouse(out key, out mousePos, out mouseButtons);
                bool throwRequested = key != null && key.KeyCode == Keys.F;
                if (key == null)
                {
                    Point? mouseTarget = GrenadeTargetFromMouse(MouseToMap(mousePos),
                        map, m_MapViewRect, player.Location.Position, maxThrowDist);
                    if (mouseTarget.HasValue)
                    {
                        targetThrow = mouseTarget.Value;
                        if (mouseButtons == MouseButtons.Left)
                        {
                            LoT.Clear();
                            canThrowAtTarget = m_Rules.CanActorThrowTo(player, targetThrow, LoT, out reason);
                            throwRequested = true;
                        }
                    }
                }

                // 3. Handle input
                if ((key != null && key.KeyCode == Keys.Escape) || mouseButtons == MouseButtons.Right)
                {
                    loop = false;
                }
                else if (throwRequested) // do throw.
                {
                    if (canThrowAtTarget)
                    {
                        bool doIt = true;

                        // if within the blast radius, ask for confirmation...
                        if (m_Rules.GridDistance(player.Location.Position, targetThrow) <= grenadeModel.BlastAttack.Radius)
                        {
                            ClearMessages();
                            AddMessage(new Message("You are in the blast radius!", m_Session.WorldTime.TurnCounter, Color.Yellow));
                            AddMessage(MakeYesNoMessage("Really throw there"));
                            RedrawPlayScreen();
                            doIt = WaitYesOrNo();
                            ClearMessages();
                            RedrawPlayScreen();
                        }

                        if (doIt)
                        {
                            // fire in the hole!
                            if (unprimedGrenade != null)
                                DoThrowGrenadeUnprimed(player, targetThrow);
                            else
                                DoThrowGrenadePrimed(player, targetThrow);
                            RedrawPlayScreen();
                            loop = false;
                            actionDone = true;
                        }
                    }
                    else
                    {
                        AddMessage(MakeErrorMessage(String.Format("Can't throw there : {0}.", reason)));
                    }
                }
                else if (key != null)
                {
                    // direction?
                    Direction dir = CommandToDirection(InputTranslator.KeyToCommand(key));
                    if (dir != null)
                    {
                        Point? next = GrenadeTargetFromDirection(targetThrow, dir, map,
                            player.Location.Position, maxThrowDist);
                        if (next.HasValue) targetThrow = next.Value;
                    }
                }
            }
            while (loop);

            // cleanup.
            ClearOverlays();

            // return if we did an action.
            return actionDone;
        }

        internal static Point? GrenadeTargetFromMouse(Point target, Map map,
            Rectangle view, Point origin, int maxDistance)
        {
            if (!view.Contains(target) || !map.IsInBounds(target) ||
                Math.Max(Math.Abs(target.X - origin.X), Math.Abs(target.Y - origin.Y)) > maxDistance)
                return null;
            return target;
        }

        internal static Point? GrenadeTargetFromDirection(Point current, Direction direction,
            Map map, Point origin, int maxDistance)
        {
            Point target = current + direction;
            if (!map.IsInBounds(target) ||
                Math.Max(Math.Abs(target.X - origin.X), Math.Abs(target.Y - origin.Y)) > maxDistance)
                return null;
            return target;
        }

        bool HandlePlayerSleep(Actor player)
        {
            // Check rule.
            string reason;
            if (!m_Rules.CanActorSleep(player, out reason))
            {
                AddMessage(MakeErrorMessage(String.Format("Cannot sleep now : {0}.", reason)));
                return false;
            }

            // Ask for confirmation.
            AddMessage(MakeYesNoMessage("Really sleep there"));
            RedrawPlayScreen();
            bool confirm = WaitYesOrNo();
            if (!confirm)
            {
                AddMessage(new Message("Good, keep those eyes wide open.", m_Session.WorldTime.TurnCounter, Color.Yellow));
                return false;
            }

            // alpha10.1 check autosave before player starts to sleep
            CheckAutoSaveTime();

            // Start sleeping.
            AddMessage(new Message("Goodnight, happy nightmares!", m_Session.WorldTime.TurnCounter, Color.Yellow));
            DoStartSleeping(player);
            RedrawPlayScreen();
            // check music.
            m_MusicManager.Stop();
            m_MusicManager.PlayLooping(GameMusics.SLEEP, MusicPriority.PRIORITY_EVENT);
            return true;
        }

        bool HandlePlayerSwitchPlace(Actor player)
        {
            bool loop = true;
            bool actionDone = false;

            ClearOverlays();
            AddOverlay(new OverlayPopup(SWITCH_PLACE_MODE_TEXT, MODE_TEXTCOLOR, MODE_BORDERCOLOR, MODE_FILLCOLOR, new Point(0, 0)));

            do
            {
                ///////////////////
                // 1. Redraw
                // 2. Get input.
                // 3. Handle input
                ///////////////////

                // 1. Redraw
                RedrawPlayScreen();

                // 2. Get input.
                Direction dir = WaitDirectionOrCancel();

                // 3. Handle input
                if (dir == null)
                {
                    loop = false;
                }
                else if (dir != Direction.NEUTRAL)
                {
                    Point pos = player.Location.Position + dir;
                    if (player.Location.Map.IsInBounds(pos))
                    {
                        Actor other = player.Location.Map.GetActorAt(pos);
                        if (other != null)
                        {
                            string reason;
                            if (m_Rules.CanActorSwitchPlaceWith(player, other, out reason))
                            {
                                // switch place.
                                actionDone = true;
                                loop = false;
                                DoSwitchPlace(player, other);
                            }
                            else
                            {
                                AddMessage(MakeErrorMessage(String.Format("Can't switch place : {0}", reason)));
                            }
                        }
                        else
                            AddMessage(MakeErrorMessage("Noone there."));
                    }
                }
            }
            while (loop);

            // cleanup.
            ClearOverlays();

            // return if we did an action.
            return actionDone;
        }

        bool HandlePlayerTakeLead(Actor player)
        {
            bool loop = true;
            bool actionDone = false;

            ClearOverlays();
            AddOverlay(new OverlayPopup(TAKE_LEAD_MODE_TEXT, MODE_TEXTCOLOR, MODE_BORDERCOLOR, MODE_FILLCOLOR, new Point(0, 0)));

            do
            {
                ///////////////////
                // 1. Redraw
                // 2. Get input.
                // 3. Handle input
                ///////////////////

                // 1. Redraw
                RedrawPlayScreen();

                // 2. Get input.
                Direction dir = WaitDirectionOrCancel();

                // 3. Handle input
                if (dir == null)
                {
                    loop = false;
                }
                else if (dir != Direction.NEUTRAL)
                {
                    Point pos = player.Location.Position + dir;
                    if (player.Location.Map.IsInBounds(pos))
                    {
                        Actor other = player.Location.Map.GetActorAt(pos);
                        if (other != null)
                        {
                            string reason;
                            if (m_Rules.CanActorTakeLead(player, other, out reason))
                            {
                                // take lead.
                                actionDone = true;
                                loop = false;

                                // alpha10.1 steal lead vs take lead
                                if (other.HasLeader)
                                    DoStealLead(player, other);
                                else
                                    DoTakeLead(player, other);

                                // scoring.
                                m_Session.Scoring.AddEvent(m_Session.WorldTime.TurnCounter, String.Format("Recruited {0}.", other.TheName));

                                // help message.
                                AddMessage(new Message("(you can now set directives and orders for your new follower).", m_Session.WorldTime.TurnCounter, Color.White));
                                AddMessage(new Message(String.Format("(to give order : press <{0}>).", s_KeyBindings.Get(PlayerCommand.ORDER_MODE).ToString()), m_Session.WorldTime.TurnCounter, Color.White));

                            }
                            else if (other.Leader == player)
                            {
                                if (m_Rules.CanActorCancelLead(player, other, out reason))
                                {
                                    // ask for confirmation.
                                    AddMessage(MakeYesNoMessage(String.Format("Really ask {0} to leave", other.TheName)));
                                    RedrawPlayScreen();
                                    bool confirm = WaitYesOrNo();
                                    if (confirm)
                                    {
                                        // cancel lead.
                                        actionDone = true;
                                        loop = false;
                                        DoCancelLead(player, other);

                                        // scoring.
                                        m_Session.Scoring.AddEvent(m_Session.WorldTime.TurnCounter, String.Format("Fired {0}.", other.TheName));
                                    }
                                    else
                                        AddMessage(new Message("Good, together you are strong.", m_Session.WorldTime.TurnCounter, Color.Yellow));
                                }
                                else
                                {
                                    AddMessage(MakeErrorMessage(String.Format("{0} can't leave : {1}.", other.TheName, reason)));
                                }
                            }
                            else
                            {
                                AddMessage(MakeErrorMessage(String.Format("Can't lead {0} : {1}.", other.TheName, reason)));
                            }
                        }
                        else
                            AddMessage(MakeErrorMessage("Noone there."));
                    }
                }
            }
            while (loop);

            // cleanup.
            ClearOverlays();

            // return if we did an action.
            return actionDone;
        }

    }
}
