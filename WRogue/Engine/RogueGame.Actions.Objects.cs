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
        #region Breaking stuff
        void DoDestroyObject(MapObject mapObj)
        {
            DoorWindow door = mapObj as DoorWindow;
            bool isWindow = (door != null && door.IsWindow);

            // force HP to zero.
            mapObj.HitPoints = 0;

            // drop plank and improvised weapons?
            if (mapObj.GivesWood)
            {
                // drop planks.
                int nbPlanks = 1 + mapObj.MaxHitPoints / DoorWindow.BASE_HITPOINTS;
                while (nbPlanks > 0)
                {
                    Item planks = new ItemBarricadeMaterial(m_GameItems.WOODENPLANK)
                    {
                        Quantity = Math.Min(m_GameItems.WOODENPLANK.StackingLimit, nbPlanks)
                    };
                    if (planks.Quantity < 1) planks.Quantity = 1;
                    mapObj.Location.Map.DropItemAt(planks, mapObj.Location.Position);
                    nbPlanks -= planks.Quantity;
                }

                // drop improvised weapons?
                if (m_Rules.RollChance(Rules.IMPROVED_WEAPONS_FROM_BROKEN_WOOD_CHANCE))
                {
                    // improvised club, improvised spear.
                    ItemMeleeWeapon impWpn;
                    if (m_Rules.RollChance(50))
                        impWpn = new ItemMeleeWeapon(m_GameItems.IMPROVISED_CLUB);
                    else
                        impWpn = new ItemMeleeWeapon(m_GameItems.IMPROVISED_SPEAR);

                    // drop it.
                    mapObj.Location.Map.DropItemAt(impWpn, mapObj.Location.Position);
                }
            }

            // remove object - but not windows.
            if (isWindow)
            {
                door.SetState(DoorWindow.STATE_BROKEN);
            }
            else
                mapObj.Location.Map.RemoveMapObjectAt(mapObj.Location.Position.X, mapObj.Location.Position.Y);

            // loud noise.
            OnLoudNoise(mapObj.Location.Map, mapObj.Location.Position, "A loud *CRASH*");
        }

        public void DoBreak(Actor actor, MapObject mapObj)
        {
            Attack bashAttack = m_Rules.ActorMeleeAttack(actor, actor.CurrentMeleeAttack, null, mapObj);


            #region Attacking a barricaded door.
            DoorWindow door = mapObj as DoorWindow;
            if (door != null && door.IsBarricaded)
            {
                // Spend APs & STA.
                int bashCost = Rules.BASE_ACTION_COST;
                SpendActorActionPoints(actor, bashCost);
                SpendActorStaminaPoints(actor, Rules.STAMINA_COST_MELEE_ATTACK);

                // Bash.
                door.BarricadePoints -= bashAttack.DamageValue;

                // loud noise.
                OnLoudNoise(door.Location.Map, door.Location.Position, "A loud *BASH*");

                // message.
                if (IsVisibleToPlayer(actor) || IsVisibleToPlayer(door))
                {
                    if (IsVisibleToPlayer(door))
                    {
                        // alpha10 tell & show damage
                        Point screenPos = MapToScreen(mapObj.Location.Position);
                        AddOverlay(new OverlayImage(screenPos, GameImages.ICON_MELEE_DAMAGE));
                        AddOverlay(new OverlayText(screenPos.Add(DAMAGE_DX, DAMAGE_DY), Color.White, bashAttack.DamageValue.ToString(), Color.Black)); // alpha10
                        AddMessage(MakeMessage(actor, string.Format("{0} the barricade for {1} damage.", Conjugate(actor, VERB_BASH), bashAttack.DamageValue))); // alpha10
                        RedrawPlayScreen();
                        AnimDelay(actor.IsPlayer ? DELAY_NORMAL : DELAY_SHORT);
                        ClearOverlays();
                    }
                    else
                    {
                        AddMessage(MakeMessage(actor, string.Format("{0} the barricade.", Conjugate(actor, VERB_BASH)))); // alpha10
                    }
                }
                else
                {
                    if (m_Rules.RollChance(PLAYER_HEAR_BASH_CHANCE))
                        AddMessageIfAudibleForPlayer(door.Location, MakePlayerCentricMessage("You hear someone bashing barricades", door.Location.Position));

                }


                // done.
                return;
            }
            #endregion

            #region Attacking a un-barricaded door or a normal object
            else
            {
                // Always hit.
                mapObj.HitPoints -= bashAttack.DamageValue;

                // Spend APs & STA.
                int bashCost = Rules.BASE_ACTION_COST;
                SpendActorActionPoints(actor, bashCost);
                SpendActorStaminaPoints(actor, Rules.STAMINA_COST_MELEE_ATTACK);

                // Broken?
                bool isBroken = false;
                if (mapObj.HitPoints <= 0)
                {
                    // breaks.
                    DoDestroyObject(mapObj);
                    isBroken = true;
                }

                // loud noise.
                OnLoudNoise(mapObj.Location.Map, mapObj.Location.Position, "A loud *CRASH*");

                // Message.
                bool isActorVisible = IsVisibleToPlayer(actor);
                bool isDoorVisible = IsVisibleToPlayer(mapObj);
                bool isPlayer = actor.IsPlayer;

                if (isActorVisible || isDoorVisible)
                {
                    if (isActorVisible)
                        AddOverlay(new OverlayRect(Color.Yellow, new Rectangle(MapToScreen(actor.Location.Position), new Size(TILE_SIZE, TILE_SIZE))));
                    if (isDoorVisible)
                        AddOverlay(new OverlayRect(Color.Red, new Rectangle(MapToScreen(mapObj.Location.Position), new Size(TILE_SIZE, TILE_SIZE))));

                    if (isBroken)
                    {
                        AddMessage(MakeMessage(actor, Conjugate(actor, VERB_BREAK), mapObj));
                        if (isActorVisible)
                            AddOverlay(new OverlayImage(MapToScreen(actor.Location.Position), GameImages.ICON_MELEE_ATTACK));
                        if (isDoorVisible)
                            AddOverlay(new OverlayImage(MapToScreen(mapObj.Location.Position), GameImages.ICON_KILLED));
                        RedrawPlayScreen();
                        AnimDelay(DELAY_LONG);
                    }
                    else
                    {
                        if (isDoorVisible)
                        {
                            AddMessage(MakeMessage(actor, string.Format("{0} {1} for {2} damage.", Conjugate(actor, VERB_BASH), mapObj.TheName, bashAttack.DamageValue))); // alpha10
                            AddOverlay(new OverlayImage(MapToScreen(mapObj.Location.Position), GameImages.ICON_MELEE_DAMAGE));
                            AddOverlay(new OverlayText(MapToScreen(mapObj.Location.Position).Add(DAMAGE_DX, DAMAGE_DY), Color.White, bashAttack.DamageValue.ToString(), Color.Black)); // alpha10
                        }
                        else if (isActorVisible)
                        {
                            AddMessage(MakeMessage(actor, string.Format("{0} {1}.", Conjugate(actor, VERB_BASH), mapObj.TheName))); // alpha10
                        }

                        if (isActorVisible)
                            AddOverlay(new OverlayImage(MapToScreen(actor.Location.Position), GameImages.ICON_MELEE_ATTACK));

                        RedrawPlayScreen();
                        AnimDelay(isPlayer ? DELAY_NORMAL : DELAY_SHORT);
                    }

                    // alpha10 bug fix; clear overlays only if action is visible
                    ClearOverlays(); // was in the wrong place!
                }  // any is visible
                else
                {
                    if (isBroken)
                    {
                        if (m_Rules.RollChance(PLAYER_HEAR_BREAK_CHANCE))
                            AddMessageIfAudibleForPlayer(mapObj.Location, MakePlayerCentricMessage("You hear someone breaking furniture", mapObj.Location.Position));
                    }
                    else
                    {
                        if (m_Rules.RollChance(PLAYER_HEAR_BASH_CHANCE))
                            AddMessageIfAudibleForPlayer(mapObj.Location, MakePlayerCentricMessage("You hear someone bashing furniture", mapObj.Location.Position));
                    }
                }
            }
            #endregion
        }
        #endregion
        #region Pushing/Pulling & Shoving
        // alpha10
        void DoPushPullFollowersHelp(Actor actor, MapObject mapObj, bool isPulling, ref int staCost)
        {
            bool isVisibleMobj = IsVisibleToPlayer(mapObj);

            Location objLoc = new Location(actor.Location.Map, mapObj.Location.Position);
            List<Actor> helpers = null;
            foreach (Actor fo in actor.Followers)
            {
                // follower can help if: not sleeping, idle and adj to map object.
                if (!fo.IsSleeping && (fo.Activity == Activity.IDLE || fo.Activity == Activity.FOLLOWING) && m_Rules.IsAdjacent(fo.Location, mapObj.Location))
                {
                    if (helpers == null) helpers = new List<Actor>(actor.CountFollowers);
                    helpers.Add(fo);
                }
            }
            if (helpers != null)
            {
                // share the sta cost.
                staCost = mapObj.Weight / (1 + helpers.Count);
                foreach (Actor h in helpers)
                {
                    // spend fo AP & STA.
                    SpendActorActionPoints(h, Rules.BASE_ACTION_COST);
                    SpendActorStaminaPoints(h, staCost);
                    // message.
                    if (isVisibleMobj || IsVisibleToPlayer(h))
                        AddMessage(MakeMessage(h, String.Format("{0} {1} {2} {3}.", Conjugate(h, VERB_HELP), actor.Name, (isPulling ? "pulling" : "pushing"), mapObj.TheName)));
                }
            }
        }

        public void DoPush(Actor actor, MapObject mapObj, Point toPos)
        {
            bool isVisible = IsVisibleToPlayer(actor) || IsVisibleToPlayer(mapObj);
            int staCost = mapObj.Weight;

            // followers help?
            if (actor.CountFollowers > 0)
                DoPushPullFollowersHelp(actor, mapObj, false, ref staCost); // alpha10

            // spend AP & STA.
            SpendActorActionPoints(actor, Rules.BASE_ACTION_COST);
            SpendActorStaminaPoints(actor, staCost);

            // do it : move object, then move actor if he is pushing it away and can enter the tile.
            Map map = mapObj.Location.Map;
            Point prevObjPos = mapObj.Location.Position;
            map.RemoveMapObjectAt(mapObj.Location.Position.X, mapObj.Location.Position.Y);
            map.PlaceMapObjectAt(mapObj, toPos);
            if (!m_Rules.IsAdjacent(toPos, actor.Location.Position) && m_Rules.IsWalkableFor(actor, map, prevObjPos.X, prevObjPos.Y))
            {
                // pushing away, need to follow.
                if (TryActorLeaveTile(actor))  // alpha10
                {
                    map.RemoveActor(actor);
                    map.PlaceActorAt(actor, prevObjPos);
                    OnActorEnterTile(actor);  // alpha10
                }
            }

            // noise/message.
            if (isVisible)
            {
                AddMessage(MakeMessage(actor, Conjugate(actor, VERB_PUSH), mapObj));
                RedrawPlayScreen();
            }
            else
            {
                // loud noise.
                OnLoudNoise(map, toPos, "Something being pushed");

                // player hears?
                if (m_Rules.RollChance(PLAYER_HEAR_PUSHPULL_CHANCE))
                {
                    AddMessageIfAudibleForPlayer(mapObj.Location, MakePlayerCentricMessage("You hear something being pushed", toPos));
                }
            }

            // check traps.
            CheckMapObjectTriggersTraps(map, toPos);
        }

        public void DoShove(Actor actor, Actor target, Point toPos)
        {
            // Target try to leave tile.
            if (!TryActorLeaveTile(target))
            {
                // waste ap.
                SpendActorActionPoints(actor, Rules.BASE_ACTION_COST);
                return;
            }

            // spend AP & STA.
            SpendActorActionPoints(actor, Rules.BASE_ACTION_COST);
            SpendActorStaminaPoints(actor, Rules.DEFAULT_ACTOR_WEIGHT);

            // force target to stop dragging corpses.
            DoStopDraggingCorpses(target);

            // do it : move target, then move actor if he is pushing it away and can enter the tile.
            Map map = target.Location.Map;
            Point prevTargetPos = target.Location.Position;
            map.PlaceActorAt(target, toPos);
            if (!m_Rules.IsAdjacent(toPos, actor.Location.Position) && m_Rules.IsWalkableFor(actor, map, prevTargetPos.X, prevTargetPos.Y))
            {
                // shoving away, need to follow.
                // Try to leave tile.
                if (TryActorLeaveTile(actor))  // alpha10
                {
                    map.RemoveActor(actor);
                    map.PlaceActorAt(actor, prevTargetPos);
                    // Trigger stuff.
                    OnActorEnterTile(actor);
                }
            }

            // message.
            bool isVisible = IsVisibleToPlayer(actor) || IsVisibleToPlayer(target) || IsVisibleToPlayer(map, toPos);
            if (isVisible)
            {
                AddMessage(MakeMessage(actor, Conjugate(actor, VERB_SHOVE), target));
                RedrawPlayScreen();
            }

            // if target is sleeping, wakes him up!
            if (target.IsSleeping)
                DoWakeUp(target);

            // Trigger stuff.
            OnActorEnterTile(target);
        }

        // alpha10
        public void DoPull(Actor actor, MapObject mapObj, Point moveActorToPos)
        {
            bool isVisible = IsVisibleToPlayer(actor) || IsVisibleToPlayer(mapObj);
            int staCost = mapObj.Weight;

            // try leaving tile
            if (!TryActorLeaveTile(actor))
            {
                // waste ap.
                SpendActorActionPoints(actor, Rules.BASE_ACTION_COST);
                return;
            }

            // followers help?
            if (actor.CountFollowers > 0)
                DoPushPullFollowersHelp(actor, mapObj, true, ref staCost);

            // spend AP & STA.
            SpendActorActionPoints(actor, Rules.BASE_ACTION_COST);
            SpendActorStaminaPoints(actor, staCost);

            // do it : move actor then move object
            Map map = mapObj.Location.Map;
            // actor...
            Point pullObjectTo = actor.Location.Position;
            map.RemoveActor(actor);
            map.PlaceActorAt(actor, moveActorToPos);  // assumed to be walkable, checked by rules
            // ...object
            map.RemoveMapObjectAt(mapObj.Location.Position.X, mapObj.Location.Position.Y);
            map.PlaceMapObjectAt(mapObj, pullObjectTo);

            // noise/message.
            if (isVisible)
            {
                AddMessage(MakeMessage(actor, Conjugate(actor, VERB_PULL), mapObj));
                RedrawPlayScreen();
            }
            else
            {
                // loud noise.
                OnLoudNoise(map, mapObj.Location.Position, "Something being pushed");

                // player hears?
                if (m_Rules.RollChance(PLAYER_HEAR_PUSHPULL_CHANCE))
                {
                    AddMessageIfAudibleForPlayer(mapObj.Location, MakePlayerCentricMessage("You hear something being pushed", mapObj.Location.Position));
                }
            }

            // check triggers
            OnActorEnterTile(actor);
            CheckMapObjectTriggersTraps(map, mapObj.Location.Position);
        }

        // alpha10
        public void DoPullActor(Actor actor, Actor target, Point moveActorToPos)
        {
            bool isVisible = IsVisibleToPlayer(actor) || IsVisibleToPlayer(target);

            // try leaving tile, both actors and target
            if (!TryActorLeaveTile(actor))
            {
                // waste ap.
                SpendActorActionPoints(actor, Rules.BASE_ACTION_COST);
                return;
            }
            if (!TryActorLeaveTile(target))
            {
                // waste ap.
                SpendActorActionPoints(actor, Rules.BASE_ACTION_COST);
                return;
            }

            // spend AP & STA.
            SpendActorActionPoints(actor, Rules.BASE_ACTION_COST);
            SpendActorStaminaPoints(actor, Rules.DEFAULT_ACTOR_WEIGHT);

            // force target to stop dragging corpses.
            DoStopDraggingCorpses(target);

            // do it : move actor then move target
            Map map = target.Location.Map;
            // move actor...
            Point pullTargetTo = actor.Location.Position;
            map.RemoveActor(actor);
            map.PlaceActorAt(actor, moveActorToPos);
            // ...move target
            map.RemoveActor(target);
            map.PlaceActorAt(target, pullTargetTo);

            // if target is sleeping, wakes him up!
            if (target.IsSleeping)
                DoWakeUp(target);

            // message
            if (isVisible)
            {
                AddMessage(MakeMessage(actor, Conjugate(actor, VERB_PULL), target));
                RedrawPlayScreen();
            }

            // Trigger stuff.
            OnActorEnterTile(actor);
            OnActorEnterTile(target);
        }
        #endregion
        #region Sleeping & Waking up
        public void DoStartSleeping(Actor actor)
        {
            // spend AP.
            SpendActorActionPoints(actor, Rules.BASE_ACTION_COST);

            // force actor to stop dragging corpses.
            DoStopDraggingCorpses(actor);

            // set activity & state.
            actor.Activity = Activity.SLEEPING;
            actor.IsSleeping = true;
        }

        public void DoWakeUp(Actor actor)
        {
            // set activity & state.
            actor.Activity = Activity.IDLE;
            actor.IsSleeping = false;

            // message.
            if (IsVisibleToPlayer(actor))
            {
                AddMessage(MakeMessage(actor, String.Format("{0}.", Conjugate(actor, VERB_WAKE_UP))));
            }

            // stop sleep music if player.
            if (actor.IsPlayer && m_MusicManager.Music == GameMusics.SLEEP)
                m_MusicManager.Stop();
        }
        #endregion
        #region Tagging
        void DoTag(Actor actor, ItemSprayPaint spray, Point pos)
        {
            // spend AP.
            SpendActorActionPoints(actor, Rules.BASE_ACTION_COST);

            // spend paint.
            --spray.PaintQuantity;

            // add tag decoration.
            Map map = actor.Location.Map;
            map.GetTileAt(pos.X, pos.Y).AddDecoration((spray.Model as ItemSprayPaintModel).TagImageID);

            // message.
            if (IsVisibleToPlayer(actor))
            {
                AddMessage(MakeMessage(actor, String.Format("{0} a tag.", Conjugate(actor, VERB_SPRAY))));
            }
        }
        #endregion
        // alpha10 new way to use spray scent
        #region Spray scent
        public void DoSprayOdorSuppressor(Actor actor, ItemSprayScent suppressor, Actor sprayOn)
        {
            // spend AP.
            SpendActorActionPoints(actor, Rules.BASE_ACTION_COST);

            // spend spray.
            --suppressor.SprayQuantity;

            // add odor suppressor on spray target
            sprayOn.OdorSuppressorCounter += suppressor.Strength;

            // message.
            if (IsVisibleToPlayer(actor))
            {
                AddMessage(MakeMessage(actor, string.Format("{0} {1}.", Conjugate(actor, VERB_SPRAY),
                    (sprayOn == actor ? HimselfOrHerself(actor) : sprayOn.Name))));
            }
        }
        #endregion
        #region Ordering
        void DoGiveOrderTo(Actor master, Actor slave, ActorOrder order)
        {
            // master spend AP.
            SpendActorActionPoints(master, Rules.BASE_ACTION_COST);

            // refuse if :
            // - master is not slave leader.
            // - slave is not trusting leader.
            if (master != slave.Leader)
            {
                DoSay(slave, master, "Who are you to give me orders?", Sayflags.IS_FREE_ACTION);
                return;
            }
            if (!m_Rules.IsActorTrustingLeader(slave))
            {
                DoSay(slave, master, "Sorry, I don't trust you enough yet.", Sayflags.IS_FREE_ACTION | Sayflags.IS_IMPORTANT);
                return;
            }

            // get AI.
            AIController ai = slave.Controller as AIController;
            if (ai == null)
                return;

            // give order.
            ai.SetOrder(order);

            // message.
            if (IsVisibleToPlayer(master) || IsVisibleToPlayer(slave))
            {
                AddMessage(MakeMessage(master, Conjugate(master, VERB_ORDER), slave, String.Format(" to {0}.", order.ToString())));
            }
        }

        void DoCancelOrder(Actor master, Actor slave)
        {
            // master spend AP.
            SpendActorActionPoints(master, Rules.BASE_ACTION_COST);

            // get AI.
            AIController ai = slave.Controller as AIController;
            if (ai == null)
                return;

            // cancel order.
            ai.SetOrder(null);

            // message.
            if (IsVisibleToPlayer(master) || IsVisibleToPlayer(slave))
            {
                AddMessage(MakeMessage(master, Conjugate(master, VERB_ORDER), slave, " to forget its orders."));
            }
        }
        #endregion
    }
}
