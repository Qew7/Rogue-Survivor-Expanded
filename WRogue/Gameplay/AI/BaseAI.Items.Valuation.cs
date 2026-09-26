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
        public enum ItemRating
        {
            JUNK = 0,   // dont want it at all, the lowest possible rating.
            OKAY = 1,
            NEED = 2    // wants it to cover a need, the highest possible rating.
        };

        /// <summary>
        /// Rate item when trading for another item of a different type and in most some cases if checking is interesting to pick up/steal/not drop.
        ///
        /// Items of same type MUST be checked with RateItemExhange instead, as a new item might be
        /// an improvement over an old one, even if individually it would be rated as junk by RateItem.
        /// This is because items come from different sources : pick up new items (it replaces nothing and only its
        /// own worth is important), trade/replace items (it replace an old item and need to be compared to the
        /// one we lost).
        /// Eg: picking up another spray scent is junk if we have already one with spray left (rated Junk by RateItem)
        /// BUT exhanging a spray scent with more spray is better (exhange rated Accept by RateItemExhange).
        ///
        /// FIXME -- ideally we would like the AI to be able to go pickup say a better melee weapon and drop the worse
        /// one it had previously; but this needs a new behaviour; implement that BehaviorImproveOnItems() later...
        ///
        /// </summary>
        /// <param name="game"></param>
        /// <param name="it"></param>
        /// <param name="owned"></param>
        /// <returns></returns>
        /// <see cref="RateTradeOffer(RogueGame, Actor, Item, Item)"/>
        /// <see cref="IsInterestingItemToOwn(RogueGame, Item, bool)"/>
        /// <see cref="RateItemExhange(RogueGame, Item, Item)"/>
        public ItemRating RateItem(RogueGame game, Item it, bool owned)
        {
            //////////////////////////////////////////////////////
            // Junk :
            // j1 AI forbidden items.
            // **disabled; handled preventively in IsInterestingItemToOwn* Anything new not food if only one slot left.**
            // j3 Spray paint (ai never use it)
            // j4 Melee weapons if martial arts or enough.
            // j5 Unsafe activated traps and empty cans
            // j6 Primed explosives.
            // j7 Light/Tracker/Spray scent out of batteries/paint or if has already enough
            // j8 Entertainment: boring or already enough
            // j9 Ammo with no compatible ranged weapon.
            // j10 Ranged weapons
            //     - ai not interested in rw
            //     - no ammo for it
            //     - has already same model with more potential ammo
            //     - has already better scoring rw with ammo
            // j11 Barricading material if has already enough.
            // Need :
            // n1 Food if hungry or not enough food in inventory.
            // n2 Ammo for ranged weapon if not enough.
            // n3 Melee weapon if no ranged weapon with ammo.
            // n4 Ranged weapon if none with ammo.
            // n5 Any meds if none. Other meds if need it.
            // n6 Barricade material if none.
            // **disabled** n7 Light if bad fov.
            // n8 (Unprimed) Explosive if none.
            // n9 Armor if none.
            // n10 Entertainment if not sane.
            // Okay:
            // - Anything else.
            /////////////////////////////////////////////////////

            // Junk :
            // j1 AI forbidden items.
            if (it.IsForbiddenToAI)
                return ItemRating.JUNK;

            // j2 Anything new not food if only one slot left.
            //if (!owned && (m_Actor.Inventory.CountItems >= game.Rules.ActorMaxInv(m_Actor) - 1) && !(it is ItemFood))
            //    return ItemRating.JUNK;

            // j3 Spray paint.
            if (it is ItemSprayPaint)
                return ItemRating.JUNK;

            // j4 Melee weapons if martial arts or enough.
            if (it is ItemMeleeWeapon)
            {
                if (m_Actor.Sheet.SkillTable.GetSkillLevel((int)Skills.IDs.MARTIAL_ARTS) > 0)
                    return ItemRating.JUNK;

                // one melee weapon is enough
                if (CountItemsOfSameType(typeof(ItemMeleeWeapon), it) >= 1)
                    return ItemRating.JUNK;
            }

            // j5 Unsafe activated traps and empty cans
            if (it is ItemTrap)
            {
                ItemTrap tr = it as ItemTrap;
                if (tr.Model == game.GameItems.EMPTY_CAN)
                    return ItemRating.JUNK;
                if (tr.IsActivated && !game.Rules.IsSafeFromTrap(tr, m_Actor))
                    return ItemRating.JUNK;
            }

            // j6 Primed explosives.
            if (it is ItemPrimedExplosive)
                return ItemRating.JUNK;

            // j7 Light/Tracker/Spray scent out of batteries/paint or if has already enough
            if (it is ItemLight)
            {
                ItemLight itLight = it as ItemLight;

                if (itLight.Batteries <= 0)
                    return ItemRating.JUNK;

                // light is junk if already has 6 hours of batteries worth.
                int totalLightsBatteries = 0;
                m_Actor.Inventory.ForEach((i) =>
                    {
                        if (i == it)
                            return;
                        ItemLight l = i as ItemLight;
                        if (l == null)
                            return;
                        totalLightsBatteries += l.Batteries;
                    });

                if (totalLightsBatteries >= 6 * WorldTime.TURNS_PER_HOUR)
                    return ItemRating.JUNK;
            }

            if (it is ItemTracker)
            {
                if ((it as ItemTracker).Batteries <= 0)
                    return ItemRating.JUNK;

                // don't hoard trackers, one with batteries is enough.
                bool enough = false;
                enough = m_Actor.Inventory.HasItemMatching((i) =>
                {
                    if (i == it)
                        return false;
                    ItemTracker t = i as ItemTracker;
                    return t != null && t.Batteries > 0;
                });
                if (enough)
                    return ItemRating.JUNK;
            }

            if (it is ItemSprayScent)
            {
                if ((it as ItemSprayScent).SprayQuantity <= 0)
                    return ItemRating.JUNK;

                // don't hoard spray scent, one with spray left is enough.
                bool enough;
                enough = m_Actor.Inventory.HasItemMatching((i) =>
                {
                    if (i == it)
                        return false;
                    ItemSprayScent t = i as ItemSprayScent;
                    return t != null && t.SprayQuantity > 0;
                });
                if (enough)
                    return ItemRating.JUNK;
            }

            // j8 Entertainment: boring or already enough
            if (it is ItemEntertainment)
            {
                if ((it as ItemEntertainment).IsBoringFor(m_Actor))
                    return ItemRating.JUNK;

                // one full stack of entertainment is enough if sane
                if (!game.Rules.IsActorDisturbed(m_Actor) && CountItemsFullStacksOfSameType(typeof(ItemEntertainment), it) >= 1)
                    return ItemRating.JUNK;
            }

            // j9 Ammo with no compatible ranged weapon.
            if (it is ItemAmmo)
            {
                if (GetCompatibleRangedWeapon(game, it as ItemAmmo) == null)
                    return ItemRating.JUNK;
            }

            // j10 Ranged weapons
            //     - ai not interested in rw
            //     - no ammo for it
            //     - has already same model with at least more potential ammo
            //     - has already better scoring rw with ammo
            if (it is ItemRangedWeapon)
            {
                ItemRangedWeapon itRw = it as ItemRangedWeapon;

                // ai not interested in rw
                if (m_Actor.Model.Abilities.AI_NotInterestedInRangedWeapons)
                    return ItemRating.JUNK;

                // no ammo for it
                int ammoInInv = CountTotalAmmoInInventoryFor(itRw);
                if (ammoInInv == 0)
                    return ItemRating.JUNK;

                // has already same model with at least more potential ammo
                // has already at least better scoring rw with ammo
                int scoreIt = ScoreRangedWeapon(itRw);
                foreach (Item invIt in m_Actor.Inventory.Items)
                {
                    if (invIt != itRw)
                    {
                        ItemRangedWeapon invRw = invIt as ItemRangedWeapon;
                        if (invRw != null)
                        {
                            if (invRw.Model == it.Model && CountTotalAmmoInInventoryFor(invRw) >= itRw.Ammo)
                                return ItemRating.JUNK;
                            if (invRw.Ammo > 0 && ScoreRangedWeapon(invRw) >= scoreIt)
                                return ItemRating.JUNK;
                        }
                    }
                }
            }

            // j11 Barricading material if has already enough.
            if (it is ItemBarricadeMaterial)
            {
                // one full stack of barricading material is enough
                if (CountItemsFullStacksOfSameType(typeof(ItemBarricadeMaterial), it) >= 1)
                    return ItemRating.JUNK;
            }

            // Need :

            // n1 Food if hungry or not enough food in inventory.
            if (it is ItemFood)
            {
                if (game.Rules.IsActorHungry(m_Actor))
                    return ItemRating.NEED;
                int nutritionPoints = GetTotalNutritionInInventory(game);
                if (owned)
                    nutritionPoints -= GetItemNutritionValue(game, it as ItemFood);
                // rule of thumb: has to cover 25% more than hungry level
                if (nutritionPoints <= ((5 * Session.Get.GamePreset.HungerPoints) / 4))
                    return ItemRating.NEED;
            }

            // n2 Ammo for ranged weapon if not enough.
            if (it is ItemAmmo)
            {
                ItemAmmo itAmmo = it as ItemAmmo;
                ItemRangedWeapon rWp = GetCompatibleRangedWeapon(game, itAmmo);
                if (rWp != null)
                {
                    // we want 2 full stacks of ammo
                    if (CountFullAmmoStacksInInventoryFor(rWp) < 2)
                        return ItemRating.NEED;
                }
            }

            // n3 Melee weapon if no ranged weapon with ammo.
            if (it is ItemMeleeWeapon)
            {
                if (!HasAnyRangedWeaponWithAmmo())
                    return ItemRating.NEED;
            }

            // n4 Ranged weapon if none with ammo.
            if (it is ItemRangedWeapon)
            {
                if (!HasAnyRangedWeaponWithAmmo(it))
                    return ItemRating.NEED;
            }

            // n5 Any meds if none. Other meds if need (or could) it.
            if (it is ItemMedicine)
            {
                ItemMedicine itMed = it as ItemMedicine;
                if (CountItemsOfSameType(typeof(ItemMedicine), it) == 0)
                    return ItemRating.NEED;

                // be lenient and consider we need a med if the corresponding stat is about 75% or less.
                // exception: always want to cure health and infection.
                // this is will allow the player to trade meds for other items, which will increase the
                // value of meds players mostly ignored previously (eg: sta healers).
                if ((itMed.Healing > 0) && (m_Actor.HitPoints < game.Rules.ActorMaxHPs(m_Actor)))
                    return ItemRating.NEED;
                if ((itMed.StaminaBoost > 0) && (m_Actor.StaminaPoints < 0.75f * game.Rules.ActorMaxSTA(m_Actor)))
                    return ItemRating.NEED;
                if ((itMed.SleepBoost > 0) && (m_Actor.SleepPoints < 0.75f * game.Rules.ActorMaxSleep(m_Actor)))
                    return ItemRating.NEED;
                if ((itMed.SanityCure > 0) && (m_Actor.Sanity < 0.75f * game.Rules.ActorMaxSanity(m_Actor)))
                    return ItemRating.NEED;
                if ((itMed.InfectionCure > 0) && (m_Actor.Infection > 0)) // always want to cure infection
                    return ItemRating.NEED;
            }

            // n6 Barricade material if none.
            if (it is ItemBarricadeMaterial)
            {
                if (CountItemsOfSameType(typeof(ItemBarricadeMaterial), it) == 0)
                    return ItemRating.NEED;
            }

            // **disabled; was willing to eg trade away a weapon for a light during the night! **
            //// n7 Light if bad fov
            //// already handled lights out of batteries as junk
            //if (it is ItemLight)
            //{
            //    WorldTime time = m_Actor.Location.Map.LocalTime;
            //    if (game.Rules.NightFovPenalty(m_Actor, time) > 0)
            //        return ItemRating.NEED;
            //    Weather weather = game.Session.World.Weather;
            //    if (game.Rules.WeatherFovPenalty(m_Actor, weather) > 0)
            //        return ItemRating.NEED;
            //}

            // n8 (Unprimed) Explosive if none.
            if (it is ItemExplosive)
            {
                if (CountItemsOfSameType(typeof(ItemExplosive), it) == 0)
                    return ItemRating.NEED;
            }

            // n9 Armor if none.
            if (it is ItemBodyArmor)
            {
                if (CountItemsOfSameType(typeof(ItemBodyArmor), it) == 0)
                    return ItemRating.NEED;
            }

            // n10 Entertainment if not sane.
            if (it is ItemEntertainment)
            {
                if (game.Rules.IsActorDisturbed(m_Actor))
                    return ItemRating.NEED;
            }

            // Okay:
            // - Anything else.
            return ItemRating.OKAY;
        }

        public enum TradeRating
        {
            REFUSE = 0,
            MAYBE = 1,  // will need a charisma roll, accept if success refuse if failed.
            ACCEPT = 3
        };

        // offeredRating X askedRating => tradeRating
        private static readonly TradeRating[,] TRADE_RATING_MATRIX = new TradeRating[3, 3]
        {
            // asked JUNK,        asked OKAY,         asked NEED
            { TradeRating.ACCEPT, TradeRating.MAYBE,  TradeRating.REFUSE },  // offered JUNK
            { TradeRating.ACCEPT, TradeRating.ACCEPT, TradeRating.REFUSE },  // offered OKAY
            { TradeRating.ACCEPT, TradeRating.ACCEPT, TradeRating.MAYBE }   // offered NEED
        };

        /// <summary>
        /// Rates a trade offer by another actor.
        /// Check for trusted leader but do not check for charisma here, handled by the caller on "Maybe" answers.
        /// Mostly wants to get an item of equal or better value, unless the item asked is needed, see the matrix.
        /// Some additional rules are used for special tricky cases.
        /// </summary>
        /// <param name="game"></param>
        /// <param name="tradingWith"></param>
        /// <param name="offered">the item the other actor is offering</param>
        /// <param name="asked">the item the other actor wants from us</param>
        /// <returns></returns>
        /// <see cref="RateItem(RogueGame, Item, bool)"/>
        /// <see cref="RateItemExhange(RogueGame, Item, Item)"/>
        /// <see cref="TRADE_RATING_MATRIX"/>
        public TradeRating RateTradeOffer(RogueGame game, Actor tradingWith, Item offered, Item asked)
        {
            // always accept deals with trusted leader
            if (tradingWith == m_Actor.Leader && game.Rules.IsActorTrustingLeader(m_Actor))
                return TradeRating.ACCEPT;

            // handle special case of trading items of the same type. eg: trading melee weapons.
            if (offered.GetType() == asked.GetType())
                return RateItemExhange(game, asked, offered);

            // special case of asking a rw and offering compatible ammo.
            // eg: offering light rifle bullets but asking the rifle.
            // due to items individual ratings this could be accepted
            // (eg: both rated as needed and rolling charisma), which is silly.
            // always refuse such trades!
            if (asked is ItemRangedWeapon && offered is ItemAmmo)
            {
                if ((asked as ItemRangedWeapon).AmmoType == (offered as ItemAmmo).AmmoType)
                    return TradeRating.REFUSE;
            }

            // alpha10.1 never trade away a unique item, unless for another unique item
            if (asked.IsUnique && !offered.IsUnique)
                return TradeRating.REFUSE;

            // not a special case, compare item ratings.
            ItemRating offeredRating = RateItem(game, offered, false);
            ItemRating askedRating = RateItem(game, asked, true);
            // compare ratings with matrix (lazy way of doing lots of if/else)
            return TRADE_RATING_MATRIX[(int)offeredRating, (int)askedRating];
        }

        #region Rating exhange of items of same type
        /// <summary>
        /// Compare items of the same type for trading. Items MUST be of the same type.
        /// Needs to be handled differently than trading items of different types.
        /// Wants to exhange items if get an improvement over the old one eg: a ranged weapon with better range.
        /// TODO -- should also be used when considering picking up items
        /// </summary>
        /// <param name="game"></param>
        /// <param name="oIt">item we are losing</param>
        /// <param name="nIt">item we are getting</param>
        /// <returns></returns>
        /// <see cref="RateTradeOffer(RogueGame, Actor, Item, Item)"/>
        protected TradeRating RateItemExhange(RogueGame game, Item oIt, Item nIt)
        {
            // first reject/accept if one is junk and not the other
            ItemRating oRating = RateItem(game, oIt, true);
            ItemRating nRating = RateItem(game, nIt, false);
            if (nRating == ItemRating.JUNK && oRating != ItemRating.JUNK) return TradeRating.REFUSE;
            if (oRating == ItemRating.JUNK && nRating != ItemRating.JUNK) return TradeRating.ACCEPT;

            // then compare items value

            if (oIt is ItemAmmo)
            {
                // just compare quantity
                return nIt.Quantity > oIt.Quantity ? TradeRating.ACCEPT :
                    nIt.Quantity < oIt.Quantity ? TradeRating.REFUSE :
                    TradeRating.MAYBE;
            }

            if (oIt is ItemBarricadeMaterial)
            {
                // just compare quantity
                return nIt.Quantity > oIt.Quantity ? TradeRating.ACCEPT :
                    nIt.Quantity < oIt.Quantity ? TradeRating.REFUSE :
                    TradeRating.MAYBE;
            }

            if (oIt is ItemBodyArmor)
            {
                ItemBodyArmor oArm = oIt as ItemBodyArmor;
                ItemBodyArmor nArm = nIt as ItemBodyArmor;

                // prefer better overal protection
                int oScore = oArm.Protection_Hit + oArm.Protection_Shot;
                int nScore = nArm.Protection_Hit + nArm.Protection_Shot;

                return nScore > oScore ? TradeRating.ACCEPT :
                    nScore < oScore ? TradeRating.REFUSE :
                    TradeRating.MAYBE;
            }

            if (oIt is ItemEntertainment)
            {
                ItemEntertainment oEnt = oIt as ItemEntertainment;
                ItemEntertainment nEnt = nIt as ItemEntertainment;

                // prefer non-boring ent first. if both are boring then maybe.
                bool oBored = oEnt.IsBoringFor(m_Actor);
                bool nBored = nEnt.IsBoringFor(m_Actor);
                if (!nBored && oBored) return TradeRating.ACCEPT;
                if (nBored && !nBored) return TradeRating.REFUSE;
                if (nBored && oBored) return TradeRating.MAYBE;

                // then prefer ent with more sanity potential
                int oScore = (oEnt.Quantity * 100 * oEnt.EntertainmentModel.Value) / (1 + oEnt.EntertainmentModel.BoreChance);
                int nScore = (nEnt.Quantity * 100 * nEnt.EntertainmentModel.Value) / (1 + nEnt.EntertainmentModel.BoreChance);

                return nScore > oScore ? TradeRating.ACCEPT :
                    nScore < oScore ? TradeRating.REFUSE :
                    TradeRating.MAYBE;
            }

            if (oIt is ItemExplosive)  // also ItemGrenade
            {
                ItemExplosiveModel oEx = (oIt as ItemExplosive).Model as ItemExplosiveModel;
                ItemExplosiveModel nEx = (nIt as ItemExplosive).Model as ItemExplosiveModel;

                // prefer explosive with more range 0 damage
                return nEx.BlastAttack.Damage[0] > oEx.BlastAttack.Damage[0] ? TradeRating.ACCEPT :
                     nEx.BlastAttack.Damage[0] < oEx.BlastAttack.Damage[0] ? TradeRating.REFUSE :
                     TradeRating.MAYBE;
            }

            if (oIt is ItemFood)
            {
                ItemFood oFood = oIt as ItemFood;
                ItemFood nFood = nIt as ItemFood;

                // prefer food with more nutrition
                int oNut = GetItemNutritionValue(game, oFood);
                int nNut = GetItemNutritionValue(game, nFood);

                return nNut > oNut ? TradeRating.ACCEPT :
                    nNut < oNut ? TradeRating.REFUSE :
                    TradeRating.MAYBE;
            }

            if (oIt is ItemLight)
            {
                ItemLight oLt = oIt as ItemLight;
                ItemLight nLt = nIt as ItemLight;

                // score
                int oScore = ScoreLight(oLt);
                int nScore = ScoreLight(nLt);

                return nScore > oScore ? TradeRating.ACCEPT :
                    nScore < oScore ? TradeRating.REFUSE :
                    TradeRating.MAYBE;
            }

            if (oIt is ItemMedicine)
            {
                ItemMedicine oMed = oIt as ItemMedicine;
                ItemMedicine nMed = nIt as ItemMedicine;

                // first prefer med we need the most (basically re-use the med logic from item rating)
                if (nRating > oRating) return TradeRating.ACCEPT;
                if (oRating < nRating) return TradeRating.REFUSE;

                // for other cases, prefer in order: hp, inf, slp, san, sta
                // use scoring.
                int oScore = 10000 * oMed.Healing + 1000 * oMed.InfectionCure + 100 * oMed.SleepBoost + 10 * oMed.SanityCure + oMed.StaminaBoost;
                int nScore = 10000 * nMed.Healing + 1000 * nMed.InfectionCure + 100 * nMed.SleepBoost + 10 * nMed.SanityCure + nMed.StaminaBoost;

                return nScore > oScore ? TradeRating.ACCEPT :
                    nScore < oScore ? TradeRating.REFUSE :
                    TradeRating.MAYBE;
            }

            if (oIt is ItemMeleeWeapon)
            {
                ItemMeleeWeapon oMw = oIt as ItemMeleeWeapon;
                ItemMeleeWeapon nMw = nIt as ItemMeleeWeapon;

                // score
                int oScore = ScoreMeleeWeapon(oMw);
                int nScore = ScoreMeleeWeapon(nMw);

                return nScore > oScore ? TradeRating.ACCEPT :
                    nScore < oScore ? TradeRating.REFUSE :
                    TradeRating.MAYBE;
            }

            if (oIt is ItemPrimedExplosive) // also ItemGrenadePrimed
            {
                // refuse any primed explosive
                return TradeRating.REFUSE;
            }

            if (oIt is ItemRangedWeapon)
            {
                ItemRangedWeapon oRw = oIt as ItemRangedWeapon;
                ItemRangedWeapon nRw = nIt as ItemRangedWeapon;

                // score
                int oScore = ScoreRangedWeapon(oRw);
                int nScore = ScoreRangedWeapon(nRw);

                return nScore > oScore ? TradeRating.ACCEPT :
                    nScore < oScore ? TradeRating.REFUSE :
                    TradeRating.MAYBE;
            }

            if (oIt is ItemSprayPaint)
            {
                ItemSprayPaint oSp = oIt as ItemSprayPaint;
                ItemSprayPaint nSp = nIt as ItemSprayPaint;

                // useless items for ai, but prefer one with more spray left...
                return nSp.PaintQuantity > oSp.PaintQuantity ? TradeRating.ACCEPT :
                    nSp.PaintQuantity < oSp.PaintQuantity ? TradeRating.REFUSE :
                    TradeRating.MAYBE;
            }

            if (oIt is ItemSprayScent)
            {
                ItemSprayScent oSp = oIt as ItemSprayScent;
                ItemSprayScent nSp = nIt as ItemSprayScent;

                // prefer spray scent with more spray left
                return nSp.SprayQuantity > oSp.SprayQuantity ? TradeRating.ACCEPT :
                    nSp.SprayQuantity < oSp.SprayQuantity ? TradeRating.REFUSE :
                    TradeRating.MAYBE;
            }

            if (oIt is ItemTrap)
            {
                ItemTrap oTr = oIt as ItemTrap;
                ItemTrap nTr = nIt as ItemTrap;

                // prefer trap with more potential damage then blocking.
                // use scoring
                ItemTrapModel oMtr = oTr.TrapModel;
                ItemTrapModel nMtr = nTr.TrapModel;

                int oScore = 100 * oMtr.Damage * oMtr.TriggerChance + oMtr.BlockChance * oMtr.TriggerChance;
                int nScore = 100 * nMtr.Damage * nMtr.TriggerChance + nMtr.BlockChance * nMtr.TriggerChance;

                return nScore > oScore ? TradeRating.ACCEPT :
                    nScore < oScore ? TradeRating.REFUSE :
                    TradeRating.MAYBE;
            }

            // unhandled items! should not happen!
            throw new Exception("RateItemExhange: unhandled item type" + oIt.GetType());
        }
        #endregion
#if false
            alpha10 previous attempt, use scoring, not satisfying hard to balance
        protected bool IsJunkItem(RogueGame game, Item it)
        {
            ////////////////////////////////////////////////
            // Junk items:
            // 0 Anything not food if only one slot left.
            // 1 AI forbidden items.
            // 2 Spray paint.
            // 3 Activated traps!
            // 4 Melee weapons if martial arts
            // 5 Lights out of batteries.
            // 9 Primed explosives!
            // 10 Boring items.
            ///////////////////////////////////////////////

            // 0 Anything not food if only one slot left.
            bool onlyOneSlotLeft = (m_Actor.Inventory.CountItems == game.Rules.ActorMaxInv(m_Actor) - 1);
            if (onlyOneSlotLeft)
                return !(it is ItemFood);

            // 1 AI forbidden items.
            if (it.IsForbiddenToAI)
                return true;

            // 2 Spray paint.
            if (it is ItemSprayPaint)
                return true;

            // 3 Activated traps!
            if (it is ItemTrap)
            {
                if ((it as ItemTrap).IsActivated)
                    return true;
            }

            // 4 Melee weapons if martial arts
            // Reject medecine if we alredy have full stacks.
            if (it is ItemMeleeWeapon)
            {
                if (m_Actor.Sheet.SkillTable.GetSkillLevel((int)Skills.IDs.MARTIAL_ARTS) > 0)
                    return true;
            }

            // 5 Lights out of batteries.
            if (IsLightOutOfBatteries(it))
                return true;

            // 6 Primed explosives!
            if (it is ItemPrimedExplosive)
                return true;

            // 10 Boring items!
            if (m_Actor.IsBoredOf(it))
                return true;

                // not junk
                return false;
        }

        // alpha10 previous attempt
        /// <summary>
        ///
        /// </summary>
        /// <param name="game"></param>
        /// <param name="it"></param>
        /// <param name="fromOwnInventory"></param>
        /// <returns></returns>
        public int ScoreItemValue(RogueGame game, Item it, bool fromOwnInventory)
        {
            // First we reject "junk" items and give them a score of 0.
            if (IsJunkItem(game, it))
                return 0;

            Rules rules = game.Rules;
            float score;

            bool isLastOfItsTypeInMyInventory = false;
            if (fromOwnInventory)
            {
                if (CountItemsOfSameType(it.GetType()) == 1)
                    isLastOfItsTypeInMyInventory = true;
            }

            // Score item ranking in its category (type). Average should be around 1000 so items from different
            // categories can be compared fairly.
            // Then score need for this type for item.
            // Final score is ranking score modified by need.
            // Eg of heuristics:
            // A food item
            // ranking score: nutrition of the food relative to our food bar
            // need: high if we are hungry
            float rankingScore = 1000;
            float needFactor = 1f;

            if (it is ItemFood)
            {
                // -- Food item ranking score

                ItemFood itFood = it as ItemFood;
                int turn = m_Actor.Location.Map.LocalTime.TurnCounter;
                int maxFood = rules.ActorMaxFood(m_Actor);

                // score food nutrition respective to our half our food bar.
                int nutritionScore1000 = (2 * 1000 * rules.FoodItemNutrition(itFood, turn)) / maxFood;

                // score duration.
                // consider non-perishable food as lasting 7 days.
                // consider 3 days as average (1000)
                int duration;
                if (!itFood.IsPerishable)
                    duration = 7 * WorldTime.TURNS_PER_DAY;
                else
                    duration = (itFood.BestBefore.TurnCounter - turn);
                int durationScore1000 = (1000 * duration) / (3 * WorldTime.TURNS_PER_DAY);

                // base score is nutrition and duration
                rankingScore = nutritionScore1000 + durationScore1000;

                // penalize even more spoiled/expired
                if (rules.IsFoodExpired(itFood, turn))
                    rankingScore /= 4;
                else if (rules.IsFoodSpoiled(itFood, turn))
                    rankingScore /= 2;

                // -- Need for food

                // base need if starved/hungry
                if (rules.IsActorStarving(m_Actor))
                    needFactor = 10;
                else if (rules.IsActorHungry(m_Actor))
                    needFactor = 2;

                // need food if not enough stockpiled to cover our needs
                // FIXME -- including last means the ai is not willing to trade for a better food!
                if (!HasEnoughFoodFor(game, maxFood - Session.Get.GamePreset.HungerPoints) || isLastOfItsTypeInMyInventory)
                    needFactor += 0.5f;

            }
            else if (it is ItemRangedWeapon)
            {
                // -- Ranged weapon ranking score
                ItemRangedWeapon itRw = it as ItemRangedWeapon;

                // ranking score is just range with 5 considered average
                rankingScore = (1000 * (itRw.Model as ItemRangedWeaponModel).Attack.Range) / 5;

                // small bonus for ammo left (to sort identical weapons)
                rankingScore += itRw.Ammo;

                // -- Need for ranged weapon

                // need ranged weapon if none yet/last
                // FIXME -- including last means the ai is not willing to trade for a better ranged weapon!
                if (CountItemsOfSameType(typeof(ItemRangedWeapon)) == 0 || isLastOfItsTypeInMyInventory)
                    needFactor = 4f;

                // less need for a weapon we have no ammo for
                if (GetCompatibleAmmoItem(game, itRw) == null)
                    needFactor *= 2f / 3f;
            }
            else if (it is ItemAmmo)
            {
                // -- Ammo ranking score
                ItemAmmo itAmmo = it as ItemAmmo;
                // quantity to helping sort but misleading (eg: bolts have larger stacks and will be valued more than shotgun shells)
                rankingScore = 1000 + itAmmo.Quantity;

                // -- Need for ammo

                // need ammo if compatible weapon and even more if not 2 full stacks of it
                if (GetCompatibleRangedWeapon(game, itAmmo) != null)
                {
                    needFactor = 2f;
                    if (!HasAtLeastFullStackOfItemTypeOrModel(it, 2))
                        needFactor += 1f;
                }
                else
                    // ammo are really not valuable if no ranged weapon for it
                    needFactor = 0.1f;
            }
            else if (it is ItemMeleeWeapon)
            {
                // -- Melee weapon ranking score
                ItemMeleeWeapon itMw = it as ItemMeleeWeapon;
                ItemMeleeWeaponModel mMw = itMw.Model as ItemMeleeWeaponModel;

                // base is damage, consider 6 as average (1000)
                rankingScore = (1000 * mMw.Attack.DamageValue) / 6;

                // small penalty for stamina
                rankingScore -= mMw.Attack.StaminaPenalty;

                // -- Melee weapon need

                // need melee weapon if none/last and has no ranged weapon with ammo
                // FIXME -- including last means the ai is not willing to trade for a better melee weapon!
                if (CountItemsOfSameType(typeof(ItemMeleeWeapon)) == 0 || isLastOfItsTypeInMyInventory)
                {
                    if (!HasAnyRangedWeaponWithAmmo())
                        needFactor = 2;
                }
            }
            else if (it is ItemMedicine)
            {
                // -- Medecine ranking score
                ItemMedicine itMed = it as ItemMedicine;

                // base is heal value, consider 2 as average (1000)
                rankingScore = (1000 * itMed.Healing) / 2;

                // in games with infection, big bonus for infection cure
                if (game.Session.HasInfection)
                    rankingScore += 100 * itMed.InfectionCure;

                // smaller bonus for sleep
                rankingScore += 10 * itMed.SleepBoost;

                // small bonuses for other effects
                rankingScore += 2*itMed.SanityCure + itMed.StaminaBoost;

                // bigger stacks are better
                rankingScore += it.Quantity;

                // -- Need for medecine

                // need medecine if none or last
                // FIXME -- including last means the ai is not willing to trade for a better medecine!
                if (CountItemsOfSameType(typeof(ItemMedicine)) == 0 || isLastOfItsTypeInMyInventory)
                    needFactor = 2;

                // need healing if hurt / sleep if sleepy / stamina if tired etc...
                if (m_Actor.HitPoints < rules.ActorMaxHPs(m_Actor) && itMed.Healing > 0)
                    needFactor += 1;
                if (rules.IsActorSleepy(m_Actor) && itMed.SleepBoost > 0)
                    needFactor += 1;
                if (rules.IsActorTired(m_Actor) && itMed.StaminaBoost > 0)
                    needFactor += 1;
                if (rules.IsActorInsane(m_Actor) && itMed.SanityCure > 0)
                    needFactor += 1;
                if (m_Actor.Infection > 0 && itMed.InfectionCure > 0)
                    needFactor += 1;
            }
            else if (it is ItemExplosive)
            {
                // TODO -- refine explosive scoring, basically stupid now. also explosive vs primed is a mess.

                // -- Explosive ranking score
                ItemExplosive itEx = it as ItemExplosive;
                rankingScore = 1000;
                rankingScore += it.Quantity;

                // -- Need for explosive

                // need explosive if none or last
                // FIXME -- including last means the ai is not willing to trade for a better explosive!
                if (CountItemsOfSameType(typeof(ItemExplosive)) == 0 || isLastOfItsTypeInMyInventory)
                    needFactor = 2;
            }
            else if (it is ItemBarricadeMaterial)
            {
                // -- Barricade material ranking
                rankingScore = 1000;
                rankingScore += it.Quantity;

                // -- Need for barricade

                // need barricade if none or last
                // FIXME -- including last means the ai is not willing to trade for a better ranged weapon!
                if (CountItemsOfSameType(typeof(ItemBarricadeMaterial)) == 0 || isLastOfItsTypeInMyInventory)
                    needFactor = 2;
            }
            else if (it is ItemEntertainment)
            {
                // -- Entertainment ranking
                ItemEntertainment itEnt = it as ItemEntertainment;
                ItemEntertainmentModel mEnt = it.Model as ItemEntertainmentModel;

                rankingScore = 1000;
                rankingScore += it.Quantity;
                rankingScore += mEnt.Value;

                // -- Entertainment need

                // need of entertainment if turning insane
                // mostly ignore entertainment altogether if san high enough
                if (rules.IsActorDisturbed(m_Actor))
                    needFactor = 4;
                else if (rules.IsActorInsane(m_Actor))
                    needFactor = 10;
                else if (rules.SanityToHoursUntilUnstable(m_Actor) >= 6)
                    needFactor /= 10;
            }
            else if (it is ItemLight)
            {
                // -- Light ranking
                ItemLight itLight = it as ItemLight;

                rankingScore = 1000;
                rankingScore += 10 * itLight.FovBonus;
                rankingScore += itLight.Batteries / WorldTime.TURNS_PER_HOUR;

                // -- Light need

                // need for light if dark / no need at all if lit
                Lighting mapL = m_Actor.Location.Map.Lighting;
                if (mapL == Lighting.DARKNESS)
                    needFactor = 2;
                else if (mapL == Lighting.LIT)
                    needFactor = 0;
            }
            // TODO -- other items

            // final score
            score = rankingScore * needFactor;

            // make sure scoring is above zero as the item is not junk.
            return Math.Max((int)score, 1);
        }
#endif

        // end alpha10

        protected bool HasEnoughFoodFor(RogueGame game, int nutritionNeed)
        {
            if (m_Actor.Inventory == null || m_Actor.Inventory.IsEmpty)
                return false;

            int turnCounter = m_Actor.Location.Map.LocalTime.TurnCounter;
            int nutritionTotal = 0;
            foreach (Item it in m_Actor.Inventory.Items)
            {
                if (it is ItemFood)
                {
                    nutritionTotal += game.Rules.FoodItemNutrition(it as ItemFood, turnCounter);
                    if (nutritionTotal >= nutritionNeed) // exit asap
                        return true;
                }
            }

            return false;
        }

        [Obsolete]
        protected bool HasAtLeastFullStackOfItemTypeOrModel(Item it, int n)
        {
            if (m_Actor.Inventory == null || m_Actor.Inventory.IsEmpty)
                return false;

            if (it.Model.IsStackable)
            {
                // we want N stacks of it.
                return CountItemsQuantityOfModel(it.Model) >= n * it.Model.StackingLimit;
            }
            else
            {
                // not stackable, we are happy with N items of its type.
                return CountItemsOfSameType(it.GetType()) >= n;
            }
        }

        protected bool HasItemOfModel(ItemModel model)
        {
            if (m_Actor.Inventory == null || m_Actor.Inventory.IsEmpty)
                return false;

            foreach (Item it in m_Actor.Inventory.Items)
                if (it.Model == model)
                    return true;

            return false;
        }

        protected int CountItemsQuantityOfModel(ItemModel model)
        {
            if (m_Actor.Inventory == null || m_Actor.Inventory.IsEmpty)
                return 0;

            int count = 0;
            foreach (Item it in m_Actor.Inventory.Items)
            {
                if (it.Model == model)
                    count += it.Quantity;
            }

            return count;
        }

        protected bool HasItemOfType(Type tt)
        {
            if (m_Actor.Inventory == null || m_Actor.Inventory.IsEmpty)
                return false;

            return m_Actor.Inventory.HasItemOfType(tt);
        }

        protected int CountItemQuantityOfType(Type tt, Item excludingThisOne = null)
        {
            if (m_Actor.Inventory == null || m_Actor.Inventory.IsEmpty)
                return 0;

            int quantity = 0;
            foreach (Item otherIt in m_Actor.Inventory.Items)
            {
                if (otherIt != excludingThisOne && otherIt.GetType() == tt)
                    quantity += otherIt.Quantity;
            }

            return quantity;
        }

        protected int CountItemsOfSameType(Type tt, Item excludingThisOne = null)
        {
            if (m_Actor.Inventory == null || m_Actor.Inventory.IsEmpty)
                return 0;

            int count = 0;
            foreach (Item otherIt in m_Actor.Inventory.Items)
            {
                if ((otherIt != excludingThisOne) && (otherIt.GetType() == tt))
                    ++count;
            }

            return count;
        }

        // alpha10
        protected bool HasAnyRangedWeaponWithAmmo(Item excludingThisRangedWeapon = null)
        {
            foreach (Item it in m_Actor.Inventory.Items)
            {
                if ((it != excludingThisRangedWeapon) && (it is ItemRangedWeapon))
                {
                    ItemRangedWeapon itRw = it as ItemRangedWeapon;
                    if (itRw.Ammo > 0)
                        return true;
                    foreach (Item otherIt in m_Actor.Inventory.Items)
                    {
                        if (otherIt is ItemAmmo)
                        {
                            if (itRw.AmmoType == (otherIt as ItemAmmo).AmmoType)
                                return true;
                        }
                    }
                }
            }

            return false;
        }
        #endregion
    }
}
