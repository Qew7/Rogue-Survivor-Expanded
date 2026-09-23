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
        bool HandlePlayerPush(Actor player)
        {
            // fail immediatly for stupid cases.
            if (!m_Rules.HasActorPushAbility(player))
            {
                AddMessage(MakeErrorMessage("Cannot push objects."));
                return false;
            }
            if (m_Rules.IsActorTired(player))
            {
                AddMessage(MakeErrorMessage("Too tired to push."));
                return false;
            }


            bool loop = true;
            bool actionDone = false;

            ClearOverlays();
            AddOverlay(new OverlayPopup(PUSH_MODE_TEXT, MODE_TEXTCOLOR, MODE_BORDERCOLOR, MODE_FILLCOLOR, new Point(0, 0)));

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
                        // shove actor vs push object.
                        Actor other = player.Location.Map.GetActorAt(pos);
                        MapObject mapObj = player.Location.Map.GetMapObjectAt(pos);
                        string reason;
                        if (other != null)
                        {
                            // shove.
                            if (m_Rules.CanActorShove(player, other, out reason))
                            {
                                if (HandlePlayerShoveActor(player, other))
                                {
                                    loop = false;
                                    actionDone = true;
                                }
                            }
                            else
                                AddMessage(MakeErrorMessage(String.Format("Cannot shove {0} : {1}.", other.TheName, reason)));

                        }
                        else if (mapObj != null)
                        {
                            // push.
                            if (m_Rules.CanActorPush(player, mapObj, out reason))
                            {
                                if (HandlePlayerPushObject(player, mapObj))
                                {
                                    loop = false;
                                    actionDone = true;
                                }
                            }
                            else
                            {
                                AddMessage(MakeErrorMessage(String.Format("Cannot move {0} : {1}.", mapObj.TheName, reason)));
                            }
                        }
                        else
                        {
                            // nothing to push/shove.
                            AddMessage(MakeErrorMessage("Nothing to push there."));
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

        bool HandlePlayerPushObject(Actor player, MapObject mapObj)
        {
            bool loop = true;
            bool actionDone = false;

            ClearOverlays();
            AddOverlay(new OverlayPopup(new string[] { String.Format(PUSH_OBJECT_MODE_TEXT, mapObj.TheName) }, MODE_TEXTCOLOR, MODE_BORDERCOLOR, MODE_FILLCOLOR, new Point(0, 0)));
            AddOverlay(new OverlayRect(Color.Yellow, new Rectangle(MapToScreen(mapObj.Location.Position), new Size(TILE_SIZE, TILE_SIZE))));

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
                    Point movePos = mapObj.Location.Position + dir;
                    if (player.Location.Map.IsInBounds(movePos))
                    {
                        string reason;
                        if (m_Rules.CanPushObjectTo(mapObj, movePos, out reason))
                        {
                            DoPush(player, mapObj, movePos);
                            loop = false;
                            actionDone = true;
                        }
                        else
                        {
                            AddMessage(MakeErrorMessage(String.Format("Cannot move {0} there : {1}.", mapObj.TheName, reason)));
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

        bool HandlePlayerShoveActor(Actor player, Actor other)
        {
            bool loop = true;
            bool actionDone = false;

            ClearOverlays();
            AddOverlay(new OverlayPopup(new string[] { String.Format(SHOVE_ACTOR_MODE_TEXT, other.TheName) }, MODE_TEXTCOLOR, MODE_BORDERCOLOR, MODE_FILLCOLOR, new Point(0, 0)));
            AddOverlay(new OverlayRect(Color.Yellow, new Rectangle(MapToScreen(other.Location.Position), new Size(TILE_SIZE, TILE_SIZE))));

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
                    Point movePos = other.Location.Position + dir;
                    if (player.Location.Map.IsInBounds(movePos))
                    {
                        string reason;
                        if (m_Rules.CanShoveActorTo(other, movePos, out reason))
                        {
                            DoShove(player, other, movePos);
                            loop = false;
                            actionDone = true;
                        }
                        else
                        {
                            AddMessage(MakeErrorMessage(String.Format("Cannot shove {0} there : {1}.", other.TheName, reason)));
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

        // alpha10
        bool HandlePlayerPull(Actor player)
        {
            // fail immediatly for stupid cases.
            if (!m_Rules.HasActorPushAbility(player))
            {
                AddMessage(MakeErrorMessage("Cannot pull objects."));
                return false;
            }
            if (m_Rules.IsActorTired(player))
            {
                AddMessage(MakeErrorMessage("Too tired to pull."));
                return false;
            }
            MapObject otherMobj = player.Location.Map.GetMapObjectAt(player.Location.Position);
            if (otherMobj != null)
            {
                AddMessage(MakeErrorMessage(string.Format("Cannot pull : {0} is blocking.", otherMobj.TheName)));
                return false;
            }


            bool loop = true;
            bool actionDone = false;

            ClearOverlays();
            AddOverlay(new OverlayPopup(PULL_MODE_TEXT, MODE_TEXTCOLOR, MODE_BORDERCOLOR, MODE_FILLCOLOR, new Point(0, 0)));

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
                        Actor other = player.Location.Map.GetActorAt(pos);
                        string reason;
                        if (other != null)
                        {
                            // pull-shove.
                            if (m_Rules.CanActorShove(player, other, out reason))  // if can shove, can pull-shove.
                            {
                                if (HandlePlayerPullActor(player, other))
                                {
                                    loop = false;
                                    actionDone = true;
                                }
                            }
                            else
                                AddMessage(MakeErrorMessage(String.Format("Cannot pull {0} : {1}.", other.TheName, reason)));
                        }
                        else if (mapObj != null)
                        {
                            // pull.
                            if (m_Rules.CanActorPush(player, mapObj, out reason))  // if can push, can pull.
                            {
                                if (HandlePlayerPullObject(player, mapObj))
                                {
                                    loop = false;
                                    actionDone = true;
                                }
                            }
                            else
                            {
                                AddMessage(MakeErrorMessage(String.Format("Cannot move {0} : {1}.", mapObj.TheName, reason)));
                            }
                        }
                        else
                        {
                            // nothing to pull.
                            AddMessage(MakeErrorMessage("Nothing to pull there."));
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

        // alpha10
        bool HandlePlayerPullObject(Actor player, MapObject mapObj)
        {
            bool loop = true;
            bool actionDone = false;

            ClearOverlays();
            AddOverlay(new OverlayPopup(new string[] { String.Format(PULL_OBJECT_MODE_TEXT, mapObj.TheName) }, MODE_TEXTCOLOR, MODE_BORDERCOLOR, MODE_FILLCOLOR, new Point(0, 0)));
            AddOverlay(new OverlayRect(Color.Yellow, new Rectangle(MapToScreen(mapObj.Location.Position), new Size(TILE_SIZE, TILE_SIZE))));

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
                    Point moveToPos = player.Location.Position + dir;
                    if (player.Location.Map.IsInBounds(moveToPos))
                    {
                        string reason;
                        if (m_Rules.CanPullObject(player, mapObj, moveToPos, out reason))
                        {
                            DoPull(player, mapObj, moveToPos);
                            loop = false;
                            actionDone = true;
                        }
                        else
                        {
                            AddMessage(MakeErrorMessage(String.Format("Cannot pull there : {0}.", reason)));
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

        // alpha10
        bool HandlePlayerPullActor(Actor player, Actor other)
        {
            bool loop = true;
            bool actionDone = false;

            ClearOverlays();
            AddOverlay(new OverlayPopup(new string[] { String.Format(PULL_ACTOR_MODE_TEXT, other.TheName) }, MODE_TEXTCOLOR, MODE_BORDERCOLOR, MODE_FILLCOLOR, new Point(0, 0)));
            AddOverlay(new OverlayRect(Color.Yellow, new Rectangle(MapToScreen(other.Location.Position), new Size(TILE_SIZE, TILE_SIZE))));

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
                    Point moveToPos = player.Location.Position + dir;
                    if (player.Location.Map.IsInBounds(moveToPos))
                    {
                        string reason;
                        if (m_Rules.CanPullActor(player, other, moveToPos, out reason))
                        {
                            DoPullActor(player, other, moveToPos);
                            loop = false;
                            actionDone = true;
                        }
                        else
                        {
                            AddMessage(MakeErrorMessage(String.Format("Cannot pull there : {0}.", reason)));
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

        bool HandlePlayerUseSpray(Actor player)
        {
            // get equipped item.
            Item it = player.GetEquippedItem(DollPart.LEFT_HAND);
            if (it == null)
            {
                AddMessage(MakeErrorMessage("No spray equipped."));
                RedrawPlayScreen();
                return false;
            }

            //////////////////////////////////////////////
            // Handle concrete action depending on spray.
            // 1. Spray paint.
            // 2. Spray scent.
            //////////////////////////////////////////////

            // 1. Spray paint.
            ItemSprayPaint sprayPaint = it as ItemSprayPaint;
            if (sprayPaint != null)
                return HandlePlayerTag(player);

            // 2. Spray scent.
            ItemSprayScent sprayScent = it as ItemSprayScent;
            if (sprayScent != null)
            {
                // alpha10 new way to use stench killer
                return HandlePlayerSprayOdorSuppressor(player);
            }

            // no spray equipped.
            AddMessage(MakeErrorMessage("No spray equipped."));
            RedrawPlayScreen();
            return false;
        }

        bool HandlePlayerTag(Actor player)
        {
            bool loop = true;
            bool actionDone = false;

            // Check if has spray paint.
            ItemSprayPaint sprayPaint = player.GetEquippedItem(DollPart.LEFT_HAND) as ItemSprayPaint;
            if (sprayPaint == null)
            {
                AddMessage(MakeErrorMessage("No spray paint equipped."));
                RedrawPlayScreen();
                return false;
            }
            if (sprayPaint.PaintQuantity <= 0)
            {
                AddMessage(MakeErrorMessage("No paint left."));
                RedrawPlayScreen();
                return false;
            }


            ClearOverlays();
            AddOverlay(new OverlayPopup(TAG_MODE_TEXT, MODE_TEXTCOLOR, MODE_BORDERCOLOR, MODE_FILLCOLOR, new Point(0, 0)));
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
                        if (CanTag(player.Location.Map, pos, out reason))
                        {
                            DoTag(player, sprayPaint, pos);
                            loop = false;
                            actionDone = true;
                        }
                        else
                        {
                            AddMessage(MakeErrorMessage(String.Format("Can't tag there : {0}.", reason)));
                            RedrawPlayScreen();
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

        bool CanTag(Map map, Point pos, out string reason)
        {
            ///////////////////////
            // Can't tag if:
            // 1. Out of bounds.
            // 2. An actor there.
            // 3. An object there.
            ///////////////////////

            // 1. Out of bounds.
            if (!map.IsInBounds(pos))
            {
                reason = "out of map";
                return false;
            }

            // 2. An actor there.
            Actor other = map.GetActorAt(pos);
            if (other != null)
            {
                reason = "someone there";
                return false;
            }

            // 3. An object there.
            MapObject mapObj = map.GetMapObjectAt(pos);
            if (mapObj != null)
            {
                reason = "something there";
                return false;
            }

            reason = "";
            return true;
        }

        // alpha10 new way to use stench killer
        bool HandlePlayerSprayOdorSuppressor(Actor player)
        {
            bool loop = true;
            bool actionDone = false;

            // Check if has odor suppressor.
            ItemSprayScent spray = player.GetEquippedItem(DollPart.LEFT_HAND) as ItemSprayScent;
            if (spray == null)
            {
                AddMessage(MakeErrorMessage("No spray equipped."));
                RedrawPlayScreen();
                return false;
            }
            if (spray.SprayQuantity <= 0)
            {
                AddMessage(MakeErrorMessage("No spray left."));
                RedrawPlayScreen();
                return false;
            }

            ClearOverlays();
            AddOverlay(new OverlayPopup(SPRAY_MODE_TEXT, MODE_TEXTCOLOR, MODE_BORDERCOLOR, MODE_FILLCOLOR, new Point(0, 0)));
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
                    Actor sprayOn = null;

                    if (dir == Direction.NEUTRAL)
                    {
                        sprayOn = player;
                    }
                    else
                    {
                        Point pos = player.Location.Position + dir;
                        if (player.Location.Map.IsInBounds(pos))
                            sprayOn = player.Location.Map.GetActorAt(pos);
                    }

                    if (sprayOn == null)
                    {
                        AddMessage(MakeErrorMessage("No one to spray on here."));
                        RedrawPlayScreen();
                    }
                    else
                    {
                        string reason;
                        if (m_Rules.CanActorSprayOdorSuppressor(player, spray, sprayOn, out reason))
                        {
                            DoSprayOdorSuppressor(player, spray, sprayOn);
                            loop = false;
                            actionDone = true;
                        }
                        else
                        {
                            AddMessage(MakeErrorMessage(String.Format("Can't spray here : {0}.", reason)));
                            RedrawPlayScreen();
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

        void StartPlayerWaitLong(Actor player)
        {
            // alpha10.1 check autosave before player starts long wait
            CheckAutoSaveTime();

            // start waiting.
            m_IsPlayerLongWait = true;
            m_IsPlayerLongWaitForcedStop = false;
            m_PlayerLongWaitEnd = new WorldTime(m_Session.WorldTime.TurnCounter + WorldTime.TURNS_PER_HOUR);

            // message.
            AddMessage(MakeMessage(player, String.Format("{0} waiting.", Conjugate(player, VERB_START))));
            RedrawPlayScreen();
        }

        bool CheckPlayerWaitLong(Actor player)
        {
            ///////////////////////
            // Stop waiting if:
            // 1. Force stop wait flag set.
            // 2. Time reached.
            // 3. Hungry or sleepy.
            // 4. Enemy.
            // 5. Sanity check
            ///////////////////////

            // 1. Force stop wait flag set.
            if (m_IsPlayerLongWaitForcedStop)
                return false;

            // 2. Time reached.
            if (m_Session.WorldTime.TurnCounter >= m_PlayerLongWaitEnd.TurnCounter)
                return false;

            // 3. Hungry or sleepy.
            if (m_Rules.IsActorHungry(player) || m_Rules.IsActorStarving(player) || m_Rules.IsActorSleepy(player) || m_Rules.IsActorExhausted(player))
                return false;

            // 4. Enemy.
            foreach (Point p in m_PlayerFOV)
            {
                Actor other = player.Location.Map.GetActorAt(p);
                if (other != null && m_Rules.AreEnemies(player, other))
                    return false;
            }

            // 5. Sanity check
            if (TryPlayerInsanity())
                return false;

            // all clear, waiting not interrupted.
            return true;
        }

    }
}
