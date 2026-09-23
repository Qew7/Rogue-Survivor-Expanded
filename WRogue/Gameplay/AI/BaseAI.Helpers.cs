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
        #region Behaviors helpers

        #region Messages
        string MakeCentricLocationDirection(RogueGame game, Location from, Location to)
        {
            // if not same location, just says the map.
            if (from.Map != to.Map)
            {
                return String.Format("in {0}", to.Map.Name);
            }

            // same location, says direction.
            Point fromPos = from.Position;
            Point toPos = to.Position;
            Point vDir = new Point(toPos.X - fromPos.X, toPos.Y - fromPos.Y);
            return String.Format("{0} tiles to the {1}", (int)game.Rules.StdDistance(vDir), Direction.ApproximateFromVector(vDir));
        }
        #endregion


        #region Running
        protected void RunIfPossible(Rules rules)
        {
            m_Actor.IsRunning = rules.CanActorRun(m_Actor);  // alpha10 fix
        }
        #endregion

        #region Distances & Safety
        protected int GridDistancesSum(Rules rules, Point from, List<Percept> goals)
        {
            int sum = 0;
            foreach (Percept to in goals)
                sum += rules.GridDistance(from, to.Location.Position);
            return sum;
        }

        // alpha10 new safety scoring
        /// <summary>
        /// Compute safety from a list of dangers at a given position.
        /// </summary>
        /// <param name="rules"></param>
        /// <param name="from">position to compute the safety</param>
        /// <param name="dangers">dangers to avoid</param>
        /// <returns>a heuristic value, the higher the better the safety from the dangers; base 100</returns>
        protected int SafetyFrom(RogueGame game, Point from, List<Percept> dangers)
        {
            Rules rules = game.Rules;
            Map map = m_Actor.Location.Map;
            int distFromDangers = GridDistancesSum(rules, from, dangers);
            int currentDistFromDangers = GridDistancesSum(rules, m_Actor.Location.Position, dangers);

            int score = 0;

            // Base score is 100*distance to danger then add minor heuristics.
            if (dangers.Count > 0)
                score = (100 * distFromDangers) / dangers.Count;

            // Heuristics
            // 1. Reward more potential escape tiles.
            // 2. Reward going outside/inside if majority of dangers are inside/outside.
            // 3. Reward ladder/stairs exits.
            // 4. If can tire, prefer not jumping.
            // 5. Punish stepping into traps.
            // 6. Punish moving on or adj to explosives.

            // 1. Reward more potential escape tiles.
            // "Escape tile" = we can walk into it or open a door.
            // Better if it is farther to dangers. Better if ladders/stairs exit.
            foreach (Direction d in Direction.COMPASS)
            {
                Point to = from + d;
                bool isEscape = rules.IsWalkableFor(m_Actor, map, to.X, to.Y);
                if (!isEscape && m_Actor.Model.Abilities.CanUseMapObjects)
                {
                    DoorWindow door = map.GetMapObjectAt(to) as DoorWindow;
                    if (door != null && !door.IsBarricaded)
                        isEscape = true;
                }
                if (isEscape)
                {
                    score += 20;
                    if (distFromDangers >= currentDistFromDangers)
                    {
                        score += 20;
                    }
                    else if (distFromDangers == currentDistFromDangers)
                    {
                        score += 10;
                    }
                    if (m_Actor.Model.Abilities.AI_CanUseAIExits)
                    {
                        Exit adjExit = map.GetExitAt(to);
                        if (adjExit != null)
                            score += 10;
                    }
                }
            }

            // 2. Reward going outside/inside if majority of dangers are inside/outside.
            bool isFromInside = map.GetTileAt(from).IsInside;
            int majorityDangersInside = 0;
            foreach (Percept p in dangers)
            {
                if (map.GetTileAt(p.Location.Position).IsInside)
                    ++majorityDangersInside;
                else
                    --majorityDangersInside;
            }
            if (isFromInside)
            {
                // from is inside, want that if majority dangers are outside.
                if (majorityDangersInside < 0) score += 100;
            }
            else
            {
                // from is outside, want that if majority dangers are inside.
                if (majorityDangersInside > 0) score += 100;
            }

            // 3. Reward ladder/stairs exits.
            if (m_Actor.Model.Abilities.AI_CanUseAIExits)
            {
                Exit exitThere = map.GetExitAt(from);
                if (exitThere != null && exitThere.IsAnAIExit && exitThere.ToMap.District == map.District)
                {
                    score += 200;
                }
            }

            // 4. If can tire, prefer not jumping.
            if (m_Actor.Model.Abilities.CanTire && m_Actor.Model.Abilities.CanJump)
            {
                MapObject obj = map.GetMapObjectAt(from);
                if (obj != null && obj.IsJumpable)
                {
                    score -= 50;
                }
            }

            // 5. Punish stepping into traps.
            // Less if has Light Feet
            if (IsAnyUnsafeDamagingTrapThere(game, map, from))
            {
                int lightFeetSkill = m_Actor.Sheet.SkillTable.GetSkillLevel((int)Skills.IDs.LIGHT_FEET);
                score -= 100 / (1 + lightFeetSkill);
            }

            // 6. Punish moving on or adj to explosives.
            if (IsAnyPrimedExplosiveThere(map, from))
                score -= 100;
            foreach (Direction d in Direction.COMPASS)
            {
                Point to = from + d;
                if (IsAnyPrimedExplosiveThere(map, to))
                    score -= 50;
            }

            // done
            return score;
        }
        #endregion


        #region Choice making
        protected ChoiceEval<_T_> Choose<_T_>(RogueGame game, List<_T_> listOfChoices,
            Func<_T_, bool> isChoiceValidFn,
            Func<_T_, float> evalChoiceFn,
            Func<float, float, bool> isBetterEvalThanFn)
        {
            //Console.Out.WriteLine("Evaluating choices");

            // Degenerate cases.
            if (listOfChoices.Count == 0)
            {
                //Console.Out.WriteLine("no choice.");
                return null;
            }

            // Find valid choices and best value.
            bool hasValue = false;
            float bestValue = 0;    // irrevelant for 1st value, use flag hasValue instead.
            List<ChoiceEval<_T_>> validChoices = new List<ChoiceEval<_T_>>(listOfChoices.Count);
            for (int i = 0; i < listOfChoices.Count; i++)
            {
                if (!isChoiceValidFn(listOfChoices[i]))
                    continue;

                float value_i = evalChoiceFn(listOfChoices[i]);
                if (float.IsNaN(value_i))
                    continue;

                validChoices.Add(new ChoiceEval<_T_>(listOfChoices[i], value_i));

                if (!hasValue || isBetterEvalThanFn(value_i, bestValue))
                {
                    hasValue = true;
                    bestValue = value_i;
                }
            }

            /*Console.Out.WriteLine("Evals {");
            for (int j = 0; j < validChoices.Count; j++)
            {
                Console.Out.WriteLine("  {0}", validChoices[j].ToString());
            }
            Console.Out.WriteLine("}");*/

            // Degenerate cases.
            if (validChoices.Count == 0)
            {
                //Console.Out.WriteLine("no valid choice!");
                return null;
            }
            if (validChoices.Count == 1)
            {
                return validChoices[0];
            }

            // Keep all the candidates that have the best value.
            List<ChoiceEval<_T_>> candidates = new List<ChoiceEval<_T_>>(validChoices.Count);
            for (int i = 0; i < validChoices.Count; i++)
                if (validChoices[i].Value == bestValue)
                    candidates.Add(validChoices[i]);

            /*Console.Out.WriteLine("Candidates {");
            for (int j = 0; j < candidates.Count; j++)
            {
                Console.Out.WriteLine("  {0}", candidates[j].ToString());
            }
            Console.Out.WriteLine("}");*/

            // Of all the candidates randomly choose one.
            int iChoice = game.Rules.Roll(0, candidates.Count);
            return candidates[iChoice];
        }

        // alpha10 evalChoiceFn now also accepts data param from isChoiceValidFn; eg: an action
        protected ChoiceEval<_DATA_> ChooseExtended<_T_, _DATA_>(RogueGame game,
            List<_T_> listOfChoices,
            Func<_T_, _DATA_> isChoiceValidFn,
            Func<_T_, _DATA_, float> evalChoiceFn,
            Func<float, float, bool> isBetterEvalThanFn)
        {
            //Console.Out.WriteLine("Evaluating choices");

            // Degenerate cases.
            if (listOfChoices.Count == 0)
            {
                //Console.Out.WriteLine("no choice.");
                return null;
            }

            // Find valid choices and best value.
            bool hasValue = false;
            float bestValue = 0;    // irrevelant for 1st value, use flag hasValue instead.
            List<ChoiceEval<_DATA_>> validChoices = new List<ChoiceEval<_DATA_>>(listOfChoices.Count);
            for (int i = 0; i < listOfChoices.Count; i++)
            {
                _DATA_ choiceData = isChoiceValidFn(listOfChoices[i]);
                if (choiceData == null)
                    continue;

                float value_i = evalChoiceFn(listOfChoices[i], choiceData);

                if (float.IsNaN(value_i))
                    continue;

                validChoices.Add(new ChoiceEval<_DATA_>(choiceData, value_i));

                if (!hasValue || isBetterEvalThanFn(value_i, bestValue))
                {
                    hasValue = true;
                    bestValue = value_i;
                }
            }

            /*Console.Out.WriteLine("Evals {");
            for (int j = 0; j < validChoices.Count; j++)
            {
                Console.Out.WriteLine("  {0}", validChoices[j].ToString());
            }
            Console.Out.WriteLine("}");*/

            // Degenerate cases.
            if (validChoices.Count == 0)
            {
                //Console.Out.WriteLine("no valid choice!");
                return null;
            }
            if (validChoices.Count == 1)
            {
                return validChoices[0];
            }

            // Keep all the candidates that have the best value.
            List<ChoiceEval<_DATA_>> candidates = new List<ChoiceEval<_DATA_>>(validChoices.Count);
            for (int i = 0; i < validChoices.Count; i++)
                if (validChoices[i].Value == bestValue)
                    candidates.Add(validChoices[i]);

            // TEST: if no best value, nope.
            if (candidates.Count == 0)
                return null;

            /*Console.Out.WriteLine("Candidates {");
            for (int j = 0; j < candidates.Count; j++)
            {
                Console.Out.WriteLine("  {0}", candidates[j].ToString());
            }
            Console.Out.WriteLine("}");*/

            // Of all the candidates randomly choose one.
            int iChoice = game.Rules.Roll(0, candidates.Count);
            return candidates[iChoice];
        }
        #endregion

        #region Action filtering
        /// <summary>
        /// Checks if an action can be considered a valid fleeing action : Move, OpenDoor, SwitchPlace.
        /// </summary>
        /// <param name="a"></param>
        /// <returns></returns>
        protected bool IsValidFleeingAction(ActorAction a)
        {
            return a != null && (a is ActionMoveStep || a is ActionOpenDoor || a is ActionSwitchPlace);
        }

        /// <summary>
        /// Checks if an action can be considered a valid wandering action : Move, SwitchPlace, Push, OpenDoor, Chat/Trade, Bash, GetFromContainer, Barricade.
        /// </summary>
        /// <param name="a"></param>
        /// <returns></returns>
        protected bool IsValidWanderAction(RogueGame game, ActorAction a)
        {
            return a != null &&
                (a is ActionMoveStep ||
                a is ActionSwitchPlace ||
                a is ActionPush ||
                a is ActionOpenDoor ||
                (a is ActionChat && (this.Directives.CanTrade || (a as ActionChat).Target == m_Actor.Leader)) ||
                a is ActionBashDoor ||
                a is ActionBreak ||  // alpha10.1 prevent rare cases of getting stuck or going back and forth but we rate it very very low
                a is ActionBashDoor || // alpha10.1 prevent rare cases of getting stuck or going back and forth but we rate it very low
                (a is ActionGetFromContainer && IsInterestingItemToOwn(game, (a as ActionGetFromContainer).Item, ItemSource.GROUND_STACK)) ||
                a is ActionBarricadeDoor);
        }

        /// <summary>
        /// Checks if an action can be considered a valid action to move toward a goal.
        /// Not valid actions : Chat, GetFromContainer, SwitchPowerGenerator, RechargeItemBattery
        /// </summary>
        /// <param name="a"></param>
        /// <returns></returns>
        protected bool IsValidMoveTowardGoalAction(ActorAction a)
        {
            return a != null &&
                !(a is ActionChat || a is ActionGetFromContainer || a is ActionSwitchPowerGenerator || a is ActionRechargeItemBattery);
        }
        #endregion

        #region Actors predicates
        protected bool HasNoFoodItems(Actor actor)
        {
            Inventory inv = actor.Inventory;
            if (inv == null || inv.IsEmpty)
                return true;
            return !inv.HasItemOfType(typeof(ItemFood));
        }

        protected bool IsSoldier(Actor actor)
        {
            return actor != null && actor.Controller is SoldierAI;
        }

        protected bool WouldLikeToSleep(RogueGame game, Actor actor)
        {
            return game.Rules.IsAlmostSleepy(actor) || game.Rules.IsActorSleepy(actor);
        }

        protected bool IsOccupiedByOther(Map map, Point position)
        {
            Actor other = map.GetActorAt(position);
            return other != null && other != m_Actor;
        }

        protected bool IsAdjacentToEnemy(RogueGame game, Actor actor)
        {
            if (actor == null)
                return false;

            Map map = actor.Location.Map;

            return map.HasAnyAdjacentInMap(actor.Location.Position,
                (pt) =>
                {
                    Actor other = map.GetActorAt(pt);
                    if (other == null)
                        return false;
                    return game.Rules.AreEnemies(actor, other);
                });
        }

        protected bool IsInside(Actor actor)
        {
            if (actor == null)
                return false;

            return actor.Location.Map.GetTileAt(actor.Location.Position.X, actor.Location.Position.Y).IsInside;
        }

        protected bool HasEquipedRangedWeapon(Actor actor)
        {
            return (actor.GetEquippedWeapon() as ItemRangedWeapon) != null;
        }

        protected ItemAmmo GetCompatibleAmmoItem(RogueGame game, ItemRangedWeapon rw, bool checkForUseNow)
        {
            if (m_Actor.Inventory == null)
                return null;

            // get first compatible ammo item.
            foreach (Item it in m_Actor.Inventory.Items)
            {
                ItemAmmo ammoIt = it as ItemAmmo;
                if (ammoIt == null)
                    continue;
                if (ammoIt.AmmoType == rw.AmmoType && (!checkForUseNow || game.Rules.CanActorUseItem(m_Actor, ammoIt)))
                    return ammoIt;
            }

            // failed.
            return null;
        }

        protected ItemRangedWeapon GetCompatibleRangedWeapon(RogueGame game, ItemAmmo am)
        {
            if (m_Actor.Inventory == null)
                return null;

            // get first compatible ammo item.
            foreach (Item it in m_Actor.Inventory.Items)
            {
                ItemRangedWeapon rangedIt = it as ItemRangedWeapon;
                if (rangedIt == null)
                    continue;
                if (rangedIt.AmmoType == am.AmmoType)
                    return rangedIt;
            }

            // failed.
            return null;
        }

        protected ItemBodyArmor GetBestBodyArmor(RogueGame game, Predicate<Item> fn)
        {
            if (m_Actor.Inventory == null)
                return null;

            // best = most PRO.
            int bestPRO = 0;
            ItemBodyArmor bestArmor = null;

            foreach (Item it in m_Actor.Inventory.Items)
            {
                if (fn != null && !fn(it))
                    continue;

                ItemBodyArmor armor = it as ItemBodyArmor;
                if (armor == null)
                    continue;

                int pro = armor.Protection_Hit + armor.Protection_Shot;
                if (pro > bestPRO)
                {
                    bestPRO = pro;
                    bestArmor = armor;
                }
            }

            // done.
            return bestArmor;
        }

        protected bool WantToEvadeMelee(RogueGame game, Actor actor, ActorCourage courage, Actor target)
        {
            ///////////////////////////////////////////////////////
            // Targets to evade or not:
            // 1. Yes : if fighting makes me tired vs a slower target (so i will lose my speed advantage by tiring) // alpha10 added slower target condition
            // 2. Yes : slower targets that will act next turn (kiting) and are targetting us.
            // 3. No  : target is weaker.
            // 4. Yes : actor is weaker.
            // 5. Unclear cases, utimately decide on courage.
            ///////////////////////////////////////////////////////

            bool hasSpeedAdvantage = game.Rules.ActorSpeed(actor) > game.Rules.ActorSpeed(target);

            // 1. Yes : if fighting makes me tired vs a slower target (so i will lose my speed advantage by tiring) // alpha10 added slower target condition
            if (hasSpeedAdvantage && WillTireAfterAttack(game, actor))
                return true;

            // 2. Yes : slower targets that will act next turn (kiting) and are targetting us.
            if (hasSpeedAdvantage)
            {
                // don't evade if we're gonna act again.
                if (game.Rules.WillActorActAgainBefore(actor, target))
                    return false;
                else
                {
                    // evade if he is targetting us.
                    if (target.TargetActor == actor)
                        return true;
                }
            }

            // get weaker actor in melee.
            Actor weakerOne = FindWeakerInMelee(game, m_Actor, target);

            // 3. No : target is weaker.
            if (weakerOne == target)
                return false;

            // 4. Yes : actor is weaker.
            if (weakerOne == m_Actor)
                return true;

            // 5. Unclear cases, utimately decide on courage.
            return courage == ActorCourage.COURAGEOUS ? false : true;
        }

        /// <summary>
        /// Get which of the two actor can be considered as a weaker one in a melee fight.
        /// </summary>
        /// <returns>weaker actor, null if they are equal.</returns>
        protected Actor FindWeakerInMelee(RogueGame game, Actor a, Actor b)
        {
            // alpha10 count how many hits it would take to kill each other
            // the actor that dies faster is the weaker one

            // silly cases of peope already dead, you never know -_-
            if (a.IsDead)
                return a;
            if (b.IsDead)
                return b;

            // count hits, lowest hit dies first
            int hitsToKillA = (int)Math.Ceiling((double)a.HitPoints / (double)b.CurrentMeleeAttack.DamageValue);
            int hitsToKillB = (int)Math.Ceiling((double)b.HitPoints / (double)a.CurrentMeleeAttack.DamageValue);

            return hitsToKillA < hitsToKillB ? a :
                hitsToKillA > hitsToKillB ? b :
                null;

            /* previous bizarre logic, what was I thinking? -_-
            int value_A = a.HitPoints + a.CurrentMeleeAttack.DamageValue;
            int value_B = b.HitPoints + b.CurrentMeleeAttack.DamageValue;
            return value_A < value_B ? a : value_A > value_B ? b : null; */
        }

        protected bool WillTireAfterAttack(RogueGame game, Actor actor)
        {
            if (!actor.Model.Abilities.CanTire)
                return false;
            int staAfter = actor.StaminaPoints - Rules.STAMINA_COST_MELEE_ATTACK;
            return staAfter < Rules.STAMINA_MIN_FOR_ACTIVITY;
        }

        protected bool WillTireAfterRunning(RogueGame game, Actor actor)
        {
            if (!actor.Model.Abilities.CanTire)
                return false;
            int staAfter = actor.StaminaPoints - Rules.STAMINA_COST_RUNNING;
            return staAfter < Rules.STAMINA_MIN_FOR_ACTIVITY;
        }

        protected bool HasSpeedAdvantage(RogueGame game, Actor actor, Actor target)
        {
            int actorSpeed = game.Rules.ActorSpeed(actor);
            int targetSpeed = game.Rules.ActorSpeed(target);

            // if better speed, yes.
            if (actorSpeed > targetSpeed)
                return true;

            // if we can run and the target can't and that would make us faster without tiring us, then yes!
            if (game.Rules.CanActorRun(actor) && !game.Rules.CanActorRun(target) &&
                !WillTireAfterRunning(game, actor) && actorSpeed * 2 > targetSpeed)
                return true;

            // TODO: other tricky cases?

            return false;
        }

        protected bool NeedsLight(RogueGame game)
        {
            switch (m_Actor.Location.Map.Lighting)
            {
                case Lighting.DARKNESS:
                    return true;
                case Lighting.LIT:
                    return false;
                case Lighting.OUTSIDE:
                    // alpha10 outside, lights have an effect only during the night.
                    return m_Actor.Location.Map.LocalTime.IsNight;
                // pre alpha10 more conservative usage of lights
                //// Needs only if At Night & (Outside or Heavy Rain).
                //return m_Actor.Location.Map.LocalTime.IsNight &&
                //    (game.Session.World.Weather == Weather.HEAVY_RAIN || !m_Actor.Location.Map.GetTileAt(m_Actor.Location.Position.X, m_Actor.Location.Position.Y).IsInside);
                default:
                    throw new ArgumentOutOfRangeException("unhandled lighting");
            }
        }

        /// <summary>
        /// Check if a point can be considered between two others.
        /// </summary>
        /// <param name="game"></param>
        /// <param name="A"></param>
        /// <param name="B"></param>
        /// <param name="C"></param>
        /// <returns></returns>
        protected bool IsBetween(RogueGame game, Point A, Point between, Point B)
        {
            float A_between = game.Rules.StdDistance(A, between);
            float B_between = game.Rules.StdDistance(B, between);
            float A_B = game.Rules.StdDistance(A, B);

            return A_between + B_between <= A_B + 0.25f;
        }

        protected bool IsDoorwayOrCorridor(RogueGame game, Map map, Point pos)
        {
            ///////////////////////////////////////
            // Check for simple shapes:
            // FREE-WALL-FREE       FREE-FREE-FREE
            // FREE-FREE-FREE       WALL-FREE-WALL
            // FREE-WALL-FREE       FREE-FREE-FREE
            ///////////////////////////////////////

            bool wall = !map.GetTileAt(pos).Model.IsWalkable;
            if (wall)
                return false;

            Point N = pos + Direction.N;
            bool nWall = map.IsInBounds(N) && !map.GetTileAt(N).Model.IsWalkable;
            Point S = pos + Direction.S;
            bool sWall = map.IsInBounds(S) && !map.GetTileAt(S).Model.IsWalkable;
            Point E = pos + Direction.E;
            bool eWall = map.IsInBounds(E) && !map.GetTileAt(E).Model.IsWalkable;
            Point W = pos + Direction.W;
            bool wWall = map.IsInBounds(W) && !map.GetTileAt(W).Model.IsWalkable;

            Point NE = pos + Direction.NE;
            bool neWall = map.IsInBounds(NE) && !map.GetTileAt(NE).Model.IsWalkable;
            Point NW = pos + Direction.NW;
            bool nwWall = map.IsInBounds(NW) && !map.GetTileAt(NW).Model.IsWalkable;
            Point SE = pos + Direction.SE;
            bool seWall = map.IsInBounds(SE) && !map.GetTileAt(SE).Model.IsWalkable;
            Point SW = pos + Direction.SW;
            bool swWall = map.IsInBounds(SW) && !map.GetTileAt(SW).Model.IsWalkable;

            bool freeCorners = !neWall && !seWall && !nwWall && !swWall;

            if (freeCorners && nWall && sWall && !eWall && !wWall)
                return true;
            if (freeCorners && eWall && wWall && !nWall && !sWall)
                return true;

            return false;
        }

        /// <summary>
        /// Not an enemy AND same faction.
        /// </summary>
        /// <param name="game"></param>
        /// <param name="other"></param>
        /// <returns></returns>
        protected bool IsFriendOf(RogueGame game, Actor other)
        {
            return !game.Rules.AreEnemies(m_Actor, other) && m_Actor.Faction == other.Faction;
        }

        protected Actor GetNearestTargetFor(RogueGame game, Actor actor)
        {
            Map map = actor.Location.Map;
            Actor nearest = null;
            int best = int.MaxValue;

            // quite uggly but better than computing the whole FoV...
            foreach (Actor a in map.Actors)
            {
                if (a.IsDead) continue;
                if (a == actor) continue;
                if (!game.Rules.AreEnemies(actor, a)) continue;

                int d = game.Rules.GridDistance(a.Location.Position, actor.Location.Position);
                if (d < best)
                {
                    if (d == 1 || LOS.CanTraceViewLine(actor.Location, a.Location.Position))
                    {
                        best = d;
                        nearest = a;
                    }
                }
            }

            return nearest;
        }

        // alpha10
        protected Attack GetActorAttack(RogueGame game, Actor actor)
        {
            return actor.GetEquippedRangedWeapon() != null ? actor.CurrentRangedAttack : actor.CurrentMeleeAttack;
        }
        #endregion

        #region Exits
        protected List<Exit> ListAdjacentExits(RogueGame game, Location fromLocation)
        {
            List<Exit> list = null;
            foreach (Direction d in Direction.COMPASS)
            {
                Point nextPos = fromLocation.Position + d;
                Exit exit = fromLocation.Map.GetExitAt(nextPos);
                if (exit == null)
                    continue;
                if (list == null)
                    list = new List<Exit>(8);
                list.Add(exit);
            }

            return list;
        }

        protected Exit PickAnyAdjacentExit(RogueGame game, Location fromLocation)
        {
            // get all adjacent exits.
            List<Exit> list = ListAdjacentExits(game, fromLocation);

            // if none, failed.
            if (list == null)
                return null;

            // pick one at random.
            return list[game.Rules.Roll(0, list.Count)];
        }
        #endregion

        #region Map
        // alpha10
        public bool IsAnyUnsafeDamagingTrapThere(RogueGame game, Map map, Point pos)
        {
            Inventory inv = map.GetItemsAt(pos);
            if (inv == null || inv.IsEmpty) return false;
            return inv.GetFirstMatching((it) =>
            {
                ItemTrap trap = it as ItemTrap;
                return trap != null && trap.IsActivated && trap.TrapModel.Damage > 0 && !game.Rules.IsSafeFromTrap(trap, m_Actor);
            }) != null;
        }

        // alpha10
        public static bool IsAnyPrimedExplosiveThere(Map map, Point pos)
        {
            Inventory inv = map.GetItemsAt(pos);
            if (inv == null || inv.IsEmpty) return false;
            return inv.GetFirstMatching((it) => { ItemPrimedExplosive ex = it as ItemPrimedExplosive; return ex != null; }) != null;
        }

        public static bool IsZoneChange(Map map, Point pos)
        {
            List<Zone> zonesHere = map.GetZonesAt(pos.X, pos.Y);
            if (zonesHere == null) return false;

            // adjacent to another zone.
            return map.HasAnyAdjacentInMap(pos, (adj) =>
            {
                List<Zone> zonesAdj = map.GetZonesAt(adj.X, adj.Y);
                if (zonesAdj == null) return false;
                if (zonesHere == null) return true;
                foreach (Zone z in zonesAdj)
                    if (!zonesHere.Contains(z))
                        return true;
                return false;
            });
        }

        protected Point RandomPositionNear(Rules rules, Map map, Point goal, int range)
        {
            int x = goal.X + rules.Roll(-range, +range);
            int y = goal.Y + rules.Roll(-range, +range);

            map.TrimToBounds(ref x, ref y);

            return new Point(x, y);
        }

        // alpha10.1
        /// <summary>
        ///
        /// </summary>
        /// <param name="mobj">can be null (will return 0)</param>
        /// <returns>0 for null objs, total hitpoints for breakable objects, a silly large amount for unbreakable objs</returns>
        protected int GetObjectHitPoints(MapObject mobj)
        {
            if (mobj == null)
                return 0;

            if (!mobj.IsBreakable)
                return 100000;

            int hp = mobj.HitPoints;

            // add barricade hps
            DoorWindow dw = mobj as DoorWindow;
            if (dw != null)
            {
                if (dw.IsBarricaded)
                    hp += dw.BarricadePoints;
            }

            return hp;
        }
        #endregion

        // alpha10
        #region Route checking
        /// <summary>
        ///
        /// </summary>
        /// <param name="game"></param>
        /// <param name="dest"></param>
        /// <param name="allowedActions"></param>
        /// <returns></returns>
        /// <see cref="RouteFinder.CanReachSimple(RogueGame, Point, int, Func{Point, Point, int})"/>
        protected bool CanReachSimple(RogueGame game, Point dest, RouteFinder.SpecialActions allowedActions)
        {
            if (m_RouteFinder == null)
                m_RouteFinder = new RouteFinder(this);
            m_RouteFinder.AllowedActions = allowedActions;
            int maxDist = game.Rules.GridDistance(m_Actor.Location.Position, dest);
            return m_RouteFinder.CanReachSimple(game, dest, maxDist, game.Rules.GridDistance);
        }

        protected void FilterOutUnreachablePercepts(RogueGame game, ref List<Percept> percepts, RouteFinder.SpecialActions allowedActions)
        {
            int i = 0;
            while (i < percepts.Count)
            {
                if (CanReachSimple(game, percepts[i].Location.Position, allowedActions))
                    i++;
                else
                    percepts.RemoveAt(i);
            }
        }
        #endregion
        #endregion
    }
}
