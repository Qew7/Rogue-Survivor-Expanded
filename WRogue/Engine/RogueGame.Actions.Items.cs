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
        #region Chatting, Trading, Saying and Shouting.
        public void DoChat(Actor speaker, Actor target)
        {
            // spend APs.
            SpendActorActionPoints(speaker, Rules.BASE_ACTION_COST);

            // message
            bool isSpeakerVisible = IsVisibleToPlayer(speaker);
            bool isTargetVisible = IsVisibleToPlayer(target);
            if (isSpeakerVisible || isTargetVisible)
                AddMessage(MakeMessage(speaker, Conjugate(speaker, VERB_CHAT_WITH), target));

            // trade?
            if (m_Rules.CanActorInitiateTradeWith(speaker, target))
            {
                DoTrade(speaker, target);
            }

            // alpha10 recover san after "normal" chat or fast trade
            if (speaker.Model.Abilities.HasSanity)
            {
                RegenActorSanity(speaker, Rules.SANITY_RECOVER_CHAT_OR_TRADE);
                if (IsVisibleToPlayer(speaker))
                    AddMessage(MakeMessage(speaker, string.Format("{0} better after chatting with", Conjugate(speaker, VERB_FEEL)), target));
            }

            if (target.Model.Abilities.HasSanity)
            {
                RegenActorSanity(target, Rules.SANITY_RECOVER_CHAT_OR_TRADE);
                if (IsVisibleToPlayer(target))
                    AddMessage(MakeMessage(target, string.Format("{0} better after chatting with", Conjugate(speaker, VERB_FEEL)), speaker));
            }
        }

        // alpha10 "fast" trade uses new trade mechanic of rating items and trades.
        // npcs will mostly only make mutually beneficial deals.
        // speaker and target are also somehow reversed from how they were in rs9(!?)
        // for the player should try to mimick most of trade results obtained by player negociating trade but not mandatory.
        public void DoTrade(Actor speaker, Actor target)
        {
            // clean up activities
            speaker.Activity = Activity.IDLE;
            target.Activity = Activity.IDLE;

            bool isVisible = IsVisibleToPlayer(speaker) || IsVisibleToPlayer(target);
            if (isVisible) AddMessage(MakeMessage(speaker, string.Format("wants to make a quick trade with {0}.", target.Name)));

            // the basic idea is to pick an item the speaker wants from target,
            // and offer an item the speaker is willing to get rid of.
            BaseAI speakerAI = speaker.Controller as BaseAI;
            BaseAI targetAI = target.Controller as BaseAI;
            Item offered, asked;
            offered = asked = null;

            // target not willing to trade if is ordered not to
            if ((!targetAI.Directives.CanTrade) && (speaker != target.Leader))
            {
                if (isVisible) AddMessage(MakeMessage(target, "is not willing to trade."));
                return;
            }

            // if speaker is the player, make the npc the speaker so the npc is the one offering an item.
            // alpha10.1 but not for bot
            if (speaker.IsPlayer)
            {
                // swap speaker and target so npc is always speaker in fast trade
                Actor swap = target;
                target = speaker;
                speaker = swap;
                targetAI = null;  // now player
                speakerAI = speaker.Controller as BaseAI;
            }

            // local lambdas just because -_-

            // get an item the speaker would like from target inventory.
            Item pickAskedItem(out ItemRating rating)
            {
                // pick an item in target inventory the speaker wants, or any item if target has only junk.
                List<Item> wants = target.Inventory.Filter((it) =>
                {
                    ItemRating r = speakerAI.RateItem(this, it, false);
                    // wants anything but junk.
                    // don't limit to things speaker needs because the target ai is more likely to value the same item
                    // as being needed for himself! also makes for more varied deals.
                    return r != ItemRating.JUNK;
                });
                if (wants.Count == 0)
                {
                    // no non-junk items, extend to all items...
                    wants.AddRange(target.Inventory.Items);
                }

                // pick one from the wanted list.
                Item wantIt = wants[m_Rules.Roll(0, wants.Count)];
                rating = speakerAI.RateItem(this, wantIt, false);
                return wantIt;
            };

            // can return null
            // get an item the speaker is willing to exhange for the target item it wants.
            Item pickOfferedItem(Item askedItem, ItemRating askedItemRating)
            {
                List<Item> offerables;

                // if target is npc:
                //   - offer any item that could pass a trade deal with this npc (read their ai mind)
                // if target is player:
                //   - cannot use rate trade offer on the npc itself...
                //   - so offer only items we rate less than the one we want (player should negociate deal instead)
                //   - accepting equal item ratings lead to bad deals for the npc, offering a need for a need (eg: a rifle for bullets!)
                // in all offers, never offer the same item model as the one asked eg: a pistol for a pistol!
                if (target.IsPlayer)
                {
                    offerables = speaker.Inventory.Filter((it) =>
                    {
                        return it.Model != askedItem.Model && speakerAI.RateItem(this, it, true) < askedItemRating;
                    });
                }
                else
                {
                    offerables = speaker.Inventory.Filter((it) =>
                    {
                        if (it.Model == askedItem.Model)
                            return false;
                        // read target ai mind...
                        TradeRating tr = targetAI.RateTradeOffer(this, speaker, it, askedItem);
                        // accept "Maybe" items to be a bit more realistic in not always making perfect deals
                        // ("hey! the ai always accept ai trades! they are cheating!")
                        // and let charisma influence the final result.
                        return tr != TradeRating.REFUSE;
                    });
                }

                if (offerables.Count == 0)
                {
                    // all our items are more valuable than the one we want or only silly deals. no deal.
                    return null;
                }

                Item offerIt = offerables[m_Rules.Roll(0, offerables.Count)];
                return offerIt;
            }

            ItemRating askedRating;
            asked = pickAskedItem(out askedRating);
            offered = pickOfferedItem(asked, askedRating);

            // if no item pairs found, failed trade.
            // either the target has no interesting items for speaker,
            // or the speaker has items too valuable for a trade.
            if ((asked == null) || (offered == null))
            {
                if (asked == null)
                {
                    // speaker finds nothing interesting in target inventory
                    if (isVisible)
                        AddMessage(MakeMessage(speaker, "is not interested in any item of your items."));
                }
                else
                {
                    // speaker has no item to give away (should not happen if target is player)
                    if (isVisible)
                        AddMessage(MakeMessage(speaker, string.Format("would prefer to keep {0} items.", HisOrHer(speaker))));
                }
                if (target.IsPlayer)
                    // help confused players...
                    AddMessage(new Message("(maybe try negociating a deal instead)", m_Session.WorldTime.TurnCounter, Color.Yellow));
                return;
            }

            // propose.
            // if player, ask.
            // if target is ai, check for it.
            // alpha10.1 handle bot player

            bool acceptTrade;
            if (isVisible) AddMessage(MakeMessage(speaker, string.Format("{0} {1} for {2}.", Conjugate(speaker, VERB_OFFER), offered.AName, asked.AName)));
            if (target.IsPlayer && !target.IsBotPlayer)  // speaker always ai unless bot
            {
                // ask player.
                AddOverlay(new OverlayPopup(TRADE_MODE_TEXT, MODE_TEXTCOLOR, MODE_BORDERCOLOR, MODE_FILLCOLOR, Point.Empty));
                RedrawPlayScreen();
                acceptTrade = WaitYesOrNo();
                ClearOverlays();
                RedrawPlayScreen();
            }
            else
            {
                // ask target ai/bot
                BaseAI ai;
#if DEBUG
                ai = target.IsPlayer && target.IsBotPlayer ? m_botControl : targetAI;
#else
                ai = targetAI;
#endif

                TradeRating r = ai.RateTradeOffer(this, speaker, offered, asked);
                if (r == TradeRating.ACCEPT)
                    acceptTrade = true;
                else if (r == TradeRating.REFUSE)
                    acceptTrade = false;
                else
                {
                    // use charisma on "maybe" trades, similar to what we do for the player in the negociating command we the ai won't
                    // exploit the game by asking several times so its ok not to store the charisma roll -_-
                    // note that a duo of charismatic npcs could in theory trade back and forth ha!
                    if (m_Rules.RollChance(m_Rules.ActorCharismaticTradeChance(speaker)))
                    {
                        if (isVisible) DoEmote(target, "Okay you convinced me.");
                        acceptTrade = true;
                    }
                    else
                        acceptTrade = false;
                }
            }

            // so, deal or not?
            if (acceptTrade)
            {
                if (isVisible) AddMessage(MakeMessage(target, string.Format("{0}.", Conjugate(target, VERB_ACCEPT_THE_DEAL))));
                if (target.IsPlayer || speaker.IsPlayer)
                    RedrawPlayScreen();

                // do it
                SwapActorItems(speaker, offered, target, asked);
            }
            else
            {
                if (isVisible) AddMessage(MakeMessage(target, string.Format("{0}.", Conjugate(target, VERB_REFUSE_THE_DEAL))));
                if (target.IsPlayer || speaker.IsPlayer)
                    RedrawPlayScreen();
            }
        }

        /// <summary>
        /// Swap items after a succesful trade. Used in "fast" trades and player negociating trade.
        /// </summary>
        /// <param name="a"></param>
        /// <param name="itA"></param>
        /// <param name="b"></param>
        /// <param name="itB"></param>
        void SwapActorItems(Actor a, Item itA, Actor b, Item itB)
        {
            if (itA.IsEquipped)
                DoUnequipItem(a, itA);
            if (itB.IsEquipped)
                DoUnequipItem(b, itB);

            a.Inventory.RemoveAllQuantity(itA);
            b.Inventory.RemoveAllQuantity(itB);

            a.Inventory.AddAll(itB);
            b.Inventory.AddAll(itA);
        }

        [Flags]
        public enum Sayflags
        {
            NONE = 0,
            /// <summary>
            /// If told to the player and visible will highlight pause the game.
            /// </summary>
            IS_IMPORTANT = (1 << 0),

            /// <summary>
            /// Does not cost action points (emote).
            /// </summary>
            IS_FREE_ACTION = (1 << 1),

            // alpha10
            /// <summary>
            /// A warning or menace, should be highlighted.
            /// </summary>
            IS_DANGER = (1 << 2)
        }

        public void DoSay(Actor speaker, Actor target, string text, Sayflags flags)
        {
            Color sayColor = ((flags & Sayflags.IS_DANGER) != 0) ? SAYOREMOTE_DANGER_COLOR : SAYOREMOTE_NORMAL_COLOR;

            // spend APS?
            if ((flags & Sayflags.IS_FREE_ACTION) == 0)
                SpendActorActionPoints(speaker, Rules.BASE_ACTION_COST);

            // message.
            if (IsVisibleToPlayer(speaker) || (IsVisibleToPlayer(target) && !(m_Player.IsSleeping && target == m_Player)))
            {
                bool isPlayer = target.IsPlayer;
                bool isBot = target.IsBotPlayer; // alpha10.1 handle bot
                bool isImportant = (flags & Sayflags.IS_IMPORTANT) != 0;
                if (isPlayer && isImportant)
                    ClearMessages();
                AddMessage(MakeMessage(speaker, String.Format("to {0} : ", target.TheName), sayColor));
                AddMessage(MakeMessage(speaker, String.Format("\"{0}\"", text), sayColor));
                if (isPlayer && isImportant && !isBot)
                {
                    AddOverlay(new OverlayRect(Color.Yellow, new Rectangle(MapToScreen(speaker.Location.Position), new Size(TILE_SIZE, TILE_SIZE))));
                    AddMessagePressEnter();
                    ClearOverlays();
                    RemoveLastMessage();
                    RedrawPlayScreen();
                }
            }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="speaker"></param>
        /// <param name="text">can be null</param>
        public void DoShout(Actor speaker, string text)
        {
            // spend APs.
            SpendActorActionPoints(speaker, Rules.BASE_ACTION_COST);

            // loud noise.
            OnLoudNoise(speaker.Location.Map, speaker.Location.Position, "A SHOUT");

            // message.
            if (IsVisibleToPlayer(speaker) || AreLinkedByPhone(speaker, m_Player))
            {
                // if player follower, alert!
                if (speaker.Leader == m_Player && !m_Player.IsBotPlayer)  // alpha10.1 handle bot
                {
                    ClearMessages();
                    AddOverlay(new OverlayRect(Color.Yellow, new Rectangle(MapToScreen(speaker.Location.Position), new Size(TILE_SIZE, TILE_SIZE))));
                    AddMessage(MakeMessage(speaker, String.Format("{0}!!", Conjugate(speaker, VERB_RAISE_ALARM))));
                    if (text != null)
                        DoEmote(speaker, text, true);
                    AddMessagePressEnter();
                    ClearOverlays();
                    RemoveLastMessage();
                }
                else
                {
                    if (text == null)
                        AddMessage(MakeMessage(speaker, String.Format("{0}!", Conjugate(speaker, VERB_SHOUT))));
                    else
                        DoEmote(speaker, String.Format("{0} \"{1}\"", Conjugate(speaker, VERB_SHOUT), text), true);
                }
            }
        }

        public void DoEmote(Actor actor, string text, bool isDanger = false)
        {
            if (IsVisibleToPlayer(actor))
                AddMessage(new Message(String.Format("{0} : {1}", actor.Name, text), actor.Location.Map.LocalTime.TurnCounter, isDanger ? SAYOREMOTE_DANGER_COLOR : SAYOREMOTE_NORMAL_COLOR));
        }
        #endregion
        #region Items
        public void DoTakeFromContainer(Actor actor, Point position)
        {
            Map map = actor.Location.Map;

            // get topmost item.
            Item it = map.GetItemsAt(position).TopItem;

            // take it.
            DoTakeItem(actor, position, it);
        }

        public void DoTakeItem(Actor actor, Point position, Item it)
        {
            Map map = actor.Location.Map;

            // spend APs.
            SpendActorActionPoints(actor, Rules.BASE_ACTION_COST);

            // special case for traps
            if (it is ItemTrap)
            {
                ItemTrap trap = it as ItemTrap;
                // taking a trap desactivates it.
                trap.Desactivate(); // alpha10 // trap.IsActivated = false;
            }

            // add to inventory.
            int quantityAdded;
            int quantityBefore = it.Quantity;
            actor.Inventory.AddAsMuchAsPossible(it, out quantityAdded);
            // if added all, remove from map.
            if (quantityAdded == quantityBefore)
            {
                Inventory itemsThere = map.GetItemsAt(position);
                if (itemsThere != null && itemsThere.Contains(it))
                    map.RemoveItemAt(it, position);
            }

            // message
            if (IsVisibleToPlayer(actor) || IsVisibleToPlayer(new Location(map, position)))
            {
                AddMessage(MakeMessage(actor, Conjugate(actor, VERB_TAKE), it));
            }

            // automatically equip item if flags set & possible, and not already equipped something.
            if (!it.Model.DontAutoEquip && m_Rules.CanActorEquipItem(actor, it) && actor.GetEquippedItem(it.Model.EquipmentPart) == null)
                DoEquipItem(actor, it);
        }

        public void DoGiveItemTo(Actor actor, Actor target, Item gift)
        {
            // spend APs.
            SpendActorActionPoints(actor, Rules.BASE_ACTION_COST);

            // if leader give to follower, improve trust.
            if (target.Leader == actor)
            {
                // interesting item?
                BaseAI ai = target.Controller as BaseAI;
                bool isInterestingItem = (ai != null && ai.IsInterestingItemToOwn(this, gift, BaseAI.ItemSource.ANOTHER_ACTOR));

                // emote.
                if (isInterestingItem)
                    DoSay(target, actor, "Thank you, I really needed that!", Sayflags.IS_FREE_ACTION);
                else
                    DoSay(target, actor, "Thanks I guess...", Sayflags.IS_FREE_ACTION);

                // update trust.
                ModifyActorTrustInLeader(target, isInterestingItem ? Rules.TRUST_GOOD_GIFT_INCREASE : Rules.TRUST_MISC_GIFT_INCREASE, true);
            }
            // if follower give to leader, decrease trust.
            else if (actor.Leader == target)
            {
                // emote.
                DoSay(target, actor, "Well, here it is...", Sayflags.IS_FREE_ACTION);

                // update trust.
                ModifyActorTrustInLeader(actor, Rules.TRUST_GIVE_ITEM_ORDER_PENALTY, true);
            }

            // transfer item : drop then take (solves problem of partial quantities transfer).
            DropItem(actor, gift);
            DoTakeItem(target, actor.Location.Position, gift);

            // message.
            if (IsVisibleToPlayer(actor) || IsVisibleToPlayer(target))
            {
                AddMessage(MakeMessage(actor, String.Format("{0} {1} to", Conjugate(actor, VERB_GIVE), gift.TheName), target));
            }

        }

        /// <summary>
        /// AP free
        /// </summary>
        /// <param name="actor"></param>
        /// <param name="it"></param>
        public void DoEquipItem(Actor actor, Item it)
        {
            // unequip previous item first.
            Item previousItem = actor.GetEquippedItem(it.Model.EquipmentPart);
            if (previousItem != null)
            {
                DoUnequipItem(actor, previousItem);
            }

            // equip part.
            it.EquippedPart = it.Model.EquipmentPart;

            // update revelant datas.
            OnEquipItem(actor, it);

            // message
            if (IsVisibleToPlayer(actor))
                AddMessage(MakeMessage(actor, Conjugate(actor, VERB_EQUIP), it));
        }

        /// <summary>
        /// AP free
        /// </summary>
        /// <param name="actor"></param>
        /// <param name="it"></param>
        public void DoUnequipItem(Actor actor, Item it, bool canMessage = true)
        {
            // unequip part.
            it.EquippedPart = DollPart.NONE;

            // update revelant datas.
            OnUnequipItem(actor, it);

            // message.
            if (canMessage && IsVisibleToPlayer(actor))
                AddMessage(MakeMessage(actor, Conjugate(actor, VERB_UNEQUIP), it));
        }

        void OnEquipItem(Actor actor, Item it)
        {
            #region Weapons
            if (it.Model is ItemWeaponModel)
            {
                if (it.Model is ItemMeleeWeaponModel)
                {
                    ItemMeleeWeaponModel meleeModel = it.Model as ItemMeleeWeaponModel;
                    actor.CurrentMeleeAttack = Attack.MeleeAttack(
                        meleeModel.Attack.Verb,
                        (meleeModel.Attack.HitValue + actor.Sheet.UnarmedAttack.HitValue),
                        (meleeModel.Attack.DamageValue + actor.Sheet.UnarmedAttack.DamageValue),
                        meleeModel.Attack.StaminaPenalty,
                        meleeModel.Attack.DisarmChance);
                }
                else if (it.Model is ItemRangedWeaponModel)
                {
                    ItemRangedWeaponModel rangedModel = it.Model as ItemRangedWeaponModel;
                    actor.CurrentRangedAttack = Attack.RangedAttack(
                        rangedModel.Attack.Kind, rangedModel.Attack.Verb,
                        rangedModel.Attack.HitValue, rangedModel.Attack.Hit2Value, rangedModel.Attack.Hit3Value,
                        rangedModel.Attack.DamageValue,
                        rangedModel.Attack.Range);
                }
            }
            #endregion
            #region Armors
            else if (it.Model is ItemBodyArmorModel)
            {
                ItemBodyArmorModel armorModel = it.Model as ItemBodyArmorModel;
                actor.CurrentDefence += armorModel.ToDefence();
            }
            #endregion
            #region Batteries
            else if (it.Model is ItemTrackerModel)
            {
                ItemTracker trIt = it as ItemTracker;
                --trIt.Batteries;
            }
            else if (it.Model is ItemLightModel)
            {
                ItemLight ltIt = it as ItemLight;
                --ltIt.Batteries;
            }
            #endregion
        }

        void OnUnequipItem(Actor actor, Item it)
        {
            if (it.Model is ItemWeaponModel)
            {
                if (it.Model is ItemMeleeWeaponModel)
                {
                    actor.CurrentMeleeAttack = actor.Sheet.UnarmedAttack;
                }
                else if (it.Model is ItemRangedWeaponModel)
                {
                    actor.CurrentRangedAttack = Attack.BLANK;
                }
            }
            else if (it.Model is ItemBodyArmorModel)
            {
                ItemBodyArmorModel armorModel = it.Model as ItemBodyArmorModel;
                actor.CurrentDefence -= armorModel.ToDefence();
            }
        }

        public void DoDropItem(Actor actor, Item it)
        {
            // spend APs.
            SpendActorActionPoints(actor, Rules.BASE_ACTION_COST);

            // which item to drop (original or a clone)
            Item dropIt = it;
            // discard?
            bool discardMe = false;

            // special case for traps and discared items.
            if (it is ItemTrap)
            {
                ItemTrap trap = it as ItemTrap;

                // drop one at a time.
                ItemTrap clone = trap.Clone();
                //alpha10 clone.IsActivated = trap.IsActivated;
                if (trap.IsActivated) // alpha10
                    clone.Activate(actor);
                dropIt = clone;

                // trap activates when dropped?
                if (clone.TrapModel.ActivatesWhenDropped)
                    clone.Activate(actor); // alpha10 //clone.IsActivated = true;

                // make sure source stack is desactivated (activate only activate the stack top item).
                trap.Desactivate();  // alpha10  //trap.IsActivated = false;
            }
            else
            {
                // drop or discard.
                if (it is ItemTracker)
                {
                    discardMe = (it as ItemTracker).Batteries <= 0;
                }
                else if (it is ItemLight)
                {
                    discardMe = (it as ItemLight).Batteries <= 0;
                }
                else if (it is ItemSprayPaint)
                {
                    discardMe = (it as ItemSprayPaint).PaintQuantity <= 0;
                }
                else if (it is ItemSprayScent)
                {
                    discardMe = (it as ItemSprayScent).SprayQuantity <= 0;
                }
            }

            if (discardMe)
            {
                DiscardItem(actor, it);
                // message
                if (IsVisibleToPlayer(actor))
                    AddMessage(MakeMessage(actor, Conjugate(actor, VERB_DISCARD), it));
            }
            else
            {
                if (dropIt == it)
                    DropItem(actor, it);
                else
                    DropCloneItem(actor, it, dropIt);
                // message
                if (IsVisibleToPlayer(actor))
                    AddMessage(MakeMessage(actor, Conjugate(actor, VERB_DROP), dropIt));
            }
        }

        void DiscardItem(Actor actor, Item it)
        {
            // remove from inventory.
            actor.Inventory.RemoveAllQuantity(it);

            // make sure it is unequipped.
            it.EquippedPart = DollPart.NONE;
        }

        void DropItem(Actor actor, Item it)
        {
            // remove from inventory.
            actor.Inventory.RemoveAllQuantity(it);

            // add to ground.
            actor.Location.Map.DropItemAt(it, actor.Location.Position);

            // make sure it is unequipped.
            it.EquippedPart = DollPart.NONE;
        }

        void DropCloneItem(Actor actor, Item it, Item clone)
        {
            // remove one quantity from inventory.
            if (--it.Quantity <= 0)
                actor.Inventory.RemoveAllQuantity(it);

            // add to ground.
            actor.Location.Map.DropItemAt(clone, actor.Location.Position);

            // make sure it is unequipped.
            clone.EquippedPart = DollPart.NONE;
        }

        public void DoUseItem(Actor actor, Item it)
        {
            // alpha10 defrag ai inventories
            bool defragInventory = !actor.IsPlayer && it.Model.IsStackable;

            // concrete use.
            if (it is ItemFood)
                DoUseFoodItem(actor, it as ItemFood);
            else if (it is ItemMedicine)
                DoUseMedicineItem(actor, it as ItemMedicine);
            else if (it is ItemAmmo)
                DoUseAmmoItem(actor, it as ItemAmmo);
            //else if (it is ItemSprayScent)  // alpha10 new way to use spray scent
            //    DoUseSprayScentItem(actor, it as ItemSprayScent);
            else if (it is ItemTrap)
                DoUseTrapItem(actor, it as ItemTrap);
            else if (it is ItemEntertainment)
                DoUseEntertainmentItem(actor, it as ItemEntertainment);

            // alpha10 defrag ai inventories
            if (defragInventory)
                actor.Inventory.Defrag();
        }

        public void DoEatFoodFromGround(Actor actor, Item it)
        {
            ItemFood food = it as ItemFood;

            // spend APs.
            SpendActorActionPoints(actor, Rules.BASE_ACTION_COST);

            // recover food points.
            int baseNutrition = m_Rules.FoodItemNutrition(food, actor.Location.Map.LocalTime.TurnCounter);
            actor.FoodPoints = Math.Min(actor.FoodPoints + m_Rules.ActorItemNutritionValue(actor, baseNutrition), m_Rules.ActorMaxFood(actor));

            // consume it.
            Inventory inv = actor.Location.Map.GetItemsAt(actor.Location.Position);
            inv.Consume(food);

            // message.
            bool isVisible = IsVisibleToPlayer(actor);
            if (isVisible)
                AddMessage(MakeMessage(actor, Conjugate(actor, VERB_EAT), food));

            // vomit?
            if (m_Rules.IsFoodSpoiled(food, actor.Location.Map.LocalTime.TurnCounter))
            {
                if (m_Rules.RollChance(Rules.FOOD_EXPIRED_VOMIT_CHANCE))
                {
                    DoVomit(actor);

                    // message.
                    if (isVisible)
                    {
                        AddMessage(MakeMessage(actor, String.Format("{0} from eating spoiled food!", Conjugate(actor, VERB_VOMIT))));
                    }
                }
            }
        }

        void DoUseFoodItem(Actor actor, ItemFood food)
        {
            //////////////////////////////////////
            // If player, prevent wasteful usage.
            //////////////////////////////////////
            if (actor == m_Player && actor.FoodPoints >= m_Rules.ActorMaxFood(actor) - 1)
            {
                AddMessage(MakeErrorMessage("Don't waste food!"));
                return;
            }

            // spend APs.
            SpendActorActionPoints(actor, Rules.BASE_ACTION_COST);

            // recover food points.
            int baseNutrition = m_Rules.FoodItemNutrition(food, actor.Location.Map.LocalTime.TurnCounter);
            actor.FoodPoints = Math.Min(actor.FoodPoints + m_Rules.ActorItemNutritionValue(actor, baseNutrition), m_Rules.ActorMaxFood(actor));

            // consume it.
            actor.Inventory.Consume(food);

            // canned food drops empty cans.
            if (food.Model == GameItems.CANNED_FOOD)
            {
                ItemTrap emptyCan = new ItemTrap(GameItems.EMPTY_CAN);// alpha10 { IsActivated = true };
                emptyCan.Activate(actor);  // alpha10
                actor.Location.Map.DropItemAt(emptyCan, actor.Location.Position);
            }

            // message.
            bool isVisible = IsVisibleToPlayer(actor);
            if (isVisible)
                AddMessage(MakeMessage(actor, Conjugate(actor, VERB_EAT), food));

            // vomit?
            if (m_Rules.IsFoodSpoiled(food, actor.Location.Map.LocalTime.TurnCounter))
            {
                if (m_Rules.RollChance(Rules.FOOD_EXPIRED_VOMIT_CHANCE))
                {
                    DoVomit(actor);

                    // message.
                    if (isVisible)
                    {
                        AddMessage(MakeMessage(actor, String.Format("{0} from eating spoiled food!", Conjugate(actor, VERB_VOMIT))));
                    }
                }
            }
        }

        void DoVomit(Actor actor)
        {
            // beuargh.
            actor.StaminaPoints -= Rules.FOOD_VOMIT_STA_COST;
            actor.SleepPoints = Math.Max(0, actor.SleepPoints - WorldTime.TURNS_PER_HOUR);
            actor.FoodPoints = Math.Max(0, actor.FoodPoints - WorldTime.TURNS_PER_HOUR);

            // drop vomit ^^.
            Location loc = actor.Location;
            Map map = loc.Map;
            map.GetTileAt(loc.Position.X, loc.Position.Y).AddDecoration(GameImages.DECO_VOMIT);
        }

        void DoUseMedicineItem(Actor actor, ItemMedicine med)
        {
            //////////////////////////////////////
            // If player, prevent wasteful usage.
            //////////////////////////////////////
            if (actor == m_Player)
            {
                int HPneed = m_Rules.ActorMaxHPs(actor) - actor.HitPoints;
                int STAneed = m_Rules.ActorMaxSTA(actor) - actor.StaminaPoints;
                int SLPneed = m_Rules.ActorMaxSleep(actor) - 2 - actor.SleepPoints;
                int CureNeed = actor.Infection;
                int SanNeed = m_Rules.ActorMaxSanity(actor) - actor.Sanity;

                bool HPwaste = HPneed <= 0 || med.Healing <= 0;
                bool STAwaste = STAneed <= 0 || med.StaminaBoost <= 0;
                bool SLPwaste = SLPneed <= 0 || med.SleepBoost <= 0;
                bool CureWaste = CureNeed <= 0 || med.InfectionCure <= 0;
                bool SanWaste = SanNeed <= 0 || med.SanityCure <= 0;

                if (HPwaste && STAwaste && SLPwaste && CureWaste && SanWaste)
                {
                    AddMessage(MakeErrorMessage("Don't waste medicine!"));
                    return;
                }
            }

            // spend APs.
            SpendActorActionPoints(actor, Rules.BASE_ACTION_COST);

            // recover HPs, STA, SLP, INF, SAN.
            actor.HitPoints = Math.Min(actor.HitPoints + m_Rules.ActorMedicineEffect(actor, med.Healing), m_Rules.ActorMaxHPs(actor));
            actor.StaminaPoints = Math.Min(actor.StaminaPoints + m_Rules.ActorMedicineEffect(actor, med.StaminaBoost), m_Rules.ActorMaxSTA(actor));
            actor.SleepPoints = Math.Min(actor.SleepPoints + m_Rules.ActorMedicineEffect(actor, med.SleepBoost), m_Rules.ActorMaxSleep(actor));
            actor.Infection = Math.Max(0, actor.Infection - m_Rules.ActorMedicineEffect(actor, med.InfectionCure));
            actor.Sanity = Math.Min(actor.Sanity + m_Rules.ActorMedicineEffect(actor, med.SanityCure), m_Rules.ActorMaxSanity(actor));

            // consume it.
            actor.Inventory.Consume(med);

            // message.
            if (IsVisibleToPlayer(actor))
                AddMessage(MakeMessage(actor, Conjugate(actor, VERB_HEAL_WITH), med));
        }

        void DoUseAmmoItem(Actor actor, ItemAmmo ammoItem)
        {
            // spend APs.
            SpendActorActionPoints(actor, Rules.BASE_ACTION_COST);

            // get weapon.
            ItemRangedWeapon ranged = actor.GetEquippedWeapon() as ItemRangedWeapon;
            ItemRangedWeaponModel model = ranged.Model as ItemRangedWeaponModel;

            // compute ammo spent.
            int ammoSpent = Math.Min(model.MaxAmmo - ranged.Ammo, ammoItem.Quantity);

            // reload.
            ranged.Ammo += ammoSpent;

            // spend ammo clip.
            ammoItem.Quantity -= ammoSpent;

            // if no ammo left, remove item.
            if (ammoItem.Quantity <= 0)
                actor.Inventory.RemoveAllQuantity(ammoItem);

            // message.
            if (IsVisibleToPlayer(actor))
            {
                AddMessage(MakeMessage(actor, Conjugate(actor, VERB_RELOAD), ranged));
            }
        }

        // alpha10 obsolete
#if false
        void DoUseSprayScentItem(Actor actor, ItemSprayScent spray)
        {
            // spend APs.
            SpendActorActionPoints(actor, Rules.BASE_ACTION_COST);

            // consume spray.
            --spray.SprayQuantity;

            // add odor.
            Map map = actor.Location.Map;
            ItemSprayScentModel model = spray.Model as ItemSprayScentModel;
            map.ModifyScentAt(model.Odor, model.Strength, actor.Location.Position);

            // message.
            if (IsVisibleToPlayer(actor))
            {
                AddMessage(MakeMessage(actor, Conjugate(actor, VERB_SPRAY), spray));
            }
        }
#endif

        void DoUseTrapItem(Actor actor, ItemTrap trap)
        {
            // spend APs.
            SpendActorActionPoints(actor, Rules.BASE_ACTION_COST);

            // toggle activation.
            // alpha10 //trap.IsActivated = !trap.IsActivated;
            if (trap.IsActivated)
                trap.Desactivate();
            else
                trap.Activate(actor);

            // message.
            if (IsVisibleToPlayer(actor))
                AddMessage(MakeMessage(actor, Conjugate(actor, (trap.IsActivated ? VERB_ACTIVATE : VERB_DESACTIVATE)), trap));
        }

        void DoUseEntertainmentItem(Actor actor, ItemEntertainment ent)
        {
            bool visible = IsVisibleToPlayer(actor);

            // spend APs.
            SpendActorActionPoints(actor, Rules.BASE_ACTION_COST);

            // recover san.
            RegenActorSanity(actor, ent.EntertainmentModel.Value);

            // message.
            if (visible)
                AddMessage(MakeMessage(actor, Conjugate(actor, VERB_ENJOY), ent));

            // check boring chance.
            // 100% means discard it.
            int boreChance = ent.EntertainmentModel.BoreChance;
            bool bored = false;
            bool discarded = false;
            if (boreChance == 100)
            {
                actor.Inventory.Consume(ent);
                discarded = true;
            }
            else if (boreChance > 0)
            {
                if (m_Rules.RollChance(boreChance))
                    bored = true;
            }
            if (bored)
                ent.AddBoringFor(actor); // alpha10 boring items item centric

            // message.
            if (visible)
            {
                if (bored)
                    AddMessage(MakeMessage(actor, String.Format("{0} now bored of {1}.", Conjugate(actor, VERB_BE), ent.TheName)));
                if (discarded)
                    AddMessage(MakeMessage(actor, Conjugate(actor, VERB_DISCARD), ent));
            }
        }

        public void DoRechargeItemBattery(Actor actor, Item it)
        {
            // spend APs.
            SpendActorActionPoints(actor, Rules.BASE_ACTION_COST);

            // recharge.
            if (it is ItemLight)
            {
                ItemLight light = it as ItemLight;
                light.Batteries += WorldTime.TURNS_PER_HOUR;
            }
            else if (it is ItemTracker)
            {
                ItemTracker track = it as ItemTracker;
                track.Batteries += WorldTime.TURNS_PER_HOUR;
            }

            // message.
            if (IsVisibleToPlayer(actor))
            {
                AddMessage(MakeMessage(actor, Conjugate(actor, VERB_RECHARGE), it, " batteries."));
            }

        }
        #endregion
        #region Doors
        public void DoOpenDoor(Actor actor, DoorWindow door)
        {
            // Do it.
            door.SetState(DoorWindow.STATE_OPEN);

            // Message.
            if (IsVisibleToPlayer(actor) || IsVisibleToPlayer(door))
            {
                AddMessage(MakeMessage(actor, Conjugate(actor, VERB_OPEN), door));
                RedrawPlayScreen();
            }

            // Spend APs.
            int openCost = Rules.BASE_ACTION_COST;
            SpendActorActionPoints(actor, openCost);
        }

        public void DoCloseDoor(Actor actor, DoorWindow door)
        {
            // Do it.
            door.SetState(DoorWindow.STATE_CLOSED);

            // Message.
            if (IsVisibleToPlayer(actor) || IsVisibleToPlayer(door))
            {
                AddMessage(MakeMessage(actor, Conjugate(actor, VERB_CLOSE), door));
                RedrawPlayScreen();
            }

            // Spend APs.
            int closeCost = Rules.BASE_ACTION_COST;
            SpendActorActionPoints(actor, closeCost);
        }

        public void DoBarricadeDoor(Actor actor, DoorWindow door)
        {
            // get barricading item.
            ItemBarricadeMaterial it = actor.Inventory.GetSmallestStackByType(typeof(ItemBarricadeMaterial)) as ItemBarricadeMaterial; // alpha10
            ItemBarricadeMaterialModel m = it.Model as ItemBarricadeMaterialModel;

            // do it.
            actor.Inventory.Consume(it);
            door.BarricadePoints = Math.Min(door.BarricadePoints + m_Rules.ActorBarricadingPoints(actor, m.BarricadingValue), Rules.BARRICADING_MAX);

            // message.
            bool isVisible = IsVisibleToPlayer(actor) || IsVisibleToPlayer(door);
            if (isVisible)
            {
                AddMessage(MakeMessage(actor, Conjugate(actor, VERB_BARRICADE), door));
            }

            // spend AP.
            int barricadingCost = Rules.BASE_ACTION_COST;
            SpendActorActionPoints(actor, barricadingCost);
        }
        #endregion
        #region Building & Repairing
        public void DoBuildFortification(Actor actor, Point buildPos, bool isLarge)
        {
            // spend AP.
            SpendActorActionPoints(actor, Rules.BASE_ACTION_COST);

            // consume material.
            int need = m_Rules.ActorBarricadingMaterialNeedForFortification(actor, isLarge);
            for (int i = 0; i < need; i++)
            {
                Item it = actor.Inventory.GetSmallestStackByType(typeof(ItemBarricadeMaterial)); // alpha10
                                                                                                 //actor.Inventory.GetFirstByType(typeof(ItemBarricadeMaterial));
                actor.Inventory.Consume(it);
            }

            // add object.
            Fortification fortObj = isLarge ? m_TownGenerator.MakeObjLargeFortification(GameImages.OBJ_LARGE_WOODEN_FORTIFICATION) : m_TownGenerator.MakeObjSmallFortification(GameImages.OBJ_SMALL_WOODEN_FORTIFICATION);
            actor.Location.Map.PlaceMapObjectAt(fortObj, buildPos);

            // message.
            if (IsVisibleToPlayer(actor) || IsVisibleToPlayer(new Location(actor.Location.Map, buildPos)))
            {
                AddMessage(MakeMessage(actor, String.Format("{0} a {1} fortification.", Conjugate(actor, VERB_BUILD), isLarge ? "large" : "small")));
            }

            // check traps.
            CheckMapObjectTriggersTraps(actor.Location.Map, buildPos);
        }

        public void DoRepairFortification(Actor actor, Fortification fort)
        {
            // spend AP.
            SpendActorActionPoints(actor, Rules.BASE_ACTION_COST);

            // spend material.
            ItemBarricadeMaterial material = actor.Inventory.GetSmallestStackByType(typeof(ItemBarricadeMaterial)) as ItemBarricadeMaterial; // alpha10
                                                                                                                                             //actor.Inventory.GetFirstByType(typeof(ItemBarricadeMaterial)) as ItemBarricadeMaterial;
            if (material == null)
                throw new InvalidOperationException("no material");
            actor.Inventory.Consume(material);

            // repair HP.
            fort.HitPoints = Math.Min(fort.MaxHitPoints,
                fort.HitPoints + m_Rules.ActorBarricadingPoints(actor, (material.Model as ItemBarricadeMaterialModel).BarricadingValue));

            // message.
            if (IsVisibleToPlayer(actor) || IsVisibleToPlayer(fort))
            {
                AddMessage(MakeMessage(actor, Conjugate(actor, VERB_REPAIR), fort));
            }
        }
        #endregion
        #region Switching map object
        public void DoSwitchPowerGenerator(Actor actor, PowerGenerator powGen)
        {
            // spend AP.
            SpendActorActionPoints(actor, Rules.BASE_ACTION_COST);

            // switch it.
            powGen.TogglePower();

            // message.
            if (IsVisibleToPlayer(actor) || IsVisibleToPlayer(powGen))
            {
                AddMessage(MakeMessage(actor, Conjugate(actor, VERB_SWITCH), powGen, powGen.IsOn ? " on." : " off."));
            }

            // check for special effects.
            OnMapPowerGeneratorSwitch(actor.Location, powGen);

            // done.
        }
        #endregion
    }
}
