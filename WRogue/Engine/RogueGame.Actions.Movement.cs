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
        #region Movement
        public void DoMoveActor(Actor actor, Location newLocation)
        {
            Location oldLocation = actor.Location;

            // Try to leave tile.
            if (!TryActorLeaveTile(actor))
            {
                // waste ap.
                SpendActorActionPoints(actor, Rules.BASE_ACTION_COST);
                return;
            }

            // Do the move.
            if (oldLocation.Map == newLocation.Map)
                newLocation.Map.PlaceActorAt(actor, newLocation.Position);
            else
                throw new NotImplementedException("DoMoveActor : illegal to change map.");

            // If dragging corpse, move it along.
            Corpse draggedCorpse = actor.DraggedCorpse;
            if (draggedCorpse != null)
            {
                oldLocation.Map.MoveCorpseTo(draggedCorpse, newLocation.Position);
                if (IsVisibleToPlayer(newLocation) || IsVisibleToPlayer(oldLocation))
                    AddMessage(MakeMessage(actor, String.Format("{0} {1} corpse.", Conjugate(actor, VERB_DRAG), draggedCorpse.DeadGuy.TheName)));
            }

            // Spend AP & STA, check for running, jumping and dragging corpse.
            #region
            int moveCost = Rules.BASE_ACTION_COST;

            // running?
            if (actor.IsRunning)
            {
                // x2 faster.
                moveCost /= 2;
                // cost STA.
                SpendActorStaminaPoints(actor, Rules.STAMINA_COST_RUNNING);
            }

            bool isJump = false;
            MapObject mapObj = newLocation.Map.GetMapObjectAt(newLocation.Position.X, newLocation.Position.Y);
            if (mapObj != null && !mapObj.IsWalkable && mapObj.IsJumpable)
                isJump = true;

            // jumping?
            if (isJump)
            {
                // cost STA.
                SpendActorStaminaPoints(actor, Rules.STAMINA_COST_JUMP);

                // show.
                if (IsVisibleToPlayer(actor))
                    AddMessage(MakeMessage(actor, Conjugate(actor, VERB_JUMP_ON), mapObj));

                // if CanJumpStumble ability, has a chance to stumble.
                if (actor.Model.Abilities.CanJumpStumble && m_Rules.RollChance(Rules.JUMP_STUMBLE_CHANCE))
                {
                    // stumble!
                    moveCost += Rules.JUMP_STUMBLE_ACTION_COST;

                    // show.
                    if (IsVisibleToPlayer(actor))
                        AddMessage(MakeMessage(actor, String.Format("{0}!", Conjugate(actor, VERB_STUMBLE))));
                }
            }

            // dragging?
            if (draggedCorpse != null)
            {
                // cost STA.
                SpendActorStaminaPoints(actor, Rules.STAMINA_COST_MOVE_DRAGGED_CORPSE);
            }

            // spend move AP.
            SpendActorActionPoints(actor, moveCost);
            #endregion

            // If actor can move again, make sure he drops his scent here.
            // If we don't do this, since scents are dropped only in new turns,
            // there will be "holes" in the scent paths, and this is not fair
            // for zombies who will loose track of running livings easily.
            if (actor.ActionPoints > 0) // alpha10 fix; was Rules.BASE_ACTION_COST
                DropActorScents(actor);

            // Screams of terror?
            #region
            if (!actor.IsPlayer &&
                (actor.Activity == Activity.FLEEING || actor.Activity == Activity.FLEEING_FROM_EXPLOSIVE) &&
                !actor.Model.Abilities.IsUndead &&
                actor.Model.Abilities.CanTalk)
            {
                // loud noise.
                OnLoudNoise(newLocation.Map, newLocation.Position, "A loud SCREAM");

                // player hears?
                if (m_Rules.RollChance(PLAYER_HEAR_SCREAMS_CHANCE) && !IsVisibleToPlayer(actor))
                {
                    AddMessageIfAudibleForPlayer(actor.Location, MakePlayerCentricMessage("You hear screams of terror", actor.Location.Position));
                }
            }
            #endregion

            // Trigger stuff.
            OnActorEnterTile(actor);
        }

        public void DoMoveActor(Actor actor, Direction direction)
        {
            DoMoveActor(actor, actor.Location + direction);
        }

        public void OnActorEnterTile(Actor actor)
        {
            Map map = actor.Location.Map;
            Point pos = actor.Location.Position;

            // Check traps.
            // Don't check if there is a covering mobj there.
            if (!m_Rules.IsTrapCoveringMapObjectThere(map, pos))
            {
                Inventory itemsThere = map.GetItemsAt(pos);
                if (itemsThere != null)
                {
                    List<Item> removeThem = null;
                    foreach (Item it in itemsThere.Items)
                    {
                        ItemTrap trap = it as ItemTrap;
                        if (trap == null || !trap.IsActivated)
                            continue;
                        if (TryTriggerTrap(trap, actor))
                        {
                            if (removeThem == null) removeThem = new List<Item>(itemsThere.CountItems);
                            removeThem.Add(it);
                        }
                    }
                    if (removeThem != null)
                    {
                        foreach (Item it in removeThem)
                            map.RemoveItemAt(it, pos);
                    }
                    // Kill actor?
                    if (actor.HitPoints <= 0)
                        KillActor(null, actor, "trap");
                }
            }
        }

        bool TryActorLeaveTile(Actor actor)
        {
            Map map = actor.Location.Map;
            Point pos = actor.Location.Position;
            bool canLeave = true;

            // Check traps.
            if (!m_Rules.IsTrapCoveringMapObjectThere(map, pos))
            {
                Inventory itemsThere = map.GetItemsAt(pos);
                if (itemsThere != null)
                {
                    List<Item> removeThem = null;
                    bool hasTriggeredTraps = false;
                    foreach (Item it in itemsThere.Items)
                    {
                        ItemTrap trap = it as ItemTrap;
                        if (trap == null || !trap.IsTriggered)
                            continue;
                        hasTriggeredTraps = true;
                        bool isDestroyed = false;
                        if (!TryEscapeTrap(trap, actor, out isDestroyed))
                        {
                            canLeave = false;
                            continue;
                        }
                        if (isDestroyed)
                        {
                            if (removeThem == null) removeThem = new List<Item>(itemsThere.CountItems);
                            removeThem.Add(it);
                        }
                    }
                    if (removeThem != null)
                    {
                        foreach (Item it in removeThem)
                            map.RemoveItemAt(it, pos);
                    }
                    // if can leave, force un-trigger all traps.
                    if (canLeave && hasTriggeredTraps)
                        UntriggerAllTrapsHere(actor.Location);
                }
            }

            // Check adjacent Z-Grabs
            bool visible = IsVisibleToPlayer(actor);
            map.ForEachAdjacentInMap(pos, (adj) =>
            {
                Actor grabber = map.GetActorAt(adj);
                if (grabber == null)
                    return;
                if (!grabber.Model.Abilities.IsUndead)
                    return;
                if (!m_Rules.AreEnemies(grabber, actor))
                    return;
                int chance = m_Rules.ZGrabChance(grabber, actor);
                if (chance == 0)
                    return;
                if (m_Rules.RollChance(m_Rules.ZGrabChance(grabber, actor)))
                {
                    // grabbed!
                    if (visible)
                        AddMessage(MakeMessage(grabber, Conjugate(grabber, VERB_GRAB), actor));
                    // stuck there!
                    canLeave = false;
                }
            });

            return canLeave;
        }
        #endregion
        #region Traps
        /// <summary>
        /// @return true if item must be removed (destroyed).
        /// </summary>
        /// <param name="trap"></param>
        /// <returns></returns>
        bool TryTriggerTrap(ItemTrap trap, Actor victim)
        {
            // check trigger chance.
            if (m_Rules.CheckTrapTriggers(trap, victim))
                DoTriggerTrap(trap, victim.Location.Map, victim.Location.Position, victim, null);
            else
            {
                if (IsVisibleToPlayer(victim))
                    AddMessage(MakeMessage(victim, String.Format("safely {0} {1}.", Conjugate(victim, VERB_AVOID), trap.TheName)));
            }
            // destroy?
            return trap.Quantity == 0;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="trap"></param>
        /// <param name="victim"></param>
        /// <param name="isDestroyed"></param>
        /// <returns>true succesful escape</returns>
        bool TryEscapeTrap(ItemTrap trap, Actor victim, out bool isDestroyed)
        {
            isDestroyed = false;
            ItemTrapModel model = trap.TrapModel;

            // no brainer.
            if (model.BlockChance <= 0)
                return true;

            bool visible = IsVisibleToPlayer(victim);
            bool canEscape = false;

            // check escape chance.
            if (m_Rules.CheckTrapEscape(trap, victim))
            {
                // un-triggered and escape.
                trap.IsTriggered = false;
                canEscape = true;

                // tell
                if (visible)
                    AddMessage(MakeMessage(victim, String.Format("{0} {1}.", Conjugate(victim, VERB_ESCAPE), trap.TheName)));

                // then check break on escape chance.
                if (m_Rules.CheckTrapEscapeBreaks(trap, victim))
                {
                    if (visible)
                        AddMessage(MakeMessage(victim, String.Format("{0} {1}.", Conjugate(victim, VERB_BREAK), trap.TheName)));
                    --trap.Quantity;
                    isDestroyed = trap.Quantity <= 0;
                }
            }
            else
            {
                // tell
                if (visible)
                    AddMessage(MakeMessage(victim, String.Format("is trapped by {0}!", trap.TheName)));
            }

            // escape?
            return canEscape;
        }

        void UntriggerAllTrapsHere(Location loc)
        {
            Inventory itemsThere = loc.Map.GetItemsAt(loc.Position);
            if (itemsThere == null) return;
            foreach (Item it in itemsThere.Items)
            {
                ItemTrap trap = it as ItemTrap;
                if (trap == null || !trap.IsTriggered)
                    continue;
                trap.IsTriggered = false;
            }
        }

        /// <summary>
        /// Checks that there is a map object that triggers the traps there.
        /// If so, a map object triggers ALL activated the traps here.
        /// </summary>
        /// <param name="map"></param>
        /// <param name="pos"></param>
        void CheckMapObjectTriggersTraps(Map map, Point pos)
        {
            if (!m_Rules.IsTrapTriggeringMapObjectThere(map, pos))
                return;

            MapObject mobj = map.GetMapObjectAt(pos);

            Inventory itemsThere = map.GetItemsAt(pos);
            if (itemsThere == null) return;

            List<Item> removeThem = null;
            foreach (Item it in itemsThere.Items)
            {
                ItemTrap trap = it as ItemTrap;
                if (trap == null || !trap.IsActivated)
                    continue;
                DoTriggerTrap(trap, map, pos, null, mobj);
                if (trap.Quantity <= 0)
                {
                    if (removeThem == null) removeThem = new List<Item>(itemsThere.CountItems);
                    removeThem.Add(it);
                }
            }
            if (removeThem != null)
            {
                foreach (Item it in removeThem)
                    map.RemoveItemAt(it, pos);
            }
        }

        /// <summary>
        /// Trigger a trap by an actor or a map object.
        /// </summary>
        /// <param name="trap"></param>
        /// <param name="map"></param>
        /// <param name="pos"></param>
        /// <param name="victim"></param>
        /// <param name="mobj"></param>
        void DoTriggerTrap(ItemTrap trap, Map map, Point pos, Actor victim, MapObject mobj)
        {
            ItemTrapModel model = trap.TrapModel;
            bool visible = IsVisibleToPlayer(map, pos);

            // flag.
            trap.IsTriggered = true;

            // effect: damage on victim? (actor)
            int damage = model.Damage * trap.Quantity;
            if (damage > 0 && victim != null)
            {
                InflictDamage(victim, damage);
                if (visible)
                {
                    AddMessage(MakeMessage(victim, String.Format("is hurt by {0} for {1} damage!", trap.AName, damage)));
                    AddOverlay(new OverlayImage(MapToScreen(victim.Location.Position), GameImages.ICON_MELEE_DAMAGE));
                    AddOverlay(new OverlayText(MapToScreen(victim.Location.Position).Add(DAMAGE_DX, DAMAGE_DY), Color.White, damage.ToString(), Color.Black));
                    RedrawPlayScreen();
                    AnimDelay(victim.IsPlayer ? DELAY_NORMAL : DELAY_SHORT);
                    ClearOverlays();
                    RedrawPlayScreen();
                }
            }
            // effect: noise? (actor, mobj)
            if (model.IsNoisy)
            {
                if (visible)
                {
                    if (victim != null)
                        AddMessage(MakeMessage(victim, String.Format("stepping on {0} makes a bunch of noise!", trap.AName)));
                    else if (mobj != null)
                        AddMessage(new Message(String.Format("{0} makes a lot of noise!", Capitalize(trap.TheName)), map.LocalTime.TurnCounter));
                }
                OnLoudNoise(map, pos, model.NoiseName);
            }

            // if one time trigger = desactivate.
            if (model.IsOneTimeUse)
                trap.Desactivate();  //alpha10 //trap.IsActivated = false;

            // then check break chance (actor, mobj)
            if (m_Rules.CheckTrapStepOnBreaks(trap, mobj))
            {
                if (visible)
                {
                    if (victim != null)
                        AddMessage(MakeMessage(victim, String.Format("{0} {1}.", Conjugate(victim, VERB_CRUSH), trap.TheName)));
                    else if (mobj != null)
                        AddMessage(new Message(String.Format("{0} breaks the {1}.", Capitalize(mobj.TheName), trap.TheName), map.LocalTime.TurnCounter));
                }
                --trap.Quantity;
            }
        }
        #endregion
        #region Leaving maps & Using exits.
        public bool DoLeaveMap(Actor actor, Point exitPoint, bool askForConfirmation)
        {
            bool isPlayer = actor.IsPlayer;

            Map fromMap = actor.Location.Map;
            Point fromPos = actor.Location.Position;

            // get exit.
            Exit exit = fromMap.GetExitAt(exitPoint);
            if (exit == null)
            {
                if (isPlayer)
                {
                    AddMessage(MakeErrorMessage("There is nowhere to go there."));
                }
                return true;
            }

            // if player, ask for a confirmation.
            if (isPlayer && askForConfirmation)
            {
                ClearMessages();
                AddMessage(MakeYesNoMessage(String.Format("REALLY LEAVE {0}", fromMap.Name)));
                RedrawPlayScreen();
                bool confirm = WaitYesOrNo();
                if (!confirm)
                {
                    AddMessage(new Message("Let's stay here a bit longer...", m_Session.WorldTime.TurnCounter, Color.Yellow));
                    RedrawPlayScreen();
                    return false;
                }
            }

            // alpha10.1 check autosave before player leaving map
            if (isPlayer)
                CheckAutoSaveTime();

            // Try to leave tile.
            if (!TryActorLeaveTile(actor))
            {
                // waste ap.
                SpendActorActionPoints(actor, Rules.BASE_ACTION_COST);
                return false;
            }

            // spend AP **IF AI**
            if (!actor.IsPlayer)
                SpendActorActionPoints(actor, Rules.BASE_ACTION_COST);

            // if player is leaving and changing district, prepare district.
            // alpha10.1 disallow bots from leaving districts
            bool playerChangedDistrict = false;  // alpha10
            if (isPlayer && !actor.IsBotPlayer && exit.ToMap.District != fromMap.District)
            {
                playerChangedDistrict = true;  // alpha10
                BeforePlayerEnterDistrict(exit.ToMap.District);
            }

            /////////////////////////////////////
            // 1. If spot not available, cancel.
            // 2. Remove from previous map (+ corpse)
            // 3. Enter map (+corpse).
            // 4. Handle followers.
            /////////////////////////////////////

            // 1. If spot not available, cancel.
            Actor other = exit.ToMap.GetActorAt(exit.ToPosition);
            if (other != null)
            {
                if (isPlayer)
                {
                    AddMessage(MakeErrorMessage(String.Format("{0} is blocking your way.", other.Name)));
                }
                return true;
            }
            MapObject blockingObj = exit.ToMap.GetMapObjectAt(exit.ToPosition);
            if (blockingObj != null)
            {
                bool canJump = blockingObj.IsJumpable && m_Rules.HasActorJumpAbility(actor);
                bool ignoreIt = blockingObj.IsCouch;
                if (!canJump && !ignoreIt)
                {
                    if (isPlayer)
                    {
                        AddMessage(MakeErrorMessage(String.Format("{0} is blocking your way.", blockingObj.AName)));
                    }
                    return true;
                }
            }

            // 2. Remove from previous map (+corpse)
            if (IsVisibleToPlayer(actor))
            {
                AddMessage(MakeMessage(actor, String.Format("{0} {1}.", Conjugate(actor, VERB_LEAVE), fromMap.Name)));
            }
            fromMap.RemoveActor(actor);
            if (actor.DraggedCorpse != null)
                fromMap.RemoveCorpse(actor.DraggedCorpse);
            if (isPlayer && exit.ToMap.District != fromMap.District)
            {
                OnPlayerLeaveDistrict();
            }

            // 3. Enter map (+corpse)
            exit.ToMap.PlaceActorAt(actor, exit.ToPosition);
            exit.ToMap.MoveActorToFirstPosition(actor);
            if (actor.DraggedCorpse != null)
            {
                exit.ToMap.AddCorpseAt(actor.DraggedCorpse, exit.ToPosition);
            }
            if (IsVisibleToPlayer(actor) || isPlayer)
            {
                AddMessage(MakeMessage(actor, String.Format("{0} {1}.", Conjugate(actor, VERB_ENTER), exit.ToMap.Name)));
            }
            if (isPlayer)
            {
                // scoring event.
                if (fromMap.District != exit.ToMap.District)
                {
                    m_Session.Scoring.AddEvent(m_Session.WorldTime.TurnCounter, String.Format("Entered district {0}.", exit.ToMap.District.Name));
                }

                // change map.
                SetCurrentMap(exit.ToMap);
            }
            // Trigger stuff.
            OnActorEnterTile(actor);
            // 4. Handle followers.
            if (actor.CountFollowers > 0)
            {
                DoFollowersEnterMap(actor, fromMap, fromPos, exit.ToMap, exit.ToPosition);
            }

            // alpha10
            // handle player changing district
            if (playerChangedDistrict)
                AfterPlayerEnterDistrict();

            // done.
            return true;
        }

        void DoFollowersEnterMap(Actor leader, Map fromMap, Point fromPos, Map toMap, Point toPos)
        {
            bool leavePeopleBehind = toMap.District != fromMap.District;
            bool isPlayer = m_Player == leader;
            List<Actor> leftBehind = null;

            foreach (Actor fo in leader.Followers)
            {
                // can follow only if was adj to leader and find free adj spot on the new map.
                bool canFollow = false;
                List<Point> adjList = null;

                if (m_Rules.IsAdjacent(fromPos, fo.Location.Position))
                {
                    adjList = toMap.FilterAdjacentInMap(toPos, (pt) => m_Rules.IsWalkableFor(fo, toMap, pt.X, pt.Y));
                    if (adjList == null || adjList.Count == 0)
                        canFollow = false;
                    else
                        canFollow = true;
                }

                if (!canFollow)
                {
                    // cannot follow.
                    if (leftBehind == null) leftBehind = new List<Actor>(3);
                    leftBehind.Add(fo);
                }
                else
                {
                    // can follow, do it now.
                    // Try to leave tile.
                    if (TryActorLeaveTile(fo))
                    {
                        Point spot = adjList[m_Rules.Roll(0, adjList.Count)];
                        fromMap.RemoveActor(fo);
                        toMap.PlaceActorAt(fo, spot);
                        toMap.MoveActorToFirstPosition(fo);
                        // Trigger stuff.
                        OnActorEnterTile(fo);
                    }
                }
            }

            // make followers left behind leave if must.
            if (leftBehind != null)
            {
                foreach (Actor leaveMe in leftBehind)
                {
                    if (leavePeopleBehind)
                    {
                        leader.RemoveFollower(leaveMe);
                        if (isPlayer)
                        {
                            // scoring.
                            m_Session.Scoring.AddEvent(m_Session.WorldTime.TurnCounter, String.Format("{0} was left behind.", leaveMe.TheName));

                            // message.
                            ClearMessages();
                            AddMessage(new Message(String.Format("{0} could not follow you out of the district and left you!", leaveMe.TheName), m_Session.WorldTime.TurnCounter, Color.Red));
                            AddMessagePressEnter();
                            ClearMessages();
                        }
                    }
                    else
                    {
                        if (leaveMe.Location.Map == fromMap)
                        {
                            if (isPlayer)
                            {
                                // message.
                                ClearMessages();
                                AddMessage(new Message(String.Format("{0} could not follow and is still in {1}.", leaveMe.TheName, fromMap.Name), m_Session.WorldTime.TurnCounter, Color.Yellow));
                                AddMessagePressEnter();
                                ClearMessages();
                            }
                        }
                    }
                }
            }
        }

        public bool DoUseExit(Actor actor, Point exitPoint)
        {
            // leave map.
            return DoLeaveMap(actor, exitPoint, false);
        }
        #endregion
        #region Leading
        public void DoSwitchPlace(Actor actor, Actor other)
        {
            // spend a bunch of ap.
            SpendActorActionPoints(actor, 2 * Rules.BASE_ACTION_COST);

            // swap positions.
            Map map = other.Location.Map;
            Point actorPos = actor.Location.Position;
            map.RemoveActor(other);
            map.PlaceActorAt(actor, other.Location.Position);
            map.PlaceActorAt(other, actorPos);

            // message.
            if (IsVisibleToPlayer(actor) || IsVisibleToPlayer(other))
            {
                AddMessage(MakeMessage(actor, Conjugate(actor, VERB_SWITCH_PLACE_WITH), other));
            }
        }

        public void DoTakeLead(Actor actor, Actor other)
        {
            // spend AP.
            SpendActorActionPoints(actor, Rules.BASE_ACTION_COST);

            // take lead.
            actor.AddFollower(other);

            // reset trust in leader.
            int prevTrust = other.GetTrustIn(actor);
            other.TrustInLeader = prevTrust;

            // message.
            if (IsVisibleToPlayer(actor) || IsVisibleToPlayer(other))
            {
                if (actor == m_Player)
                    ClearMessages();
                AddMessage(MakeMessage(actor, Conjugate(actor, VERB_PERSUADE), other, " to join."));
                if (prevTrust != 0)
                    DoSay(other, actor, "Ah yes I remember you.", Sayflags.IS_FREE_ACTION);
            }
        }

        // alpha10.1
        public void DoStealLead(Actor actor, Actor other)
        {
            Actor prevLeader = other.Leader;

            // spend AP.
            SpendActorActionPoints(actor, Rules.BASE_ACTION_COST);

            // remove from previous leader
            prevLeader.RemoveFollower(other);

            // take lead.
            actor.AddFollower(other);

            // reset trust in leader.
            int prevTrust = other.GetTrustIn(actor);
            other.TrustInLeader = prevTrust;

            // message.
            if (IsVisibleToPlayer(actor) || IsVisibleToPlayer(other))
            {
                if (actor == m_Player)
                    ClearMessages();
                AddMessage(MakeMessage(actor, Conjugate(actor, VERB_PERSUADE), other, String.Format(" to leave {0} and join.", prevLeader.Name)));
                if (prevTrust != 0)
                    DoSay(other, actor, "Ah yes I remember you.", Sayflags.IS_FREE_ACTION);
            }
        }

        public void DoCancelLead(Actor actor, Actor follower)
        {
            // spend AP.
            SpendActorActionPoints(actor, Rules.BASE_ACTION_COST);

            // remove lead.
            actor.RemoveFollower(follower);

            // reset trust in leader.
            follower.SetTrustIn(actor, follower.TrustInLeader);
            follower.TrustInLeader = Rules.TRUST_NEUTRAL;

            // message.
            if (IsVisibleToPlayer(actor) || IsVisibleToPlayer(follower))
            {
                if (actor == m_Player)
                    ClearMessages();
                AddMessage(MakeMessage(actor, Conjugate(actor, VERB_PERSUADE), follower, " to leave."));
            }
        }
        #endregion
        #region Waiting
        public void DoWait(Actor actor)
        {
            // spend AP.
            SpendActorActionPoints(actor, Rules.BASE_ACTION_COST);

            // message.
            if (IsVisibleToPlayer(actor))
            {
                if (actor.StaminaPoints < m_Rules.ActorMaxSTA(actor))
                    AddMessage(MakeMessage(actor, String.Format("{0} {1} breath.", Conjugate(actor, VERB_CATCH), HisOrHer(actor))));
                else
                    AddMessage(MakeMessage(actor, String.Format("{0}.", Conjugate(actor, VERB_WAIT))));
            }

            // regen STA.
            RegenActorStaminaPoints(actor, Rules.STAMINA_REGEN_WAIT);
        }
        #endregion
        #region Bumping
        public bool DoPlayerBump(Actor player, Direction direction)
        {
            ActionBump bump = new ActionBump(player, this, direction);

            if (bump == null)
                return false;

            // special case: tearing down barricades as living.
            // alpha10.1 moved up because civs models can now bash doors as a bump action; added break check and simplified test
            if ((bump.ConcreteAction is ActionBreak || bump.ConcreteAction is ActionBashDoor) && !player.Model.Abilities.IsUndead)
            {
                string doWhat = bump.ConcreteAction is ActionBreak ? ("break " + (bump.ConcreteAction as ActionBreak).MapObject.TheName) : "tear down the barricade";

                if (m_Rules.IsActorTired(player))
                {
                    AddMessage(MakeErrorMessage("Too tired to " + doWhat + "."));
                    RedrawPlayScreen();
                    return false;
                }
                else
                {
                    // ask for confirmation.
                    AddMessage(MakeYesNoMessage("Really " + doWhat));
                    RedrawPlayScreen();
                    bool confirm = WaitYesOrNo();

                    if (confirm)
                    {
                        //DoBreak(player, door);
                        bump.ConcreteAction.Perform();
                        return true;
                    }
                    else
                    {
                        AddMessage(new Message("Good, keep everything secure.", m_Session.WorldTime.TurnCounter, Color.Yellow));
                        return false;
                    }
                }
                //DoorWindow door = player.Location.Map.GetMapObjectAt(player.Location.Position + direction) as DoorWindow;
                //if (door != null && door.IsBarricaded && !player.Model.Abilities.IsUndead)
                //{
                //    if (!m_Rules.IsActorTired(player))
                //    {
                //        // ask for confirmation.
                //        AddMessage(MakeYesNoMessage("Really tear down the barricade"));
                //        RedrawPlayScreen();
                //        bool confirm = WaitYesOrNo();

                //        if (confirm)
                //        {
                //            DoBreak(player, door);
                //            return true;
                //        }
                //        else
                //        {
                //            AddMessage(new Message("Good, keep everything secure.", m_Session.WorldTime.TurnCounter, Color.Yellow));
                //            return false;
                //        }
                //    }
                //    else
                //    {
                //        AddMessage(MakeErrorMessage("Too tired to tear down the barricade."));
                //        RedrawPlayScreen();
                //        return false;
                //    }
                //}
            }

            if (bump.IsLegal())
            {
                bump.Perform();
                return true;
            }

            AddMessage(MakeErrorMessage(String.Format("Cannot do that : {0}.", bump.FailReason)));
            return false;
        }
        #endregion
    }
}
