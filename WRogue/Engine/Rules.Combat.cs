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
        #region Pushing/Pulling objects & Shoving actors
        public bool HasActorPushAbility(Actor actor)
        {
            return actor.Model.Abilities.CanPush ||
                actor.Sheet.SkillTable.GetSkillLevel((int)Skills.IDs.STRONG) > 0 ||
                actor.Sheet.SkillTable.GetSkillLevel((int)Skills.IDs.Z_STRONG) > 0;
        }

        public bool CanActorPush(Actor actor, MapObject mapObj)
        {
            string reason;
            return CanActorPush(actor, mapObj, out reason);
        }

        public bool CanActorPush(Actor actor, MapObject mapObj, out string reason)
        {
            if (actor == null)
                throw new ArgumentNullException("actor");
            if (mapObj == null)
                throw new ArgumentNullException("mapObj");

            //////////////////////////////////
            // Not movable:
            // 1. Actor cannot push/pull.
            // 2. Actor is tired.
            // 3. Map obj is not movable.
            // 4. Another actor there.
            // 5. Map obj is on fire.
            // 6. Actor is dragging a corpse.  // alpha10
            /////////////////////////////////

            // 1. Actor cannot push/pull.
            if (!HasActorPushAbility(actor))
            {
                reason = "cannot push objects";
                return false;
            }

            // 2. Actor is tired.
            if (IsActorTired(actor))
            {
                reason = "tired";
                return false;
            }

            // 3. Map obj is not movable.
            if (!mapObj.IsMovable)
            {
                reason = "cannot be moved";
                return false;
            }

            // 4. Another actor there.
            if (mapObj.Location.Map.GetActorAt(mapObj.Location.Position) != null)
            {
                reason = "someone is there";
                return false;
            }

            // 5. Map obj is on fire.
            if (mapObj.IsOnFire)
            {
                reason = "on fire";
                return false;
            }

            // 6. Actor is dragging a corpse.  // alpha10
            if (actor.DraggedCorpse != null)
            {
                reason = "dragging a corpse";
                return false;
            }

            // all clear
            reason = "";
            return true;
        }

        public bool CanPushObjectTo(MapObject mapObj, Point toPos)
        {
            string reason;
            return CanPushObjectTo(mapObj, toPos, out reason);
        }

        public bool CanPushObjectTo(MapObject mapObj, Point toPos, out string reason)
        {
            if (mapObj == null)
                throw new ArgumentNullException("mapObj");

            ///////////////////////////
            // Not pushable there:
            // 1. Out of bounds.
            // 2. Not walkable.
            // 3. Another object there.
            // 4. An actor there.
            ///////////////////////////

            // 1. Out of bounds.
            if (!mapObj.Location.Map.IsInBounds(toPos))
            {
                reason = "out of map";
                return false;
            }

            // 2. Not walkable.
            if (!mapObj.Location.Map.GetTileAt(toPos.X, toPos.Y).Model.IsWalkable)
            {
                reason = "blocked by an obstacle";
                return false;
            }

            // 3. Another object there.
            if (mapObj.Location.Map.GetMapObjectAt(toPos) != null)
            {
                reason = "blocked by an object";
                return false;
            }

            // 4. An actor there.
            if (mapObj.Location.Map.GetActorAt(toPos) != null)
            {
                reason = "blocked by someone";
                return false;
            }

            // all clear.
            reason = "";
            return true;
        }

        // alpha10
        public bool CanPullObject(Actor actor, MapObject mapObj, Point toPos)
        {
            string reason;
            return CanPullObject(actor, mapObj, toPos, out reason);
        }

        // alpha10
        public bool CanPullObject(Actor actor, MapObject mapObj, Point moveToPos, out string reason)
        {
            /////////////////////////////////////////////
            // Basically check if can push and can walk.
            // 1. Actor cannot push.
            // 2. Another object already there.
            // 3. Actor cannot walk to pos.
            /////////////////////////////////////////////

            // 1. Actor cannot push this object.
            if (!CanActorPush(actor, mapObj, out reason))
                return false;

            // 2. Another object already there. eg: actor standing on a bed.
            MapObject otherMobj = actor.Location.Map.GetMapObjectAt(actor.Location.Position);
            if (otherMobj != null)
            {
                reason = string.Format("{0} is blocking", otherMobj.TheName);
                return false;
            }

            // 3. Actor cannot walk to pos.
            if (!IsWalkableFor(actor, new Location(actor.Location.Map, moveToPos), out reason))
                return false;

            // all clear
            reason = "";
            return true;
        }

        // alpha10
        public bool CanPullActor(Actor actor, Actor other, Point moveToPos, out string reason)
        {
            /////////////////////////////////////////////
            // Basically check if can push and can walk.
            // 1. Actor cannot shove.
            // 2. Actor cannot walk to pos.
            /////////////////////////////////////////////

            // 1. Actor cannot shove.
            if (!CanActorShove(actor, other, out reason))
                return false;

            // 3. Actor cannot walk to pos.
            if (!IsWalkableFor(actor, new Location(actor.Location.Map, moveToPos), out reason))
                return false;

            // all clear
            reason = "";
            return true;
        }

        public bool CanActorShove(Actor actor, Actor other, out string reason)
        {
            if (actor == null)
                throw new ArgumentNullException("actor");
            if (other == null)
                throw new ArgumentNullException("other");

            ///////////////////////////////
            // Not "shovable"
            // 1. Actor cannot push/pull.
            // 2. Actor is tired.
            // 3. Actor is dragging corpse  // alpha10
            ///////////////////////////////

            // 1. Actor cannot push/pull.
            if (!HasActorPushAbility(actor))
            {
                reason = "cannot shove people";
                return false;
            }

            // 2. Actor is tired.
            if (IsActorTired(actor))
            {
                reason = "tired";
                return false;
            }

            // 3. Actor is dragging corpse  // alpha10
            if (actor.DraggedCorpse != null)
            {
                reason = "dragging a corpse";
                return false;
            }

            // FIXME: in theory, should test if tile is walkable for the pusher. in practice, assume if other can walk here, we can too...
            //        this will cause problems in zombie mode (eg: undead player pushing living down from a car = move on the car...)

            // all clear
            reason = "";
            return true;
        }

        public bool CanShoveActorTo(Actor actor, Point toPos, out string reason)
        {
            if (actor == null)
                throw new ArgumentNullException("actor");

            ///////////////////////////
            // Not pushable there:
            // 1. Out of bounds.
            // 2. Not walkable.
            // 3. Unwalkable object.
            // 4. An actor there.
            // 5. Actor is dragging corpse  // alpha10
            ///////////////////////////

            Map map = actor.Location.Map;

            // 1. Out of bounds.
            if (!map.IsInBounds(toPos))
            {
                reason = "out of map";
                return false;
            }

            // 2. Not walkable.
            if (!map.GetTileAt(toPos.X, toPos.Y).Model.IsWalkable)
            {
                reason = "blocked";
                return false;
            }

            // 3. Unwalkable object.
            MapObject obj = map.GetMapObjectAt(toPos);
            if (obj != null && !obj.IsWalkable)
            {
                reason = "blocked by an object";
                return false;
            }

            // 4. An actor there.
            if (map.GetActorAt(toPos) != null)
            {
                reason = "blocked by someone";
                return false;
            }

            // 5. Actor is dragging corpse  // alpha10
            if (actor.DraggedCorpse != null)
            {
                reason = "dragging a corpse";
                return false;
            }

            // all clear.
            reason = "";
            return true;
        }
        #endregion
        #region Targeting, Firing and Throwing
        /// <summary>
        /// List enemies in fov, sorted by distance (closest first).
        /// </summary>
        /// <param name="actor"></param>
        /// <param name="fov"></param>
        /// <returns></returns>
        public List<Actor> GetEnemiesInFov(Actor actor, HashSet<Point> fov)
        {
            if (actor == null)
                throw new ArgumentNullException("actor");
            if (fov == null)
                throw new ArgumentNullException("fov");

            List<Actor> list = null;

            foreach (Point pt in fov)
            {
                Actor other = actor.Location.Map.GetActorAt(pt);
                if (other == null)
                    continue;
                if (other == actor)
                    continue;
                if (!AreEnemies(actor, other))
                    continue;

                if (list == null)
                    list = new List<Actor>(3);
                list.Add(other);
            }

            if (list != null)
            {
                list.Sort((a, b) =>
                    {
                        float dA = StdDistance(a.Location.Position, actor.Location.Position);
                        float dB = StdDistance(b.Location.Position, actor.Location.Position);

                        return dA < dB ? -1 :
                            dA > dB ? 1 :
                            0;
                    });
            }

            return list;
        }

        public bool CanActorFireAt(Actor actor, Actor target)
        {
            string reason;
            return CanActorFireAt(actor, target, out reason);
        }

        public bool CanActorFireAt(Actor actor, Actor target, out string reason)
        {
            return CanActorFireAt(actor, target, null, out reason);
        }

        public bool CanActorFireAt(Actor actor, Actor target, List<Point> LoF)
        {
            string reason;
            return CanActorFireAt(actor, target, LoF, out reason);
        }

        public bool CanActorFireAt(Actor actor, Actor target, List<Point> LoF, out string reason)
        {
            if (actor == null)
                throw new ArgumentNullException("actor");
            if (target == null)
                throw new ArgumentNullException("target");

            ///////////////////////////////////////
            // Cannot fire if:
            // 1. No ranged weapon or out of range.
            // 2. No ammo.
            // 3. No LoF.
            // 4. Target is dead (doh!).
            ///////////////////////////////////////
            if (LoF != null)
                LoF.Clear();

            // 1. No ranged weapon or out of range.
            ItemRangedWeapon rangedWeapon = actor.GetEquippedWeapon() as ItemRangedWeapon;
            if (rangedWeapon == null)
            {
                reason = "no ranged weapon equipped";
                return false;
            }
            if (actor.CurrentRangedAttack.Range < GridDistance(actor.Location.Position, target.Location.Position))
            {
                reason = "out of range";
                return false;
            }

            // 2. No ammo.
            if (rangedWeapon.Ammo <= 0)
            {
                reason = "no ammo left";
                return false;
            }

            // 3. No LoF.
            if (!LOS.CanTraceFireLine(actor.Location, target.Location.Position, actor.CurrentRangedAttack.Range, LoF))
            {
                reason = "no line of fire";
                return false;
            }

            // 4. Target is dead (doh!).
            // oddly this can happen for the AI when simulating... not clear why...
            // so this is a lame fix to prevent KillingActor from throwing an exception :-)
            if (target.IsDead)
            {
                reason = "already dead!";
                return false;
            }

            // all clear.
            reason = "";
            return true;
        }

        public bool CanActorThrowTo(Actor actor, Point pos, List<Point> LoF)
        {
            string reason;
            return CanActorThrowTo(actor, pos, LoF, out reason);
        }

        public bool CanActorThrowTo(Actor actor, Point pos, List<Point> LoF, out string reason)
        {
            if (actor == null)
                throw new ArgumentNullException("actor");

            ///////////////////////////////////////
            // Cannot fire if:
            // 1. No throwable item or out of range.
            // 2. No LoT.
            ///////////////////////////////////////
            if (LoF != null)
                LoF.Clear();

            // 1. No throwable item or out of range.
            ItemGrenade unprimedGrenade = actor.GetEquippedWeapon() as ItemGrenade;
            ItemGrenadePrimed primedGrenade = actor.GetEquippedWeapon() as ItemGrenadePrimed;
            if (unprimedGrenade == null && primedGrenade == null)
            {
                reason = "no grenade equiped";
                return false;
            }
            ItemGrenadeModel model;
            if (unprimedGrenade != null)
                model = unprimedGrenade.Model as ItemGrenadeModel;
            else
                model = (primedGrenade.Model as ItemGrenadePrimedModel).GrenadeModel;
            int maxThrowDist = ActorMaxThrowRange(actor, model.MaxThrowDistance);
            if (GridDistance(actor.Location.Position, pos) > maxThrowDist)
            {
                reason = "out of throwing range";
                return false;
            }

            // 2. No LoT.
            if (!LOS.CanTraceThrowLine(actor.Location, pos, maxThrowDist, LoF))
            {
                reason = "no line of throwing";
                return false;
            }

            // all clear.
            reason = "";
            return true;

        }
        #endregion
        #region Hunger/Rot & Sleep & Sanity
        public bool IsActorHungry(Actor a)
        {
            return a.Model.Abilities.HasToEat && a.FoodPoints <= FOOD_HUNGRY_LEVEL;
        }

        public bool IsActorStarving(Actor a)
        {
            return a.Model.Abilities.HasToEat && a.FoodPoints <= 0;
        }

        public bool IsRottingActorHungry(Actor a)
        {
            return a.Model.Abilities.IsRotting && a.FoodPoints <= ROT_HUNGRY_LEVEL;
        }

        public bool IsRottingActorStarving(Actor a)
        {
            return a.Model.Abilities.IsRotting && a.FoodPoints <= 0;
        }

        public bool IsFoodStillFresh(ItemFood food, int turnCounter)
        {
            if (!food.IsPerishable)
                return true;
            return turnCounter < food.BestBefore.TurnCounter;
        }

        public bool IsFoodExpired(ItemFood food, int turnCounter)
        {
            return food.IsPerishable && turnCounter >= food.BestBefore.TurnCounter && turnCounter < 2 * food.BestBefore.TurnCounter;
        }

        public bool IsFoodSpoiled(ItemFood food, int turnCounter)
        {
            return food.IsPerishable && turnCounter >= 2 * food.BestBefore.TurnCounter;
        }

        public int FoodItemNutrition(ItemFood food, int turnCounter)
        {
            return (IsFoodStillFresh(food, turnCounter) ? food.Nutrition :
                IsFoodExpired(food, turnCounter) ? 2 * food.Nutrition / 3 :
                food.Nutrition / 3);
        }

        public bool IsActorSleepy(Actor a)
        {
            return a.Model.Abilities.HasToSleep && a.SleepPoints <= SLEEP_SLEEPY_LEVEL;
        }

        public bool IsActorExhausted(Actor a)
        {
            return a.Model.Abilities.HasToSleep && a.SleepPoints <= 0;
        }

        public int SleepToHoursUntilSleepy(int sleep, bool isNight)
        {
            int left = sleep - Rules.SLEEP_SLEEPY_LEVEL;
            if (isNight)
                left /= 2;
            if (left <= 0)
                return 0;
            return left / WorldTime.TURNS_PER_HOUR;
        }

        public bool IsAlmostSleepy(Actor actor)
        {
            if (!actor.Model.Abilities.HasToSleep)
                return false;
            return SleepToHoursUntilSleepy(actor.SleepPoints, actor.Location.Map.LocalTime.IsNight) <= 3;
        }

        public bool CanActorSleep(Actor actor)
        {
            string reason;
            return CanActorSleep(actor, out reason);
        }

        public bool CanActorSleep(Actor actor, out string reason)
        {
            if (actor == null)
                throw new ArgumentNullException("actor");

            /////////////////////////
            // Can't sleep if:
            // 1. Already sleeping.
            // 2. Has not to sleep.
            // 3. Is hungry or worse.
            // 4. No need to sleep.
            /////////////////////////

            // 1. Already sleeping.
            if (actor.IsSleeping)
            {
                reason = "already sleeping";
                return false;
            }

            // 2. Has not to sleep.
            if (!actor.Model.Abilities.HasToSleep)
            {
                reason = "no ability to sleep";
                return false;
            }

            // 3. Is hungry or worse.
            if (IsActorHungry(actor) || IsActorStarving(actor))
            {
                reason = "hungry";
                return false;
            }

            // 4. No need to sleep.
            if (actor.SleepPoints >= ActorMaxSleep(actor) - WorldTime.TURNS_PER_HOUR)
            {
                reason = "not sleepy at all";
                return false;
            }

            // all clear.
            reason = "";
            return true;
        }

        public bool IsOnCouch(Actor actor)
        {
            if (actor == null)
                throw new ArgumentNullException("actor");

            MapObject mapObj = actor.Location.Map.GetMapObjectAt(actor.Location.Position);
            if (mapObj == null)
                return false;

            return mapObj.IsCouch;
        }

        public bool IsActorDisturbed(Actor a)
        {
            return a.Model.Abilities.HasSanity && a.Sanity <= ActorDisturbedLevel(a);
        }

        public bool IsActorInsane(Actor a)
        {
            return a.Model.Abilities.HasSanity && a.Sanity <= 0;
        }

        public int SanityToHoursUntilUnstable(Actor a)
        {
            int left = a.Sanity - ActorDisturbedLevel(a);
            if (left <= 0) return 0;
            return left / WorldTime.TURNS_PER_HOUR;
        }
        #endregion
        #region Leading
        public bool CanActorTakeLead(Actor actor, Actor target)
        {
            string reason;
            return CanActorTakeLead(actor, target, out reason);
        }

        public bool CanActorTakeLead(Actor actor, Actor target, out string reason)
        {
            if (actor == null)
                throw new ArgumentNullException("actor");
            if (target == null)
                throw new ArgumentNullException("target");

            ///////////////////////////////////////
            // Can't if any is true:
            // 1. Target is undead or an enemy.
            // 2. Target is sleeping.
            // 3. Target has already a leader and can't steal the follower.  alpha10.1
            // 4. Target is a leader.
            // 5. Actor has reached max followers.
            // 6. Target is player!
            // 7. Faction restrictions.
            ///////////////////////////////////////

            // 1. Target is undead or an enemy.
            if (target.Model.Abilities.IsUndead)
            {
                reason = "undead";
                return false;
            }
            if (AreEnemies(actor, target))
            {
                reason = "enemy";
                return false;
            }

            // 2. Target is sleeping.
            if (target.IsSleeping)
            {
                reason = "sleeping";
                return false;
            }

            // 3. Target has already a leader and can't steal the follower.  alpha10.1
            if (target.HasLeader)
            {
                // alpha10.1
                // can "steal" followers only if actor has better charismatic skill than current leader
                int actorCharism = actor.Sheet.SkillTable.GetSkillLevel((int)Skills.IDs.CHARISMATIC);
                int leaderCharism = target.Leader.Sheet.SkillTable.GetSkillLevel((int)Skills.IDs.CHARISMATIC);
                if (actorCharism <= leaderCharism)
                {
                    reason = "has already a leader at least as charismatic as you";
                    return false;
                }
            }

            // 4. Target is a leader.
            if (target.CountFollowers > 0)
            {
                reason = "is a leader";
                return false;
            }

            // 5. Actor has reached max followers.
            int maxFollowers = ActorMaxFollowers(actor);
            if (maxFollowers == 0)
            {
                reason = "can't lead";
                return false;
            }
            if (actor.CountFollowers >= maxFollowers)
            {
                reason = "too many followers";
                return false;
            }

            // 6. Target is player!
            if (target.IsPlayer)
            {
                reason = "is player";
                return false;
            }

            // 7. Faction restrictions.
            if (actor.Faction != target.Faction && target.Faction.LeadOnlyBySameFaction)
            {
                reason = String.Format("{0} can't lead {1}", actor.Faction.Name, target.Faction.Name);
                return false;
            }

            // all clear.
            reason = "";
            return true;
        }

        public bool CanActorCancelLead(Actor actor, Actor target, out string reason)
        {
            if (actor == null)
                throw new ArgumentNullException("actor");
            if (target == null)
                throw new ArgumentNullException("target");

            //////////////////////////////////
            // Can't if any is true:
            // 1. Target not a actor follower.
            // 2. Target is sleeping.
            //////////////////////////////////

            // 1. Target not a actor follower.
            if (target.Leader != actor)
            {
                reason = "not your follower";
                return false;
            }

            // 2. Target is sleeping.
            if (target.IsSleeping)
            {
                reason = "sleeping";
                return false;
            }

            // all clear.
            reason = "";
            return true;
        }

        public bool CanActorSwitchPlaceWith(Actor actor, Actor target)
        {
            string reason;
            return CanActorSwitchPlaceWith(actor, target, out reason);
        }

        public bool CanActorSwitchPlaceWith(Actor actor, Actor target, out string reason)
        {
            if (actor == null)
                throw new ArgumentNullException("actor");
            if (target == null)
                throw new ArgumentNullException("target");

            //////////////////////////////////
            // Can't if any is true:
            // 1. Target not a actor follower.
            // 2. Target is sleeping.
            //////////////////////////////////

            // 1. Target not a actor follower.
            if (target.Leader != actor)
            {
                reason = "not your follower";
                return false;
            }

            // 2. Target is sleeping.
            if (target.IsSleeping)
            {
                reason = "sleeping";
                return false;
            }

            // all clear.
            reason = "";
            return true;
        }

        #endregion
        #region Trust
        public bool IsActorTrustingLeader(Actor actor)
        {
            if (actor == null)
                throw new ArgumentNullException("actor");

            if (!actor.HasLeader)
                return false;

            return actor.TrustInLeader >= TRUST_TRUSTING_THRESHOLD;
        }

        public bool HasActorBondWith(Actor actor, Actor target)
        {
            if (actor.Leader == target)
                return actor.TrustInLeader >= TRUST_BOND_THRESHOLD;
            else if (target.Leader == actor)
                return target.TrustInLeader >= TRUST_BOND_THRESHOLD;
            else
                return false;
        }
        #endregion
        #region Building & Repairing
        public int CountBarricadingMaterial(Actor actor)
        {
            if (actor.Inventory == null || actor.Inventory.IsEmpty)
                return 0;

            int count = 0;
            foreach (Item it in actor.Inventory.Items)
                if (it is ItemBarricadeMaterial)
                    count += it.Quantity;
            return count;
        }

        public bool CanActorBuildFortification(Actor actor, Point pos, bool isLarge)
        {
            string reason;
            return CanActorBuildFortification(actor, pos, isLarge, out reason);
        }

        public bool CanActorBuildFortification(Actor actor, Point pos, bool isLarge, out string reason)
        {
            if (actor == null)
                throw new ArgumentNullException("actor");

            ///////////////////////////
            // Can't if any is true:
            // 1. No carpentry skill.
            // 2. Not walkable.
            // 3. Not engouh material.
            // 4. Tile occupied.
            ///////////////////////////

            // 1. No carpentry skill.
            if (actor.Sheet.SkillTable.GetSkillLevel((int)Skills.IDs.CARPENTRY) == 0)
            {
                reason = "no skill in carpentry";
                return false;
            }

            // 2. Not walkable.
            Map map = actor.Location.Map;
            if (!map.GetTileAt(pos).Model.IsWalkable)
            {
                reason = "cannot build on walls";
                return false;
            }

            // 3. Not enough material.
            int need = ActorBarricadingMaterialNeedForFortification(actor, isLarge);
            if (CountBarricadingMaterial(actor) < need)
            {
                reason = String.Format("not enough barricading material, need {0}.", need);
                return false;
            }

            // 4. Tile occupied.
            if (map.GetMapObjectAt(pos) != null || map.GetActorAt(pos) != null)
            {
                reason = "blocked";
                return false;
            }

            // all clear.
            reason = "";
            return true;
        }

        public bool CanActorRepairFortification(Actor actor, Fortification fort, out string reason)
        {
            if (actor == null)
                throw new ArgumentNullException("actor");

            /////////////////////////////
            // Can't if any is true:
            // 1. Cannot use map objects.
            // 2. No material.
            /////////////////////////////

            // 1. Cannot use map objects.
            if (!actor.Model.Abilities.CanUseMapObjects)
            {
                reason = "cannot use map objects";
                return false;
            }

            // 2. No material.
            int material = CountBarricadingMaterial(actor);
            if (material <= 0)
            {
                reason = "no barricading material";
                return false;
            }

            // all clear.
            reason = "";
            return true;
        }
        #endregion
        #region Corpses
        public int ActorDamageVsCorpses(Actor a)
        {
            // base = melee HALVED.
            int dmg = a.CurrentMeleeAttack.DamageValue / 2;

            // Necrology.
            dmg += SKILL_NECROLOGY_CORPSE_BONUS * a.Sheet.SkillTable.GetSkillLevel((int)Skills.IDs.NECROLOGY);

            return dmg;
        }

        public bool CanActorEatCorpse(Actor actor, Corpse corpse)
        {
            string reason;
            return CanActorEatCorpse(actor, corpse, out reason);
        }

        public bool CanActorEatCorpse(Actor actor, Corpse corpse, out string reason)
        {
            if (actor == null)
                throw new ArgumentNullException("actor");
            if (corpse == null)
                throw new ArgumentNullException("corpse");


            ///////////////////////////////////////////////
            // Can't if any is true:
            // 1. Actor is not undead or a starving/insane living.
            ///////////////////////////////////////////////

            // 1. Actor is not undead or a starving living.
            if (!actor.Model.Abilities.IsUndead)
            {
                if (!IsActorStarving(actor) && !IsActorInsane(actor))
                {
                    reason = "not starving or insane";
                    return false;
                }
            }

            // ok
            reason = "";
            return true;
        }

        public bool CanActorButcherCorpse(Actor actor, Corpse corpse)
        {
            string reason;
            return CanActorButcherCorpse(actor, corpse, out reason);
        }

        public bool CanActorButcherCorpse(Actor actor, Corpse corpse, out string reason)
        {
            if (actor == null)
                throw new ArgumentNullException("actor");
            if (corpse == null)
                throw new ArgumentNullException("corpse");


            ////////////////////////////////////////
            // Can't if any is true:
            // 1. Actor tired.
            // 2. Corpse not in same tile as actor.
            ////////////////////////////////////////
            // 1. Actor tired.
            if (IsActorTired(actor))
            {
                reason = "tired";
                return false;
            }
            // 2. Corpse not in same tile as actor.
            if (corpse.Position != actor.Location.Position || !actor.Location.Map.HasCorpse(corpse))
            {
                reason = "not in same location";
                return false;
            }

            // ok
            reason = "";
            return true;
        }

        public bool CanActorStartDragCorpse(Actor actor, Corpse corpse)
        {
            string reason;
            return CanActorStartDragCorpse(actor, corpse, out reason);
        }

        public bool CanActorStartDragCorpse(Actor actor, Corpse corpse, out string reason)
        {
            if (actor == null)
                throw new ArgumentNullException("actor");
            if (corpse == null)
                throw new ArgumentNullException("corpse");


            ////////////////////////////////////////
            // Can't if any is true:
            // 1. Corpse already dragged.
            // 2. Actor tired.
            // 3. Corpse not in same tile as actor.
            // 4. Actor already dragging a corpse.
            ////////////////////////////////////////

            // 1. Corpse already dragged.
            if (corpse.IsDragged)
            {
                reason = "corpse is already being dragged";
                return false;
            }
            // 2. Actor tired.
            if (IsActorTired(actor))
            {
                reason = "tired";
                return false;
            }
            // 3. Corpse not in same tile as actor.
            if (corpse.Position != actor.Location.Position || !actor.Location.Map.HasCorpse(corpse))
            {
                reason = "not in same location";
                return false;
            }
            // 4. Actor already dragging a corpse.
            if (actor.DraggedCorpse != null)
            {
                reason = "already dragging a corpse";
                return false;
            }

            // ok
            reason = "";
            return true;
        }

        public bool CanActorStopDragCorpse(Actor actor, Corpse corpse)
        {
            string reason;
            return CanActorStopDragCorpse(actor, corpse, out reason);
        }

        public bool CanActorStopDragCorpse(Actor actor, Corpse corpse, out string reason)
        {
            if (actor == null)
                throw new ArgumentNullException("actor");
            if (corpse == null)
                throw new ArgumentNullException("corpse");


            ////////////////////////////////////////
            // Can't if Corpse not being dragged by actor.
            ////////////////////////////////////////

            // Can't if Corpse not being dragged by actor.
            if (corpse.DraggedBy != actor)
            {
                reason = "not dragging this corpse";
                return false;
            }

            // ok
            reason = "";
            return true;
        }

        public bool CanActorReviveCorpse(Actor actor, Corpse corpse)
        {
            string reason;
            return CanActorReviveCorpse(actor, corpse, out reason);
        }

        public bool CanActorReviveCorpse(Actor actor, Corpse corpse, out string reason)
        {
            if (actor == null)
                throw new ArgumentNullException("actor");
            if (corpse == null)
                throw new ArgumentNullException("corpse");

            ///////////////////////////
            // Can't if
            // 1. No medic skill.
            // 2. Not on same tile.
            // 3. Corpse not fresh.
            // 4. No medikit.
            //////////////////////////

            // 1. No medic skill.
            if (actor.Sheet.SkillTable.GetSkillLevel((int)Skills.IDs.MEDIC) == 0)
            {
                reason = "lack medic skill";
                return false;
            }

            // 2. Not on same tile.
            if (corpse.Position != actor.Location.Position)
            {
                reason = "not there";
                return false;
            }

            // 3. Corpse not fresh.
            if (CorpseRotLevel(corpse) > 0)
            {
                reason = "corpse not fresh";
                return false;
            }

            // 4. No medikit.
            if (!actor.Inventory.HasItemMatching((it) => it.Model.ID == (int)GameItems.IDs.MEDICINE_MEDIKIT))
            {
                reason = "no medikit";
                return false;
            }

            // ok
            reason = "";
            return true;
        }
        #endregion
    }
}
