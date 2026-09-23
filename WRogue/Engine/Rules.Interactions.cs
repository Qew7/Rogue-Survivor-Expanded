using System;
using System.Collections.Generic;
using System.Text;
using System.Drawing;

using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Gameplay;
using djack.RogueSurvivor.Gameplay.AI;
using djack.RogueSurvivor.Engine.Actions;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Engine.MapObjects;

namespace djack.RogueSurvivor.Engine
{
    partial class Rules
    {
        #region Switching Map Objects
        public bool IsSwitchableFor(Actor actor, PowerGenerator powGen, out string reason)
        {
            if (actor == null)
                throw new ArgumentNullException("actor");
            if (powGen == null)
                throw new ArgumentNullException("powGen");


            ////////////////////////
            // Switchable only if:
            // 1. Actor has ability.
            // 2. Is not sleeping.
            ////////////////////////

            // 1. Actor has ability.
            if (!actor.Model.Abilities.CanUseMapObjects)
            {
                reason = "cannot use map objects";
                return false;
            }

            // 2. Is not sleeping.
            if (actor.IsSleeping)
            {
                reason = "is sleeping";
                return false;
            }


            // all clear.
            reason = "";
            return true;
        }
        #endregion
        #region Leaving maps
        public bool CanActorLeaveMap(Actor actor, out string reason)
        {
            if (actor == null)
                throw new ArgumentNullException("actor");

            /////////////////////////////////
            // Only if :
            // 1. Player and not bot // alpha10.1
            /////////////////////////////////
            if(!actor.IsPlayer || actor.IsBotPlayer)
            {
                reason = "can't leave maps";
                return false;
            }

            reason = "";
            return true;
        }
        #endregion
        #region Using exits
        public bool CanActorUseExit(Actor actor, Point exitPoint)
        {
            string reason;
            return CanActorUseExit(actor, exitPoint, out reason);
        }

        public bool CanActorUseExit(Actor actor, Point exitPoint, out string reason)
        {
            if (actor == null)
                throw new ArgumentNullException("actor");

            /////////////////////////////
            // Can't if any is true:
            // 1. No exit there.
            // 2. AI: can't use AI exits.
            // 3. Is sleeping.
            /////////////////////////////

            // 1. No exit there.
            if (actor.Location.Map.GetExitAt(exitPoint) == null)
            {
                reason = "no exit there";
                return false;
            }

            // 2. AI: can't use AI exits.
            // alpha10.1 handle bots
            if ((!actor.IsPlayer || actor.IsBotPlayer) && !actor.Model.Abilities.AI_CanUseAIExits)
            {
                reason = "this AI can't use exits";
                return false;
            }

            // 3. Is sleeping.
            if (actor.IsSleeping)
            {
                reason = "is sleeping";
                return false;
            }

            // all clear.
            reason = "";
            return true;
        }

        #endregion
        #region Chatting & Trading
        public bool CanActorChatWith(Actor speaker, Actor target, out string reason)
        {
            if (speaker == null)
                throw new ArgumentNullException("speaker");
            if (target == null)
                throw new ArgumentNullException("target");

            //////////////////////////////
            // Can't if any is true:
            // 1. One of them can't talk.
            // 2. One of them is sleeping.
            //////////////////////////////

            // 1. One of them can't talk.
            if (!speaker.Model.Abilities.CanTalk)
            {
                reason = "can't talk";
                return false;
            }
            if (!target.Model.Abilities.CanTalk)
            {
                reason = String.Format("{0} can't talk", target.TheName);
                return false;
            }

            // 2. One of them is sleeping.
            if (speaker.IsSleeping)
            {
                reason = "sleeping";
                return false;
            }
            if (target.IsSleeping)
            {
                reason = String.Format("{0} is sleeping", target.TheName);
                return false;
            }

            // all clear.
            reason = "";
            return true;
        }

        public bool CanActorInitiateTradeWith(Actor speaker, Actor target)
        {
            string reason;
            return CanActorInitiateTradeWith(speaker, target, out reason);
        }

        public bool CanActorInitiateTradeWith(Actor speaker, Actor target, out string reason)
        {
            if (speaker == null)
                throw new ArgumentNullException("speaker");
            if (target == null)
                throw new ArgumentNullException("target");

            /////////////////////////////
            // Can't if any is true:
            // 1. Target is player.
            // 2. Actors are not traders and not leader->follower relationship.
            // 3. Target is sleeping.
            // 4. No item to trade.
            /////////////////////////////

            // 1. Target is player.
            if (target.IsPlayer)
            {
                reason = "target is player";
                return false;
            }

            // 2. Actors are not traders and not leader->follower relationship.
            if (!speaker.Model.Abilities.CanTrade && target.Leader != speaker)
            {
                reason = "can't trade";
                return false;
            }
            if (!target.Model.Abilities.CanTrade && target.Leader != speaker)
            {
                reason = "target can't trade";
                return false;
            }

            if (AreEnemies(speaker, target))
            {
                reason = "is an enemy";
                return false;
            }

            // 3. Target is sleeping.
            if (target.IsSleeping)
            {
                reason = "is sleeping";
                return false;
            }

            // 4. No item to trade.
            if (speaker.Inventory == null || speaker.Inventory.IsEmpty)
            {
                reason = "nothing to offer";
                return false;
            }
            if (target.Inventory == null || target.Inventory.IsEmpty)
            {
                reason = "has nothing to trade";
                return false;
            }

            // all clear.
            reason = "";
            return true;
        }

        public bool CanActorShout(Actor speaker)
        {
            string reason;
            return CanActorShout(speaker, out reason);
        }

        public bool CanActorShout(Actor speaker, out string reason)
        {
            if (speaker == null)
                throw new ArgumentNullException("speaker");

            //////////////////////////
            // Can't shout if:
            // 1. Actor is sleeping.
            // 2. Actor can't talk.
            //////////////////////////

            // 1. Actor is sleeping.
            if (speaker.IsSleeping)
            {
                reason = "sleeping";
                return false;
            }

            // 2. Actor can't talk
            if (!speaker.Model.Abilities.CanTalk)
            {
                reason = "can't talk";
                return false;
            }

            // all clear.
            reason = "";
            return true;
        }
        #endregion
        #region Doors
        public bool IsOpenableFor(Actor actor, DoorWindow door)
        {
            string reason;
            return IsOpenableFor(actor, door, out reason);
        }

        public bool IsOpenableFor(Actor actor, DoorWindow door, out string reason)
        {
            if (actor == null)
                throw new ArgumentNullException("actor");
            if (door == null)
                throw new ArgumentNullException("door");

            ////////////////////////////////////////
            // Not openable if any is true:
            // 1. Actor cannot use map objects.
            // 2. Door is not closed nor barricaded.
            ////////////////////////////////////////

            // 1. Actor cannot use map objects.
            if (!actor.Model.Abilities.CanUseMapObjects)
            {
                reason = "no ability to open";
                return false;
            }

            // 2. Door is not closed nor barricaded.
            if (!door.IsClosed || door.BarricadePoints > 0)
            {
                reason = "not closed nor barricaded";
                return false;
            }

            // all clear
            reason = "";
            return true;
        }

        public bool IsClosableFor(Actor actor, DoorWindow door)
        {
            string reason;
            return IsClosableFor(actor, door, out reason);
        }

        public bool IsClosableFor(Actor actor, DoorWindow door, out string reason)
        {
            if (actor == null)
                throw new ArgumentNullException("actor");
            if (door == null)
                throw new ArgumentNullException("door");

            ///////////////////////////////////////
            // Not close if any is true:
            // 1. Actor cannot use map objects.
            // 2. Door is not open.
            // 3. Another actor here.
            ///////////////////////////////////////

            // 1. Actor cannot use map objects.
            if (!actor.Model.Abilities.CanUseMapObjects)
            {
                reason = "can't use objects";
                return false;
            }

            // 2. Door is not open.
            if (!door.IsOpen)
            {
                reason = "not open";
                return false;
            }

            // 3. Another actor here.
            if (door.Location.Map.GetActorAt(door.Location.Position) != null)
            {
                reason = "someone is there";
                return false;
            }

            // all clear
            reason = "";
            return true;
        }

        public bool CanActorBarricadeDoor(Actor actor, DoorWindow door)
        {
            string reason;
            return CanActorBarricadeDoor(actor, door, out reason);
        }

        public bool CanActorBarricadeDoor(Actor actor, DoorWindow door, out string reason)
        {
            if (actor == null)
                throw new ArgumentNullException("actor");
            if (door == null)
                throw new ArgumentNullException("door");

            ////////////////////////////////////
            // Not close if any is true:
            // 1. Actor cannot barricade doors.
            // 2. Door is not closed or broken.
            // 3. Barricading limit reached.
            // 4. An actor is there.
            // 5. No barricading material.
            ////////////////////////////////////

            // 1. Actor cannot barricade doors.
            if (!actor.Model.Abilities.CanBarricade)
            {
                reason = "no ability to barricade";
                return false;
            }

            // 2. Door is not closed or broken.
            if (door.State != DoorWindow.STATE_CLOSED && door.State != DoorWindow.STATE_BROKEN)
            {
                reason = "not closed or broken"; // alpha10 typo fix
                return false;
            }

            // 3. Barricading limit reached.
            if (door.BarricadePoints >= BARRICADING_MAX)
            {
                reason = "barricade limit reached";
                return false;
            }

            // 4. An actor is there.
            if (door.Location.Map.GetActorAt(door.Location.Position) != null)
            {
                reason = "someone is there";
                return false;
            }

            // 5. No barricading material.
            if (actor.Inventory == null || actor.Inventory.IsEmpty)
            {
                reason = "no items";
                return false;
            }
            if (!actor.Inventory.HasItemOfType(typeof(ItemBarricadeMaterial)))
            {
                reason = "no barricading material";
                return false;
            }

            // all clear.
            reason = "";
            return true;
        }
        #endregion
        #region Bashing doors & Breaking stuff
        public bool IsBashableFor(Actor actor, DoorWindow door)
        {
            string reason;
            return IsBashableFor(actor, door, out reason);
        }

        public bool IsBashableFor(Actor actor, DoorWindow door, out string reason)
        {
            if (actor == null)
                throw new ArgumentNullException("actor");
            if (door == null)
                throw new ArgumentNullException("door");

            ///////////////////////////////
            // Not bashable:
            // 1. Actor cannot bash doors.
            // 2. Actor is tired.
            // 3. Door is not breakable.
            ///////////////////////////////

            // 1. Actor cannot bash doors.
            if (!actor.Model.Abilities.CanBashDoors)
            {
                reason = "can't bash doors";
                return false;
            }

            // 2. Actor is tired.
            if (IsActorTired(actor))
            {
                reason = "tired";
                return false;
            }

            // 2. Door is not breakable.
            if (door.BreakState != MapObject.Break.BREAKABLE && !door.IsBarricaded)
            {
                reason = "can't break this object";
                return false;
            }

            // all clear
            reason = "";
            return true;
        }

        public bool IsBreakableFor(Actor actor, MapObject mapObj)
        {
            string reason;
            return IsBreakableFor(actor, mapObj, out reason);
        }

        public bool IsBreakableFor(Actor actor, MapObject mapObj, out string reason)
        {
            if (actor == null)
                throw new ArgumentNullException("actor");
            if (mapObj == null)
                throw new ArgumentNullException("mapObj");

            //////////////////////////////////
            // Not breakable:
            // 1. Actor cannot break.
            // 2. Actor is tired.
            // 3. Map object is not breakable.
            // 4. Another actor there.
            //////////////////////////////////

            // 1. Actor cannot break.
            if (!actor.Model.Abilities.CanBreakObjects)
            {
                reason = "cannot break objects";
                return false;
            }

            // 2. Actor is tired.
            if (IsActorTired(actor))
            {
                reason = "tired";
                return false;
            }

            // 3. Map object is not breakable.
            DoorWindow door = mapObj as DoorWindow;
            bool isBarricadedDoor = (door != null && door.IsBarricaded);
            if (mapObj.BreakState != MapObject.Break.BREAKABLE && !isBarricadedDoor)
            {
                reason = "can't break this object";
                return false;
            }

            // 4. Another actor there.
            if (mapObj.Location.Map.GetActorAt(mapObj.Location.Position) != null)
            {
                reason = "someone is there";
                return false;
            }

            // all clear.
            reason = "";
            return true;
        }
        #endregion
    }
}
