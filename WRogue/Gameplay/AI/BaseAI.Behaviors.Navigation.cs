using System;
using System.Collections.Generic;
using System.Drawing;

using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Actions;
using djack.RogueSurvivor.Engine.AI;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Engine.MapObjects;
using djack.RogueSurvivor.Gameplay.AI.Sensors;
using djack.RogueSurvivor.Gameplay.AI.Tools;

namespace djack.RogueSurvivor.Gameplay.AI
{
    abstract partial class BaseAI
    {
        #region Movement
        /// <summary>
        ///
        /// </summary>
        /// <param name="game"></param>
        /// <param name="goodWanderLocFn"></param>
        /// <param name="exploration">can be null for ais with no exploration</param>
        /// <returns></returns>
        protected ActorAction BehaviorWander(RogueGame game, Predicate<Location> goodWanderLocFn, ExplorationData exploration)
        {
            ChoiceEval<Direction> chooseDir = Choose<Direction>(game,
                Direction.COMPASS_LIST,
                (dir) =>
                {
                    Location next = m_Actor.Location + dir;
                    if (goodWanderLocFn != null && !goodWanderLocFn(next))
                        return false;
                    ActorAction bumpAction = game.Rules.IsBumpableFor(m_Actor, game, next);
                    return IsValidWanderAction(game, bumpAction);
                },
                (dir) =>
                {
                    Location next = m_Actor.Location + dir;

                    int score = 0;

                    // alpha10.1
                    const int BREAKING_OBJ = -50000;
                    const int BACKTRACKING = -10000;
                    const int BREAKING_BARRICADES = -1000;
                    const int AVOID_TRAPS = -1000;
                    const int UNEXPLORED_LOC = 1000;  // should not happen, see below
                    const int DOORWINDOWS = 100;
                    const int EXITS = 50;
                    const int INSIDE_WHEN_ALMOST_SLEEPY = 100;
                    const int WANDER_RANDOM = 10;  // alpha10.1 much smaller random factor

                    if (next == m_prevLocation)
                        score += BACKTRACKING;

                    if (m_Actor.Model.Abilities.IsIntelligent)
                    {
                        if (IsAnyUnsafeDamagingTrapThere(game, m_Actor.Location.Map, next.Position))
                            score += AVOID_TRAPS;
                    }

                    // alpha10.1 allowing break action prevent rare cases of getting stuck or going back and forth but we penalize it
                    if (next.Map.GetMapObjectAt(next.Position) != null)
                    {
                        ActorAction bumpObjAction = game.Rules.IsBumpableFor(m_Actor, game, next);
                        if (bumpObjAction != null && (bumpObjAction is ActionBreak || bumpObjAction is ActionBashDoor))
                        {
                            if (bumpObjAction is ActionBashDoor)
                                score += BREAKING_BARRICADES;
                            else
                                score += BREAKING_OBJ;
                            // if we have to break things, prefer objs we can break more quickly
                            score -= GetObjectHitPoints(next.Map.GetMapObjectAt(next.Position));
                        }
                    }

                    // alpha10.1 prefer unexplored/oldest
                    // unexplored should not happen because exploration rule is tested before wander rule but just to be more robust...
                    if (exploration != null)
                    {
                        int locAge = exploration.GetExploredAge(next);
                        if (locAge == 0)
                            score += UNEXPLORED_LOC;
                        else
                            score += locAge;
                    }

                    // alpha10.1 prefer wandering to doorwindows and exits.
                    // helps civs ai getting stuck in semi-infinite loop when running out of new exploration to do.
                    // as a side effect, make ais with no exploration data (eg zombies) more eager to visit door/windows and exits.
                    DoorWindow doorWindow = next.Map.GetMapObjectAt(next.Position) as DoorWindow;
                    if (doorWindow != null)
                        score += DOORWINDOWS;
                    if (next.Map.GetExitAt(next.Position) != null)
                        score += EXITS;

                    // alpha10.1 prefer inside when almost sleepy
                    if (game.Rules.IsAlmostSleepy(m_Actor) && next.Map.GetTileAt(next.Position).IsInside)
                        score += INSIDE_WHEN_ALMOST_SLEEPY;

                    // alpha10.1 add random factor
                    score += game.Rules.Roll(0, WANDER_RANDOM);

                    // done
                    return score;
                },
                (a, b) => a > b);

            if (chooseDir != null)
                return new ActionBump(m_Actor, game, chooseDir.Choice);
            else
                return null;
        }

        protected ActorAction BehaviorWander(RogueGame game, ExplorationData exploration)
        {
            return BehaviorWander(game, null, exploration);
        }

        // alpha10 added break & push
        /// <summary>
        ///
        /// </summary>
        /// <param name="game"></param>
        /// <param name="goal"></param>
        /// <param name="canCheckBreak">if blocked by mapobject, can check if can break it?</param>
        /// <param name="canCheckPush">if blocked by mapobject, can check if can push it?</param>
        /// <param name="distanceFn">float.Nan to forbid a move</param>
        /// <returns></returns>
        protected ActorAction BehaviorBumpToward(RogueGame game, Point goal, bool canCheckBreak, bool canCheckPush, Func<Point, Point, float> distanceFn)
        {
            ChoiceEval<ActorAction> bestCloserDir = ChooseExtended<Direction, ActorAction>(game,
                Direction.COMPASS_LIST,
                (dir) =>
                {
                    Location next = m_Actor.Location + dir;
                    ActorAction bumpAction = game.Rules.IsBumpableFor(m_Actor, game, next);
                    if (bumpAction == null)
                    {
                        // for undeads, try to push the blocking object randomly.
                        if (m_Actor.Model.Abilities.IsUndead && game.Rules.HasActorPushAbility(m_Actor))
                        {
                            MapObject obj = m_Actor.Location.Map.GetMapObjectAt(next.Position);
                            if (obj != null)
                            {
                                if (game.Rules.CanActorPush(m_Actor, obj))
                                {
                                    Direction pushDir = game.Rules.RollDirection();
                                    if (game.Rules.CanPushObjectTo(obj, obj.Location.Position + pushDir))
                                        return new ActionPush(m_Actor, game, obj, pushDir);
                                }
                            }
                        }

                        // alpha10 check special actions
                        if (canCheckBreak)
                        {
                            MapObject obj = m_Actor.Location.Map.GetMapObjectAt(next.Position);
                            if (obj != null)
                            {
                                if (game.Rules.IsBreakableFor(m_Actor, obj))
                                    return new ActionBreak(m_Actor, game, obj);
                            }
                        }
                        if (canCheckPush)
                        {
                            MapObject obj = m_Actor.Location.Map.GetMapObjectAt(next.Position);
                            if (obj != null)
                            {
                                if (game.Rules.CanActorPush(m_Actor, obj))
                                {
                                    // push in a valid direction at random
                                    List<Direction> validPushes = new List<Direction>(8);
                                    foreach (Direction pushDir in Direction.COMPASS)
                                    {
                                        if (game.Rules.CanPushObjectTo(obj, obj.Location.Position + pushDir))
                                            validPushes.Add(pushDir);
                                    }
                                    if (validPushes.Count > 0)
                                        return new ActionPush(m_Actor, game, obj, validPushes[game.Rules.Roll(0, validPushes.Count)]);
                                }
                            }
                        }

                        // failed.
                        return null;
                    }
                    if (next.Position == goal)
                        return bumpAction;
                    if (IsValidMoveTowardGoalAction(bumpAction))
                        return bumpAction;
                    else
                        return null;
                },
                (dir, action) =>
                {
                    Location next = m_Actor.Location + dir;
                    float cost = (distanceFn != null ? distanceFn(next.Position, goal) : game.Rules.StdDistance(next.Position, goal));

                    // alpha10 add action cost heuristic if npc is intelligent
                    if (!float.IsNaN(cost))
                    {
                        if (m_Actor.Model.Abilities.IsIntelligent)
                            cost += EstimateBumpActionCost(game, next, action);
                    }

                    return cost;
                },
                (a, b) => !float.IsNaN(a) && a < b);

            if (bestCloserDir != null)
                return bestCloserDir.Choice;
            else
                return null;
        }

        // alpha10
        /// <summary>
        /// For intelligent npcs, additional cost to distance cost when chosing which adj tile to bump to.
        /// </summary>
        /// <param name="game"></param>
        /// <param name="loc"></param>
        /// <param name="action"></param>
        /// <returns></returns>
        /// <see cref="BehaviorBumpToward(RogueGame, Point, bool, bool, Func{Point, Point, float})"/>
        protected float EstimateBumpActionCost(RogueGame game, Location loc, ActorAction action)
        {
            float cost = 0;

            if (action is ActionBump)
                action = (action as ActionBump).ConcreteAction;

            // Consuming additional sta
            if (m_Actor.Model.Abilities.CanTire)
            {
                // jumping
                if (action is ActionMoveStep)
                {
                    MapObject mobj = loc.Map.GetMapObjectAt(loc.Position);
                    if (mobj != null && mobj.IsJumpable)
                        cost = MOVE_DISTANCE_PENALTY;
                }

                // actions that always consume sta or may take more than one turn
                if (action is ActionBashDoor ||
                    action is ActionBreak ||
                    action is ActionPush)
                    cost = MOVE_DISTANCE_PENALTY;
            }

            return cost;
        }

        // alpha10 can check break and push
        protected ActorAction BehaviorStupidBumpToward(RogueGame game, Point goal, bool canCheckBreak, bool canCheckPush)
        {
            return BehaviorBumpToward(game, goal,
                canCheckBreak, canCheckPush,
                (ptA, ptB) =>
                {
                    if (ptA == ptB) return 0;
                    float distance = game.Rules.StdDistance(ptA, ptB);
                    //if (distance < 2f) return distance;

                    // penalize having to push/bash/jump.
                    if (!game.Rules.IsWalkableFor(m_Actor, m_Actor.Location.Map, ptA.X, ptA.Y))
                        distance += MOVE_DISTANCE_PENALTY;

                    return distance;
                });
        }

        // alpha10 added break & push
        protected ActorAction BehaviorIntelligentBumpToward(RogueGame game, Point goal,
            bool canCheckBreak, bool canCheckPush)
        {
            float currentDistance = game.Rules.StdDistance(m_Actor.Location.Position, goal);
            bool imStarvingOrCourageous = game.Rules.IsActorStarving(m_Actor) || Directives.Courage == ActorCourage.COURAGEOUS;

            ActorAction bump = BehaviorBumpToward(game, goal,
                canCheckBreak, canCheckPush,
                (ptA, ptB) =>
                {
                    if (ptA == ptB) return 0;
                    float distance = game.Rules.StdDistance(ptA, ptB);
                    //if (distance < 2f) return distance;

                    // consider only moves that make takes us closer.
                    if (distance >= currentDistance)
                        return float.NaN;

                    // avoid stepping on damaging traps, unless starving or courageous.
                    if (!imStarvingOrCourageous)
                    {
                        int trapsDamage = ComputeTrapsMaxDamageForMe(game, m_Actor.Location.Map, ptA);
                        if (trapsDamage > 0)
                        {
                            // if instant death, don't do it.
                            if (trapsDamage >= m_Actor.HitPoints)
                                return float.NaN;
                            // avoid.
                            distance += MOVE_INTO_TRAPS_PENALTY;
                        }
                    }

                    return distance;
                });
            return bump;
        }

        protected ActorAction BehaviorWalkAwayFrom(RogueGame game, Percept goal)
        {
            return BehaviorWalkAwayFrom(game, new List<Percept>(1) { goal });
        }

        protected ActorAction BehaviorWalkAwayFrom(RogueGame game, List<Percept> goals)
        {
            // stuff to avoid stepping into leader LoF.
            Actor myLeader = m_Actor.Leader;
            bool leaderIsFiring = m_Actor.HasLeader && m_Actor.GetEquippedWeapon() is ItemRangedWeapon;
            Actor leaderNearestTarget = null;
            if (leaderIsFiring) leaderNearestTarget = GetNearestTargetFor(game, m_Actor.Leader);
            bool checkLeaderLoF = leaderNearestTarget != null && leaderNearestTarget.Location.Map == m_Actor.Location.Map;
            List<Point> leaderLoF = null;
            if (checkLeaderLoF)
            {
                leaderLoF = new List<Point>(1);
                ItemRangedWeapon wpn = m_Actor.GetEquippedWeapon() as ItemRangedWeapon;
                LOS.CanTraceFireLine(myLeader.Location, leaderNearestTarget.Location.Position, (wpn.Model as ItemRangedWeaponModel).Attack.Range, leaderLoF);
            }

            ChoiceEval<Direction> bestAwayDir = Choose<Direction>(game,
                Direction.COMPASS_LIST,
                (dir) =>
                {
                    Location next = m_Actor.Location + dir;
                    ActorAction bumpAction = game.Rules.IsBumpableFor(m_Actor, game, next);
                    return IsValidFleeingAction(bumpAction);
                },
                (dir) =>
                {
                    Location next = m_Actor.Location + dir;
                    // Heuristic value:
                    // - Safety from dangers.
                    // - If follower, stay close to leader but avoid stepping into leader LoF.
                    int safetyValue = SafetyFrom(game, next.Position, goals);
                    if (m_Actor.HasLeader)
                    {
                        // stay close to leader.
                        safetyValue -= 100 * game.Rules.GridDistance(next.Position, m_Actor.Leader.Location.Position);
                        // don't step into leader LoF.
                        if (checkLeaderLoF)
                        {
                            if (leaderLoF.Contains(next.Position))
                                safetyValue -= 100 * IN_LEADER_LOF_SAFETY_PENALTY;
                        }
                    }

                    return safetyValue;
                },
                (a, b) => a > b);

            if (bestAwayDir != null)// && bestAwayDir.Value > notMovingValue) nope, moving is always better than not moving
                return new ActionBump(m_Actor, game, bestAwayDir.Choice);
            else
                return null;
        }
        #endregion
        #region Following
        protected ActorAction BehaviorFollowActor(RogueGame game, Actor other, Point otherPosition, bool isVisible, int maxDist)
        {
            // if no other or dead, don't.
            if (other == null || other.IsDead)
                return null;

            // if close enough and visible, wait there.
            int dist = game.Rules.GridDistance(m_Actor.Location.Position, otherPosition);
            if (isVisible && dist <= maxDist)
                return new ActionWait(m_Actor, game);

            // if in different map and standing on an exit that leads there, try to use the exit.
            if (other.Location.Map != m_Actor.Location.Map)
            {
                Exit exitThere = m_Actor.Location.Map.GetExitAt(m_Actor.Location.Position);
                if (exitThere != null && exitThere.ToMap == other.Location.Map)
                {
                    if (game.Rules.CanActorUseExit(m_Actor, m_Actor.Location.Position))
                        return new ActionUseExit(m_Actor, m_Actor.Location.Position, game);
                }
            }

            // try to get close.
            ActorAction bumpAction = BehaviorIntelligentBumpToward(game, otherPosition, false, false);
            if (bumpAction != null && bumpAction.IsLegal())
            {
                // run if other is running.
                if (other.IsRunning)
                    RunIfPossible(game.Rules);

                // done.
                return bumpAction;
            }

            // fail.
            return null;
        }

        protected ActorAction BehaviorHangAroundActor(RogueGame game, Actor other, Point otherPosition, int minDist, int maxDist)
        {
            // if no other or dead, don't.
            if (other == null || other.IsDead)
                return null;

            // pick a random spot around other within distance.
            Point hangSpot;
            int tries = 0;
            const int maxTries = 100;
            do
            {
                hangSpot = otherPosition;
                hangSpot.X += game.Rules.Roll(minDist, maxDist + 1) - game.Rules.Roll(minDist, maxDist + 1);
                hangSpot.Y += game.Rules.Roll(minDist, maxDist + 1) - game.Rules.Roll(minDist, maxDist + 1);
                m_Actor.Location.Map.TrimToBounds(ref hangSpot);
            }
            while (game.Rules.GridDistance(hangSpot, otherPosition) < minDist && ++tries < maxTries);

            // try to get close.
            ActorAction bumpAction = BehaviorIntelligentBumpToward(game, hangSpot, false, false);
            if (bumpAction != null && IsValidMoveTowardGoalAction(bumpAction) && bumpAction.IsLegal())
            {
                // run if other is running.
                if (other.IsRunning)
                    RunIfPossible(game.Rules);

                // done.
                return bumpAction;
            }

            // fail.
            return null;
        }
        #endregion
        #region Tracking scents
        protected ActorAction BehaviorTrackScent(RogueGame game, List<Percept> scents)
        {
            // if no scents, nothing to do.
            if (scents == null || scents.Count == 0)
                return null;

            // get highest scent.
            Percept best = FilterStrongestScent(game, scents);

            // 2 cases:
            // 1. Standing on best scent.
            // or
            // 2. Best scent is adjacent.
            #region
            Map map = m_Actor.Location.Map;
            // 1. Standing on best scent.
            if (m_Actor.Location.Position == best.Location.Position)
            {
                // if exit there and can and want to use it, do it.
                Exit exitThere = map.GetExitAt(m_Actor.Location.Position);
                if (exitThere != null && m_Actor.Model.Abilities.AI_CanUseAIExits)
                    return BehaviorUseExit(game, UseExitFlags.ATTACK_BLOCKING_ENEMIES | UseExitFlags.BREAK_BLOCKING_OBJECTS);
                else
                    return null;
            }

            // 2. Best scent is adjacent.
            // try to bump there.
            ActorAction bump = BehaviorIntelligentBumpToward(game, best.Location.Position, false, false);
            if (bump != null)
                return bump;
            #endregion

            // nope.
            return null;
        }
        #endregion
        #region Leading
        protected ActorAction BehaviorLeadActor(RogueGame game, Percept target)
        {
            Actor other = target.Percepted as Actor;

            // if can't lead him, fail.
            if (!game.Rules.CanActorTakeLead(m_Actor, other))
                return null;

            // if next to him, lead him.
            if (game.Rules.IsAdjacent(m_Actor.Location.Position, other.Location.Position))
            {
                return new ActionTakeLead(m_Actor, game, other);
            }

            // then try getting closer.
            ActorAction bumpAction = BehaviorIntelligentBumpToward(game, other.Location.Position, false, false);
            if (bumpAction != null)
                return bumpAction;

            // failed.
            return null;

        }

        protected ActorAction BehaviorDontLeaveFollowersBehind(RogueGame game, int distance, out Actor target)
        {
            target = null;

            // alpha10.1 dont always check for lagging followers, prevent leader from getting stuck waiting too much.
            // side effect is more occurence of followers lagging behind.
            if (game.Rules.RollChance(25))
                return null;

            // Scan the group:
            // - Find farthest member of the group.
            // - If at least half the group is close enough we consider the group cohesion to be good enough and do nothing.
            int worstDist = Int32.MinValue;
            Map map = m_Actor.Location.Map;
            Point myPos = m_Actor.Location.Position;
            int closeCount = 0;
            int halfGroup = m_Actor.CountFollowers / 2;
            foreach (Actor a in m_Actor.Followers)
            {
                // ignore actors on different map.
                if (a.Location.Map != map)
                    continue;

                // this actor close enough?
                if (game.Rules.GridDistance(a.Location.Position, myPos) <= distance)
                {
                    // if half close enough, nothing to do.
                    if (++closeCount >= halfGroup)
                        return null;
                }

                // farthest?
                int dist = game.Rules.GridDistance(a.Location.Position, myPos);
                if (target == null || dist > worstDist)
                {
                    target = a;
                    worstDist = dist;
                }
            }

            // try to move toward farther dude.
            if (target == null)
                return null;
            return BehaviorIntelligentBumpToward(game, target.Location.Position, false, false);
        }
        #endregion
        #region Communication
        protected ActorAction BehaviorWarnFriends(RogueGame game, List<Percept> friends, Actor nearestEnemy)
        {
            // Never if actor is itself adjacent to the enemy.
            if (game.Rules.IsAdjacent(m_Actor.Location, nearestEnemy.Location))
                return null;

            // Shout if leader is sleeping.
            // (kinda hax, but make followers more useful for players over phone)
            if (m_Actor.HasLeader && m_Actor.Leader.IsSleeping)
                return new ActionShout(m_Actor, game);

            // Shout if we have a friend sleeping.
            foreach (Percept p in friends)
            {
                Actor other = p.Percepted as Actor;
                if (other == null)
                    throw new ArgumentException("percept not an actor");
                if (other == null || other == m_Actor)
                    continue;
                if (!other.IsSleeping)
                    continue;
                if (game.Rules.AreEnemies(m_Actor, other))
                    continue;
                if (!game.Rules.AreEnemies(other, nearestEnemy))
                    continue;

                // friend sleeping, wake up!
                string shoutText = nearestEnemy == null ? String.Format("Wake up {0}!", other.Name) : String.Format("Wake up {0}! {1} sighted!", other.Name, nearestEnemy.Name);
                return new ActionShout(m_Actor, game, shoutText);
            }

            // no one to alert.
            return null;
        }

        protected ActorAction BehaviorTellFriendAboutPercept(RogueGame game, Percept percept)
        {
            // get an adjacent awake friend, if none nothing to do.
            Map map = m_Actor.Location.Map;
            List<Point> friends = map.FilterAdjacentInMap(m_Actor.Location.Position,
                (pt) =>
                {
                    Actor otherActor = map.GetActorAt(pt);
                    if (otherActor == null)
                        return false;
                    if (otherActor.IsSleeping)
                        return false;
                    if (game.Rules.AreEnemies(m_Actor, otherActor))
                        return false;
                    return true;
                });
            if (friends == null || friends.Count == 0)
                return null;

            // pick a random friend.
            Actor friend = map.GetActorAt(friends[game.Rules.Roll(0, friends.Count)]);

            // make message.
            string tellMsg;
            string whereMsg = MakeCentricLocationDirection(game, m_Actor.Location, percept.Location);
            string timeMsg = String.Format("{0} ago", WorldTime.MakeTimeDurationMessage(m_Actor.Location.Map.LocalTime.TurnCounter - percept.Turn));
            if (percept.Percepted is Actor)
            {
                Actor who = percept.Percepted as Actor;
                tellMsg = String.Format("I saw {0} {1} {2}.", who.Name, whereMsg, timeMsg);
            }
            else if (percept.Percepted is Inventory)
            {
                // tell about a random item from the pile.
                // warning: the items might have changed since then, the AI cheats a bit by knowing which items are there now.
                Inventory inv = percept.Percepted as Inventory;
                if (inv.IsEmpty)
                    return null;    // all items were taken or destroyed.
                Item what = inv[game.Rules.Roll(0, inv.CountItems)];

                // ignore worthless items (eg: don't spam about stupid items like planks)
                if (!IsItemWorthTellingAbout(what))
                    return null;

                // ignore stacks that are probably in plain view of the friend.
                int friendFOVRange = game.Rules.ActorFOV(friend, map.LocalTime, game.Session.World.Weather);
                if (percept.Location.Map == friend.Location.Map &&
                    game.Rules.StdDistance(percept.Location.Position, friend.Location.Position) <= 2 + friendFOVRange)
                {
                    return null;
                }

                // do it.
                tellMsg = String.Format("I saw {0} {1} {2}.", what.AName, whereMsg, timeMsg);
            }
            else if (percept.Percepted is String)
            {
                String raidDesc = percept.Percepted as String;
                tellMsg = String.Format("I heard {0} {1} {2}!", raidDesc, whereMsg, timeMsg);
            }
            else
                throw new InvalidOperationException("unhandled percept.Percepted type");

            // tell friend - if legal.
            ActionSay say = new ActionSay(m_Actor, game, friend, tellMsg, RogueGame.Sayflags.NONE);
            if (say.IsLegal())
                return say;
            else
                return null;
        }

        #endregion
        #region Exploring
        protected ActorAction BehaviorExplore(RogueGame game, ExplorationData exploration)
        {
            // prepare data.
            Direction prevDirection = Direction.FromVector(m_Actor.Location.Position.X - m_prevLocation.Position.X, m_Actor.Location.Position.Y - m_prevLocation.Position.Y);
            bool imStarvingOrCourageous = game.Rules.IsActorStarving(m_Actor) || Directives.Courage == ActorCourage.COURAGEOUS;
            bool isIntelligent = m_Actor.Model.Abilities.IsIntelligent;

            // eval all adjacent tiles for exploration utility and get the best one.

            ChoiceEval<Direction> chooseExploreDir = Choose<Direction>(game,
                Direction.COMPASS_LIST,
                (dir) =>
                {
                    Location next = m_Actor.Location + dir;

                    // alpha10.1 bot mode fix
#if DEBUG
                    if (!next.Map.IsInBounds(next.Position))
                        return false;
#endif

                    if (exploration.HasExplored(next))
                        return false;

                    // alpha10.1 dont break stuff to explore
                    ActorAction bumpAction = game.Rules.IsBumpableFor(m_Actor, game, next);
                    if (bumpAction != null && (bumpAction is ActionBreak || bumpAction is ActionBashDoor))
                        return false;

                    return IsValidMoveTowardGoalAction(bumpAction);
                },
                (dir) =>
                {
                    Location next = m_Actor.Location + dir;
                    Map map = next.Map;
                    Point pos = next.Position;

                    // intelligent NPC: forbid stepping on deadly traps, unless starving or courageous (desperate).
                    if (m_Actor.Model.Abilities.IsIntelligent && !imStarvingOrCourageous)
                    {
                        int trapsDamage = ComputeTrapsMaxDamageForMe(game, map, pos);
                        if (trapsDamage >= m_Actor.HitPoints)
                            return float.NaN;
                    }

                    // Heuristic scoring:
                    // 0st Punish backtracking // alpha10.1
                    // 1st Prefer unexplored zones.
                    // 2nd Prefer unexplored locs.
                    // 3rd Prefer doors and barricades (doors/windows, pushables)
                    // 4th If intelligent punish stepping on unsafe traps. // alpha10
                    // 5th Prefer inside during the night vs outside during the day, and inside if sleepy // alpha10.1
                    // 6th Prefer continue in same direction.
                    // 7th Small randomness.
                    const int BACKTRACKING = -10000;  // alpha10.1
                    const int EXPLORE_ZONES = 1000;
                    const int EXPLORE_LOCS = 500;
                    const int EXPLORE_BARRICADES = 100;
                    const int AVOID_TRAPS = -1000; // alpha10 greatly increase penalty and x by potential damage; was -50
                    const int EXPLORE_INOUT = 50;
                    const int EXPLORE_IN_ALMOST_SLEEPY = 100; // alpha10.1
                    const int EXPLORE_DIRECTION = 25;
                    const int EXPLORE_RANDOM = 10;

                    int score = 0;
                    // 0st Punish backtracking // alpha10.1
                    if (next.Map == m_prevLocation.Map && pos == m_prevLocation.Position)
                        score += BACKTRACKING;
                    // 1st Prefer unexplored zones.
                    // alpha10.1 use age to prefer older zones
                    int zoneAge = exploration.GetExploredAge(map.GetZonesAt(pos.X, pos.Y));
                    if (zoneAge == 0)  // unexplored (or expired)
                        score += EXPLORE_ZONES;
                    else
                        score += zoneAge;
                    //if (!exploration.HasExplored(map.GetZonesAt(pos.X, pos.Y)))
                    //    score += EXPLORE_ZONES;
                    // 2nd Prefer unexplored locs.
                    // alpha10.1 use age to prefer older locs
                    int locAge = exploration.GetExploredAge(next);
                    if (locAge == 0)  // unexplored (or expired)
                        score += EXPLORE_LOCS;
                    else
                        score += 2 * locAge;
                    //if (!exploration.HasExplored(next))
                    //    score += EXPLORE_LOCS;
                    // 3rd Prefer doors and barricades (doors/windows, pushables)
                    MapObject mapObj = map.GetMapObjectAt(pos);
                    if (mapObj != null && (mapObj.IsMovable || mapObj is DoorWindow))
                        score += EXPLORE_BARRICADES;
                    // 4th If intelligent punish stepping on unsafe traps. // alpha10
                    if (isIntelligent)
                    {
                        int trapsDmg = ComputeTrapsMaxDamageForMe(game, map, pos);
                        if (trapsDmg > 0)
                            score += trapsDmg * AVOID_TRAPS;
                    }
                    // 5th Prefer inside during the night vs outside during the day, and inside if sleepy // alpha10.1
                    bool isInside = map.GetTileAt(pos.X, pos.Y).IsInside;
                    if (isInside)
                    {
                        if (game.Rules.IsAlmostSleepy(m_Actor))
                            score += EXPLORE_IN_ALMOST_SLEEPY;
                        if (map.LocalTime.IsNight)
                            score += EXPLORE_INOUT;
                    }
                    else
                    {
                        if (!map.LocalTime.IsNight)
                            score += EXPLORE_INOUT;
                    }
                    // 6th Prefer continue in same direction.
                    if (dir == prevDirection)
                        score += EXPLORE_DIRECTION;
                    // 7th Small randomness.
                    score += game.Rules.Roll(0, EXPLORE_RANDOM);

                    // done.
                    return score;
                },
                (a, b) => !float.IsNaN(a) && a > b);

            if (chooseExploreDir != null)
                return new ActionBump(m_Actor, game, chooseExploreDir.Choice);
            else
                return null;
        }
        #endregion
        #region Advanced movement
        protected ActorAction BehaviorCloseDoorBehindMe(RogueGame game, Location previousLocation)
        {
            // if we've gone through a door, try to close it.
            DoorWindow prevDoor = previousLocation.Map.GetMapObjectAt(previousLocation.Position) as DoorWindow;
            if (prevDoor == null)
                return null;
            if (game.Rules.IsClosableFor(m_Actor, prevDoor))
                return new ActionCloseDoor(m_Actor, game, prevDoor);

            // nope.
            return null;
        }

        protected ActorAction BehaviorSecurePerimeter(RogueGame game, HashSet<Point> fov)
        {
            /////////////////////////////////////
            // Secure room procedure:
            // 1. Close doors/windows.
            // 2. Barricade unbarricaded windows.
            /////////////////////////////////////
            Map map = m_Actor.Location.Map;

            foreach (Point pt in fov)
            {
                MapObject mapObj = map.GetMapObjectAt(pt);
                if (mapObj == null)
                    continue;
                DoorWindow door = mapObj as DoorWindow;
                if (door == null)
                    continue;

                // 1. Close doors/windows.
                if (door.IsOpen && game.Rules.IsClosableFor(m_Actor, door))
                {
                    if (game.Rules.IsAdjacent(door.Location.Position, m_Actor.Location.Position))
                        return new ActionCloseDoor(m_Actor, game, door);
                    else
                        return BehaviorIntelligentBumpToward(game, door.Location.Position, false, false);
                }

                // 2. Barricade unbarricaded windows.
                if (door.IsWindow && !door.IsBarricaded && game.Rules.CanActorBarricadeDoor(m_Actor, door))
                {
                    if (game.Rules.IsAdjacent(door.Location.Position, m_Actor.Location.Position))
                        return new ActionBarricadeDoor(m_Actor, game, door);
                    else
                        return BehaviorIntelligentBumpToward(game, door.Location.Position, false, false);
                }
            }

            // nothing to secure.
            return null;
        }
        #endregion
        #region Exits
        [Flags]
        protected enum UseExitFlags
        {
            /// <summary>
            /// Use only free exits.
            /// </summary>
            NONE = 0,

            /// <summary>
            /// Can try to break a blocking object.
            /// </summary>
            BREAK_BLOCKING_OBJECTS = (1 << 0),

            /// <summary>
            /// Can try to attack a blocking actor.
            /// </summary>
            ATTACK_BLOCKING_ENEMIES = (1 << 1),

            /// <summary>
            /// Do not use exit if we go back to our last location.
            /// </summary>
            DONT_BACKTRACK = (1 << 2)
        }

        /// <summary>
        /// Intelligent use of exit through flags : can prevent from backtracking, can attack object, can attack actor.
        /// </summary>
        /// <param name="game"></param>
        /// <param name="useFlags">combination of flags.</param>
        /// <returns></returns>
        protected ActorAction BehaviorUseExit(RogueGame game, UseExitFlags useFlags)
        {
            // get exit at location, if none or ai flag not set, fail.
            Exit exit = m_Actor.Location.Map.GetExitAt(m_Actor.Location.Position);
            if (exit == null)
                return null;
            if (!exit.IsAnAIExit)
                return null;

            // don't backtrack?
            if ((useFlags & UseExitFlags.DONT_BACKTRACK) != 0)
            {
                if (exit.ToMap == m_prevLocation.Map && exit.ToPosition == m_prevLocation.Position)
                    return null;
            }

            // if exit blocked by an enemy and want to attack it, do it.
            if ((useFlags & UseExitFlags.ATTACK_BLOCKING_ENEMIES) != 0)
            {
                Actor blockingActor = exit.ToMap.GetActorAt(exit.ToPosition);
                if (blockingActor != null && game.Rules.AreEnemies(m_Actor, blockingActor) && game.Rules.CanActorMeleeAttack(m_Actor, blockingActor))
                    return new ActionMeleeAttack(m_Actor, game, blockingActor);
            }

            // if exit blocked by a breakable object and want to bash, do it.
            if ((useFlags & UseExitFlags.BREAK_BLOCKING_OBJECTS) != 0)
            {
                MapObject blockingObj = exit.ToMap.GetMapObjectAt(exit.ToPosition);
                if (blockingObj != null && game.Rules.IsBreakableFor(m_Actor, blockingObj))
                    return new ActionBreak(m_Actor, game, blockingObj);
            }

            // if using exit is illegal, fail.
            if (!game.Rules.CanActorUseExit(m_Actor, m_Actor.Location.Position))
                return null;

            // use the exit.
            return new ActionUseExit(m_Actor, m_Actor.Location.Position, game);
        }
        #endregion
    }
}
