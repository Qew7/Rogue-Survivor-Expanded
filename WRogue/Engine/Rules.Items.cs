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
        #region Items
        public bool CanActorGetItemFromContainer(Actor actor, Point position)
        {
            string reason;
            return CanActorGetItemFromContainer(actor, position, out reason);
        }

        public bool CanActorGetItemFromContainer(Actor actor, Point position, out string reason)
        {
            if (actor == null)
                throw new ArgumentNullException("actor");

            ////////////////////////////////////
            // Cant if any is true:
            // 1. Map object is not a container.
            // 2. There is no items there.
            // 3. Actor cannot take the item.
            ////////////////////////////////////

            // 1. Map object is not a container.
            MapObject mapObj = actor.Location.Map.GetMapObjectAt(position);
            if (mapObj == null || !mapObj.IsContainer)
            {
                reason = "object is not a container";
                return false;
            }

            // 2. There is no items there.
            Inventory invThere = actor.Location.Map.GetItemsAt(position);
            if (invThere == null || invThere.IsEmpty)
            {
                reason = "nothing to take there";
                return false;
            }

            // 3. Actor cannot take the item.
            if (!actor.Model.Abilities.HasInventory ||
                !actor.Model.Abilities.CanUseMapObjects ||
                actor.Inventory == null ||
                !CanActorGetItem(actor, invThere.TopItem))
            {
                reason = "cannot take an item";
                return false;
            }

            // all clear.
            reason = "";
            return true;
        }

        public bool CanActorGetItem(Actor actor, Item it)
        {
            string reason;
            return CanActorGetItem(actor, it, out reason);
        }

        public bool CanActorGetItem(Actor actor, Item it, out string reason)
        {
            if (actor == null)
                throw new ArgumentNullException("actor");
            if (it == null)
                throw new ArgumentNullException("item");

            //////////////////////////////////////////////
            // Cant if any is true:
            // 1. Actor cannot take items.
            // 2. Inventory is full and cannot stack item
            // 3. Triggered trap.
            //////////////////////////////////////////////

            // 1. Actor cannot take items
            if (!actor.Model.Abilities.HasInventory ||
                !actor.Model.Abilities.CanUseMapObjects ||
                actor.Inventory == null)
            {
                reason = "no inventory";
                return false;
            }
            if (actor.Inventory.IsFull && !actor.Inventory.CanAddAtLeastOne(it))
            {
                reason = "inventory is full";
                return false;
            }
            if (it is ItemTrap && (it as ItemTrap).IsTriggered)
            {
                reason = "triggered trap";
                return false;
            }

            // all clear.
            reason = "";
            return true;
        }

        public bool CanActorEquipItem(Actor actor, Item it)
        {
            string reason;
            return CanActorEquipItem(actor, it, out reason);
        }

        public bool CanActorEquipItem(Actor actor, Item it, out string reason)
        {
            if (actor == null)
                throw new ArgumentNullException("actor");
            if (it == null)
                throw new ArgumentNullException("item");

            ////////////////////////////
            // Can't if any is true:
            // 1. Actor cant use items.
            // 2. Item not equipable.
            // (DISABLED 3. No free slot on doll.)
            ////////////////////////////

            // 1. Actor cant use items.
            if (!actor.Model.Abilities.CanUseItems)
            {
                reason = "no ability to use items";
                return false;
            }

            // 2. Item not equipable.
            if (!it.Model.IsEquipable)
            {
                reason = "this item cannot be equipped";
                return false;
            }

            // (DISABLED 3. No free slot on doll.)
            // reason: equipping automatically unequip other first.
#if false
            if (actor.GetEquippedItem(it.Model.EquipmentPart) != null)
            {
                reason = "equipment slot not free";
                return false;
            }
#endif

            // all clear.
            reason = "";
            return true;
        }

        public bool CanActorUnequipItem(Actor actor, Item it)
        {
            string reason;
            return CanActorUnequipItem(actor, it, out reason);
        }

        public bool CanActorUnequipItem(Actor actor, Item it, out string reason)
        {
            if (actor == null)
                throw new ArgumentNullException("actor");
            if (it == null)
                throw new ArgumentNullException("item");

            /////////////////////////
            // Can't if any is true:
            // 1. Not equipped.
            // 2. Not in inventory.
            ////////////////////////

            // 1. Not equipped.
            if (!it.IsEquipped)
            {
                reason = "not equipped";
                return false;
            }

            // 2. Not in inventory.
            Inventory inv = actor.Inventory;
            if (inv == null || !inv.Contains(it))
            {
                reason = "not in inventory";
                return false;
            }

            // all clear.
            reason = "";
            return true;
        }

        public bool CanActorDropItem(Actor actor, Item it)
        {
            string reason;
            return CanActorDropItem(actor, it, out reason);
        }

        public bool CanActorDropItem(Actor actor, Item it, out string reason)
        {
            if (actor == null)
                throw new ArgumentNullException("actor");
            if (it == null)
                throw new ArgumentNullException("item");

            /////////////////////////
            // Can't if any is true:
            // 1. Equipped.
            // 2. Not in inventory.
            ////////////////////////

            // 1. Equipped.
            if(it.IsEquipped)
            {
                reason = "unequip first";
                return false;
            }

            // 2. Not in inventory.
            Inventory inv = actor.Inventory;
            if (inv == null || !inv.Contains(it))
            {
                reason = "not in inventory";
                return false;
            }

            // all clear.
            reason = "";
            return true;
        }

        public bool CanActorUseItem(Actor actor, Item it)
        {
            string reason;
            return CanActorUseItem(actor, it, out reason);
        }

        public bool CanActorUseItem(Actor actor, Item it, out string reason)
        {
            if (actor == null)
                throw new ArgumentNullException("actor");
            if (it == null)
                throw new ArgumentNullException("item");

            //////////////////////////////////////////////////
            // Can't if any is true:
            // 1. Actor cant use items.
            // 2. Actor cant use this specific kind of items.
            // (unused 3. Actor cant use item in his present state.)
            // 4. Not in inventory.
            //////////////////////////////////////////////////

            // 1. Actor cant use items.
            if (!(actor.Model.Abilities.CanUseItems))
            {
                reason = "no ability to use items";
                return false;
            }

            // 2. Actor cant use this specific kind of items.
            if (it is ItemWeapon)
            {
                reason = "to use a weapon, equip it";
                return false;
            }
            else if (it is ItemFood && !actor.Model.Abilities.HasToEat)
            {
                reason = "no ability to eat";
                return false;
            }
            else if (it is ItemMedicine && actor.Model.Abilities.IsUndead)
            {
                reason = "undeads cannot use medecine";
                return false;
            }
            else if (it is ItemBarricadeMaterial)
            {
                reason = "to use material, build a barricade";
                return false;
            }
            else if (it is ItemAmmo)
            {
                ItemAmmo ammoIt = it as ItemAmmo;
                /////////////////////////////////////
                // Can't use ammo if
                // 1. No compatible weapon equipped.
                // 2. Weapon fully loaded.
                /////////////////////////////////////
                // 1. No compatible weapon equipped.
                ItemRangedWeapon eqRanged = actor.GetEquippedWeapon() as ItemRangedWeapon;
                if (eqRanged == null || eqRanged.AmmoType != ammoIt.AmmoType)
                {
                    reason = "no compatible ranged weapon equipped";
                    return false;
                }
                // 2. Weapon fully loaded.
                if(eqRanged.Ammo >= (eqRanged.Model as ItemRangedWeaponModel).MaxAmmo)
                {
                    reason = "weapon already fully loaded";
                    return false;
                }
                // fine!
            }
            // alpha10 new way to use spray scent
            //else if (it is ItemSprayScent)
            //{
            //    ItemSprayScent spray = it as ItemSprayScent;
            //    // can't if empty.
            //    if (spray.SprayQuantity <= 0)
            //    {
            //        reason = "no spray left.";
            //        return false;
            //    }
            //    // fine!
            //}
            else if (it is ItemTrap)
            {
                ItemTrap trap = it as ItemTrap;
                if (!trap.TrapModel.UseToActivate)
                {
                    reason = "does not activate manually";
                    return false;
                }
            }
            else if (it is ItemEntertainment)
            {
                if (!actor.Model.Abilities.IsIntelligent)
                {
                    reason = "not intelligent";
                    return false;
                }
                if ((it as ItemEntertainment).IsBoringFor(actor)) // alpha10 boring items item centric
                {
                    reason = "bored by this";
                    return false;
                }
            }

            // (3. Actor cant use item in his present state.)
            // todo if needed.

            // 4. Not in inventory.
            Inventory inv = actor.Inventory;
            if (inv == null || !inv.Contains(it))
            {
                reason = "not in inventory";
                return false;
            }

            // all clear.
            reason = "";
            return true;
        }

        public bool CanActorEatFoodOnGround(Actor actor, Item it, out string reason)
        {
            if (actor == null)
                throw new ArgumentNullException("actor");
            if (it == null)
                throw new ArgumentNullException("item");


            ///////////////////////////////
            // Can't if any is true:
            // 1. Item not food.
            // 2. Item not in actor tile.
            //////////////////////////////

            // 1. Item not food.
            if (!(it is ItemFood))
            {
                reason = "not food";
                return false;
            }

            // 2. Item not in actor tile.
            Inventory stackHere = actor.Location.Map.GetItemsAt(actor.Location.Position);
            if (stackHere == null || !stackHere.Contains(it))
            {
                reason = "item not here";
                return false;
            }

            // ok
            reason = "";
            return true;
        }

        public bool CanActorRechargeItemBattery(Actor actor, Item it, out string reason)
        {
            if (actor == null)
                throw new ArgumentNullException("actor");
            if (it == null)
                throw new ArgumentNullException("item");

            //////////////////////////////////////////////////
            // Can't if any is true:
            // 1. Actor cant use items.
            // 2. Item not equipped by actor.
            // 3. Not a battery powered item.
            //////////////////////////////////////////////////

            // 1. Actor cant use items.
            if (!(actor.Model.Abilities.CanUseItems))
            {
                reason = "no ability to use items";
                return false;
            }

            // 2. Item not equipped.
            if (!it.IsEquipped || !actor.Inventory.Contains(it))
            {
                reason = "item not equipped";
                return false;
            }

            // 3. Not a battery powered item.
            if (!IsItemBatteryPowered(it))
            {
                reason = "not a battery powered item";
                return false;
            }

            // all clear.
            reason = "";
            return true;
        }

        public bool IsItemBatteryPowered(Item it)
        {
            if (it == null) return false;
            return (it is ItemLight || it is ItemTracker);
        }

        public bool IsItemBatteryFull(Item it)
        {
            if (it == null) return true;
            ItemLight light = it as ItemLight;
            if (light != null && light.IsFullyCharged) return true;
            ItemTracker tracker = it as ItemTracker;
            if (tracker != null && tracker.IsFullyCharged) return true;
            return false;
        }

        public bool CanActorGiveItemTo(Actor actor, Actor target, Item gift, out string reason)
        {
            if (actor == null)
                throw new ArgumentNullException("actor");
            if (target == null)
                throw new ArgumentNullException("target");
            if (gift == null)
                throw new ArgumentNullException("gift");

            /////////////////////////////////
            // Can't if any is true:
            // 1. Target is enemy.
            // 2. Item is equipped.
            // 3. Target is sleeping.
            // 4. Target can't receive item.
            /////////////////////////////////

            // 1. Target is enemy.
            if (AreEnemies(actor, target))
            {
                reason = "enemy";
                return false;
            }

            // 2. Item is equipped.
            if (gift.IsEquipped)
            {
                reason = "equipped";
                return false;
            }

            // 3. Target is sleeping.
            if (target.IsSleeping)
            {
                reason = "sleeping";
                return false;
            }

            // 4. Target can't receive item.
            if (!CanActorGetItem(target, gift, out reason))
            {
                return false;
            }

            // all clear.
            return true;
        }

        // alpha10
        public bool CanActorSprayOdorSuppressor(Actor actor, ItemSprayScent suppressor, Actor sprayOn)
        {
            string reason;
            return CanActorSprayOdorSuppressor(actor, suppressor, sprayOn, out reason);
        }

        // alpha10
        public bool CanActorSprayOdorSuppressor(Actor actor, ItemSprayScent suppressor, Actor sprayOn, out string reason)
        {
            if (actor == null)
                throw new ArgumentNullException("actor");
            if (suppressor == null)
                throw new ArgumentNullException("suppressor");
            if (sprayOn == null)
                throw new ArgumentNullException("target");

            ////////////////////////////////////////////////////////
            // Cant if any is true:
            // 1. Actor cannot use items
            // 2. Not an odor suppressor
            // 3. Spray is not equiped by actor or has no spray left.
            // 4. SprayOn is not self or adjacent.
            ////////////////////////////////////////////////////////

            // 1. Actor cannot use items
            if (!actor.Model.Abilities.CanUseItems)
            {
                reason = "cannot use items";
                return false;
            }

            // 2. Not an odor suppressor
            if (suppressor.Odor != Odor.SUPPRESSOR)
            {
                reason = "not an odor suppressor";
                return false;
            }

            // 2. Spray is not equiped by actor or has no spray left.
            if (suppressor.SprayQuantity <= 0)
            {
                reason = "no spray left";
                return false;
            }
            if (!(suppressor.IsEquipped && (actor.Inventory != null && actor.Inventory.Contains(suppressor))))
            {
                reason = "spray not equipped";
                return false;
            }

            // 3. SprayOn is not self or adjacent.
            if (sprayOn != actor)
            {
                if (!(actor.Location.Map == sprayOn.Location.Map && IsAdjacent(actor.Location.Position, sprayOn.Location.Position)))
                {
                    reason = "not adjacent";
                    return false;
                }
            }

            // all clear.
            reason = "";
            return true;
        }
        #endregion
        #region Movement/Melee
        public bool CanActorRun(Actor actor)
        {
            string reason;
            return CanActorRun(actor, out reason);
        }

        public bool CanActorRun(Actor actor, out string reason)
        {
            if (actor == null)
                throw new ArgumentNullException("actor");

            //////////////////////////////
            // Cant run if any is true:
            // 1. Has not CanRun ability.
            // 2. Stamina below min level.
            //////////////////////////////

            // 1. Has not CanRun ability.
            if (!actor.Model.Abilities.CanRun)
            {
                reason = "no ability to run";
                return false;
            }

            // 2. Stamina below min level.
            if (actor.StaminaPoints < STAMINA_MIN_FOR_ACTIVITY)
            {
                reason = "not enough stamina to run";
                return false;
            }

            // all clear.
            reason = "";
            return true;
        }

        public bool IsActorTired(Actor actor)
        {
            if (actor == null)
                throw new ArgumentNullException("actor");

            return actor.Model.Abilities.CanTire && actor.StaminaPoints < STAMINA_MIN_FOR_ACTIVITY;
        }

        public bool CanActorMeleeAttack(Actor actor, Actor target)
        {
            string reason;
            return CanActorMeleeAttack(actor, target, out reason);
        }

        public bool CanActorMeleeAttack(Actor actor, Actor target, out string reason)
        {
            if (actor == null)
                throw new ArgumentNullException("actor");
            if (target == null)
                throw new ArgumentNullException("target");

            //////////////////////////////
            // Cant run if any is true:
            // 1. Target not adjacent in map or not sharing an exit.
            // 2. Stamina below min level.
            // 3. Target is dead (doh!).
            //////////////////////////////

            // 1. Target not adjacent in map or not sharing an exit.
            if (actor.Location.Map == target.Location.Map)
            {
                if (!IsAdjacent(actor.Location.Position, target.Location.Position))
                {
                    reason = "not adjacent";
                    return false;
                }
            }
            else
            {
                // check there is an exit at both positions.
                Exit fromExit = actor.Location.Map.GetExitAt(actor.Location.Position);
                if (fromExit == null)
                {
                    reason = "not reachable";
                    return false;
                }
                Exit toExit = target.Location.Map.GetExitAt(target.Location.Position);
                if (toExit == null)
                {
                    reason = "not reachable";
                    return false;
                }
                // check the target stands on the exit.
                if (fromExit.ToMap != target.Location.Map ||fromExit.ToPosition != target.Location.Position)
                {
                    reason = "not reachable";
                    return false;
                }
            }

            // 2. Stamina below min level.
            if (actor.StaminaPoints < STAMINA_MIN_FOR_ACTIVITY)
            {
                reason = "not enough stamina to attack";
                return false;
            }

            // 3. Target is dead (doh!).
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

        public bool HasActorJumpAbility(Actor actor)
        {
            return actor.Model.Abilities.CanJump ||
                actor.Sheet.SkillTable.GetSkillLevel((int)Skills.IDs.AGILE) > 0 ||
                actor.Sheet.SkillTable.GetSkillLevel((int)Skills.IDs.Z_AGILE) > 0;
        }

        public bool IsWalkableFor(Actor actor, Map map, int x, int y)
        {
            string reason;
            return IsWalkableFor(actor, map, x, y, out reason);
        }

        public bool IsWalkableFor(Actor actor, Map map, int x, int y, out string reason)
        {
            if (map == null)
                throw new ArgumentNullException("map");
            if (actor == null)
                throw new ArgumentNullException("actor");

            ////////////////////////////////
            // Not walkable if any is true:
            // 1. Out of map.
            // 2. Tile not walkable.
            // 3. Map object not passable.
            // 4. An actor already there.
            // 5. Dragging a corpse when tired.
            ////////////////////////////////
            // 1. Out of map.
            if (!map.IsInBounds(x, y))
            {
                reason = "out of map";
                return false;
            }

            // 2. Tile not walkable.
            if (!map.GetTileAt(x, y).Model.IsWalkable)
            {
                reason = "blocked";
                return false;
            }

            // 3. Map object not passable.
            MapObject mapObj = map.GetMapObjectAt(x, y);
            if (mapObj != null)
            {
                if (!mapObj.IsWalkable)
                {
                    // jump?
                    if (mapObj.IsJumpable)
                    {
                        if (!HasActorJumpAbility(actor))
                        {
                            reason = "cannot jump";
                            return false;
                        }
                        if (actor.StaminaPoints < STAMINA_COST_JUMP)
                        {
                            reason = "not enough stamina to jump";
                            return false;
                        }
                    }
                    // small actor blocked only by closed doors.
                    else if (actor.Model.Abilities.IsSmall)
                    {
                        DoorWindow door = mapObj as DoorWindow;
                        if (door != null && door.IsClosed)
                        {
                            reason = "cannot slip through closed door";
                            return false;
                        }
                    }
                    else
                    {
                        // nope, blocking object.
                        reason = "blocked by object";
                        return false;
                    }
                }
            }

            // 4. An actor already there.
            if (map.GetActorAt(x, y) != null)
            {
                reason = "someone is there";
                return false;
            }

            // 5. Dragging a corpse when tired.
            if (actor.DraggedCorpse != null && IsActorTired(actor))
            {
                reason = "dragging a corpse when tired";
                return false;
            }

            // all checks clear.
            reason = "";
            return true;
        }

        public bool IsWalkableFor(Actor actor, Location location)
        {
            string reason;
            return IsWalkableFor(actor, location, out reason);
        }

        public bool IsWalkableFor(Actor actor, Location location, out string reason)
        {
            return IsWalkableFor(actor, location.Map, location.Position.X, location.Position.Y, out reason);
        }

        public ActorAction IsBumpableFor(Actor actor, RogueGame game, Map map, int x, int y, out string reason)
        {
            if (map == null)
                throw new ArgumentNullException("map");
            if (actor == null)
                throw new ArgumentNullException("actor");
            reason = "";

            ///////////////////////////////
            // Out of map : leave district?
            ///////////////////////////////
            if (!map.IsInBounds(x, y))
            {
                if (CanActorLeaveMap(actor, out reason))
                {
                    reason = "";
                    return new ActionLeaveMap(actor, game, new Point(x, y));
                }
                else
                {
                    return null;
                }
            }

            //////////////////////////////////////////
            // 1. Movement?
            // 2. Actor interact: fight or chat?
            // 3. Object interact?
            // 4. No action possible.
            //////////////////////////////////////////
            Point to = new Point(x, y);

            // 1. Move?
            ActionMoveStep moveAction = new ActionMoveStep(actor, game, to);
            if (moveAction.IsLegal())
            {
                reason = "";
                return moveAction;
            }
            else
                reason = moveAction.FailReason;

            // 2. Actor interact: fight/chat/(AI:switch place)
            #region
            Actor targetActor = map.GetActorAt(to);
            if (targetActor != null)
            {
                // attacking?
                if (AreEnemies(actor, targetActor))
                {
                    if (CanActorMeleeAttack(actor, targetActor, out reason))
                        return new ActionMeleeAttack(actor, game, targetActor);
                    else
                        return null;
                }

                // AI: switching place  // alpha10.1 handle bot like it was an AI
                if((!actor.IsPlayer || actor.IsBotPlayer) &&
                    !targetActor.IsPlayer
                    && CanActorSwitchPlaceWith(actor, targetActor, out reason))
                    return new ActionSwitchPlace(actor, game, targetActor);

                // chatting?
                if (CanActorChatWith(actor, targetActor, out reason))
                    return new ActionChat(actor, game, targetActor);

                // nope, blocked.
                return null;
            }
            #endregion

            // 3. Object interact?
            #region
            MapObject mapObj = map.GetMapObjectAt(to);
            if (mapObj != null)
            {
                // 3.1 Door?
                #region
                DoorWindow door = mapObj as DoorWindow;
                if (door != null)
                {
                    if (door.IsClosed)  // closed: open/bash/barricade
                    {
                        if (IsOpenableFor(actor, door, out reason))
                            return new ActionOpenDoor(actor, game, door);
                        else if (IsBashableFor(actor, door, out reason))
                            return new ActionBashDoor(actor, game, door);
                        else
                            return null;
                    }
                    if (door.BarricadePoints > 0) // barricaded: bash
                    {
                        if (IsBashableFor(actor, door, out reason))
                            return new ActionBashDoor(actor, game, door);
                        else
                        {
                            reason = "cannot bash the barricade";
                            return null;
                        }
                    }
                }
                #endregion

                // 3.2 Container?
                if (CanActorGetItemFromContainer(actor, to, out reason))
                    return new ActionGetFromContainer(actor, game, to);

                // alpha10.1 removed break restriction, made civs ai get stuck in some rare cases but we punish break actions in baseai wander.
                //// 3.3 Break?
                if (IsBreakableFor(actor, mapObj, out reason))
                    return new ActionBreak(actor, game, mapObj);
                //// 3.3 Break? Only allow bashing actors to do that (don't want Civilians to bash stuff on their way)
                //if (actor.Model.Abilities.CanBashDoors && IsBreakableFor(actor, mapObj, out reason))
                //    return new ActionBreak(actor, game, mapObj);

                // 3.4 Power Generator?
                PowerGenerator powGen = mapObj as PowerGenerator;
                if (powGen != null)
                {
                    // Recharge battery powered item?
                    if (powGen.IsOn)
                    {
                        Item leftItem = actor.GetEquippedItem(DollPart.LEFT_HAND);
                        if (leftItem != null && CanActorRechargeItemBattery(actor, leftItem, out reason))
                            return new ActionRechargeItemBattery(actor, game, leftItem);
                        Item rightItem = actor.GetEquippedItem(DollPart.RIGHT_HAND);
                        if (rightItem != null && CanActorRechargeItemBattery(actor, rightItem, out reason))
                            return new ActionRechargeItemBattery(actor, game, rightItem);
                    }

                    // Switch?
                    if(IsSwitchableFor(actor, powGen, out reason))
                        // switch it.
                        return new ActionSwitchPowerGenerator(actor, game, powGen);

                    // Can do nothing by bumping.
                    return null;
                }

            }
            #endregion

            // 4. No action possible?
            //reason = "blocked";
            return null;

        }

        public ActorAction IsBumpableFor(Actor actor, RogueGame game, Location location)
        {
            string reason;
            return IsBumpableFor(actor, game, location, out reason);
        }

        public ActorAction IsBumpableFor(Actor actor, RogueGame game, Location location, out string reason)
        {
            return IsBumpableFor(actor, game, location.Map, location.Position.X, location.Position.Y, out reason);
        }
        #endregion
    }
}
