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
        bool HandleMouseLook(Point mousePos)
        {
            // Ignore if out of view rect.
            Point mouseMap = MouseToMap(mousePos);
            if (!IsInViewRect(mouseMap))
                return false;

            // Nothing to do if out of map, but still handle the mouse.
            if (!m_Session.CurrentMap.IsInBounds(mouseMap))
                return true;

            // Do look.
            ClearOverlays();
            if (IsVisibleToPlayer(m_Session.CurrentMap, mouseMap))
            {
                Point tileScreenPos = MapToScreen(mouseMap);
                string[] description = DescribeStuffAt(m_Session.CurrentMap, mouseMap);
                if (description != null)
                {
                    Point popupPos = new Point(tileScreenPos.X + TILE_SIZE, tileScreenPos.Y);
                    AddOverlay(new OverlayPopup(description, Color.White, Color.White, POPUP_FILLCOLOR, popupPos));
                    if (s_Options.ShowTargets)
                    {
                        Actor actorThere = m_Session.CurrentMap.GetActorAt(mouseMap);
                        if (actorThere != null)
                            DrawActorRelations(actorThere);
                    }
                }
            }

            // Handled mouse.
            return true;
        }

        bool HandleMouseInventory(Point mousePos, MouseButtons? mouseButtons, out bool hasDoneAction)
        {
            // Ignore if not on an inventory slot.
            Inventory inv;
            Point itemPos;
            int iSlot;
            Item it = MouseToInventoryItem(mousePos, out inv, out itemPos, out iSlot);
            if (inv == null)
            {
                hasDoneAction = false;
                return false;
            }

            // Do inventory stuff.
            bool isPlayerInventory = (inv == m_Player.Inventory);
            hasDoneAction = false;
            ClearOverlays();
            AddOverlay(new OverlayRect(Color.Cyan, new Rectangle(itemPos.X, itemPos.Y, 32, 32)));
            AddOverlay(new OverlayRect(Color.Cyan, new Rectangle(itemPos.X + 1, itemPos.Y + 1, 30, 30)));
            if (it != null)
            {
                string[] lines = DescribeItemLong(it, isPlayerInventory, iSlot);
                int longestLine = 1 + FindLongestLine(lines);
                int ovX = itemPos.X - 7 * longestLine;
                int ovY = itemPos.Y + 32;

                AddOverlay(new OverlayPopup(lines, Color.White, Color.White, POPUP_FILLCOLOR, new Point(ovX, ovY)));

                // item action?
                if (mouseButtons.HasValue)
                {
                    if (mouseButtons == MouseButtons.Left)
                        hasDoneAction = OnLMBItem(inv, it);
                    else if (mouseButtons == MouseButtons.Right)
                        hasDoneAction = OnRMBItem(inv, it);
                }
            }

            // Handled mouse.
            return true;
        }

        Item MouseToInventoryItem(Point screen, out Inventory inv, out Point itemPos, out int iSlot)
        {
            inv = null;
            itemPos = Point.Empty;
            iSlot = -1; // alpha10

            if (m_Player == null)
                return null;

            Inventory playerInv = m_Player.Inventory;
            Point playerSlot = MouseToInventorySlot(INVENTORYPANEL_X, INVENTORYPANEL_Y, screen.X, screen.Y);
            int playerItemIndex = playerSlot.X + playerSlot.Y * INVENTORY_SLOTS_PER_LINE;
            if (playerItemIndex >= 0 && playerItemIndex < playerInv.MaxCapacity)
            {
                inv = playerInv;
                itemPos = InventorySlotToScreen(INVENTORYPANEL_X, INVENTORYPANEL_Y, playerSlot.X, playerSlot.Y);
                iSlot = playerItemIndex; // alpha10
                return playerInv[playerItemIndex];
            }

            Inventory groundInv = m_Player.Location.Map.GetItemsAt(m_Player.Location.Position);
            Point groundSlot = MouseToInventorySlot(INVENTORYPANEL_X, GROUNDINVENTORYPANEL_Y, screen.X, screen.Y);
            itemPos = InventorySlotToScreen(INVENTORYPANEL_X, GROUNDINVENTORYPANEL_Y, groundSlot.X, groundSlot.Y);
            if (groundInv == null)
                return null;
            int groundItemIndex = groundSlot.X + groundSlot.Y * INVENTORY_SLOTS_PER_LINE;
            if (groundItemIndex >= 0 && groundItemIndex < groundInv.MaxCapacity)
            {
                inv = groundInv;
                iSlot = groundItemIndex; // alpha10
                return groundInv[groundItemIndex];
            }

            return null;
        }

        bool OnLMBItem(Inventory inv, Item it)
        {
            if (inv == m_Player.Inventory)
            {
                // LMB in player inv = use/equip toggle
                if (it.IsEquipped)
                {
                    string reason;
                    if (m_Rules.CanActorUnequipItem(m_Player, it, out reason))
                    {
                        DoUnequipItem(m_Player, it);
                        return false;
                    }
                    else
                    {
                        AddMessage(MakeErrorMessage(String.Format("Cannot unequip {0} : {1}.", it.TheName, reason)));
                        return false;
                    }
                }
                else if (it.Model.IsEquipable)
                {
                    string reason;
                    if (m_Rules.CanActorEquipItem(m_Player, it, out reason))
                    {
                        DoEquipItem(m_Player, it);
                        return false;
                    }
                    else
                    {
                        AddMessage(MakeErrorMessage(String.Format("Cannot equip {0} : {1}.", it.TheName, reason)));
                        return false;
                    }
                }
                else
                {
                    // try to use item.
                    string reason;
                    if (m_Rules.CanActorUseItem(m_Player, it, out reason))
                    {
                        DoUseItem(m_Player, it);
                        return true;
                    }
                    else
                    {
                        AddMessage(MakeErrorMessage(String.Format("Cannot use {0} : {1}.", it.TheName, reason)));
                    }
                }
            }
            else // ground inventory
            {
                // LMB in ground inv = take
                string reason;
                if (m_Rules.CanActorGetItem(m_Player, it, out reason))
                {
                    DoTakeItem(m_Player, m_Player.Location.Position, it);
                    return true;
                }
                else
                {
                    AddMessage(MakeErrorMessage(String.Format("Cannot take {0} : {1}.", it.TheName, reason)));
                    return false;
                }
            }

            return false;
        }

        bool OnRMBItem(Inventory inv, Item it)
        {
            if (inv == m_Player.Inventory)
            {
                string reason;
                if (m_Rules.CanActorDropItem(m_Player, it, out reason))
                {
                    DoDropItem(m_Player, it);
                    return true;
                }
                else
                {
                    AddMessage(MakeErrorMessage(String.Format("Cannot drop {0} : {1}.", it.TheName, reason)));
                    return false;
                }
            }

            return false;
        }

        #region Corpses
        bool HandleMouseOverCorpses(Point mousePos, MouseButtons? mouseButtons, out bool hasDoneAction)
        {
            // Ignore if not on a corpse slot.
            Point corpsePos;
            Corpse corpse = MouseToCorpse(mousePos, out corpsePos);
            if (corpse == null)
            {
                hasDoneAction = false;
                return false;
            }

            // Do corpse stuff.
            hasDoneAction = false;
            ClearOverlays();
            AddOverlay(new OverlayRect(Color.Cyan, new Rectangle(corpsePos.X, corpsePos.Y, 32, 32)));
            AddOverlay(new OverlayRect(Color.Cyan, new Rectangle(corpsePos.X + 1, corpsePos.Y + 1, 30, 30)));
            if (corpse != null)
            {
                string[] lines = DescribeCorpseLong(corpse, true);
                int longestLine = 1 + FindLongestLine(lines);
                int ovX = corpsePos.X - 7 * longestLine;
                int ovY = corpsePos.Y + 32;

                AddOverlay(new OverlayPopup(lines, Color.White, Color.White, POPUP_FILLCOLOR, new Point(ovX, ovY)));

                // mouse action?
                if (mouseButtons.HasValue)
                {
                    if (mouseButtons == MouseButtons.Left)
                        hasDoneAction = OnLMBCorpse(corpse);
                    else if (mouseButtons == MouseButtons.Right)
                        hasDoneAction = OnRMBCorpse(corpse);
                }
            }

            // Handled mouse.
            return true;
        }

        Corpse MouseToCorpse(Point screen, out Point corpsePos)
        {
            corpsePos = Point.Empty;

            if (m_Player == null)
                return null;

            List<Corpse> corpsesList = m_Player.Location.Map.GetCorpsesAt(m_Player.Location.Position);
            if (corpsesList == null)
                return null;
            Point corpseSlot = MouseToInventorySlot(INVENTORYPANEL_X, CORPSESPANEL_Y, screen.X, screen.Y);
            corpsePos = InventorySlotToScreen(INVENTORYPANEL_X, CORPSESPANEL_Y, corpseSlot.X, corpseSlot.Y);
            int corpseIndex = corpseSlot.X + corpseSlot.Y * INVENTORY_SLOTS_PER_LINE;
            if (corpseIndex >= 0 && corpseIndex < corpsesList.Count)
                return corpsesList[corpseIndex];

            return null;
        }

        bool OnLMBCorpse(Corpse c)
        {
            if (c.IsDragged)
            {
                string reason;
                if (m_Rules.CanActorStopDragCorpse(m_Player, c, out reason))
                {
                    DoStopDragCorpse(m_Player, c);
                    return false;
                }
                else
                {
                    AddMessage(MakeErrorMessage(String.Format("Cannot stop dragging {0} corpse : {1}.", c.DeadGuy.Name, reason)));
                    return false;
                }
            }
            else
            {
                string reason;
                if (m_Rules.CanActorStartDragCorpse(m_Player, c, out reason))
                {
                    DoStartDragCorpse(m_Player, c);
                    return false;
                }
                else
                {
                    AddMessage(MakeErrorMessage(String.Format("Cannot start dragging {0} corpse : {1}.", c.DeadGuy.Name, reason)));
                    return false;
                }
            }
        }

        bool OnRMBCorpse(Corpse c)
        {
            string reason;
            if (m_Player.Model.Abilities.IsUndead)
            {
                if (m_Rules.CanActorEatCorpse(m_Player, c, out reason))
                {
                    DoEatCorpse(m_Player, c);
                    return true;
                }
                else
                {
                    AddMessage(MakeErrorMessage(String.Format("Cannot eat {0} corpse : {1}.", c.DeadGuy.Name, reason)));
                    return false;
                }
            }
            else
            {
                if (m_Rules.CanActorButcherCorpse(m_Player, c, out reason))
                {
                    DoButcherCorpse(m_Player, c);
                    return true;
                }
                else
                {
                    AddMessage(MakeErrorMessage(String.Format("Cannot butcher {0} corpse : {1}.", c.DeadGuy.Name, reason)));
                    return false;
                }
            }
        }

        bool HandlePlayerEatCorpse(Actor player, Point mousePos)
        {
            // Ignore if not on a corpse slot.
            Point corpsePos;
            Corpse corpse = MouseToCorpse(mousePos, out corpsePos);
            if (corpse == null)
                return false;

            // Check legality.
            string reason;
            if (!m_Rules.CanActorEatCorpse(player, corpse, out reason))
            {
                AddMessage(MakeErrorMessage(String.Format("Cannot eat {0} corpse : {1}.", corpse.DeadGuy.Name, reason)));
                return false;
            }

            // Do it.
            DoEatCorpse(player, corpse);
            return true;
        }

        bool HandlePlayerReviveCorpse(Actor player, Point mousePos)
        {
            // Ignore if not on a corpse slot.
            Point corpsePos;
            Corpse corpse = MouseToCorpse(mousePos, out corpsePos);
            if (corpse == null)
                return false;

            // Check legality.
            string reason;
            if (!m_Rules.CanActorReviveCorpse(player, corpse, out reason))
            {
                AddMessage(MakeErrorMessage(String.Format("Cannot revive {0} : {1}.", corpse.DeadGuy.Name, reason)));
                return false;
            }

            // Do it.
            DoReviveCorpse(player, corpse);
            return true;
        }

        public void DoStartDragCorpse(Actor a, Corpse c)
        {
            c.DraggedBy = a;
            a.DraggedCorpse = c;
            if (IsVisibleToPlayer(a))
                AddMessage(MakeMessage(a, String.Format("{0} dragging {1} corpse.", Conjugate(a, VERB_START), c.DeadGuy.Name)));
        }

        public void DoStopDragCorpse(Actor a, Corpse c)
        {
            c.DraggedBy = null;
            a.DraggedCorpse = null;
            if (IsVisibleToPlayer(a))
                AddMessage(MakeMessage(a, String.Format("{0} dragging {1} corpse.", Conjugate(a, VERB_STOP), c.DeadGuy.Name)));
        }

        public void DoStopDraggingCorpses(Actor a)
        {
            if (a.DraggedCorpse != null)
            {
                DoStopDragCorpse(a, a.DraggedCorpse);
            }
        }

        public void DoButcherCorpse(Actor a, Corpse c)
        {
            bool isVisible = IsVisibleToPlayer(a);

            // spend ap.
            SpendActorActionPoints(a, Rules.BASE_ACTION_COST);

            // cause insanity.
            SeeingCauseInsanity(a, a.Location, Rules.SANITY_HIT_BUTCHERING_CORPSE, String.Format("{0} butchering {1}", a.Name, c.DeadGuy.Name));

            // damage.
            int dmg = m_Rules.ActorDamageVsCorpses(a);

            if (isVisible)
                AddMessage(MakeMessage(a, String.Format("{0} {1} corpse for {2} damage.", Conjugate(a, VERB_BUTCHER), c.DeadGuy.Name, dmg)));

            InflictDamageToCorpse(c, dmg);

            // destroy?
            if (c.HitPoints <= 0)
            {
                DestroyCorpse(c, a.Location.Map);
                if (isVisible)
                    AddMessage(new Message(String.Format("{0} corpse is no more.", c.DeadGuy.Name), a.Location.Map.LocalTime.TurnCounter, Color.Purple));
            }
        }

        public void DoEatCorpse(Actor a, Corpse c)
        {
            bool isVisible = IsVisibleToPlayer(a);

            // spend ap.
            SpendActorActionPoints(a, Rules.BASE_ACTION_COST);

            // damage.
            int dmg = m_Rules.ActorDamageVsCorpses(a);

            // msg.
            if (isVisible)
            {
                AddMessage(MakeMessage(a, String.Format("{0} {1} corpse.", Conjugate(a, VERB_FEAST_ON), c.DeadGuy.Name, dmg)));
                // alpha10 replace with sfx
                m_MusicManager.Stop();
                m_MusicManager.Play(GameSounds.UNDEAD_EAT, MusicPriority.PRIORITY_EVENT);
            }

            // dmh corpse.
            InflictDamageToCorpse(c, dmg);

            // destroy?
            if (c.HitPoints <= 0)
            {
                DestroyCorpse(c, a.Location.Map);
                if (isVisible)
                    AddMessage(new Message(String.Format("{0} corpse is no more.", c.DeadGuy.Name), a.Location.Map.LocalTime.TurnCounter, Color.Purple));
            }

            // heal if undead / food.
            if (a.Model.Abilities.IsUndead)
            {
                RegenActorHitPoints(a, Rules.ActorBiteHpRegen(a, dmg));
                a.FoodPoints = Math.Min(a.FoodPoints + m_Rules.ActorBiteNutritionValue(a, dmg), m_Rules.ActorMaxRot(a));
            }
            else
            {
                // recover food points.
                a.FoodPoints = Math.Min(a.FoodPoints + m_Rules.ActorBiteNutritionValue(a, dmg), m_Rules.ActorMaxFood(a));
                // infection!
                InfectActor(a, m_Rules.CorpseEeatingInfectionTransmission(c.DeadGuy.Infection));
            }

            // cause insanity.
            SeeingCauseInsanity(a, a.Location, a.Model.Abilities.IsUndead ? Rules.SANITY_HIT_UNDEAD_EATING_CORPSE : Rules.SANITY_HIT_LIVING_EATING_CORPSE,
                String.Format("{0} eating {1}", a.Name, c.DeadGuy.Name));
        }

        public void DoReviveCorpse(Actor actor, Corpse corpse)
        {
            bool visible = IsVisibleToPlayer(actor);

            // spend ap.
            SpendActorActionPoints(actor, Rules.BASE_ACTION_COST);

            // make sure there is a walkable spot for revival.
            Map map = actor.Location.Map;
            List<Point> revivePoints = actor.Location.Map.FilterAdjacentInMap(actor.Location.Position,
                (pt) =>
                {
                    if (map.GetActorAt(pt) != null) return false;
                    if (map.GetMapObjectAt(pt) != null) return false;
                    return true;
                });
            if (revivePoints == null)
            {
                if (visible)
                    AddMessage(MakeMessage(actor, String.Format("{0} not enough room for reviving {1}.", Conjugate(actor, VERB_HAVE), corpse.DeadGuy.Name)));
                return;
            }
            Point revivePt = revivePoints[m_Rules.Roll(0, revivePoints.Count)];

            // spend medikit.
            Item medikit = actor.Inventory.GetSmallestStackByModel(GameItems.MEDIKIT);  // alpha10
                                                                                        //actor.Inventory.GetFirstMatching((it) => it.Model == GameItems.MEDIKIT);
            actor.Inventory.Consume(medikit);

            // try.
            int chance = m_Rules.CorpseReviveChance(actor, corpse);
            if (m_Rules.RollChance(chance))
            {
                // do it.
                corpse.DeadGuy.IsDead = false;
                corpse.DeadGuy.HitPoints = m_Rules.CorpseReviveHPs(actor, corpse);
                corpse.DeadGuy.Doll.RemoveDecoration(GameImages.BLOODIED);
                corpse.DeadGuy.Activity = Activity.IDLE;
                corpse.DeadGuy.TargetActor = null;
                map.RemoveCorpse(corpse);
                map.PlaceActorAt(corpse.DeadGuy, revivePt);
                // msg.
                if (visible)
                    AddMessage(MakeMessage(actor, Conjugate(actor, VERB_REVIVE), corpse.DeadGuy));
                // thank you... or not?
                if (!m_Rules.AreEnemies(actor, corpse.DeadGuy))
                    DoSay(corpse.DeadGuy, actor, "Thank you, you saved my life!", Sayflags.NONE);
            }
            else
            {
                // msg.
                if (visible)
                    AddMessage(MakeMessage(actor, String.Format("{0} to revive", Conjugate(actor, VERB_FAIL)), corpse.DeadGuy));
            }
        }

        void InflictDamageToCorpse(Corpse c, float dmg)
        {
            c.HitPoints -= dmg;
        }

        void DestroyCorpse(Corpse c, Map m)
        {
            if (c.DraggedBy != null)
            {
                c.DraggedBy.DraggedCorpse = null;
                c.DraggedBy = null;
            }
            m.RemoveCorpse(c);
        }
        #endregion

        bool DoPlayerItemSlot(Actor player, int slot, KeyEventArgs key)
        {
            // get key modifier and redirect to proper action.
            // Ctrl  -> equip/unequip/use item from player inv
            // Shift -> take item from ground inv
            // Alt -> drop item from player inv.
            if ((key.Modifiers & Keys.Control) != 0)
                return DoPlayerItemSlotUse(player, slot);
            else if (key.Shift)
                return DoPlayerItemSlotTake(player, slot);
            else if (key.Alt)
                return DoPlayerItemSlotDrop(player, slot);

            // nope.
            return false;
        }

        bool DoPlayerItemSlotUse(Actor player, int slot)
        {
            Inventory inv = player.Inventory;
            Item it = inv[slot];

            // if no item, nothing to do.
            if (it == null)
            {
                AddMessage(MakeErrorMessage(String.Format("No item at inventory slot {0}.", (slot + 1))));
                return false;
            }

            // ty to unequip/equip/use.
            // shameful copy of OnLMBItem.
            // shame on me.
            if (it.IsEquipped)
            {
                string reason;
                if (m_Rules.CanActorUnequipItem(player, it, out reason))
                {
                    DoUnequipItem(player, it);
                    return false;
                }
                else
                {
                    AddMessage(MakeErrorMessage(String.Format("Cannot unequip {0} : {1}.", it.TheName, reason)));
                    return false;
                }
            }
            else if (it.Model.IsEquipable)
            {
                string reason;
                if (m_Rules.CanActorEquipItem(player, it, out reason))
                {
                    DoEquipItem(player, it);
                    return false;
                }
                else
                {
                    AddMessage(MakeErrorMessage(String.Format("Cannot equip {0} : {1}.", it.TheName, reason)));
                    return false;
                }
            }
            else
            {
                // try to use item.
                string reason;
                if (m_Rules.CanActorUseItem(player, it, out reason))
                {
                    DoUseItem(player, it);
                    return true;
                }
                else
                {
                    AddMessage(MakeErrorMessage(String.Format("Cannot use {0} : {1}.", it.TheName, reason)));
                }
            }

            // nothing done.
            return false;
        }

        bool DoPlayerItemSlotTake(Actor player, int slot)
        {
            Inventory inv = player.Location.Map.GetItemsAt(player.Location.Position);

            // if no items on ground, nothing to do.
            if (inv == null || inv.IsEmpty)
            {
                AddMessage(MakeErrorMessage("No items on ground."));
                return false;
            }

            // if no item, nothing to do.
            Item it = inv[slot];
            if (it == null)
            {
                AddMessage(MakeErrorMessage(String.Format("No item at ground slot {0}.", (slot + 1))));
                return false;
            }

            // try to take.
            string reason;
            if (m_Rules.CanActorGetItem(player, it, out reason))
            {
                DoTakeItem(player, player.Location.Position, it);
                return true;
            }
            else
            {
                AddMessage(MakeErrorMessage(String.Format("Cannot take {0} : {1}.", it.TheName, reason)));
                return false;
            }
        }

        bool DoPlayerItemSlotDrop(Actor player, int slot)
        {
            Inventory inv = player.Inventory;
            Item it = inv[slot];

            // if no item, nothing to do.
            if (it == null)
            {
                AddMessage(MakeErrorMessage(String.Format("No item at inventory slot {0}.", (slot + 1))));
                return false;
            }

            // try to drop.
            string reason;
            if (m_Rules.CanActorDropItem(player, it, out reason))
            {
                DoDropItem(player, it);
                return true;
            }
            else
            {
                AddMessage(MakeErrorMessage(String.Format("Cannot drop {0} : {1}.", it.TheName, reason)));
                return false;
            }
        }

        bool HandlePlayerShout(Actor player, string text)
        {
            string reason;
            if (!m_Rules.CanActorShout(player, out reason))
            {
                AddMessage(MakeErrorMessage(String.Format("Can't shout : {0}.", reason)));
                return false;
            }

            DoShout(player, text);
            return true;
        }

        bool HandlePlayerGiveItem(Actor player, Point screen)
        {
            // get player inventory item under mouse.
            Inventory inv;
            Point itemPos;
            int iSlot;
            Item gift = MouseToInventoryItem(screen, out inv, out itemPos, out iSlot);
            if (inv == null || inv != player.Inventory || gift == null)
                return false;

            // handle give item.
            bool loop = true;
            bool actionDone = false;
            ClearOverlays();
            AddOverlay(new OverlayPopup(GIVE_MODE_TEXT, MODE_TEXTCOLOR, MODE_BORDERCOLOR, MODE_FILLCOLOR, new Point(0, 0)));
            do
            {
                ///////////////////
                // 1. Redraw
                // 2. Get input.
                // 3. Handle input
                ///////////////////

                // 1. Redraw
                AddMessage(new Message(String.Format("Giving {0} to...", gift.TheName), m_Session.WorldTime.TurnCounter, Color.Yellow));
                RedrawPlayScreen();

                // 2. Get input.
                Direction dir = WaitDirectionOrCancel();

                // 3. Handle input
                if (dir == null)
                {
                    loop = false;
                }
                else if (dir != Direction.NEUTRAL)
                {
                    Point pos = player.Location.Position + dir;
                    if (player.Location.Map.IsInBounds(pos))
                    {
                        Actor other = player.Location.Map.GetActorAt(pos);
                        if (other != null)
                        {
                            string reason;
                            if (m_Rules.CanActorGiveItemTo(player, other, gift, out reason))
                            {
                                // do it.
                                actionDone = true;
                                loop = false;
                                DoGiveItemTo(player, other, gift);
                            }
                            else
                            {
                                AddMessage(MakeErrorMessage(String.Format("Can't give {0} to {1} : {2}.", gift.TheName, other.TheName, reason)));
                            }
                        }
                        else
                            AddMessage(MakeErrorMessage("Noone there."));
                    }
                }
            }
            while (loop);

            // cleanup.
            ClearOverlays();

            // return if we did an action.
            return actionDone;
        }

        // alpha10 new trade window dialog
        bool HandlePlayerTradeNegociation(Actor player, Actor npc)
        {
            BaseAI npcAI = npc.Controller as BaseAI;
            bool isOnPlayerInventory = true;
            int iPlayerSelectedItem = -1;
            int iNpcSelectedItem = -1;
            int state = 0; // 0 selecting 1st item; 1 selecting 2nd item; 2 making the offer

            // pre-compute all possible trade deals ratings
            TradeRating[,] ratingPairs = new TradeRating[player.Inventory.CountItems, npc.Inventory.CountItems];
            for (int i = 0; i < player.Inventory.CountItems; i++)
            {
                Item offered = player.Inventory[i];
                for (int j = 0; j < npc.Inventory.CountItems; j++)
                    ratingPairs[i, j] = npcAI.RateTradeOffer(this, player, offered, npc.Inventory[j]);
            }

            // roll charisma to later accept or refuse "maybe" deal.
            int charismaChance = m_Rules.ActorCharismaticTradeChance(player);
            bool charismaSuccess = m_Session.Player_TurnCharismaRoll < charismaChance;

            // remember if player is trusted leader to notify him.
            bool isTrustedLeader = (npc.Leader == player) && m_Rules.IsActorTrustingLeader(npc);

            // loop
            bool loop = true;
            bool actionDone = false;
            List<String> lines = new List<string>();
            List<Color> colors = new List<Color>();

            Color tradeToColor(TradeRating r)
            {
                if (r == TradeRating.ACCEPT) return TRADE_COLOR_ACCEPT;
                if (r == TradeRating.REFUSE) return TRADE_COLOR_REFUSE;
                if (charismaSuccess) return TRADE_COLOR_MAYBE_SUCCESS;
                return TRADE_COLOR_MAYBE_FAILED;
            };

            do
            {
                ///////////////////
                // 1. Redraw
                // 2. Handle state
                ///////////////////

                // 1. Redraw

                lines.Clear();
                colors.Clear();

                if (state == 2)
                {
                    lines.Add("Mode: Making the offer");
                }
                else
                {
                    if (isOnPlayerInventory)
                    {
                        if (state == 0)
                            lines.Add("Mode: Proposing an item");
                        else
                            lines.Add("Mode: Selecting your item to exhange");
                    }
                    else
                    {
                        if (state == 0)
                            lines.Add("Mode: Asking for an item");
                        else
                            lines.Add("Mode: Selecting an item to exhange");
                    }
                }
                colors.Add(Color.Yellow);

                lines.Add(" ");
                colors.Add(Color.Black);

                // header help 1: trusted leader
                if (isTrustedLeader)
                {
                    lines.Add(" "); colors.Add(Color.White);
                    lines.Add(string.Format("You are {0} trusted leader, will accept all trades.", HisOrHer(npc)));
                    colors.Add(Color.LightGreen);
                }

                // header help 2: charisma roll
                if (charismaSuccess)
                {
                    lines.Add(string.Format("Charisma roll success {0}/{1}%", m_Session.Player_TurnCharismaRoll, charismaChance));
                    colors.Add(Color.LightGreen);
                }
                else
                {
                    lines.Add(string.Format("Charisma roll failed {0}/{1}%", m_Session.Player_TurnCharismaRoll, charismaChance));
                    colors.Add(Color.Red);
                }

                void ListTradeItems(Actor a, bool isActive)
                {
                    lines.Add(string.Format("{0} items", a.TheName));
                    colors.Add(Color.White);
                    for (int i = 0; i < a.Inventory.CountItems; i++)
                    {
                        Item it = a.Inventory[i];
                        if (isActive)
                        {
                            lines.Add(string.Format("{0}. {1}", (i == 9 ? 0 : (i + 1)), DescribeItemShort(it)));
                            if (state == 0)  // proposing item
                                colors.Add(Color.Yellow);
                            else  // trading for current item
                            {
                                TradeRating r = (a == player && isActive ? ratingPairs[i, iNpcSelectedItem] : ratingPairs[iPlayerSelectedItem, i]);
                                colors.Add(tradeToColor(r));
                            }
                        }
                        else
                        {
                            lines.Add("-. " + DescribeItemShort(it));
                            colors.Add(i == (a == player ? iPlayerSelectedItem : iNpcSelectedItem) ? TRADE_COLOR_SELECTED_ITEM : Color.Gray);
                        }
                    }
                };

                // list items, player and npc
                lines.Add(" "); colors.Add(Color.Black);
                ListTradeItems(player, isOnPlayerInventory && state != 2);
                lines.Add(" "); colors.Add(Color.Black);
                ListTradeItems(npc, !isOnPlayerInventory && state != 2);

                // footnote help: trade ratings color legend
                if (state != 0 && !isTrustedLeader)
                {
                    lines.Add(" "); colors.Add(Color.White);
                    lines.Add("Trade color legend : "); colors.Add(Color.White);
                    lines.Add("  asked/offered"); colors.Add(TRADE_COLOR_SELECTED_ITEM);
                    lines.Add("  will accept"); colors.Add(TRADE_COLOR_ACCEPT);
                    lines.Add("  will accept due to your charisma"); colors.Add(TRADE_COLOR_MAYBE_SUCCESS);
                    lines.Add("  will refuse due to failed charisma"); colors.Add(TRADE_COLOR_MAYBE_FAILED);
                    lines.Add("  will refuse"); colors.Add(TRADE_COLOR_REFUSE);
                }

                // draw
                ClearOverlays();
                AddOverlay(new OverlayPopup(TRADING_DIALOG_MODE_TEXT, MODE_TEXTCOLOR, MODE_BORDERCOLOR, MODE_FILLCOLOR, new Point(0, 0)));
                OverlayPopupTitleColors ov = new OverlayPopupTitleColors(
                    string.Format("Trading with {0}", npc.TheName), Color.White,
                    lines.ToArray(), colors.ToArray(),
                    Color.White, Color.Black, new Point(32, 32));
                AddOverlay(ov);
                RedrawPlayScreen();

                // 2. Handle state
                if (state == 2)  // make offer
                {
                    ClearMessages();
                    Item offered = player.Inventory[iPlayerSelectedItem];
                    Item asked = npc.Inventory[iNpcSelectedItem];
                    AddMessage(MakeMessage(player, string.Format("{0} {1} for {2}.", Conjugate(player, VERB_OFFER), offered.TheName, asked.TheName)));

                    // get rating and apply charisma
                    TradeRating r = ratingPairs[iPlayerSelectedItem, iNpcSelectedItem];
                    if (r == TradeRating.MAYBE)
                        r = (charismaSuccess ? TradeRating.ACCEPT : TradeRating.REFUSE);

                    // npc accept or refuse
                    if (r == TradeRating.ACCEPT)
                    {
                        // accept: make deal and done.
                        AddMessage(MakeMessage(npc, Conjugate(npc, VERB_ACCEPT_THE_DEAL) + "."));
                        SwapActorItems(player, offered, npc, asked);
                        loop = false;
                        actionDone = true;
                        // sanity recover after player trade chat
                        // to be consistent with fast trade, should also recover san a failed trade but this will be
                        // abused by the player as a refused trade doesnt end the turn.
                        if (player.Model.Abilities.HasSanity)
                        {
                            RegenActorSanity(player, Rules.SANITY_RECOVER_CHAT_OR_TRADE);
                            AddMessage(MakeMessage(player, string.Format("{0} better after chatting with", Conjugate(player, VERB_FEEL)), npc));
                        }
                        if (npc.Model.Abilities.HasSanity)
                        {
                            RegenActorSanity(npc, Rules.SANITY_RECOVER_CHAT_OR_TRADE);
                            AddMessage(MakeMessage(npc, string.Format("{0} better after chatting with", Conjugate(npc, VERB_FEEL)), player));
                        }
                    }
                    else if (r == TradeRating.REFUSE)
                    {
                        // refuse: can make another offer.
                        AddMessage(MakeMessage(npc, Conjugate(npc, VERB_REFUSE_THE_DEAL) + "."));
                        isOnPlayerInventory = !isOnPlayerInventory;
                        iPlayerSelectedItem = iNpcSelectedItem = -1;
                        state = 0;
                    }

                    // done
                    AddMessagePressEnter();
                }
                else
                {
                    // Select 1st or 2nd item
                    KeyEventArgs inKey = m_UI.UI_WaitKey();

                    if (inKey.KeyCode == Keys.Escape)  // back/abort
                    {
                        if (state == 0)
                            loop = false;
                        else
                        {
                            state = 0;
                            if (isOnPlayerInventory)
                                iPlayerSelectedItem = -1;
                            else
                                iNpcSelectedItem = -1;
                            isOnPlayerInventory = !isOnPlayerInventory;
                        }
                    }
                    else if (inKey.KeyCode == Keys.Tab)  // switch inventory
                    {
                        if (state == 0)
                        {
                            isOnPlayerInventory = !isOnPlayerInventory;
                            iPlayerSelectedItem = iNpcSelectedItem = -1;
                        }
                    }
                    else
                    {
                        int slot = KeyToChoiceNumber(inKey.KeyCode);
                        if (slot != -1) // select an item
                        {
                            if (slot == 0) slot = 9;
                            else slot--;

                            if (isOnPlayerInventory)
                            {
                                if (slot < player.Inventory.CountItems)
                                {
                                    iPlayerSelectedItem = slot;
                                    if (state == 0)  // offering item 1st
                                    {
                                        state = 1;
                                        isOnPlayerInventory = false;
                                    }
                                    else
                                    {
                                        // offering item 2nd
                                        state = 2;
                                    }
                                }
                            }
                            else
                            {
                                if (slot < npc.Inventory.CountItems)
                                {
                                    iNpcSelectedItem = slot;
                                    if (state == 0)  // asking item 1st
                                    {
                                        state = 1;
                                        isOnPlayerInventory = true;
                                    }
                                    else
                                    {
                                        // asking item 2nd
                                        state = 2;
                                    }
                                }
                            }
                        }
                    }
                }

            }
            while (loop);

            // if trade done, spend player ap.
            if (actionDone)
                SpendActorActionPoints(player, Rules.BASE_ACTION_COST);

            // alpha10.1
            // cleanup
            ClearOverlays();

            return actionDone;
        }

        bool HandlePlayerNegociateTrade(Actor player)
        {
            // handle select adjacent npc
            bool loop = true;
            bool actionDone = false;
            ClearOverlays();
            AddOverlay(new OverlayPopup(NEGOCIATE_TRADE_MODE_TEXT, MODE_TEXTCOLOR, MODE_BORDERCOLOR, MODE_FILLCOLOR, new Point(0, 0)));
            do
            {
                ///////////////////
                // 1. Redraw
                // 2. Get input.
                // 3. Handle input
                ///////////////////

                // 1. Redraw
                RedrawPlayScreen();

                // 2. Get input.
                Direction dir = WaitDirectionOrCancel();

                // 3. Handle input
                if (dir == null)
                {
                    loop = false;
                }
                else if (dir != Direction.NEUTRAL)
                {
                    Point pos = player.Location.Position + dir;
                    if (player.Location.Map.IsInBounds(pos))
                    {
                        Actor other = player.Location.Map.GetActorAt(pos);
                        if (other != null)
                        {
                            string reason;
                            if (m_Rules.CanActorInitiateTradeWith(player, other, out reason))
                            {
                                actionDone = HandlePlayerTradeNegociation(player, other);
                                loop = false;
                            }
                            else
                            {
                                AddMessage(MakeErrorMessage(String.Format("Can't trade with {0} : {1}.", other.TheName, reason)));
                            }
                        }
                        else
                            AddMessage(MakeErrorMessage("Noone there."));
                    }
                }
            }
            while (loop);

            // cleanup.
            ClearOverlays();

            // return if we did an action.
            return actionDone;
        }

    }
}
