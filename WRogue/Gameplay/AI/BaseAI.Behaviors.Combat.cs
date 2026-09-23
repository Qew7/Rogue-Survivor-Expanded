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
        #region Melee attack
        protected ActorAction BehaviorMeleeAttack(RogueGame game, Percept target)
        {
            Actor targetActor = target.Percepted as Actor;
            if (targetActor == null)
                throw new ArgumentException("percepted is not an actor");

            // if illegal cant.
            if (!game.Rules.CanActorMeleeAttack(m_Actor, targetActor))
                return null;

            // melee!
            return new ActionMeleeAttack(m_Actor, game, targetActor);
        }
        #endregion
        #region Ranged attack
        protected ActorAction BehaviorRangedAttack(RogueGame game, Percept target)
        {
            Actor targetActor = target.Percepted as Actor;
            if (targetActor == null)
                throw new ArgumentException("percepted is not an actor");

            // if illegal cant.
            if (!game.Rules.CanActorFireAt(m_Actor, targetActor))
                return null;

            // alpha10
            // select rapid fire if one shot is not enough to kill target, has more than one ammo loaded and chances to hit good enough.
            FireMode fireMode = FireMode.DEFAULT;
            if ((GetEquippedWeapon() as ItemRangedWeapon).Ammo >= 2)
            {
                Attack rangedAttack = game.Rules.ActorRangedAttack(m_Actor, m_Actor.CurrentRangedAttack, game.Rules.GridDistance(m_Actor.Location.Position, targetActor.Location.Position), targetActor);
                if (rangedAttack.DamageValue < targetActor.HitPoints)
                {
                    int rapidHit1Chance = game.Rules.ComputeChancesRangedHit(m_Actor, targetActor, 1);
                    int rapidHit2Chance = game.Rules.ComputeChancesRangedHit(m_Actor, targetActor, 2);
                    // "good chances" = both hits at least 50%
                    if (rapidHit1Chance >= 50 && rapidHit2Chance >= 50)
                        fireMode = FireMode.RAPID;
                }
            }

            // fire!
            return new ActionRangedAttack(m_Actor, game, targetActor, fireMode);
        }
        #endregion
        #region Barricading & Building & Traps

        protected int ComputeTrapsMaxDamageForMe(RogueGame game, Map map, Point pos)
        {
            Inventory inv = map.GetItemsAt(pos);
            if (inv == null) return 0;

            int sum = 0;
            ItemTrap trp = null;
            foreach (Item it in inv.Items)
            {
                trp = it as ItemTrap;
                if (trp == null) continue;
                if (!game.Rules.IsSafeFromTrap(trp, m_Actor))  // alpha10 ignore safe traps we can't trigger them
                    sum += trp.TrapModel.Damage;
            }
            return sum;
        }

        protected ActorAction BehaviorBuildTrap(RogueGame game)
        {
            // don't bother if we don't have a trap.
            ItemTrap trap = m_Actor.Inventory.GetFirstByType(typeof(ItemTrap)) as ItemTrap;
            if (trap == null)
                return null;

            // is this a good spot for a trap?
            string reason;
            if (!IsGoodTrapSpot(game, m_Actor.Location.Map, m_Actor.Location.Position, out reason))
                return null;

            // if trap needs to be activated, do it.
            if (!trap.IsActivated && !trap.TrapModel.ActivatesWhenDropped)
                return new ActionUseItem(m_Actor, game, trap);

            // trap ready to setup, do it!
            game.DoEmote(m_Actor, String.Format("{0} {1}!", reason, trap.AName), true);
            return new ActionDropItem(m_Actor, game, trap);
        }

        protected bool IsGoodTrapSpot(RogueGame game, Map map, Point pos, out string reason)
        {
            reason = "";
            bool potentialSpot = false;

            // 1. Potential spot?
            // 2. Don't overdo it.

            // 1. Potential spot?
            // outside and has a corpse.
            bool isInside = map.GetTileAt(pos).IsInside;
            if (!isInside && map.GetCorpsesAt(pos) != null)
            {
                reason = "that corpse will serve as a bait for";
                potentialSpot = true;
            }
            else
            {
                //  entering or leaving a building?
                bool wasInside = m_prevLocation.Map.GetTileAt(m_prevLocation.Position).IsInside;
                if (wasInside != isInside)
                {
                    reason = "protecting the building with";
                    potentialSpot = true;
                }
                else
                {
                    // ...or a door/window?
                    MapObject objThere = map.GetMapObjectAt(pos);
                    if (objThere != null && objThere is DoorWindow)
                    {
                        reason = "protecting the doorway with";
                        potentialSpot = true;
                    }
                    // ...or an exit?
                    else if (map.GetExitAt(pos) != null)
                    {
                        reason = "protecting the exit with";
                        potentialSpot = true;
                    }
                }
            }
            if (!potentialSpot)
                return false;

            // 2. Don't overdo it.
            // Never drop more than 3 traps.
            Inventory itemsThere = map.GetItemsAt(pos);
            if (itemsThere != null)
            {
                int countActivated = itemsThere.CountItemsMatching((it) =>
                {
                    ItemTrap trap = it as ItemTrap;
                    if (trap == null) return false;
                    return trap.IsActivated;
                });
                if (countActivated > 3)
                    return false;
            }
            // TODO Need at least 2 neighbouring non adjacent tiles free of activated traps.

            // ok!
            return true;
        }

        protected ActorAction BehaviorBuildSmallFortification(RogueGame game)
        {
            // don't bother if no carpentry skill or not enough material.
            if (m_Actor.Sheet.SkillTable.GetSkillLevel((int)Skills.IDs.CARPENTRY) == 0)
                return null;
            if (game.Rules.CountBarricadingMaterial(m_Actor) < game.Rules.ActorBarricadingMaterialNeedForFortification(m_Actor, false))
                return null;

            // pick a good adjacent tile.
            // good tiles are :
            // - in bounds, walkable, empty, not border.
            // - not exits.
            // - doorways.
            // eval is random.
            Map map = m_Actor.Location.Map;
            ChoiceEval<Direction> choice = Choose<Direction>(game, Direction.COMPASS_LIST,
                (dir) =>
                {
                    Point pt = m_Actor.Location.Position + dir;
                    if (!map.IsInBounds(pt))
                        return false;
                    if (!map.IsWalkable(pt))
                        return false;
                    if (map.IsOnMapBorder(pt.X, pt.Y))
                        return false;
                    if (map.GetActorAt(pt) != null)
                        return false;
                    if (map.GetExitAt(pt) != null)
                        return false;
                    return IsDoorwayOrCorridor(game, map, pt);
                },
                (dir) => game.Rules.Roll(0, 666),
                (a, b) => a > b);

            // if no choice, fail.
            if (choice == null)
                return null;

            // get pos.
            Point adj = m_Actor.Location.Position + choice.Choice;

            // if can't build there, fail.
            if (!game.Rules.CanActorBuildFortification(m_Actor, adj, false))
                return null;

            // ok!
            return new ActionBuildFortification(m_Actor, game, adj, false);
        }

        /// <summary>
        /// Try to make a line of large fortifications.
        /// </summary>
        /// <param name="game"></param>
        /// <returns></returns>
        protected ActorAction BehaviorBuildLargeFortification(RogueGame game, int startLineChance)
        {
            // don't bother if no carpentry skill or not enough material.
            if (m_Actor.Sheet.SkillTable.GetSkillLevel((int)Skills.IDs.CARPENTRY) == 0)
                return null;
            if (game.Rules.CountBarricadingMaterial(m_Actor) < game.Rules.ActorBarricadingMaterialNeedForFortification(m_Actor, true))
                return null;

            // pick a good adjacent tile.
            // good tiles are :
            // - not exit.
            // - not map border.
            // - outside and anchor/continue wall.
            // all things being equal, eval is random.
            Map map = m_Actor.Location.Map;
            ChoiceEval<Direction> choice = Choose<Direction>(game, Direction.COMPASS_LIST,
                (dir) =>
                {
                    Point pt = m_Actor.Location.Position + dir;
                    if (!map.IsInBounds(pt))
                        return false;
                    if (!map.IsWalkable(pt))
                        return false;
                    if (map.IsOnMapBorder(pt.X, pt.Y))
                        return false;
                    if (map.GetActorAt(pt) != null)
                        return false;
                    if (map.GetExitAt(pt) != null)
                        return false;

                    // outside.
                    if (map.GetTileAt(pt.X, pt.Y).IsInside)
                        return false;

                    // count stuff there.
                    int wallsAround = map.CountAdjacentInMap(pt, (ptAdj) => !map.GetTileAt(ptAdj).Model.IsWalkable);
                    int lfortsAround = map.CountAdjacentInMap(pt,
                        (ptAdj) =>
                        {
                            Fortification f = map.GetMapObjectAt(ptAdj) as Fortification;
                            return f != null && !f.IsTransparent;
                        });

                    // good spot?
                    if (wallsAround == 3 && lfortsAround == 0 && game.Rules.RollChance(startLineChance))
                        // fort line anchor.
                        return true;
                    if (wallsAround == 0 && lfortsAround == 1)
                        // fort line continuation.
                        return true;

                    // nope.
                    return false;
                },
                (dir) => game.Rules.Roll(0, 666),
                (a, b) => a > b);

            // if no choice, fail.
            if (choice == null)
                return null;

            // get pos.
            Point adj = m_Actor.Location.Position + choice.Choice;

            // if can't build there, fail.
            if (!game.Rules.CanActorBuildFortification(m_Actor, adj, true))
                return null;

            // ok!
            return new ActionBuildFortification(m_Actor, game, adj, true);
        }

        #endregion
        #region Breaking objects
        protected ActorAction BehaviorAttackBarricade(RogueGame game)
        {
            // find barricades.
            Map map = m_Actor.Location.Map;
            List<Point> adjBarricades = map.FilterAdjacentInMap(m_Actor.Location.Position,
                (pt) =>
                {
                    DoorWindow door = map.GetMapObjectAt(pt) as DoorWindow;
                    return (door != null && door.IsBarricaded);
                });

            // if none, fail.
            if (adjBarricades == null)
                return null;

            // try to attack one at random.
            DoorWindow randomBarricade = map.GetMapObjectAt(adjBarricades[game.Rules.Roll(0, adjBarricades.Count)]) as DoorWindow;
            ActionBreak attackBarricade = new ActionBreak(m_Actor, game, randomBarricade);
            if (attackBarricade.IsLegal())
                return attackBarricade;

            // nope :(
            return null;
        }

        protected ActorAction BehaviorAssaultBreakables(RogueGame game, HashSet<Point> fov)
        {
            // find all barricades & breakables in fov.
            Map map = m_Actor.Location.Map;
            List<Percept> breakables = null;
            foreach (Point pt in fov)
            {
                MapObject mapObj = map.GetMapObjectAt(pt);
                if (mapObj == null)
                    continue;
                if (!mapObj.IsBreakable)
                    continue;
                if (breakables == null)
                    breakables = new List<Percept>();
                breakables.Add(new Percept(mapObj, map.LocalTime.TurnCounter, new Location(map, pt)));
            }

            // if nothing to assault, fail.
            if (breakables == null)
                return null;

            // get nearest.
            Percept nearest = FilterNearest(game, breakables);

            // if adjacent, try to break it.
            if (game.Rules.IsAdjacent(m_Actor.Location.Position, nearest.Location.Position))
            {
                ActionBreak breakIt = new ActionBreak(m_Actor, game, nearest.Percepted as MapObject);
                if (breakIt.IsLegal())
                    return breakIt;
                else
                    // illegal, don't bother with it.
                    return null;
            }

            // not adjacent, try to get there.
            return BehaviorIntelligentBumpToward(game, nearest.Location.Position, true, true);
        }

        #endregion
        #region Pushing
        protected ActorAction BehaviorPushNonWalkableObject(RogueGame game)
        {
            // check ability.
            if (!game.Rules.HasActorPushAbility(m_Actor))
                return null;

            // find adjacent pushables that are blocking for us.
            Map map = m_Actor.Location.Map;
            List<Point> adjPushables = map.FilterAdjacentInMap(m_Actor.Location.Position,
                (pt) =>
                {
                    MapObject obj = map.GetMapObjectAt(pt);
                    if (obj == null)
                        return false;
                    // ignore if we can walk through it.
                    if (obj.IsWalkable)
                        return false;
                    // finally only if we can push it.
                    return game.Rules.CanActorPush(m_Actor, obj);
                });

            // if none, fail.
            if (adjPushables == null)
                return null;

            // try to push one at random in a random direction.
            MapObject randomPushable = map.GetMapObjectAt(adjPushables[game.Rules.Roll(0, adjPushables.Count)]);
            ActionPush pushIt = new ActionPush(m_Actor, game, randomPushable, game.Rules.RollDirection());
            if (pushIt.IsLegal())
                return pushIt;

            // nope :(
            return null;
        }
        #endregion
        #region Charging enemy
        // alpha10 added break and push
        protected ActorAction BehaviorChargeEnemy(RogueGame game, Percept target, bool canCheckBreak, bool canCheckPush)
        {
            // try melee attack first.
            ActorAction attack = BehaviorMeleeAttack(game, target);
            if (attack != null)
                return attack;

            Actor enemy = target.Percepted as Actor;

            // if we are tired and next to enemy, use med or rest to recover our STA for the next attack.
            if (game.Rules.IsActorTired(m_Actor) && game.Rules.IsAdjacent(m_Actor.Location, target.Location))
            {
                // meds?
                ActorAction useMed = BehaviorUseMedecine(game, 0, 1, 0, 0, 0);
                if (useMed != null)
                    return useMed;

                // rest!
                return new ActionWait(m_Actor, game);
            }

            // then try getting closer.
            ActorAction bumpAction = BehaviorIntelligentBumpToward(game, target.Location.Position, canCheckBreak, canCheckPush);
            if (bumpAction != null)
            {
                // do we rush?
                // we want to rush if enemy has a range advantage, we want to get closer asap.
                if (m_Actor.CurrentRangedAttack.Range < enemy.CurrentRangedAttack.Range)
                    RunIfPossible(game.Rules);

                // chaaarge!
                return bumpAction;
            }

            // failed.
            return null;
        }
        #endregion
        #region Fight or Flee
        /// <summary>
        /// Engage in mele fight with the nearest reachable enemy or flee from him.
        /// </summary>
        /// <param name="game"></param>
        /// <param name="enemies"></param>
        /// <param name="hasVisibleLeader"></param>
        /// <param name="isLeaderFighting"></param>
        /// <param name="courage"></param>
        /// <param name="emotes">0 = flee; 1 = trapped; 2 = charge</param>
        /// <returns></returns>
        protected ActorAction BehaviorFightOrFlee(RogueGame game, List<Percept> enemies, bool hasVisibleLeader, bool isLeaderFighting, ActorCourage courage,
            string[] emotes,
            RouteFinder.SpecialActions allowedChargeActions)
        {
            // alpha10 filter out unreachables if no ranged weapon equipped
            // (we shouldnt be here anyway if we have a ranged weapon)
            if (m_Actor.GetEquippedRangedWeapon() == null)
            {
                FilterOutUnreachablePercepts(game, ref enemies, allowedChargeActions);
                if (enemies.Count == 0)
                    return null;
            }

            Percept nearestEnemy = FilterNearest(game, enemies);

            bool decideToFlee;
            bool doRun = false;  // don't run by default.

            Actor enemy = nearestEnemy.Percepted as Actor;

            // alpha10
            // get enemy attack
            Attack enemyAttack = GetActorAttack(game, enemy);

            // always try to heal if enemy next attack could kill us
            if (m_Actor.HitPoints - enemyAttack.DamageValue <= 0)
            {
                ActorAction healAction = BehaviorUseMedecine(game, 10, 0, 0, 0, 0);
                if (healAction != null)
                {
                    m_Actor.Activity = Activity.FIGHTING;
                    m_Actor.TargetActor = enemy;
                    return healAction;
                }
            }

            // get safe range from enemy, just out of his reach.
            int safeRange = Math.Max(2, enemyAttack.Range + 1);  // melee attack range is 0 not 1!
            int distToEnemy = game.Rules.GridDistance(m_Actor.Location.Position, enemy.Location.Position);
            bool inSafeRange = distToEnemy >= safeRange;

            // Cases that are a no brainer, in this order:
            // 1. Always fight if he has a ranged weapon.
            // 2. Always fight if law enforcer vs murderer.
            // 3. Always flee melee if tired

            // 1. Always fight if enemy has ranged weapon.
            // if we are here, it means we can't shoot him, cause firing behavior has priority.
            // so we want to get a chance at melee a shooting enemy.
            if (HasEquipedRangedWeapon(enemy))
                decideToFlee = false;
            // 2. Always fight if law enforcer vs murderer.
            // do our duty.
            else if (m_Actor.Model.Abilities.IsLawEnforcer && enemy.MurdersCounter > 0)
                decideToFlee = false;
            // 3. Always flee melee if tired
            else if (game.Rules.IsActorTired(m_Actor) && distToEnemy == 1)
                decideToFlee = true;
            // Case need more analysis.
            else
            {
                if (m_Actor.Leader != null)
                {
                    //////////////////////////
                    // Fighting with a leader.
                    //////////////////////////
                    #region
                    switch (courage)
                    {
                        case ActorCourage.COWARD:
                            // always flee and run.
                            decideToFlee = true;
                            doRun = true;
                            break;

                        case ActorCourage.CAUTIOUS:
                            // kite.
                            decideToFlee = WantToEvadeMelee(game, m_Actor, courage, enemy);
                            doRun = !HasSpeedAdvantage(game, m_Actor, enemy);
                            break;

                        case ActorCourage.COURAGEOUS:
                            // fight if leader is fighting.
                            // otherwise kite.
                            if (isLeaderFighting)
                                decideToFlee = false;
                            else
                            {
                                decideToFlee = WantToEvadeMelee(game, m_Actor, courage, enemy);
                                doRun = !HasSpeedAdvantage(game, m_Actor, enemy);
                            }
                            break;

                        default:
                            throw new ArgumentOutOfRangeException("unhandled courage");
                    }
                    #endregion
                }
                else
                {
                    ////////////////////////
                    // Leaderless fighting.
                    ////////////////////////
                    #region
                    switch (courage)
                    {
                        case ActorCourage.COWARD:
                            // always flee and run.
                            decideToFlee = true;
                            doRun = true;
                            break;

                        case ActorCourage.CAUTIOUS:
                        case ActorCourage.COURAGEOUS:
                            // kite.
                            decideToFlee = WantToEvadeMelee(game, m_Actor, courage, enemy);
                            doRun = !HasSpeedAdvantage(game, m_Actor, enemy);
                            break;
                        default:
                            throw new ArgumentOutOfRangeException("unhandled courage");
                    }
                    #endregion
                }
            }

            // alpha10
            // Improve STA management a bit.
            // Cancel running if this would make us tired and we don't have equipped a ranged weapon so keeping
            // TODO -- consider other cases were running would be a waste of STA.
            if (doRun && WillTireAfterRunning(game, m_Actor))
            {
                if (!HasEquipedRangedWeapon(m_Actor))
                    doRun = false;
            }

            // alpha10
            // If we have no ranged weapon and target is unreachable, no point charging him as we can't get into
            // melee contact. Flee if enemy has equipped a ranged weapon and do nothing if not.
            if (!decideToFlee)
            {
                if (!HasAnyRangedWeaponWithAmmo())
                {
                    // check route
                    if (!CanReachSimple(game, enemy.Location.Position, allowedChargeActions))
                    {
                        ItemRangedWeapon enemyWpn = enemy.GetEquippedWeapon() as ItemRangedWeapon;
                        if (enemyWpn != null)
                        {
                            // even if out of ammo assumes he will reload or find ammo, better be safe.
                            decideToFlee = true;
                        }
                        else
                        {
                            // enemy has no ranged weapon and unreachable, possibly no threat to us better
                            // do something else instead.
                            return null;
                        }
                    }
                }
            }

            // flee or fight?
            if (decideToFlee)
            {
                #region Flee
                ////////////////////////////////////////////////////////////////////////////////////////
                // Try to:
                // 1. Close door between me and the enemy if he can't open it right after we closed it.
                // 2. Barricade door between me and the enemy.
                // 3. Use exit?
                // 4. Use medecine?
                // 5. Rest if tired and at safe distance  // alpha10
                // 6. Walk/run away.
                // 7. Blocked, turn to fight.
                ////////////////////////////////////////////////////////////////////////////////////////

                // emote?
                if (m_Actor.Model.Abilities.CanTalk && game.Rules.RollChance(EMOTE_FLEE_CHANCE))
                    game.DoEmote(m_Actor, String.Format("{0} {1}!", emotes[0], enemy.Name));

                // 1. Close door between me and the enemy if he can't open it right after we closed it.
                #region
                if (m_Actor.Model.Abilities.CanUseMapObjects)
                {
                    ChoiceEval<Direction> closeDoorBetweenDirection = Choose<Direction>(game, Direction.COMPASS_LIST,
                        (dir) =>
                        {
                            Point pos = m_Actor.Location.Position + dir;
                            DoorWindow door = m_Actor.Location.Map.GetMapObjectAt(pos) as DoorWindow;
                            if (door == null)
                                return false;
                            if (!IsBetween(game, m_Actor.Location.Position, pos, enemy.Location.Position))
                                return false;
                            if (!game.Rules.IsClosableFor(m_Actor, door))
                                return false;
                            if (game.Rules.GridDistance(pos, enemy.Location.Position) == 1 && game.Rules.IsClosableFor(enemy, door))
                                return false;
                            return true;
                        },
                        (dir) =>
                        {
                            return game.Rules.Roll(0, 666);  // random eval, all things being equal.
                        },
                        (a, b) => a > b);
                    if (closeDoorBetweenDirection != null)
                    {
                        return new ActionCloseDoor(m_Actor, game, m_Actor.Location.Map.GetMapObjectAt(m_Actor.Location.Position + closeDoorBetweenDirection.Choice) as DoorWindow);
                    }
                }
                #endregion

                // 2. Barricade door between me and the enemy.
                #region
                if (m_Actor.Model.Abilities.CanBarricade)
                {
                    ChoiceEval<Direction> barricadeDoorBetweenDirection = Choose<Direction>(game, Direction.COMPASS_LIST,
                        (dir) =>
                        {
                            Point pos = m_Actor.Location.Position + dir;
                            DoorWindow door = m_Actor.Location.Map.GetMapObjectAt(pos) as DoorWindow;
                            if (door == null)
                                return false;
                            if (!IsBetween(game, m_Actor.Location.Position, pos, enemy.Location.Position))
                                return false;
                            if (!game.Rules.CanActorBarricadeDoor(m_Actor, door))
                                return false;
                            return true;
                        },
                        (dir) =>
                        {
                            return game.Rules.Roll(0, 666);  // random eval, all things being equal.
                        },
                        (a, b) => a > b);
                    if (barricadeDoorBetweenDirection != null)
                    {
                        return new ActionBarricadeDoor(m_Actor, game, m_Actor.Location.Map.GetMapObjectAt(m_Actor.Location.Position + barricadeDoorBetweenDirection.Choice) as DoorWindow);
                    }
                }
                #endregion

                // 3. Use exit?
                #region
                if (m_Actor.Model.Abilities.AI_CanUseAIExits &&
                    game.Rules.RollChance(FLEE_THROUGH_EXIT_CHANCE))
                {
                    ActorAction useExit = BehaviorUseExit(game, UseExitFlags.NONE);
                    if (useExit != null)
                    {
                        bool doUseExit = true;

                        // Exception : if follower use exit only if leading to our leader.
                        // we don't want to leave our leader behind (mostly annoying for the player).
                        if (m_Actor.HasLeader)
                        {
                            Exit exitThere = m_Actor.Location.Map.GetExitAt(m_Actor.Location.Position);
                            if (exitThere != null) // well. who knows?
                                doUseExit = (m_Actor.Leader.Location.Map == exitThere.ToMap);
                        }

                        // do it?
                        if (doUseExit)
                        {
                            m_Actor.Activity = Activity.FLEEING;
                            return useExit;
                        }
                    }
                }
                #endregion

                // 4. Use medecine?
                #region
                // when to use medecine? only when fighting vs an unranged enemy and not in contact.
                if (enemy.GetEquippedRangedWeapon() == null && !game.Rules.IsAdjacent(m_Actor.Location, enemy.Location))
                {
                    ActorAction medAction = BehaviorUseMedecine(game, 2, 2, 1, 0, 0);
                    if (medAction != null)
                    {
                        m_Actor.Activity = Activity.FLEEING;
                        return medAction;
                    }
                }
                #endregion

                // alpha10
                // 5. Rest if tired and at safe distance
                #region
                if (game.Rules.IsActorTired(m_Actor))
                {
                    if (game.Rules.GridDistance(m_Actor.Location.Position, enemy.Location.Position) >= safeRange)
                    {
                        m_Actor.Activity = Activity.FLEEING;
                        return new ActionWait(m_Actor, game);
                    }
                }
                #endregion

                // 6. Walk/run away.
                #region
                ActorAction bumpAction = BehaviorWalkAwayFrom(game, enemies);
                if (bumpAction != null)
                {
                    if (doRun)
                        RunIfPossible(game.Rules);
                    m_Actor.Activity = Activity.FLEEING;
                    return bumpAction;
                }
                #endregion

                // 7. Blocked, turn to fight.
                #region
                if (bumpAction == null)
                {
                    // fight!
                    if (IsAdjacentToEnemy(game, enemy))
                    {
                        // emote?
                        if (m_Actor.Model.Abilities.CanTalk && game.Rules.RollChance(EMOTE_FLEE_TRAPPED_CHANCE))
                            game.DoEmote(m_Actor, emotes[1], true);

                        return BehaviorMeleeAttack(game, nearestEnemy);
                    }
                }
                #endregion

                #endregion
            }
            else
            {
                #region Fight
                ActorAction attackAction = BehaviorChargeEnemy(game, nearestEnemy, true, true);
                if (attackAction != null)
                {
                    // emote?
                    if (m_Actor.Model.Abilities.CanTalk && game.Rules.RollChance(EMOTE_CHARGE_CHANCE))
                        game.DoEmote(m_Actor, String.Format("{0} {1}!", emotes[2], enemy.Name, true));

                    // chaaarge!
                    m_Actor.Activity = Activity.FIGHTING;
                    m_Actor.TargetActor = nearestEnemy.Percepted as Actor;
                    return attackAction;
                }
                #endregion
            }

            // nope.
            return null;
        }
        #endregion
        #region Explosives
        protected ActorAction BehaviorFleeFromExplosives(RogueGame game, List<Percept> itemStacks)
        {
            // if no items in view, don't bother.
            if (itemStacks == null || itemStacks.Count == 0)
                return null;

            // filter stacks that have primed explosives.
            List<Percept> primedExplosives = Filter(game, itemStacks,
                (p) =>
                {
                    Inventory stack = p.Percepted as Inventory;
                    if (stack == null || stack.IsEmpty)
                        return false;
                    foreach (Item it in stack.Items)
                    {
                        ItemPrimedExplosive explosive = it as ItemPrimedExplosive;
                        if (explosive == null)
                            continue;
                        // found a primed explosive.
                        return true;
                    }
                    // no primed explosive in this stack.
                    return false;
                });

            // if no primed explosive in sight, no worries.
            if (primedExplosives == null || primedExplosives.Count == 0)
                return null;

            // run away from primed explosives!
            ActorAction runAway = BehaviorWalkAwayFrom(game, primedExplosives);
            if (runAway == null)
                return null;
            RunIfPossible(game.Rules);
            return runAway;
        }

        protected ActorAction BehaviorThrowGrenade(RogueGame game, HashSet<Point> fov, List<Percept> enemies)
        {
            // don't bother if no enemies.
            if (enemies == null || enemies.Count == 0)
                return null;

            // only throw if enough enemies.
            if (enemies.Count < 3)
                return null;

            // don't bother if no grenade in inventory.
            Inventory inv = m_Actor.Inventory;
            if (inv == null || inv.IsEmpty)
                return null;
            ItemGrenade grenade = GetFirstGrenade((it) => !IsItemTaboo(it));
            if (grenade == null)
                return null;
            ItemGrenadeModel model = grenade.Model as ItemGrenadeModel;

            // find the best throw point : a spot with many enemies around and no friend to hurt.
            #region
            int maxThrowRange = game.Rules.ActorMaxThrowRange(m_Actor, model.MaxThrowDistance);
            Point? bestSpot = null;
            int bestSpotScore = 0;
            foreach (Point pt in fov)
            {
                // never throw within blast radius - don't suicide ^^
                if (game.Rules.GridDistance(m_Actor.Location.Position, pt) <= model.BlastAttack.Radius)
                    continue;

                // if we can't throw there, don't bother.
                if (game.Rules.GridDistance(m_Actor.Location.Position, pt) > maxThrowRange)
                    continue;
                if (!LOS.CanTraceThrowLine(m_Actor.Location, pt, maxThrowRange, null))
                    continue;

                // compute interest of throwing there.
                // - pro: number of enemies within blast radius.
                // - cons: friend in radius.
                // don't bother checking for blast wave actuallly reaching the targets.
                int score = 0;
                for (int x = pt.X - model.BlastAttack.Radius; x <= pt.X + model.BlastAttack.Radius; x++)
                    for (int y = pt.Y - model.BlastAttack.Radius; y <= pt.Y + model.BlastAttack.Radius; y++)
                    {
                        if (!m_Actor.Location.Map.IsInBounds(x, y))
                            continue;
                        Actor otherActor = m_Actor.Location.Map.GetActorAt(x, y);
                        if (otherActor == null)
                            continue;
                        if (otherActor == m_Actor)
                            continue;
                        int blastDistToTarget = game.Rules.GridDistance(pt, otherActor.Location.Position);
                        if (blastDistToTarget > model.BlastAttack.Radius)
                            continue;

                        // other actor within blast radius.
                        // - if friend, abort and never throw there.
                        // - if enemy, increase score.
                        if (game.Rules.AreEnemies(m_Actor, otherActor))
                        {
                            // score = damage inflicted vs target toughness(max hp).
                            // -> means it is better to hurt badly one big enemy than a few scratch on a group of weaklings.
                            int value = game.Rules.BlastDamage(blastDistToTarget, model.BlastAttack) * game.Rules.ActorMaxHPs(otherActor);
                            score += value;
                        }
                        else
                        {
                            score = -1;
                            break;
                        }
                    }

                // if negative score (eg: friends get hurt), don't throw.
                if (score <= 0)
                    continue;

                // possible spot. best one?
                if (bestSpot == null || score > bestSpotScore)
                {
                    bestSpot = pt;
                    bestSpotScore = score;
                }
            }
            #endregion

            // if no throw point, don't.
            if (bestSpot == null)
                return null;

            // equip then throw.
            if (!grenade.IsEquipped)
            {
                // alpha10 mark right hand as taboo so behavior BehaviorEquipBestItems will not undo us and loop forever
                MarkEquipmentSlotAsTaboo(DollPart.RIGHT_HAND);

                Item otherEquipped = m_Actor.GetEquippedWeapon();
                if (otherEquipped != null)
                    return new ActionUnequipItem(m_Actor, game, otherEquipped);
                else
                    return new ActionEquipItem(m_Actor, game, grenade);
            }
            else
            {
                // alpha10 release right hand from taboo so behavior BehaviorEquipBestItems can use right hand
                UnmarkEquipmentSlotAsTaboo(DollPart.RIGHT_HAND);

                ActorAction throwAction = new ActionThrowGrenade(m_Actor, game, bestSpot.Value);
                if (!throwAction.IsLegal())
                    return null;
                return throwAction;
            }
        }
        #endregion
        #region Law enforcement
        protected ActorAction BehaviorEnforceLaw(RogueGame game, List<Percept> percepts, out Actor target)
        {
            target = null;

            // sanity checks.
            if (!m_Actor.Model.Abilities.IsLawEnforcer)
                return null;
            if (percepts == null)
                return null;

            // filter murderers that are not our enemies yet.
            List<Percept> murderers = FilterActors(game, percepts,
                (a) => a.MurdersCounter > 0 && !game.Rules.AreEnemies(m_Actor, a));

            // if none, nothing to do.
            if (murderers == null || murderers.Count == 0)
                return null;

            // get nearest murderer.
            Percept nearestMurderer = FilterNearest(game, murderers);
            target = nearestMurderer.Percepted as Actor;

            // roll against target unsuspicious skill.
            if (game.Rules.RollChance(game.Rules.ActorUnsuspicousChance(m_Actor, target)))
            {
                // emote.
                game.DoEmote(target, String.Format("moves unnoticed by {0}.", m_Actor.Name));

                // done.
                return null;
            }

            // mmmmhhh. who's that?
            game.DoEmote(m_Actor, String.Format("takes a closer look at {0}.", target.Name));

            // then roll chance to spot and recognize him as murderer.
            int spotChance = game.Rules.ActorSpotMurdererChance(m_Actor, target);

            // if did not spot, nothing to do.
            if (!game.Rules.RollChance(spotChance))
                return null;

            // make him our enemy and tell him!
            game.DoMakeAggression(m_Actor, target);
            return new ActionSay(m_Actor, game, target,
                String.Format("HEY! YOU ARE WANTED FOR {0} MURDER{1}!", target.MurdersCounter, target.MurdersCounter > 1 ? "s" : ""), RogueGame.Sayflags.IS_IMPORTANT | RogueGame.Sayflags.IS_DANGER);
        }
        #endregion
        #region Animals
        protected ActorAction BehaviorGoEatFoodOnGround(RogueGame game, List<Percept> stacksPercepts)
        {
            // nope if no percepts.
            if (stacksPercepts == null)
                return null;

            // filter stacks with food.
            List<Percept> foodStacks = Filter(game, stacksPercepts, (p) =>
            {
                Inventory inv = p.Percepted as Inventory;
                return inv.HasItemOfType(typeof(ItemFood));
            });

            // nope if no food stacks.
            if (foodStacks == null)
                return null;

            // either 1) eat there or 2) go get it

            // 1) check food here.
            Inventory invThere = m_Actor.Location.Map.GetItemsAt(m_Actor.Location.Position);
            if (invThere != null && invThere.HasItemOfType(typeof(ItemFood)))
            {
                // eat the first food we get.
                Item eatIt = invThere.GetFirstByType(typeof(ItemFood));
                return new ActionEatFoodOnGround(m_Actor, game, eatIt);
            }
            // 2) go to nearest food.
            Percept nearest = FilterNearest(game, foodStacks);
            return BehaviorStupidBumpToward(game, nearest.Location.Position, false, false);
        }
        #endregion
        #region Corpses & Revival
        protected ActorAction BehaviorGoEatCorpse(RogueGame game, List<Percept> corpsesPercepts)
        {
            // nope if no percepts.
            if (corpsesPercepts == null)
                return null;

            // if undead, must need health.
            if (m_Actor.Model.Abilities.IsUndead && m_Actor.HitPoints >= game.Rules.ActorMaxHPs(m_Actor))
                return null;

            // either 1) eat corpses or 2) go get them.

            // 1) check corpses here.
            List<Corpse> corpses = m_Actor.Location.Map.GetCorpsesAt(m_Actor.Location.Position);
            if (corpses != null)
            {
                // eat the first corpse.
                Corpse eatIt = corpses[0];
                if (game.Rules.CanActorEatCorpse(m_Actor, eatIt))
                    return new ActionEatCorpse(m_Actor, game, eatIt);
            }
            // 2) go to nearest corpses.
            Percept nearest = FilterNearest(game, corpsesPercepts);
            return m_Actor.Model.Abilities.IsIntelligent ?
                    BehaviorIntelligentBumpToward(game, nearest.Location.Position, true, true) :
                    BehaviorStupidBumpToward(game, nearest.Location.Position, true, true);
        }

        /// <summary>
        /// TrRy to revive non-enemy corpses.
        /// </summary>
        /// <param name="game"></param>
        /// <param name="corpsesPercepts"></param>
        /// <returns></returns>
        protected ActorAction BehaviorGoReviveCorpse(RogueGame game, List<Percept> corpsesPercepts)
        {
            // nope if no percepts.
            if (corpsesPercepts == null)
                return null;

            // make sure we have the basics : medic skill & medikit item.
            if (m_Actor.Sheet.SkillTable.GetSkillLevel((int)Skills.IDs.MEDIC) == 0)
                return null;
            if (!HasItemOfModel(game.GameItems.MEDIKIT))
                return null;

            // keep only corpses stacks where we can revive at least one corpse.
            List<Percept> revivables = Filter(game, corpsesPercepts, (p) =>
                {
                    List<Corpse> corpsesThere = p.Percepted as List<Corpse>;
                    foreach (Corpse c in corpsesThere)
                    {
                        // dont revive enemies!
                        if (game.Rules.CanActorReviveCorpse(m_Actor, c) && !game.Rules.AreEnemies(m_Actor, c.DeadGuy))
                            return true;
                    }
                    return false;
                });
            if (revivables == null)
                return null;

            // either 1) revive corpse or 2) go get them.

            // 1) check corpses here.
            List<Corpse> corpses = m_Actor.Location.Map.GetCorpsesAt(m_Actor.Location.Position);
            if (corpses != null)
            {
                // get the first corpse we can revive.
                foreach (Corpse c in corpses)
                {
                    if (game.Rules.CanActorReviveCorpse(m_Actor, c) && !game.Rules.AreEnemies(m_Actor, c.DeadGuy))
                        return new ActionReviveCorpse(m_Actor, game, c);
                }
            }
            // 2) go to nearest revivable.
            Percept nearest = FilterNearest(game, revivables);
            return m_Actor.Model.Abilities.IsIntelligent ?
                    BehaviorIntelligentBumpToward(game, nearest.Location.Position, false, false) :
                    BehaviorStupidBumpToward(game, nearest.Location.Position, false, false);
        }

        #endregion
    }
}
