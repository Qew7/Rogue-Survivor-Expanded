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
        #region Equiping items
        // alpha10 BehaviorEquipWeapon obsolete
#if false
        [Obsolete]
        protected ActorAction BehaviorEquipWeapon(RogueGame game)
        {
        #region Ranged first
            // If already equiped a ranged weapon, we might want to reload it.
            Item eqWpn = GetEquippedWeapon();
            if (eqWpn != null && eqWpn is ItemRangedWeapon)
            {
                // ranged weapon equipped, if directive disabled unequip it!
                if (!this.Directives.CanFireWeapons)
                    return new ActionUnequipItem(m_Actor, game, eqWpn);

                // ranged weapon equipped, reload it?
                ItemRangedWeapon rw = eqWpn as ItemRangedWeapon;
                if (rw.Ammo <= 0)
                {
                    // reload it if we can.
                    ItemAmmo ammoIt = GetCompatibleAmmoItem(game, rw);
                    if (ammoIt != null)
                        return new ActionUseItem(m_Actor, game, ammoIt);
                }
                else
                    // nope, ranged equipped with ammo, nothing more to do with it.
                    return null;
            }

            // No ranged weapon equipped or equipped but out of ammo and no ammos to reload.
            // Equip other best available ranged weapon, if allowed to fire.
            if (this.Directives.CanFireWeapons)
            {
                Item newRanged = GetBestRangedWeaponWithAmmo((it) => !IsItemTaboo(it));
                if (newRanged != null)
                {
                    // equip new.
                    if (game.Rules.CanActorEquipItem(m_Actor, newRanged))
                        return new ActionEquipItem(m_Actor, game, newRanged);
                }
            }
        #endregion

        #region Melee second
            // Get best melee weapon in inventory.
            ItemMeleeWeapon bestMeleeWeapon = GetBestMeleeWeapon(game, (it) => !IsItemTaboo(it));

            // If none, nothing to do.
            if (bestMeleeWeapon == null)
                return null;

            // If it is already equipped, done.
            if (eqWpn == bestMeleeWeapon)
                return null;

            // If no weapon equipped, equip best now.
            if (eqWpn == null)
            {
                if (game.Rules.CanActorEquipItem(m_Actor, bestMeleeWeapon))
                    return new ActionEquipItem(m_Actor, game, bestMeleeWeapon);
                else
                    return null;
            }

            // Another weapon equipped, unequip it.
            if (eqWpn != null)
            {
                if (game.Rules.CanActorUnequipItem(m_Actor, eqWpn))
                    return new ActionUnequipItem(m_Actor, game, eqWpn);
                else
                    return null;
            }
        #endregion

            // Fail.
            return null;
        }
#endif

        protected ActorAction BehaviorEquipBestBodyArmor(RogueGame game)
        {
            // Get best armor available.
            ItemBodyArmor bestArmor = GetBestBodyArmor(game, (it) => !IsItemTaboo(it));

            // If none, don't bother.
            if (bestArmor == null)
                return null;

            // If already equipped, fine.
            Item eqArmor = GetEquippedBodyArmor();
            if (eqArmor == bestArmor)
                return null;

            // If another armor already equipped, unequip it first.
            if (eqArmor != null)
            {
                if (game.Rules.CanActorUnequipItem(m_Actor, eqArmor))
                    return new ActionUnequipItem(m_Actor, game, eqArmor);
                else
                    return null;
            }

            // Equip the new armor.
            if (eqArmor == null)
            {
                if (game.Rules.CanActorEquipItem(m_Actor, bestArmor))
                    return new ActionEquipItem(m_Actor, game, bestArmor);
                else
                    return null;
            }

            // Fail.
            return null;
        }

        protected ActorAction BehaviorEquipCellPhone(RogueGame game)
        {
            // Only equip cellphone if :
            // - is a leader.
            // - or if leader does.
            bool wantTracker = false;
            if (m_Actor.CountFollowers > 0)
                wantTracker = true;
            else if (m_Actor.HasLeader)
            {
                bool leaderHasTrackerEq = false;
                ItemTracker leaderTr = m_Actor.Leader.GetEquippedItem(DollPart.LEFT_HAND) as ItemTracker;
                if (leaderTr == null)
                    leaderHasTrackerEq = false;
                else if (leaderTr.CanTrackFollowersOrLeader)
                    leaderHasTrackerEq = true;

                wantTracker = leaderHasTrackerEq;
            }

            // If already equiped a cellphone, nothing to do or unequip it.
            Item eqTrack = GetEquippedCellPhone();
            if (eqTrack != null)
            {
                if (!wantTracker && game.Rules.CanActorUnequipItem(m_Actor, eqTrack))
                    return new ActionUnequipItem(m_Actor, game, eqTrack);
                else
                    return null;
            }

            if (!wantTracker)
                return null;

            // Equip first available cellphone.
            Item newTracker = GetFirstTracker((it) => it.CanTrackFollowersOrLeader && !IsItemTaboo(it));
            if (newTracker != null)
            {
                // equip new.
                if (game.Rules.CanActorEquipItem(m_Actor, newTracker))
                    return new ActionEquipItem(m_Actor, game, newTracker);
            }

            // Fail.
            return null;
        }

        protected ActorAction BehaviorUnequipCellPhoneIfLeaderHasNot(RogueGame game)
        {
            // alpha10
            // if we are leader, dont unequip.
            if (m_Actor.CountFollowers > 0)
                return null;

            // get left eq item.
            ItemTracker tr = m_Actor.GetEquippedItem(DollPart.LEFT_HAND) as ItemTracker;
            if (tr == null)
                return null;
            if (!tr.CanTrackFollowersOrLeader)
                return null;

            // we have a cell phone equiped.
            // unequip if leader has not one equiped.
            ItemTracker leaderTr = m_Actor.Leader.GetEquippedItem(DollPart.LEFT_HAND) as ItemTracker;
            if (leaderTr == null || !leaderTr.CanTrackFollowersOrLeader)
            {
                // unequip!
                if (game.Rules.CanActorUnequipItem(m_Actor, tr))
                    return new ActionUnequipItem(m_Actor, game, tr);
            }

            // fail.
            return null;
        }

        protected ActorAction BehaviorUnequipLeftItem(RogueGame game)
        {
            // get left eq item.
            Item eqLeft = m_Actor.GetEquippedItem(DollPart.LEFT_HAND);
            if (eqLeft == null)
                return null;

            // try to unequip it.
            if (game.Rules.CanActorUnequipItem(m_Actor, eqLeft))
                return new ActionUnequipItem(m_Actor, game, eqLeft);

            // fail.
            return null;
        }

        // alpha10
        /// <summary>
        /// Get action to perform to manage the best ranged weapon we have.
        /// - equip a new best ranged weapon
        /// - reload the one we have equiped
        /// - unequip a completly out of ammo weapon
        /// - nothing if we already have equiped the best we have
        /// </summary>
        /// <param name="game"></param>
        /// <returns>null if not wanting an action</returns>
        protected ActorAction BehaviorEquipBestRangedWeapon(RogueGame game)
        {
            // get best range weapon with ammo we have
            ItemRangedWeapon best = GetBestRangedWeaponWithAmmo((it) => !IsItemTaboo(it));

            if (best == null)
            {
                // useless equipped rw we should unequip (best is null in this case since no ammo in inv):
                // if we have a rw equiped but out of ammo and no ammo to reload it, unequip, leave hand free for melee weapon.
                ItemRangedWeapon eqRw = m_Actor.GetEquippedRangedWeapon();
                if (eqRw != null && eqRw.Ammo == 0 && GetCompatibleAmmoItem(game, eqRw, false) == null)
                    return new ActionUnequipItem(m_Actor, game, eqRw);

                // no rw with ammo to equip
                return null;
            }

            if (best.IsEquipped)
            {
                // if out of ammo try to reload, else unequip to make room for a melee weapon
                if (best.Ammo == 0)
                {
                    ItemAmmo ammo = GetCompatibleAmmoItem(game, best, true);
                    if (ammo != null)
                        return new ActionUseItem(m_Actor, game, ammo);
                    else
                        return new ActionUnequipItem(m_Actor, game, best);
                }

                // best ranged weapon equiped & has ammo, we're fine.
                return null;
            }

            // if not equiped but out of ammo and no ammo to reload it, dont equip, leave hand free for melee weapon.
            if (best.Ammo == 0 && GetCompatibleAmmoItem(game, best, false) == null)
                return null;

            // replace current weapon with best one
            return BehaviourReplaceEquipped(game, m_Actor.GetEquippedWeapon(), best);
        }

        protected ActorAction BehaviorEquipBestMeleeWeapon(RogueGame game)
        {
            Item best = GetBestMeleeWeapon(game, (it) => !IsItemTaboo(it));

            if (best == null)
                return null;
            if (best.IsEquipped)
                return null;

            return BehaviourReplaceEquipped(game, m_Actor.GetEquippedWeapon(), best);
        }

        protected ActorAction BehaviorEquipBestLight(RogueGame game)
        {
            ItemLight best = GetBestLight(game, (it) => !IsItemTaboo(it));

            if (best == null)
                return null;
            if (best.IsEquipped)
            {
                // unequip if light out of batteries
                if (best.Batteries <= 0)
                    return new ActionUnequipItem(m_Actor, game, best);
                // already got best light equiped
                return null;
            }
            // don't equip if out of batteries
            if (best.Batteries <= 0)
                return null;

            // replace current left hand item with best one
            return BehaviourReplaceEquipped(game, m_Actor.GetEquippedItem(DollPart.LEFT_HAND), best);
        }

        protected ActorAction BehaviorEquipBestCellPhone(RogueGame game)
        {
            ItemTracker best = GetBestCellPhone(game, (it) => !IsItemTaboo(it));

            if (best == null)
                return null;
            if (best.IsEquipped)
            {
                // unequip if phone out of batteries
                if (best.Batteries <= 0)
                    return new ActionUnequipItem(m_Actor, game, best);
                // already got best spray equiped
                return null;
            }
            // don't equip if out of batteries
            if (best.Batteries <= 0)
                return null;

            // replace current left hand item with best one
            return BehaviourReplaceEquipped(game, m_Actor.GetEquippedItem(DollPart.LEFT_HAND), best);
        }

        protected ActorAction BehaviorEquipBestStenchKiller(RogueGame game)
        {
            ItemSprayScent best = GetBestStenchKiller(game, (it) => !IsItemTaboo(it));

            if (best == null)
                return null;
            if (best.IsEquipped)
            {
                // unequip if out of spray
                if (best.SprayQuantity <= 0)
                    return new ActionUnequipItem(m_Actor, game, best);
                return null;
            }
            // don't equip if out of spray
            if (best.SprayQuantity <= 0)
                return null;

            // replace current left hand item with best one
            return BehaviourReplaceEquipped(game, m_Actor.GetEquippedItem(DollPart.LEFT_HAND), best);
        }

        protected bool WantsCellPhoneEquipped(RogueGame game)
        {
            // follower wants if leader has one equipped,
            // leader wants if any follower has one with batteries in its inventory
            if (m_Actor.HasLeader)
            {
                ItemTracker leaderPhone = m_Actor.Leader.GetEquippedItem(DollPart.LEFT_HAND) as ItemTracker;
                if (leaderPhone != null && leaderPhone.CanTrackFollowersOrLeader)
                    return true;
                return false;
            }
            else if (m_Actor.CountFollowers > 0)
            {
                foreach (Actor follower in m_Actor.Followers)
                {
                    if (follower.Inventory.HasItemMatching((it) =>
                        {
                            ItemTracker followerPhone = it as ItemTracker;
                            if (followerPhone != null && followerPhone.CanTrackFollowersOrLeader && followerPhone.Batteries > 0)
                                return true;
                            return false;
                        }))
                        return true;
                }
                return false;
            }
            else
                return false;
        }

        /// <summary>
        /// Will try to unequip old item first, then equip the new one.
        /// </summary>
        /// <param name="game"></param>
        /// <param name="equipped">can be null</param>
        /// <param name="replaceWith">can be null</param>
        /// <returns></returns>
        protected ActorAction BehaviourReplaceEquipped(RogueGame game, Item equipped, Item replaceWith)
        {
            if (equipped != null)
            {
                if (game.Rules.CanActorUnequipItem(m_Actor, equipped))
                    return new ActionUnequipItem(m_Actor, game, equipped);
                return null;
            }
            if (replaceWith != null)
            {
                if (game.Rules.CanActorEquipItem(m_Actor, replaceWith))
                    return new ActionEquipItem(m_Actor, game, replaceWith);
            }
            return null;
        }

        /// <summary>
        /// Equip best items available : armors, weapons, lights/sprays/phones.
        /// Will ignore an equipment slot that is reserved by other behaviors (taboo).
        /// Actions:
        /// - equip an item
        /// - unequip an item
        /// - reload a weapon
        /// </summary>
        /// <param name="game"></param>
        /// <param name="allowCellPhones"></param>
        /// <param name="allowStenchKiller"></param>
        /// <returns></returns>
        /// <see cref="IsEquipmentSlotTaboo(DollPart)"/>
        protected ActorAction BehaviorEquipBestItems(RogueGame game, bool allowCellPhones, bool allowStenchKiller)
        {
            ActorAction action;

            // keep in mind which equipment slots are reserved by other behaviors and dont mess with them
            bool canUseTorso = !IsEquipmentSlotTaboo(DollPart.TORSO);
            bool canUseRightHand = !IsEquipmentSlotTaboo(DollPart.RIGHT_HAND);
            bool canUseLeftHand = !IsEquipmentSlotTaboo(DollPart.LEFT_HAND);

            // armor
            if (canUseTorso)
            {
                action = BehaviorEquipBestBodyArmor(game);
                if (action != null)
                    return action;
            }

            // right hand weapons, prefering ranged weapon over melee in most cases.
            if (canUseRightHand)
            {
                // equip best ranged weapon only if not forbidden by directives or ai
                if (Directives.CanFireWeapons && !(m_Actor.Model as ActorModel).Abilities.AI_NotInterestedInRangedWeapons)
                {
                    action = BehaviorEquipBestRangedWeapon(game);
                    if (action != null)
                        return action;
                }
                else
                {
                    ItemRangedWeapon equippedRw = GetEquippedWeapon() as ItemRangedWeapon;
                    if (equippedRw != null)
                        return new ActionUnequipItem(m_Actor, game, equippedRw);
                }

                // equip melee only if no ranged weapon equiped and not skilled martial artist
                if (!HasEquipedRangedWeapon(m_Actor) && m_Actor.Sheet.SkillTable.GetSkillLevel((int)Skills.IDs.MARTIAL_ARTS) < 3)
                {
                    action = BehaviorEquipBestMeleeWeapon(game);
                    if (action != null)
                        return action;
                }
                else
                {
                    ItemMeleeWeapon equippedMw = GetEquippedWeapon() as ItemMeleeWeapon;
                    if (equippedMw != null)
                        return new ActionUnequipItem(m_Actor, game, equippedMw);
                }
            }

            // left-hand items
            if (canUseLeftHand)
            {
                // ordered by priority: cellphone -> lights -> spray
                ItemTracker eqCellphone = GetEquippedCellPhone();
                ItemLight eqLight = GetEquippedLight();
                ItemSprayScent eqStenchKiller = GetEquippedStenchKiller();

                // cellphone
                if (allowCellPhones && WantsCellPhoneEquipped(game))
                {
                    action = BehaviorEquipBestCellPhone(game);
                    if (action != null)
                        return action;
                }
                else
                {
                    if (eqCellphone != null)
                        return new ActionUnequipItem(m_Actor, game, eqCellphone);
                }

                // lights, if no cellphone equipped
                if (eqCellphone == null)
                {
                    if (NeedsLight(game))
                    {
                        action = BehaviorEquipBestLight(game);
                        if (action != null)
                            return action;
                    }
                    else
                    {
                        // doesnt need light, unequip if equipped.
                        if (eqLight != null)
                            return new ActionUnequipItem(m_Actor, game, eqLight);
                    }
                }

                // spray scent, if no cellphone or light equipped
                if (eqCellphone == null && eqLight == null)
                {
                    if (allowStenchKiller)
                    {
                        action = BehaviorEquipBestStenchKiller(game);
                        if (action != null)
                            return action;
                    }
                    else
                    {
                        if (eqStenchKiller != null)
                            return new ActionUnequipItem(m_Actor, game, eqStenchKiller);
                    }
                }
            }

            // no equipement action to do
            return null;
        }
        #endregion
        #region Getting items
        protected ActorAction BehaviorGrabFromStack(RogueGame game, Point position, Inventory stack, bool canBreak, bool canPush)
        {
            // ignore empty stacks.
            if (stack == null || stack.IsEmpty)
                return null;

            // fix: don't try to get items under blocking map objects - bumping will say "yes can move" but we actually cannot take it.
            MapObject objThere = m_Actor.Location.Map.GetMapObjectAt(position);
            if (objThere != null)
            {
                // un-walkable fortification
                Fortification fort = objThere as Fortification;
                if (fort != null && !fort.IsWalkable)
                    return null;
                // barricaded door/window
                DoorWindow door = objThere as DoorWindow;
                if (door != null && door.IsBarricaded)
                    return null;
            }

            // for each item in the stack, consider only the takeable and interesting ones.
            Item goodItem = null;
            foreach (Item it in stack.Items)
            {
                // if can't take, ignore.
                if (!game.Rules.CanActorGetItem(m_Actor, it))
                    continue;
                // if not interesting, ignore.
                if (!IsInterestingItemToOwn(game, it, ItemSource.GROUND_STACK))
                    continue;
                // gettable and interesting, get it.
                goodItem = it;
                break;
            }

            // if no good item, ignore.
            if (goodItem == null)
                return null;

            // take it!
            Item takeIt = goodItem;

            // emote?
            if (game.Rules.RollChance(EMOTE_GRAB_ITEM_CHANCE))
                game.DoEmote(m_Actor, String.Format("{0}! Great!", takeIt.AName));

            // try to move/get one.
            if (position == m_Actor.Location.Position)
                return new ActionTakeItem(m_Actor, game, position, takeIt);
            else
                return BehaviorIntelligentBumpToward(game, position, canBreak, canPush);
        }

        // alpha10 made improved get item rule into a new behaviour; need taboo tile upkeep by caller though!
        protected ActorAction BehaviorGoGetInterestingItems(RogueGame game, List<Percept> mapPercepts, bool canBreak, bool canPush, string cantGetItemEmote, bool setLastItemsSaw, ref Percept lastItemsSaw)
        {
            RouteFinder.SpecialActions allowedActions = RouteFinder.SpecialActions.JUMP | RouteFinder.SpecialActions.DOORS;

            Map map = m_Actor.Location.Map;

            List<Percept> interestingReachableStacks = FilterOut(game, FilterStacks(game, mapPercepts),
                (p) =>
                {
                    if (p.Turn != map.LocalTime.TurnCounter)
                        return true;
                    if (IsOccupiedByOther(map, p.Location.Position))
                        return true;
                    if (IsTileTaboo(p.Location.Position))
                        return true;
                    if (!HasAnyInterestingItem(game, p.Percepted as Inventory, ItemSource.GROUND_STACK))
                        return true;
                    // alpha10 check reachability
                    RouteFinder.SpecialActions a = allowedActions;
                    if (IsContainerAt(p.Location))
                        a |= RouteFinder.SpecialActions.ADJ_TO_DEST_IS_GOAL;
                    if (!CanReachSimple(game, p.Location.Position, a))
                        return true;
                    // can and wants to get it
                    return false;
                });

            if (interestingReachableStacks == null)
                return null;

            // update last percept saw.
            Percept nearestStack = FilterNearest(game, interestingReachableStacks);
            if (setLastItemsSaw)
                lastItemsSaw = nearestStack;

            // make room for food if needed.
            ActorAction makeRoomForFood = BehaviorMakeRoomForFood(game, interestingReachableStacks);
            if (makeRoomForFood != null)
            {
                m_Actor.Activity = Activity.IDLE;
                return makeRoomForFood;
            }

            // try to grab.
            ActorAction grabAction = BehaviorGrabFromStack(game, nearestStack.Location.Position, nearestStack.Percepted as Inventory, canBreak, canPush);
            if (grabAction != null)
            {
                m_Actor.Activity = Activity.IDLE;
                return grabAction;
            }

            // we can't grab the item. mark the tile as taboo.
            MarkTileAsTaboo(nearestStack.Location.Position);
            // emote
            game.DoEmote(m_Actor, cantGetItemEmote);
            // failed
            return null;
        }
        #endregion
        #region Droping items
        protected ActorAction BehaviorDropItem(RogueGame game, Item it)
        {
            if (it == null)
                return null;

            // 1. unequip?
            if (game.Rules.CanActorUnequipItem(m_Actor, it))
            {
                // mark item as taboo.
                MarkItemAsTaboo(it);

                // unequip.
                return new ActionUnequipItem(m_Actor, game, it);
            }

            // 2. drop?
            if (game.Rules.CanActorDropItem(m_Actor, it))
            {
                // unmark item as taboo.
                UnmarkItemAsTaboo(it);

                // drop.
                return new ActionDropItem(m_Actor, game, it);
            }

            // failed!
            return null;
        }

        protected ActorAction BehaviorDropUselessItem(RogueGame game)
        {
            if (m_Actor.Inventory.IsEmpty)
                return null;

            // unequip/drop first light/tracker/spray out of batteries/quantity.
            // alpha10 ammo with no compatible ranged weapon and inventory full
            // alpha10 duplicate ranged weapon with no ammo if inventory 50% full
            // alpha10 empty cans!
            bool isInvFull = m_Actor.Inventory.IsFull;
            bool isInv50Full = m_Actor.Inventory.CountItems >= (m_Actor.Inventory.MaxCapacity / 2);
            foreach (Item it in m_Actor.Inventory.Items)
            {
                bool dropIt = false;

                if (it is ItemLight)
                    dropIt = (it as ItemLight).Batteries <= 0;
                else if (it is ItemTracker)
                    dropIt = (it as ItemTracker).Batteries <= 0;
                else if (it is ItemSprayPaint)
                    dropIt = (it as ItemSprayPaint).PaintQuantity <= 0;
                else if (it is ItemSprayScent)
                    dropIt = (it as ItemSprayScent).SprayQuantity <= 0;
                // alpha10 ammo with no compatible ranged weapon and inventory full
                else if (isInvFull && it is ItemAmmo)
                {
                    if (GetCompatibleRangedWeapon(game, it as ItemAmmo) == null)
                        dropIt = true;
                }
                // alpha10 duplicate ranged weapon with no ammo if inventory 50% full
                else if (isInv50Full && it is ItemRangedWeapon)
                {
                    ItemRangedWeapon rw = it as ItemRangedWeapon;
                    if (rw.Ammo == 0)
                    {
                        // if we have the same rw with ammo, this one is useless as rw dont break.
                        // there is still the risk we get disarmed and would have loved a spare rw,
                        // so we are a bit conservative and drop it only if 50% inv full as we prob need
                        // the item slot for something else.
                        foreach (Item otherIt in m_Actor.Inventory.Items)
                        {
                            if (otherIt != it && otherIt.Model == it.Model)
                            {
                                if ((otherIt as ItemRangedWeapon).Ammo > 0)
                                {
                                    dropIt = true;
                                    break;
                                }
                            }
                        }
                    }
                }
                // alpha10 empty cans!
                else if (it.Model == game.GameItems.EMPTY_CAN) // comparing model instead of attributes is bad but makes sense in this case
                {
                    dropIt = true;
                }

                if (dropIt)
                    return BehaviorDropItem(game, it);
            }

            // nope.
            return null;
        }
        #endregion
        #region Resting, Eating & Sleeping
        protected ActorAction BehaviorRestIfTired(RogueGame game)
        {
            // if not tired, don't.
            if (m_Actor.StaminaPoints >= Rules.STAMINA_MIN_FOR_ACTIVITY)
                return null;

            // tired, rest.
            return new ActionWait(m_Actor, game);
        }

        protected ActorAction BehaviorEat(RogueGame game)
        {
            // find best edible eat.
            Item it = GetBestEdibleItem(game);
            if (it == null)
                return null;

            // i can haz it?
            if (!game.Rules.CanActorUseItem(m_Actor, it))
                return null;

            // eat it!
            return new ActionUseItem(m_Actor, game, it);
        }

        protected ActorAction BehaviorSleep(RogueGame game, HashSet<Point> FOV)
        {
            // can?
            if (!game.Rules.CanActorSleep(m_Actor))
                return null;

            // if next to a door/window, try moving away from it.
            Map map = m_Actor.Location.Map;
            if (map.HasAnyAdjacentInMap(m_Actor.Location.Position, (pt) => map.GetMapObjectAt(pt) is DoorWindow))
            {
                // wander where there is no door/window and not adjacent to a door window.
                ActorAction wanderAwayFromDoor = BehaviorWander(game,
                    (loc) => map.GetMapObjectAt(loc.Position) as DoorWindow == null && !map.HasAnyAdjacentInMap(loc.Position, (pt) => loc.Map.GetMapObjectAt(pt) is DoorWindow),
                    null);
                if (wanderAwayFromDoor != null)
                    return wanderAwayFromDoor;
                // no good spot, just try normal sleep behavior.
            }

            // sleep on a couch.
            if (game.Rules.IsOnCouch(m_Actor))
            {
                return new ActionSleep(m_Actor, game);
            }
            // find nearest couch.
            Point? couchPos = null;
            float nearestDist = float.MaxValue;
            foreach (Point p in FOV)
            {
                MapObject mapObj = map.GetMapObjectAt(p);
                if (mapObj != null && mapObj.IsCouch && map.GetActorAt(p) == null)
                {
                    float dist = game.Rules.StdDistance(m_Actor.Location.Position, p);
                    if (dist < nearestDist)
                    {
                        nearestDist = dist;
                        couchPos = p;
                    }
                }
            }
            // if we have a couch, try to get there.
            if (couchPos != null)
            {
                ActorAction moveThere = BehaviorIntelligentBumpToward(game, couchPos.Value, false, false);
                if (moveThere != null)
                {
                    return moveThere;
                }
            }

            // no couch or can't move there, sleep there.
            return new ActionSleep(m_Actor, game);
        }
        #endregion
        #region Healing & Entertainment
        protected ActorAction BehaviorUseMedecine(RogueGame game, int factorHealing, int factorStamina, int factorSleep, int factorCure, int factorSan)
        {
            // if no items, don't bother.
            Inventory inv = m_Actor.Inventory;
            if (inv == null || inv.IsEmpty)
                return null;

            // check needs.
            bool needHP = m_Actor.HitPoints < game.Rules.ActorMaxHPs(m_Actor);
            bool needSTA = game.Rules.IsActorTired(m_Actor);
            bool needSLP = m_Actor.Model.Abilities.HasToSleep && WouldLikeToSleep(game, m_Actor);
            bool needCure = m_Actor.Infection > 0;
            bool needSan = m_Actor.Model.Abilities.HasSanity && m_Actor.Sanity < (int)(0.75f * game.Rules.ActorMaxSanity(m_Actor));

            // if no need, don't.
            if (!needHP && !needSTA && !needSLP && !needCure && !needSan)
                return null;

            // list meds items.
            List<ItemMedicine> medItems = inv.GetItemsByType<ItemMedicine>();
            if (medItems == null)
                return null;

            // use best item.
            ChoiceEval<ItemMedicine> bestMedChoice = Choose<ItemMedicine>(game, medItems,
                (it) =>
                {
                    return true;
                },
                (it) =>
                {
                    int score = 0;
                    if (needHP) score += factorHealing * it.Healing;
                    if (needSTA) score += factorStamina * it.StaminaBoost;
                    if (needSLP) score += factorSleep * it.SleepBoost;
                    if (needCure) score += factorCure * it.InfectionCure;
                    if (needSan) score += factorSan * it.SanityCure;
                    return score;
                },
                (a, b) => a > b);

            // if no suitable items or best item scores zero, do not want!
            if (bestMedChoice == null || bestMedChoice.Value <= 0)
                return null;

            // use med.
            return new ActionUseItem(m_Actor, game, bestMedChoice.Choice);
        }

        protected ActorAction BehaviorUseEntertainment(RogueGame game)
        {
            Inventory inv = m_Actor.Inventory;
            if (inv.IsEmpty) return null;

            // use first entertainment item available.
            ItemEntertainment ent = (ItemEntertainment)inv.GetFirstByType(typeof(ItemEntertainment));
            if (ent == null) return null;

            if (!game.Rules.CanActorUseItem(m_Actor, ent))
                return null;

            return new ActionUseItem(m_Actor, game, ent);
        }

        protected ActorAction BehaviorDropBoringEntertainment(RogueGame game)
        {
            Inventory inv = m_Actor.Inventory;
            if (inv.IsEmpty) return null;

            foreach (Item it in inv.Items)
            {
                ItemEntertainment ent = it as ItemEntertainment;
                if (ent != null && ent.IsBoringFor(m_Actor))  // alpha10 boring items item centric
                    return new ActionDropItem(m_Actor, game, it);
            }

            return null;
        }

        #endregion
        #region Inventory management
        protected ActorAction BehaviorMakeRoomForFood(RogueGame game, List<Percept> stacks)
        {
            // if no items in view, fail.
            if (stacks == null || stacks.Count == 0)
                return null;

            // if inventory not full, no need.
            int maxInv = game.Rules.ActorMaxInv(m_Actor);
            if (m_Actor.Inventory.CountItems < maxInv)
                return null;

            // if food item in inventory, no need.
            if (HasItemOfType(typeof(ItemFood)))
                return null;

            // if no food item in view, fail.
            bool hasFoodVisible = false;
            foreach (Percept p in stacks)
            {
                Inventory inv = p.Percepted as Inventory;
                if (inv == null)
                    continue;

                if (inv.HasItemOfType(typeof(ItemFood)))
                {
                    hasFoodVisible = true;
                    break;
                }
            }
            if (!hasFoodVisible)
                return null;

            // want to get rid of an item.
            // order of preference:
            // 1. get rid of not interesting item.
            // 2. get rid of barricading material.
            // 3. get rid of light & sprays.
            // 4. get rid of ammo.
            // 5. get rid of entertainment  // alpha10
            // 6. get rid of medecine.
            // 7. last resort, get rid of random item.
            Inventory myInv = m_Actor.Inventory;

            // 1. get rid of not interesting item.
            Item notInteresting = myInv.GetFirstMatching((it) => !IsInterestingItemToOwn(game, it, ItemSource.OWNED));
            if (notInteresting != null)
                return BehaviorDropItem(game, notInteresting);

            // 2. get rid of barricading material.
            Item material = myInv.GetFirstMatching((it) => it is ItemBarricadeMaterial);
            if (material != null)
                return BehaviorDropItem(game, material);

            // 3. get rid of light & sprays.
            Item light = myInv.GetFirstMatching((it) => it is ItemLight);
            if (light != null)
                return BehaviorDropItem(game, light);
            Item spray = myInv.GetFirstMatching((it) => it is ItemSprayPaint);
            if (spray != null)
                return BehaviorDropItem(game, spray);
            spray = myInv.GetFirstMatching((it) => it is ItemSprayScent);
            if (spray != null)
                return BehaviorDropItem(game, spray);

            // 4. get rid of ammo.
            Item ammo = myInv.GetFirstMatching((it) => it is ItemAmmo);
            if (ammo != null)
                return BehaviorDropItem(game, ammo);

            // 5. get rid of entertainment  // alpha10
            Item ent = myInv.GetFirstMatching((it) => it is ItemEntertainment);
            if (ent != null)
                return BehaviorDropItem(game, ent);

            // 6. get rid of medecine.
            Item med = myInv.GetFirstMatching((it) => it is ItemMedicine);
            if (med != null)
                return BehaviorDropItem(game, med);

            // 7. last resort, get rid of random item.
            Item anyItem = myInv[game.Rules.Roll(0, myInv.CountItems)];
            return BehaviorDropItem(game, anyItem);
        }
        #endregion
        #region Sprays
        protected ActorAction BehaviorUseStenchKiller(RogueGame game)
        {
            ItemSprayScent spray = m_Actor.GetEquippedItem(DollPart.LEFT_HAND) as ItemSprayScent;

            // if no spray or empty, nope.
            if (spray == null)
                return null;
            if (spray.SprayQuantity <= 0)
                return null;
            // if not proper odor, nope.
            ItemSprayScentModel model = spray.Model as ItemSprayScentModel;
            if (model.Odor != Odor.SUPPRESSOR)  // alpha10
                return null;

            // alpha10
            // first check if wants to use it on self, then check on adj leader/follower
            Actor sprayOn = null;

            bool WantsToSprayOn(Actor a)
            {
                // never spray on player, could mess with his tactics
                if (a.IsPlayer)
                    return false;

                // only if self or adjacent
                if (!(a == m_Actor || game.Rules.IsAdjacent(m_Actor.Location, a.Location)))
                    return false;

                // dont spray if already suppressed for 2h or more
                if (a.OdorSuppressorCounter >= 2 * WorldTime.TURNS_PER_HOUR)
                    return false;

                // spot must be interesting to spray for either us or the target.
                if (IsGoodStenchKillerSpot(game, m_Actor.Location.Map, m_Actor.Location.Position))
                    return true;
                if (IsGoodStenchKillerSpot(game, a.Location.Map, a.Location.Position))
                    return true;
                return false;
            }

            // self?...
            if (WantsToSprayOn(m_Actor))
                sprayOn = m_Actor;
            else
            {
                // ...adj leader/mates/followers
                if (m_Actor.HasLeader)
                {
                    if (WantsToSprayOn(m_Actor.Leader))
                        sprayOn = m_Actor.Leader;
                    else
                    {
                        foreach (Actor mate in m_Actor.Leader.Followers)
                            if (sprayOn == null && mate != m_Actor && WantsToSprayOn(mate))
                                sprayOn = mate;
                    }
                }

                if (sprayOn == null && m_Actor.CountFollowers > 0)
                {
                    foreach (Actor follower in m_Actor.Followers)
                        if (sprayOn == null && WantsToSprayOn(follower))
                            sprayOn = follower;
                }
            }

            //  spray?
            if (sprayOn != null)
            {
                ActionSprayOdorSuppressor sprayIt = new ActionSprayOdorSuppressor(m_Actor, game, spray, sprayOn);
                if (sprayIt.IsLegal())
                    return sprayIt;
            }

            // nope.
            return null;
        }

        protected bool IsGoodStenchKillerSpot(RogueGame game, Map map, Point pos)
        {
            // alpha10 obsolete 1. Don't spray at an already sprayed spot.
            // 2. Spray in a good position:
            //    2.1 entering or leaving a building.
            //    2.2 a door/window.
            //    2.3 an exit.

            // alpha10 obsolete 1. Don't spray at an already sprayed spot.
            //if (map.GetScentByOdorAt(Odor.PERFUME_LIVING_SUPRESSOR, pos) > 0)
            //    return false;

            // 2. Spray in a good position:

            //    2.1 entering or leaving a building.
            bool wasInside = m_prevLocation.Map.GetTileAt(m_prevLocation.Position).IsInside;
            bool isInside = map.GetTileAt(pos).IsInside;
            if (wasInside != isInside)
                return true;
            //    2.2 a door/window.
            MapObject objThere = map.GetMapObjectAt(pos);
            if (objThere != null && objThere is DoorWindow)
                return true;
            //    2.3 an exit.
            if (map.GetExitAt(pos) != null)
                return true;

            // nope.
            return false;
        }
        #endregion
    }
}
