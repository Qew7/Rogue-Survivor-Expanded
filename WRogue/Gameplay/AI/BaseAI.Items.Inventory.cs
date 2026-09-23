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
        #region Items

        protected bool IsItemWorthTellingAbout(Item it)
        {
            if (it == null)
                return false;

            // items type to ignore:
            // - barricading material (planks drop a lot).
            if (it is ItemBarricadeMaterial)
                return false;

            // ignore items we are carrying (we have seen it then taken it)
            if (m_Actor.Inventory != null && !m_Actor.Inventory.IsEmpty && m_Actor.Inventory.Contains(it))
                return false;

            // ok.
            return true;
        }

        protected Item GetEquippedWeapon()
        {
            if (m_Actor.Inventory == null || m_Actor.Inventory.IsEmpty)
                return null;

            foreach (Item it in m_Actor.Inventory.Items)
                if (it.IsEquipped && it is ItemWeapon)
                    return it;

            return null;
        }

        /// <summary>
        /// Get best ranged weapon in our inventory that has ammo loaded or we have ammo to reload it.
        /// </summary>
        /// <param name="fn"></param>
        /// <returns></returns>
        protected ItemRangedWeapon GetBestRangedWeaponWithAmmo(Predicate<Item> fn = null)
        {
            if (m_Actor.Inventory == null || m_Actor.Inventory.IsEmpty)
                return null;

            ItemRangedWeapon best = null;
            int bestSc = 0;
            foreach (Item it in m_Actor.Inventory.Items)
            {
                ItemRangedWeapon w = it as ItemRangedWeapon;
                if (w != null && (fn == null || fn(it)))
                {
                    bool checkIt = false;
                    if (w.Ammo > 0)
                    {
                        checkIt = true;
                    }
                    else
                    {
                        // out of ammo, but do we have a matching ammo item in inventory we could reload it with?
                        foreach (Item itReload in m_Actor.Inventory.Items)
                        {
                            if (itReload is ItemAmmo && (fn == null || fn(itReload)))
                            {
                                ItemAmmo itAmmo = itReload as ItemAmmo;
                                if (itAmmo.AmmoType == w.AmmoType)
                                {
                                    checkIt = true;
                                    break;
                                }
                            }
                        }
                    }
                    if (checkIt)
                    {
                        int sc = ScoreRangedWeapon(w);
                        if (best == null || sc > bestSc)
                        {
                            best = w;
                            bestSc = sc;
                        }

                    }
                }
            }

            return best;
        }

        /// <summary>
        /// Score this rw over others. Prefer range then attack then ammo loaded.
        /// </summary>
        /// <param name="rWp"></param>
        /// <returns></returns>
        protected int ScoreRangedWeapon(ItemRangedWeapon rWp)
        {
            // prefer range then damage
            Attack a = (rWp.Model as ItemRangedWeaponModel).Attack;
            return 10000 * a.Range + 100 * a.DamageValue + rWp.Ammo;
        }

        protected Item GetFirstMeleeWeapon(Predicate<Item> fn)
        {
            if (m_Actor.Inventory == null || m_Actor.Inventory.IsEmpty)
                return null;

            foreach (Item it in m_Actor.Inventory.Items)
            {
                if (it is ItemMeleeWeapon && (fn == null || fn(it)))
                    return it;
            }

            return null;
        }

        protected Item GetFirstBodyArmor(Predicate<Item> fn)
        {
            if (m_Actor.Inventory == null || m_Actor.Inventory.IsEmpty)
                return null;

            foreach (Item it in m_Actor.Inventory.Items)
            {
                if (it is ItemBodyArmor && (fn == null || fn(it)))
                    return it;
            }

            return null;
        }

        protected ItemGrenade GetFirstGrenade(Predicate<Item> fn)
        {
            if (m_Actor.Inventory == null || m_Actor.Inventory.IsEmpty)
                return null;

            foreach (Item it in m_Actor.Inventory.Items)
            {
                if (it is ItemGrenade && (fn == null || fn(it)))
                    return it as ItemGrenade;
            }

            return null;
        }

        protected Item GetEquippedBodyArmor()
        {
            if (m_Actor.Inventory == null || m_Actor.Inventory.IsEmpty)
                return null;

            foreach (Item it in m_Actor.Inventory.Items)
                if (it.IsEquipped && it is ItemBodyArmor)
                    return it;

            return null;
        }

        protected ItemTracker GetEquippedCellPhone()
        {
            if (m_Actor.Inventory == null || m_Actor.Inventory.IsEmpty)
                return null;

            foreach (Item it in m_Actor.Inventory.Items)
                if (it.IsEquipped && it is ItemTracker)
                {
                    ItemTracker t = it as ItemTracker;
                    if (t.CanTrackFollowersOrLeader)
                        return t;
                }

            return null;
        }

        protected Item GetFirstTracker(Predicate<ItemTracker> fn)
        {
            if (m_Actor.Inventory == null || m_Actor.Inventory.IsEmpty)
                return null;

            foreach (Item it in m_Actor.Inventory.Items)
            {
                ItemTracker t = it as ItemTracker;
                if (t != null && (fn == null || fn(t)))
                    return it;
            }

            return null;
        }

        protected ItemLight GetEquippedLight()
        {
            if (m_Actor.Inventory == null || m_Actor.Inventory.IsEmpty)
                return null;

            foreach (Item it in m_Actor.Inventory.Items)
                if (it.IsEquipped && it is ItemLight)
                    return it as ItemLight;

            return null;
        }

        protected Item GetFirstLight(Predicate<Item> fn)
        {
            if (m_Actor.Inventory == null || m_Actor.Inventory.IsEmpty)
                return null;

            foreach (Item it in m_Actor.Inventory.Items)
            {
                if (it is ItemLight && (fn == null || fn(it)))
                    return it;
            }

            return null;
        }

        protected ItemSprayScent GetEquippedStenchKiller()
        {
            if (m_Actor.Inventory == null || m_Actor.Inventory.IsEmpty)
                return null;

            foreach (Item it in m_Actor.Inventory.Items)
                if (it.IsEquipped && it is ItemSprayScent)
                {
                    ItemSprayScentModel m = (it as ItemSprayScent).Model as ItemSprayScentModel;
                    if (m.Odor == Odor.SUPPRESSOR)  // alpha10
                        return it as ItemSprayScent;
                }

            return null;
        }

        protected ItemSprayScent GetFirstStenchKiller(Predicate<ItemSprayScent> fn)
        {
            if (m_Actor.Inventory == null || m_Actor.Inventory.IsEmpty)
                return null;

            foreach (Item it in m_Actor.Inventory.Items)
            {
                if (it is ItemSprayScent && (fn == null || fn(it as ItemSprayScent)))
                    return it as ItemSprayScent;
            }

            return null;
        }

        protected bool IsRangedWeaponOutOfAmmo(Item it)
        {
            ItemRangedWeapon w = it as ItemRangedWeapon;
            if (w == null)
                return false;
            return w.Ammo <= 0;
        }

        protected bool IsLightOutOfBatteries(Item it)
        {
            ItemLight l = it as ItemLight;
            if (l == null)
                return false;
            return l.Batteries <= 0;
        }

        protected Item GetBestEdibleItem(RogueGame game)
        {
            if (m_Actor.Inventory == null || m_Actor.Inventory.IsEmpty)
                return null;

            int turn = m_Actor.Location.Map.LocalTime.TurnCounter;
            int need = game.Rules.ActorMaxFood(m_Actor) - m_Actor.FoodPoints;
            Item bestFood = null;
            int bestScore = int.MinValue;
            foreach (Item it in m_Actor.Inventory.Items)
            {
                ItemFood foodIt = it as ItemFood;
                if (foodIt == null)
                    continue;

                // compute heuristic score.
                // - economize food : punish food wasting, the more waste the worse.
                // - keep non-perishable food : punish eating non-perishable food, the more nutrition the worse.
                int score = 0;

                int nutrition = game.Rules.FoodItemNutrition(foodIt, turn);
                int waste = nutrition - need;

                // - punish food wasting, the more waste the worse.
                if (waste > 0)
                    score -= waste;

                // - punish eating non-perishable food, the more nutrition the worse.
                if (!foodIt.IsPerishable)
                    score -= nutrition;

                // best?
                if (bestFood == null || score > bestScore)
                {
                    bestFood = foodIt;
                    bestScore = score;
                }
            }

            // return best.
            return bestFood;
        }

        // alpha10.1 added item/inventory source
        public enum ItemSource
        {
            /// <summary>
            /// Item is in a ground stack (could be a container, which is the same thing)
            /// </summary>
            GROUND_STACK,
            /// <summary>
            /// Item is in the actor own inventory.
            /// </summary>
            OWNED,
            /// <summary>
            /// Item is in another actor inventory.
            /// </summary>
            ANOTHER_ACTOR
        }

        /// <summary>
        /// Check if item is ok to have in inventory.
        /// Uses cases:
        /// - pickup: checking to pickup an item or not.
        /// - steal: gangs check to agress an npc to steal his item or not.
        /// - gift: how cool is it to be gifted this item by another actor.
        /// - drop: when dropping the item to make room for a food item.
        /// DO NOT USE FOR TRADING use RateItem() and RateTradeOffer() instead.
        /// </summary>
        /// <param name="game"></param>
        /// <param name="it"></param>
        /// <param name="itemSrc">where does the item comes from?</param>
        /// <returns></returns>
        /// <see cref="RateItem(RogueGame, Item, bool)"/>
        /// <see cref="RateTradeOffer(RogueGame, Actor, Item, Item)"/>
        public bool IsInterestingItemToOwn(RogueGame game, Item it, ItemSource itemSrc)
        {
            // alpha10 base idea is any non-junk non-taboo item is interesting.
            // using itemrating is consistent with new trade logic.
            // exception:
            // - reject anything new not food if no food and only one one slot left; needed to be consistent
            // with BehaviorMakeRoomForFood() or the npc will cycle drop-take-drop...

            // taboo
            if (IsItemTaboo(it))
                return false;

            // consistent with BehaviorMakeRoomForFood (was already in alpha9)
            if (itemSrc != ItemSource.OWNED && m_Actor.Inventory.CountItems >= m_Actor.Inventory.MaxCapacity - 1)
            {
                if (!(it is ItemFood) && (CountItemQuantityOfType(typeof(ItemFood)) == 0))
                    return false;
            }

            // alpha10.1 not interested in picking up safe traps from the ground : dont undo your or your friends traps!
            if (itemSrc == ItemSource.GROUND_STACK && it is ItemTrap)
            {
                ItemTrap itTrap = it as ItemTrap;
                if (game.Rules.IsSafeFromTrap(itTrap, m_Actor))
                    return false;
            }

            // then use normal rating as if was trading and accept anything non-junk.
            ItemRating rating = RateItem(game, it, false);
            return rating != ItemRating.JUNK;

#if false
            pre alpha10 logic, kept for reference.
            /////////////////////////////////////////////////////////////////////////////
            // Interesting items:
            // 0 Reject anything not food if only one slot left.
            // 1 Reject forbidden items.
            // 2 Reject spray paint.
            // 3 Reject activated traps.
            // 4 Food.
            // 5 Ranged weapons.
            // 6 Ammo.
            // 7 Other Weapons, Medicine.
            // 8 Lights.
            // 9 Reject primed explosives!
            // 10 Reject boring items.
            // 11 Rest.
            /////////////////////////////////////////////////////////////////////////////

            bool onlyOneSlotLeft = (m_Actor.Inventory.CountItems == game.Rules.ActorMaxInv(m_Actor) - 1);

            // 0 Reject anything not food if only one slot left.
            if (onlyOneSlotLeft)
            {
                if (it is ItemFood)
                    return true;
                else
                    return false;
            }

            // 1 Reject forbidden items.
            if (it.IsForbiddenToAI)
                return false;

            // 2 Reject spray paint.
            if (it is ItemSprayPaint)
                return false;

            // 3 Reject activated traps.
            if (it is ItemTrap)
            {
                if ((it as ItemTrap).IsActivated)
                    return false;
            }

            // 4 Food
            if (it is ItemFood)
            {
                // accept any food if hungry.
                if (game.Rules.IsActorHungry(m_Actor))
                    return true;

                bool hasEnoughFood = HasEnoughFoodFor(game, m_Actor.Sheet.BaseFoodPoints / 2);

                // food not urgent, only interested in not spoiled food and if need more.
                return !hasEnoughFood && !game.Rules.IsFoodSpoiled(it as ItemFood, m_Actor.Location.Map.LocalTime.TurnCounter);
            }

            // 5 Ranged weapons.
            // Reject is AI_NotInterestedInRangedWeapons flag set.
            // Reject empty if no matching ammo, not already 2 ranged weapons in inventory, and different than any weapon we already have.
            if (it is ItemRangedWeapon)
            {
                // ai flag.
                if (m_Actor.Model.Abilities.AI_NotInterestedInRangedWeapons)
                    return false;

                ItemRangedWeapon rw = it as ItemRangedWeapon;
                // empty and no matching ammo : no.
                if (rw.Ammo <= 0 && GetCompatibleAmmoItem(game, rw) == null)
                    return false;

                // already 1 ranged weapon = no
                if (CountItemsOfSameType(typeof(ItemRangedWeapon)) >= 1)
                    return false;

                // new item but same as a weapon we already have = no
                if (!m_Actor.Inventory.Contains(it) && HasItemOfModel(it.Model))
                    return false;

                // all clear, me want!
                return true;
            }

            // 6 Ammo : only if has matching weapon and if has less than two full stacks.
            if (it is ItemAmmo)
            {
                ItemAmmo am = it as ItemAmmo;
                if (GetCompatibleRangedWeapon(game, am) == null)
                    return false;
                return !HasAtLeastFullStackOfItemTypeOrModel(it, 2);
            }

            // 7 Melee weapons, Medecine
            // Reject melee weapons if we are skilled in martial arts or we alreay have 2.
            // Reject medecine if we alredy have full stacks.
            if (it is ItemMeleeWeapon)
            {
                // martial artists ignore melee weapons.
                if (m_Actor.Sheet.SkillTable.GetSkillLevel((int)Skills.IDs.MARTIAL_ARTS) > 0)
                    return false;
                // only two melee weapons max.
                int nbMeleeWeaponsInInventory = CountItemQuantityOfType(typeof(ItemMeleeWeapon));
                return nbMeleeWeaponsInInventory < 2;
            }
            if(it is ItemMedicine)
            {
                return !HasAtLeastFullStackOfItemTypeOrModel(it, 2);
            }

            // 8 Lights : ignore out of batteries.
            if (IsLightOutOfBatteries(it))
                return false;

            // 9 Reject primed explosives!
            if (it is ItemPrimedExplosive)
                return false;

            // 10 Reject boring items.
            if (m_Actor.IsBoredOf(it))
                return false;

            // 11 Rest : if has less than one full stack.
            return !HasAtLeastFullStackOfItemTypeOrModel(it, 1);
#endif
        }

        public bool HasAnyInterestingItem(RogueGame game, Inventory inv, ItemSource inventorySrc)
        {
            if (inv == null)
                return false;
            bool owned = (inv == m_Actor.Inventory);
            foreach (Item it in inv.Items)
                if (IsInterestingItemToOwn(game, it, inventorySrc))
                    return true;
            return false;
        }

        protected Item FirstInterestingItem(RogueGame game, Inventory inv, ItemSource inventorySrc)
        {
            if (inv == null)
                return null;
            bool owned = (inv == m_Actor.Inventory);
            foreach (Item it in inv.Items)
                if (IsInterestingItemToOwn(game, it, inventorySrc))
                    return it;
            return null;
        }

        // alpha10 new helpers

        public bool IsContainerAt(Location loc)
        {
            MapObject mobj = loc.Map.GetMapObjectAt(loc.Position);
            return mobj != null && mobj.IsContainer;
        }

        protected ItemMeleeWeapon GetBestMeleeWeapon(RogueGame game, Predicate<Item> fn = null)
        {
            ItemMeleeWeapon best = null;
            int bestScore = -1;

            foreach (Item it in m_Actor.Inventory.Items)
            {
                if (fn == null || fn(it))
                {
                    ItemMeleeWeapon mWp = it as ItemMeleeWeapon;
                    if (mWp != null)
                    {
                        int score = ScoreMeleeWeapon(mWp);
                        if (best == null || score > bestScore)
                        {
                            best = mWp;
                            bestScore = score;
                        }
                    }
                }
            }

            return best;
        }

        protected int ScoreMeleeWeapon(ItemMeleeWeapon mWp)
        {
            // prefer weapon with more dmg, then atk, then disarm, then less sta loss.
            Attack a = (mWp.Model as ItemMeleeWeaponModel).Attack;
            return 100000 * a.DamageValue + 1000 * a.HitValue + a.DisarmChance - a.StaminaPenalty;
        }

        /// <summary>
        /// Get best light in inventory with preference for currently equipped light to avoid infinite equip-unequip loops.
        /// Note that the returned light might have 0 batteries!
        /// </summary>
        /// <param name="game"></param>
        /// <param name="fn"></param>
        /// <returns></returns>
        protected ItemLight GetBestLight(RogueGame game, Predicate<Item> fn = null)
        {
            ItemLight equippedLight;
            ItemLight bestScoringLight = null;
            int bestScore = -1;
            ItemLight bestFovLight = null;
            int bestFov = -1;

            // keep using currently equipped light if it has the best fov and batteries left,
            // otherwise pick best scoring one.
            // we need to check equipped light because equipping a light actually consumes one battery
            // point (see RogueGame OnEquipItem, was added as an anti player fov exploit) and it will
            // make the ai loop forever switching between lights constantly since equip/unequip is a free ap action.

            equippedLight = GetEquippedLight();
            if (equippedLight != null)
            {
                bestFovLight = equippedLight;
                bestFov = (bestFovLight.Model as ItemLightModel).FovBonus;
                bestScore = ScoreLight(equippedLight);
                bestScoringLight = equippedLight;
            }

            foreach (Item it in m_Actor.Inventory.Items)
            {
                if (fn == null || fn(it))
                {
                    ItemLight light = it as ItemLight;
                    if (light != null && !light.IsEquipped) // skip equiped because we already scored it
                    {
                        int fov = (light.Model as ItemLightModel).FovBonus;
                        if (fov > bestFov)
                        {
                            bestFovLight = light;
                            bestFov = fov;
                        }

                        int score = ScoreLight(light);
                        if (bestScoringLight == null || score > bestScore)
                        {
                            bestScoringLight = light;
                            bestScore = score;
                        }
                    }
                }
            }

            if (bestFovLight == equippedLight)
                return equippedLight;
            return bestScoringLight;
        }

        protected int ScoreLight(ItemLight light)
        {
            // out of batteries sucks
            if (light.Batteries <= 0)
                return 0;

            // prefer range then batteries
            return 10000 * (light.Model as ItemLightModel).FovBonus + light.Batteries;
        }

        protected ItemTracker GetBestCellPhone(RogueGame game, Predicate<Item> fn = null)
        {
            // if one equipped with batteries, that's it.
            ItemTracker eqPhone = GetEquippedCellPhone();
            if (eqPhone != null && eqPhone.Batteries > 0 && (fn == null || fn(eqPhone)))
                return eqPhone;

            // find first phone with batteries
            return m_Actor.Inventory.GetFirstMatching((it) =>
                {
                    if (fn != null && !fn(it))
                        return false;
                    ItemTracker phone = it as ItemTracker;
                    return phone != null && phone.Batteries > 0 && phone.CanTrackFollowersOrLeader;
                }) as ItemTracker;
        }

        protected ItemSprayScent GetBestStenchKiller(RogueGame game, Predicate<Item> fn = null)
        {
            ItemSprayScent best = null;
            int bestScore = -1;

            foreach (Item it in m_Actor.Inventory.Items)
            {
                if (fn == null || fn(it))
                {
                    ItemSprayScent spray = it as ItemSprayScent;
                    if (spray != null)
                    {
                        int score = ScoreStenchKiller(spray);
                        if (best == null || score > bestScore)
                        {
                            best = spray;
                            bestScore = score;
                        }
                    }
                }
            }

            return best;
        }

        protected int ScoreStenchKiller(ItemSprayScent spray)
        {
            // out of spray sucks
            if (spray.SprayQuantity <= 0)
                return 0;

            ItemSprayScentModel mSpray = spray.Model as ItemSprayScentModel;

            // must be stench killer
            if (mSpray.Odor != Odor.SUPPRESSOR)  // alpha10
                return -1;

            // prefer stronger strength then spray quantity
            return 10000 * mSpray.Strength + spray.SprayQuantity;
        }

        protected int GetItemNutritionValue(RogueGame game, Item it)
        {
            ItemFood itFood = it as ItemFood;
            if (itFood == null)
                return 0;
            return game.Rules.ActorItemNutritionValue(m_Actor, itFood.Nutrition);
        }

        protected int GetTotalNutritionInInventory(RogueGame game)
        {
            int total = 0;

            foreach (Item it in m_Actor.Inventory.Items)
                total += GetItemNutritionValue(game, it);

            return total;
        }

        protected int CountFullAmmoStacksInInventoryFor(ItemRangedWeapon rWp)
        {
            int count = 0;

            foreach (Item it in m_Actor.Inventory.Items)
            {
                ItemAmmo itAmmo = it as ItemAmmo;
                if ((itAmmo != null) && (itAmmo.AmmoType == rWp.AmmoType))
                {
                    if (itAmmo.Quantity >= itAmmo.Model.StackingLimit)
                        count++;
                }
            }

            return count;
        }

        /// <summary>
        /// Total ammo for this weapon in our inventory, including ammo in the weapon.
        /// </summary>
        /// <param name="rWp"></param>
        /// <returns></returns>
        protected int CountTotalAmmoInInventoryFor(ItemRangedWeapon rWp)
        {
            int ammo = 0;

            // add weapon ammo
            ammo += rWp.Ammo;

            // add ammo from inventory
            foreach (Item it in m_Actor.Inventory.Items)
            {
                ItemAmmo itAmmo = it as ItemAmmo;
                if ((itAmmo != null) && (itAmmo.AmmoType == rWp.AmmoType))
                    ammo += itAmmo.Quantity;
            }

            return ammo;
        }

        protected int CountItemsFullStacksOfSameType(Type tt, Item excludingThisOne = null)
        {
            if (m_Actor.Inventory == null || m_Actor.Inventory.IsEmpty)
                return 0;

            int count = 0;
            foreach (Item otherIt in m_Actor.Inventory.Items)
            {
                if (otherIt != excludingThisOne && !otherIt.CanStackMore && otherIt.GetType() == tt)
                    count++;
            }

            return count;
        }

        // alpha10 new item rating and trading logic

        #endregion
    }
}
