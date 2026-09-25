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
        #region Ordering followers
        bool HandlePlayerOrderMode(Actor player)
        {
            // check if we have followers to order.
            if (player.CountFollowers == 0)
            {
                AddMessage(MakeErrorMessage("No followers to give orders to."));
                return false;
            }

            // get followers data.
            Actor[] followers = new Actor[player.CountFollowers];
            HashSet<Point>[] fovs = new HashSet<Point>[player.CountFollowers];
            bool[] hasLinkWith = new bool[player.CountFollowers];
            int iFo = 0;
            foreach (Actor fo in player.Followers)
            {
                followers[iFo] = fo;
                fovs[iFo] = LOS.ComputeFOVFor(m_Rules, fo, m_Session.WorldTime, m_Session.World.Weather);
                bool inView = fovs[iFo].Contains(player.Location.Position) && m_PlayerFOV.Contains(fo.Location.Position);
                bool linkedByPhone = AreLinkedByPhone(player, fo);
                hasLinkWith[iFo] = inView || linkedByPhone;
                ++iFo;
            }

            // if one follower and he's linked, skip selection and directly go to its menu.
            if (player.CountFollowers == 1 && hasLinkWith[0])
            {
                bool done = HandlePlayerOrderFollower(player, followers[0]);
                // cleanup.
                ClearOverlays();
                ClearMessages();
                // done.
                return done;
            }

            // loop.
            bool loop = true;
            bool actionDone = false;
            const int maxFoOnPage = MAX_MESSAGES - 2;
            int iFirstFollower = 0;
            do
            {

                ///////////////////
                // 1. Redraw
                // 2. Get input.
                // 3. Handle input
                ///////////////////

                // 1. Redraw
                ClearOverlays();
                AddOverlay(new OverlayPopup(ORDER_MODE_TEXT, MODE_TEXTCOLOR, MODE_BORDERCOLOR, MODE_FILLCOLOR, new Point(0, 0)));
                ClearMessages();
                AddMessage(new Message("Choose a follower.", m_Session.WorldTime.TurnCounter, Color.Yellow));
                int foShown;
                for (foShown = 0; foShown < maxFoOnPage && (iFirstFollower + foShown < followers.Length); foShown++)
                {
                    iFo = foShown + iFirstFollower;
                    Actor f = followers[iFo];
                    string desc = DescribePlayerFollowerStatus(f);

                    if (hasLinkWith[iFo])
                        AddMessage(new Message(String.Format("{0}. {1}/{2} {3}... {4}.", (1 + foShown), iFo + 1, followers.Length, f.Name, desc), m_Session.WorldTime.TurnCounter, Color.LightGreen));
                    else
                        AddMessage(new Message(String.Format("{0}. {1}/{2} ({3}) {4}.", (1 + foShown), iFo + 1, followers.Length, f.Name, desc), m_Session.WorldTime.TurnCounter, Color.DarkGray));
                }
                if (foShown < followers.Length)
                {
                    AddMessage(new Message("9. next", m_Session.WorldTime.TurnCounter, Color.LightGreen));
                }
                RedrawPlayScreen();

                // 2. Get input.
                KeyEventArgs key = m_UI.UI_WaitKey();
                int choice = KeyToChoiceNumber(key.KeyCode);

                // 3. Handle input
                if (key.KeyCode == Keys.Escape)
                {
                    loop = false;
                }
                else if (choice == 9)
                {
                    iFirstFollower += maxFoOnPage;
                    if (iFirstFollower >= followers.Length)
                        iFirstFollower = 0;
                }
                else if (choice >= 1 && choice <= foShown)
                {
                    // Follower must be linked.
                    int f = iFirstFollower + choice - 1;
                    if (hasLinkWith[f])
                    {
                        /////////////////////////////////////////////
                        // Follower selected, select directive/order
                        /////////////////////////////////////////////
                        Actor selectedFollower = followers[f];
                        if (HandlePlayerOrderFollower(player, selectedFollower))
                        {
                            loop = false;
                            actionDone = true;
                        }
                    }
                }
            }
            while (loop);

            // cleanup.
            ClearOverlays();
            ClearMessages();

            // return if we did an action.
            return actionDone;
        }

        bool HandlePlayerDirectiveFollower(Actor player, Actor follower)
        {
            bool loop = true;
            bool actionDone = false;

            // loop.
            do
            {
                ///////////////////
                // 1. Redraw
                // 2. Get input.
                // 3. Handle input
                ///////////////////
                ActorDirective directives = (follower.Controller as AIController).Directives;

                // 1. Redraw
                ClearOverlays();
                AddOverlay(new OverlayPopup(ORDER_MODE_TEXT, MODE_TEXTCOLOR, MODE_BORDERCOLOR, MODE_FILLCOLOR, new Point(0, 0)));
                ClearMessages();
                AddMessage(new Message(String.Format("{0} directives...", follower.Name), m_Session.WorldTime.TurnCounter, Color.Yellow));
                AddMessage(new Message(String.Format("1. {0} items.", directives.CanTakeItems ? "Take" : "Don't take"), m_Session.WorldTime.TurnCounter, Color.LightGreen));
                AddMessage(new Message(String.Format("2. {0} weapons.", directives.CanFireWeapons ? "Fire" : "Don't fire"), m_Session.WorldTime.TurnCounter, Color.LightGreen));
                AddMessage(new Message(String.Format("3. {0} grenades.", directives.CanThrowGrenades ? "Throw" : "Don't throw"), m_Session.WorldTime.TurnCounter, Color.LightGreen));
                AddMessage(new Message(String.Format("4. {0}.", directives.CanSleep ? "Sleep" : "Don't sleep"), m_Session.WorldTime.TurnCounter, Color.LightGreen));
                AddMessage(new Message(String.Format("5. {0}.", directives.CanTrade ? "Trade" : "Don't trade"), m_Session.WorldTime.TurnCounter, Color.LightGreen));
                AddMessage(new Message(String.Format("6. {0}.", ActorDirective.CourageString(directives.Courage)), m_Session.WorldTime.TurnCounter, Color.LightGreen));
                RedrawPlayScreen();

                // 2. Get input.
                KeyEventArgs key = m_UI.UI_WaitKey();
                int choice = KeyToChoiceNumber(key.KeyCode);

                // 3. Handle input
                if (key.KeyCode == Keys.Escape)
                {
                    loop = false;
                }
                else if (choice >= 1 && choice <= 6)
                {
                    switch (choice)
                    {
                        case 1: // items
                            directives.CanTakeItems = !directives.CanTakeItems;
                            break;
                        case 2: // weapons
                            directives.CanFireWeapons = !directives.CanFireWeapons;
                            break;
                        case 3: // grenades.
                            directives.CanThrowGrenades = !directives.CanThrowGrenades;
                            break;
                        case 4: // sleep
                            directives.CanSleep = !directives.CanSleep;
                            break;
                        case 5: // trade
                            directives.CanTrade = !directives.CanTrade;
                            break;
                        case 6:  // courage: coward -> cautious -> courageous.
                            switch (directives.Courage)
                            {
                                case ActorCourage.COWARD:
                                    directives.Courage = ActorCourage.CAUTIOUS;
                                    break;
                                case ActorCourage.CAUTIOUS:
                                    directives.Courage = ActorCourage.COURAGEOUS;
                                    break;
                                case ActorCourage.COURAGEOUS:
                                    directives.Courage = ActorCourage.COWARD;
                                    break;
                            }
                            break;
                    }
                }
            }
            while (loop);

            return actionDone;
        }

        bool HandlePlayerOrderFollower(Actor player, Actor follower)
        {
            // check trust.
            if (!m_Rules.IsActorTrustingLeader(follower))
            {
                // say/phone
                if (IsVisibleToPlayer(follower))
                    DoSay(follower, player, "Sorry, I don't trust you enough yet.", Sayflags.IS_FREE_ACTION | Sayflags.IS_IMPORTANT);
                else if (AreLinkedByPhone(follower, player))
                {
                    ClearMessages();
                    AddMessage(MakeMessage(follower, "Sorry, I don't trust you enough yet."));
                    AddMessagePressEnter();
                }
                // refuse!
                return false;
            }

            // current order.
            string desc = DescribePlayerFollowerStatus(follower);

            // compute follower fov.
            HashSet<Point> followerFOV = LOS.ComputeFOVFor(m_Rules, follower, m_Session.WorldTime, m_Session.World.Weather);

            // loop.
            bool loop = true;
            bool actionDone = false;
            do
            {
                ///////////////////
                // 1. Redraw
                // 2. Get input.
                // 3. Handle input
                ///////////////////

                // 1. Redraw
                string startStopFollow = (follower.Controller as OrderableAI).DontFollowLeader ? "Start" : "Stop";
                ClearOverlays();
                AddOverlay(new OverlayPopup(ORDER_MODE_TEXT, MODE_TEXTCOLOR, MODE_BORDERCOLOR, MODE_FILLCOLOR, new Point(0, 0)));
                ClearMessages();
                AddMessage(new Message(String.Format("Order {0} to...", follower.Name), m_Session.WorldTime.TurnCounter, Color.Yellow));
                AddMessage(new Message(String.Format("0. Cancel current order {0}.", desc), m_Session.WorldTime.TurnCounter, Color.Green));
                AddMessage(new Message("1. Set directives...", m_Session.WorldTime.TurnCounter, Color.Cyan));
                AddMessage(new Message("2. Barricade (one)...    6. Drop all items.      A. Give me...", m_Session.WorldTime.TurnCounter, Color.LightGreen));
                AddMessage(new Message("3. Barricade (max)...    7. Build small fort.    B. Sleep now.", m_Session.WorldTime.TurnCounter, Color.LightGreen));
                AddMessage(new Message(String.Format("4. Guard...              8. Build large fort.    C. {0} following me.   ", startStopFollow), m_Session.WorldTime.TurnCounter, Color.LightGreen));
                AddMessage(new Message("5. Patrol...             9. Report events.       D. Where are you?", m_Session.WorldTime.TurnCounter, Color.LightGreen));
                if (m_Session.GamePreset.Bases)
                    AddMessage(new Message("E. Scavenge supplies for base.", m_Session.WorldTime.TurnCounter, Color.LightGreen));
                RedrawPlayScreen();

                // 2. Get input.
                KeyEventArgs key = m_UI.UI_WaitKey();
                int choice = KeyToChoiceNumber(key.KeyCode);

                // 3. Handle input
                if (key.KeyCode == Keys.Escape)
                {
                    loop = false;
                }
                // first set of choices 0-9
                else if (choice >= 0 && choice <= 9)
                {

                    #region
                    switch (choice)
                    {
                        case 0: // cancel current order
                            DoCancelOrder(player, follower);
                            loop = false;
                            actionDone = true;
                            break;

                        case 1: // set directives.
                            HandlePlayerDirectiveFollower(player, follower);
                            break;

                        case 2: // barricade (one)
                            if (HandlePlayerOrderFollowerToBarricade(player, follower, followerFOV, false))
                            {
                                loop = false;
                                actionDone = true;
                            }
                            break;

                        case 3: // barricade (max)
                            if (HandlePlayerOrderFollowerToBarricade(player, follower, followerFOV, true))
                            {
                                loop = false;
                                actionDone = true;
                            }
                            break;

                        case 4: // guard
                            if (HandlePlayerOrderFollowerToGuard(player, follower, followerFOV))
                            {
                                loop = false;
                                actionDone = true;
                            }
                            break;

                        case 5: // patrol
                            if (HandlePlayerOrderFollowerToPatrol(player, follower, followerFOV))
                            {
                                loop = false;
                                actionDone = true;
                            }
                            break;

                        case 6: // drop all items
                            if (HandlePlayerOrderFollowerToDropAllItems(player, follower))
                            {
                                loop = false;
                                actionDone = true;
                            }
                            break;

                        case 7: // build small fort.
                            if (HandlePlayerOrderFollowerToBuildFortification(player, follower, followerFOV, false))
                            {
                                loop = false;
                                actionDone = true;
                            }
                            break;

                        case 8: // build large fort.
                            if (HandlePlayerOrderFollowerToBuildFortification(player, follower, followerFOV, true))
                            {
                                loop = false;
                                actionDone = true;
                            }
                            break;

                        case 9: // report
                            if (HandlePlayerOrderFollowerToReport(player, follower))
                            {
                                loop = false;
                                actionDone = true;
                            }
                            break;

                    }
                    #endregion
                }
                // second set of choices A-xxx
                #region
                else
                {
                    switch (key.KeyCode)
                    {
                        case Keys.A:    // give items...
                            if (HandlePlayerOrderFollowerToGiveItems(player, follower))
                            {
                                loop = false;
                                actionDone = true;
                            }
                            break;

                        case Keys.B: // sleep now
                            if (HandlePlayerOrderFollowerToSleep(player, follower))
                            {
                                loop = false;
                                actionDone = true;
                            }
                            break;

                        case Keys.C: // toggle follow
                            if (HandlePlayerOrderFollowerToToggleFollow(player, follower))
                            {
                                loop = false;
                                actionDone = true;
                            }
                            break;

                        case Keys.D: // where are ou?
                            if (HandlePlayerOrderFollowerToReportPosition(player, follower))
                            {
                                loop = false;
                                actionDone = true;
                            }
                            break;

                        case Keys.E:
                            if (HandlePlayerOrderFollowerToScavenge(player, follower))
                            {
                                loop = false;
                                actionDone = true;
                            }
                            break;
                    }
                }
                #endregion
            }
            while (loop);

            // return if we did an action.
            return actionDone;
        }

        bool HandlePlayerOrderFollowerToBuildFortification(Actor player, Actor follower, HashSet<Point> followerFOV, bool isLarge)
        {
            bool loop = true;
            bool actionDone = false;
            Map map = player.Location.Map;
            Point? highlightedTile = null;
            Color highlightColor = Color.White;

            do
            {
                ///////////////////
                // 1. Redraw
                // 2. Get input.
                // 3. Handle input
                ///////////////////

                // 1. Redraw
                ClearOverlays();
                AddOverlay(new OverlayPopup(ORDER_MODE_TEXT, MODE_TEXTCOLOR, MODE_BORDERCOLOR, MODE_FILLCOLOR, new Point(0, 0)));
                if (highlightedTile != null)
                    AddOverlay(new OverlayRect(highlightColor, new Rectangle(MapToScreen(highlightedTile.Value.X, highlightedTile.Value.Y), new Size(TILE_SIZE, TILE_SIZE))));
                ClearMessages();
                AddMessage(new Message(String.Format("Ordering {0} to build {1} fortification...", follower.Name, isLarge ? "large" : "small"), m_Session.WorldTime.TurnCounter, Color.Yellow));
                AddMessage(new Message("<LMB> on a map object.", m_Session.WorldTime.TurnCounter, Color.LightGreen));
                RedrawPlayScreen();

                // 2. Get input.
                KeyEventArgs key;
                Point mousePos;
                MouseButtons? mouseButtons;
                WaitKeyOrMouse(out key, out mousePos, out mouseButtons);

                if (key != null)
                {
                    if (key.KeyCode == Keys.Escape)
                        loop = false;
                }
                else
                {
                    // Get map position in view rect.
                    Point mapPos = MouseToMap(mousePos);
                    if (map.IsInBounds(mapPos) && IsInViewRect(mapPos))
                    {
                        // must be in player & follower FoV.
                        if (IsVisibleToPlayer(map, mapPos) && followerFOV.Contains(mapPos))
                        {
                            // Check if can build here.
                            string reason;
                            if (m_Rules.CanActorBuildFortification(follower, mapPos, isLarge, out reason))
                            {
                                // highlight.
                                highlightedTile = mapPos;
                                highlightColor = Color.LightGreen;
                                // if mouse down, give order.
                                if (mouseButtons.HasValue && mouseButtons.Value == MouseButtons.Left)
                                {
                                    DoGiveOrderTo(player, follower, new ActorOrder(isLarge ? ActorTasks.BUILD_LARGE_FORTIFICATION : ActorTasks.BUILD_SMALL_FORTIFICATION, new Location(player.Location.Map, mapPos)));
                                    loop = false;
                                    actionDone = true;
                                }
                            }
                            else
                            {
                                // de-hightlight.
                                highlightedTile = mapPos;
                                highlightColor = Color.Red;

                                // if mouse down, illegal.
                                if (mouseButtons.HasValue && mouseButtons.Value == MouseButtons.Left)
                                {
                                    AddMessage(MakeErrorMessage(String.Format("Can't build {0} fortification : {1}.", isLarge ? "large" : "small", reason)));
                                    AddMessagePressEnter();
                                }
                            }
                        } // visible
                        else
                        {
                            // de-hightlight.
                            highlightedTile = mapPos;
                            highlightColor = Color.Red;
                        }
                    }
                }

            }
            while (loop);

            // return if we did an action.
            return actionDone;
        }

        bool HandlePlayerOrderFollowerToBarricade(Actor player, Actor follower, HashSet<Point> followerFOV, bool toTheMax)
        {
            bool loop = true;
            bool actionDone = false;
            Map map = player.Location.Map;
            Point? highlightedTile = null;
            Color highlightColor = Color.White;

            do
            {
                ///////////////////
                // 1. Redraw
                // 2. Get input.
                // 3. Handle input
                ///////////////////

                // 1. Redraw
                ClearOverlays();
                AddOverlay(new OverlayPopup(ORDER_MODE_TEXT, MODE_TEXTCOLOR, MODE_BORDERCOLOR, MODE_FILLCOLOR, new Point(0, 0)));
                if (highlightedTile != null)
                    AddOverlay(new OverlayRect(highlightColor, new Rectangle(MapToScreen(highlightedTile.Value.X, highlightedTile.Value.Y), new Size(TILE_SIZE, TILE_SIZE))));
                ClearMessages();
                AddMessage(new Message(String.Format("Ordering {0} to barricade...", follower.Name), m_Session.WorldTime.TurnCounter, Color.Yellow));
                AddMessage(new Message("<LMB> on a map object.", m_Session.WorldTime.TurnCounter, Color.LightGreen));
                RedrawPlayScreen();

                // 2. Get input.
                KeyEventArgs key;
                Point mousePos;
                MouseButtons? mouseButtons;
                WaitKeyOrMouse(out key, out mousePos, out mouseButtons);

                if (key != null)
                {
                    if (key.KeyCode == Keys.Escape)
                        loop = false;
                }
                else
                {
                    // Get map position in view rect.
                    Point mapPos = MouseToMap(mousePos);
                    if (map.IsInBounds(mapPos) && IsInViewRect(mapPos))
                    {
                        // must be in player & follower FoV.
                        if (IsVisibleToPlayer(map, mapPos) && followerFOV.Contains(mapPos))
                        {

                            // Check if something to barricade here.
                            DoorWindow door = map.GetMapObjectAt(mapPos) as DoorWindow;
                            if (door != null)
                            {
                                // Check if can barricade here.
                                string reason;
                                if (m_Rules.CanActorBarricadeDoor(follower, door, out reason))
                                {
                                    // highlight.
                                    highlightedTile = mapPos;
                                    highlightColor = Color.LightGreen;
                                    // if mouse down, give order.
                                    if (mouseButtons.HasValue && mouseButtons.Value == MouseButtons.Left)
                                    {
                                        DoGiveOrderTo(player, follower, new ActorOrder(toTheMax ? ActorTasks.BARRICADE_MAX : ActorTasks.BARRICADE_ONE, door.Location));
                                        loop = false;
                                        actionDone = true;
                                    }
                                }
                                else
                                {
                                    // de-hightlight.
                                    highlightedTile = mapPos;
                                    highlightColor = Color.Red;
                                    // if mouse down, illegal.
                                    if (mouseButtons.HasValue && mouseButtons.Value == MouseButtons.Left)
                                    {
                                        AddMessage(MakeErrorMessage(String.Format("Can't barricade {0} : {1}.", door.TheName, reason)));
                                        AddMessagePressEnter();
                                    }
                                }
                            }
                            else
                            {
                                // de-hightlight.
                                highlightedTile = mapPos;
                                highlightColor = Color.Red;
                            }
                        } // visible
                        else
                        {
                            // de-hightlight.
                            highlightedTile = mapPos;
                            highlightColor = Color.Red;
                        }
                    }
                }

            }
            while (loop);

            // return if we did an action.
            return actionDone;
        }

        bool HandlePlayerOrderFollowerToGuard(Actor player, Actor follower, HashSet<Point> followerFOV)
        {
            bool loop = true;
            bool actionDone = false;
            Map map = player.Location.Map;
            Point? highlightedTile = null;
            Color highlightColor = Color.White;

            do
            {
                ///////////////////
                // 1. Redraw
                // 2. Get input.
                // 3. Handle input
                ///////////////////

                // 1. Redraw
                ClearOverlays();
                AddOverlay(new OverlayPopup(ORDER_MODE_TEXT, MODE_TEXTCOLOR, MODE_BORDERCOLOR, MODE_FILLCOLOR, new Point(0, 0)));
                if (highlightedTile != null)
                    AddOverlay(new OverlayRect(highlightColor, new Rectangle(MapToScreen(highlightedTile.Value.X, highlightedTile.Value.Y), new Size(TILE_SIZE, TILE_SIZE))));
                ClearMessages();
                AddMessage(new Message(String.Format("Ordering {0} to guard...", follower.Name), m_Session.WorldTime.TurnCounter, Color.Yellow));
                AddMessage(new Message("<LMB> on a map position.", m_Session.WorldTime.TurnCounter, Color.LightGreen));
                RedrawPlayScreen();

                // 2. Get input.
                KeyEventArgs key;
                Point mousePos;
                MouseButtons? mouseButtons;
                WaitKeyOrMouse(out key, out mousePos, out mouseButtons);

                if (key != null)
                {
                    if (key.KeyCode == Keys.Escape)
                        loop = false;
                }
                else
                {
                    // Get map position in view rect.
                    Point mapPos = MouseToMap(mousePos);
                    if (map.IsInBounds(mapPos) && IsInViewRect(mapPos))
                    {
                        // must be in player & follower FoV.
                        if (IsVisibleToPlayer(map, mapPos) && followerFOV.Contains(mapPos))
                        {
                            // Check if walkable here or same spot.
                            string reason;
                            if (mapPos == follower.Location.Position || m_Rules.IsWalkableFor(follower, map, mapPos.X, mapPos.Y, out reason))
                            {
                                // highlight.
                                highlightedTile = mapPos;
                                highlightColor = Color.LightGreen;
                                // if mouse down, give order.
                                if (mouseButtons.HasValue && mouseButtons.Value == MouseButtons.Left)
                                {
                                    DoGiveOrderTo(player, follower, new ActorOrder(ActorTasks.GUARD, new Location(map, mapPos)));
                                    loop = false;
                                    actionDone = true;
                                }
                            }
                            else
                            {
                                // de-hightlight.
                                highlightedTile = mapPos;
                                highlightColor = Color.Red;
                                // if mouse down, illegal.
                                if (mouseButtons.HasValue && mouseButtons.Value == MouseButtons.Left)
                                {
                                    AddMessage(MakeErrorMessage(String.Format("Can't guard here : {0}", reason)));
                                    AddMessagePressEnter();
                                }
                            }

                        } // visible
                        else
                        {
                            // de-hightlight.
                            highlightedTile = mapPos;
                            highlightColor = Color.Red;
                        }
                    }
                }

            }
            while (loop);

            // return if we did an action.
            return actionDone;
        }

        bool HandlePlayerOrderFollowerToPatrol(Actor player, Actor follower, HashSet<Point> followerFOV)
        {
            bool loop = true;
            bool actionDone = false;
            Map map = player.Location.Map;
            Point? highlightedTile = null;
            Color highlightColor = Color.White;

            do
            {
                ///////////////////
                // 1. Redraw
                // 2. Get input.
                // 3. Handle input
                ///////////////////

                // 1. Redraw
                ClearOverlays();
                AddOverlay(new OverlayPopup(ORDER_MODE_TEXT, MODE_TEXTCOLOR, MODE_BORDERCOLOR, MODE_FILLCOLOR, new Point(0, 0)));
                if (highlightedTile != null)
                {
                    AddOverlay(new OverlayRect(highlightColor, new Rectangle(MapToScreen(highlightedTile.Value.X, highlightedTile.Value.Y), new Size(TILE_SIZE, TILE_SIZE))));
                    List<Zone> zonesHere = map.GetZonesAt(highlightedTile.Value.X, highlightedTile.Value.Y);
                    if (zonesHere != null && zonesHere.Count > 0)
                    {
                        string[] zonesNames = new string[zonesHere.Count + 1];
                        zonesNames[0] = "Zone(s) here :";
                        for (int i = 0; i < zonesHere.Count; i++)
                        {
                            zonesNames[i + 1] = String.Format("- {0}", zonesHere[i].Name);
                        }
                        AddOverlay(new OverlayPopup(zonesNames, Color.White, Color.White, POPUP_FILLCOLOR, MapToScreen(highlightedTile.Value.X + 1, highlightedTile.Value.Y + 1)));
                    }
                }
                ClearMessages();
                AddMessage(new Message(String.Format("Ordering {0} to patrol...", follower.Name), m_Session.WorldTime.TurnCounter, Color.Yellow));
                AddMessage(new Message("<LMB> on a map position.", m_Session.WorldTime.TurnCounter, Color.LightGreen));
                RedrawPlayScreen();

                // 2. Get input.
                KeyEventArgs key;
                Point mousePos;
                MouseButtons? mouseButtons;
                WaitKeyOrMouse(out key, out mousePos, out mouseButtons);

                if (key != null)
                {
                    if (key.KeyCode == Keys.Escape)
                        loop = false;
                }
                else
                {
                    // Get map position in view rect.
                    Point mapPos = MouseToMap(mousePos);
                    if (map.IsInBounds(mapPos) && IsInViewRect(mapPos))
                    {
                        // must be in player & follower FoV.
                        if (IsVisibleToPlayer(map, mapPos) && followerFOV.Contains(mapPos))
                        {
                            bool validPatrol = true;
                            string reason = "";

                            // Must have a zone.
                            if (map.GetZonesAt(mapPos.X, mapPos.Y) == null)
                            {
                                validPatrol = false;
                                reason = "no zone here";
                            }
                            // Check if walkable here or same spot.
                            else if (!(mapPos == follower.Location.Position || m_Rules.IsWalkableFor(follower, map, mapPos.X, mapPos.Y, out reason)))
                            {
                                validPatrol = false;
                            }

                            if (validPatrol)
                            {
                                // highlight.
                                highlightedTile = mapPos;
                                highlightColor = Color.LightGreen;
                                // if mouse down, give order.
                                if (mouseButtons.HasValue && mouseButtons.Value == MouseButtons.Left)
                                {
                                    DoGiveOrderTo(player, follower, new ActorOrder(ActorTasks.PATROL, new Location(map, mapPos)));
                                    loop = false;
                                    actionDone = true;
                                }
                            }
                            else
                            {
                                // de-hightlight.
                                highlightedTile = mapPos;
                                highlightColor = Color.Red;
                                // if mouse down, illegal.
                                if (mouseButtons.HasValue && mouseButtons.Value == MouseButtons.Left)
                                {
                                    AddMessage(MakeErrorMessage(String.Format("Can't patrol here : {0}", reason)));
                                    AddMessagePressEnter();
                                }
                            }
                        } // visible
                        else
                        {
                            // de-hightlight.
                            highlightedTile = mapPos;
                            highlightColor = Color.Red;
                        }
                    }
                }
            }
            while (loop);

            // return if we did an action.
            return actionDone;
        }

        bool HandlePlayerOrderFollowerToDropAllItems(Actor player, Actor follower)
        {
            // if no items, nothing to drop.
            if (follower.Inventory.IsEmpty)
                return false;

            // do give order.
            DoGiveOrderTo(player, follower, new ActorOrder(ActorTasks.DROP_ALL_ITEMS, follower.Location));

            // emote.
            DoSay(follower, player, "Well ok...", Sayflags.IS_FREE_ACTION);

            // update trust. 1 give item penalty per items to drop.
            ModifyActorTrustInLeader(follower, follower.Inventory.CountItems * Rules.TRUST_GIVE_ITEM_ORDER_PENALTY, true);

            // done.
            return true;
        }

        bool HandlePlayerOrderFollowerToReport(Actor player, Actor follower)
        {
            // just give the order.
            DoGiveOrderTo(player, follower, new ActorOrder(ActorTasks.REPORT_EVENTS, follower.Location));

            // done.
            return true;
        }

        bool HandlePlayerOrderFollowerToSleep(Actor player, Actor follower)
        {
            // just give the order.
            DoGiveOrderTo(player, follower, new ActorOrder(ActorTasks.SLEEP_NOW, follower.Location));

            // done.
            return true;
        }

        bool HandlePlayerOrderFollowerToToggleFollow(Actor player, Actor follower)
        {
            // just give the order.
            DoGiveOrderTo(player, follower, new ActorOrder(ActorTasks.FOLLOW_TOGGLE, follower.Location));

            // done.
            return true;
        }

        bool HandlePlayerOrderFollowerToReportPosition(Actor player, Actor follower)
        {
            // just give the order.
            DoGiveOrderTo(player, follower, new ActorOrder(ActorTasks.WHERE_ARE_YOU, follower.Location));

            // done.
            return true;
        }

        bool HandlePlayerOrderFollowerToScavenge(Actor player, Actor follower)
        {
            if (!m_Session.GamePreset.Bases) return false;
            XpdBase baseClaim = player.Location.Map.XpdBaseAt(player.Location.Position);
            if (baseClaim == null || !baseClaim.Owns(player) || !baseClaim.Owns(follower))
            {
                AddMessage(MakeErrorMessage("Stand in your XPD base to order scavenging."));
                return false;
            }
            DoGiveOrderTo(player, follower, new ActorOrder(ActorTasks.SCAVENGE_SUPPLIES, player.Location));
            return true;
        }

        bool HandlePlayerOrderFollowerToGiveItems(Actor player, Actor follower)
        {
            // sanity checks.
            if (follower.Inventory == null || follower.Inventory.IsEmpty)
            {
                ClearMessages();
                AddMessage(MakeErrorMessage(String.Format("{0} has no items to give.", follower.TheName)));
                AddMessagePressEnter();
                return false;
            }
            // must be adjacent.
            if (player.Location.Map != follower.Location.Map || !m_Rules.IsAdjacent(player.Location.Position, follower.Location.Position))
            {
                ClearMessages();
                AddMessage(MakeErrorMessage(String.Format("{0} is not next to you.", follower.TheName)));
                AddMessagePressEnter();
                return false;
            }

            // loop.
            bool loop = true;
            bool actionDone = false;

            int iFirstItem = 0;
            const int maxItOnPage = MAX_MESSAGES - 2;
            Inventory foInventory = follower.Inventory;
            do
            {
                ///////////////////
                // 1. Redraw
                // 2. Get input.
                // 3. Handle input
                ///////////////////

                // 1. Redraw
                ClearOverlays();
                AddOverlay(new OverlayPopup(ORDER_MODE_TEXT, MODE_TEXTCOLOR, MODE_BORDERCOLOR, MODE_FILLCOLOR, new Point(0, 0)));
                ClearMessages();
                AddMessage(new Message(String.Format("Ordering {0} to give...", follower.Name), m_Session.WorldTime.TurnCounter, Color.Yellow));

                int itShown;
                for (itShown = 0; itShown < maxItOnPage && (iFirstItem + itShown < foInventory.CountItems); itShown++)
                {
                    int iIt = iFirstItem + itShown;
                    AddMessage(new Message(String.Format("{0}. {1}/{2} {3}.", (1 + itShown), iIt + 1, foInventory.CountItems, DescribeItemShort(foInventory[iIt])), m_Session.WorldTime.TurnCounter, Color.LightGreen));
                }
                if (itShown < foInventory.CountItems)
                {
                    AddMessage(new Message("9. next", m_Session.WorldTime.TurnCounter, Color.LightGreen));
                }
                RedrawPlayScreen();

                // 2. Get input.
                KeyEventArgs key = m_UI.UI_WaitKey();
                int choice = KeyToChoiceNumber(key.KeyCode);

                // 3. Handle input
                if (key.KeyCode == Keys.Escape)
                {
                    loop = false;
                }
                else if (choice == 9)
                {
                    iFirstItem += maxItOnPage;
                    if (iFirstItem >= foInventory.CountItems)
                        iFirstItem = 0;
                }
                else if (choice >= 1 && choice <= itShown)
                {
                    // get item.
                    int i = iFirstItem + choice - 1;
                    Item it = foInventory[i];

                    // try to do it.
                    string reason;
                    if (m_Rules.CanActorGiveItemTo(follower, player, it, out reason))
                    {
                        DoGiveItemTo(follower, m_Player, it);
                        loop = false;
                        actionDone = true;
                    }
                    else
                    {
                        ClearMessages();
                        AddMessage(MakeErrorMessage(String.Format("{0} cannot give {1} : {2}.", follower.TheName, DescribeItemShort(it), reason)));
                        AddMessagePressEnter();
                    }
                }
            }
            while (loop);

            // done.
            return actionDone;
        }
        #endregion
    }
}
