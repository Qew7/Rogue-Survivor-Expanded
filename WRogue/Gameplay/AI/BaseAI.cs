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
    [Serializable]
    abstract partial class BaseAI : AIController
    {
        #region Types
        protected class ChoiceEval<_T_>
        {
            public _T_ Choice { get; private set; }
            public float Value { get; private set; }

            public ChoiceEval(_T_ choice, float value)
            {
                this.Choice = choice;
                this.Value = value;
            }

            public override string ToString()
            {
                return String.Format("ChoiceEval({0}; {1:F})", (this.Choice == null ? "NULL" : this.Choice.ToString()), this.Value);
            }
        }
        #endregion

        #region Constants
        const int FLEE_THROUGH_EXIT_CHANCE = 90;  // alpha10 increased from 50%

        const int EMOTE_GRAB_ITEM_CHANCE = 30;
        const int EMOTE_FLEE_CHANCE = 30;
        const int EMOTE_FLEE_TRAPPED_CHANCE = 50;
        const int EMOTE_CHARGE_CHANCE = 30;

        const float MOVE_DISTANCE_PENALTY = 0.42f;  // slightly > to diagonal distance (sqrt(2))
        const float MOVE_INTO_TRAPS_PENALTY = 1;  // alpha10

        const int IN_LEADER_LOF_SAFETY_PENALTY = 1;  // alpha10 int
        #endregion

        #region Fields
        ActorOrder m_Order;
        ActorDirective m_Directive;
        Location m_prevLocation;
        List<Item> m_TabooItems;    // list is better than dictionary since we expect it to be very small.
        List<Point> m_TabooTiles;
        List<Actor> m_TabooTrades;
        // alpha10
        [NonSerialized] RouteFinder m_RouteFinder;
        int m_ReservedEquipmentSlots;
        #endregion

        #region Properties
        public override ActorOrder Order
        {
            get { return m_Order; }
        }

        public override ActorDirective Directives
        {
            get
            {
                if (m_Directive == null)
                    m_Directive = new ActorDirective();
                return m_Directive;
            }
            set { m_Directive = value; }
        }

        protected Location PrevLocation
        {
            get { return m_prevLocation; }
        }

        protected List<Item> TabooItems
        {
            get { return m_TabooItems; }
        }

        protected List<Point> TabooTiles
        {
            get { return m_TabooTiles; }
        }

        protected List<Actor> TabooTrades
        {
            get { return m_TabooTrades; }
        }
        #endregion

        #region AIController
        public override void TakeControl(Actor actor)
        {
            base.TakeControl(actor);

            CreateSensors();

            m_TabooItems = null;
            m_TabooTiles = null;
            m_TabooTrades = null;
        }

        public override void SetOrder(ActorOrder newOrder)
        {
            m_Order = newOrder;
        }

        public override ActorAction GetAction(RogueGame game)
        {
            /////////////////////////
            // 1. Update sensors.
            // 2. Issue action.
            /////////////////////////

            // 2. Update sensors.
            List<Percept> percepts = UpdateSensors(game);

            // 3. Issue action.
            if (m_prevLocation.Map == null)
                m_prevLocation = m_Actor.Location;
            m_Actor.TargetActor = null;
            ActorAction bestAction = SelectAction(game, percepts);
            m_prevLocation = m_Actor.Location;
            if (bestAction == null)
            {
                m_Actor.Activity = Activity.IDLE;
                return new ActionWait(m_Actor, game);
            }
            return bestAction;
        }
        #endregion

        #region Strategy followed in GetAction
        protected abstract void CreateSensors();
        protected abstract List<Percept> UpdateSensors(RogueGame game);
        protected abstract ActorAction SelectAction(RogueGame game, List<Percept> percepts);
        #endregion




        #region Taboo items
        protected void MarkItemAsTaboo(Item it)
        {
            if (m_TabooItems == null)
                m_TabooItems = new List<Item>(1);
            else if (m_TabooItems.Contains(it))
                return;
            m_TabooItems.Add(it);
        }

        protected void UnmarkItemAsTaboo(Item it)
        {
            if (m_TabooItems == null)
                return;
            m_TabooItems.Remove(it);
            if (m_TabooItems.Count == 0)
                m_TabooItems = null;
        }

        protected bool IsItemTaboo(Item it)
        {
            if (m_TabooItems == null)
                return false;
            return m_TabooItems.Contains(it);
        }
        #endregion

        #region Taboo tiles
        protected void MarkTileAsTaboo(Point p)
        {
            if (m_TabooTiles == null)
                m_TabooTiles = new List<Point>(1);
            else if (m_TabooTiles.Contains(p))
                return;
            m_TabooTiles.Add(p);
        }

        protected bool IsTileTaboo(Point p)
        {
            if (m_TabooTiles == null)
                return false;
            return m_TabooTiles.Contains(p);
        }

        protected void ClearTabooTiles()
        {
            m_TabooTiles = null;
        }
        #endregion

        #region Taboo trades
        protected void MarkActorAsRecentTrade(Actor other)
        {
            if (m_TabooTrades == null)
                m_TabooTrades = new List<Actor>(1);
            else if (m_TabooTrades.Contains(other))
                return;
            m_TabooTrades.Add(other);
        }

        protected bool IsActorTabooTrade(Actor other)
        {
            if (m_TabooTrades == null) return false;
            return m_TabooTrades.Contains(other);
        }

        protected void ClearTabooTrades()
        {
            m_TabooTrades = null;
        }
        #endregion

        // alpha10 Taboo Equipment slots
        // Simple solution to cases of ai getting stuck in an infinite unequip-equip loop.
        // Typically caused by conflicting behaviors that will "compete" for an equipment slot and will keep doing
        // infinite cycle of equip-unequip, each behavior trying to get "his" item equiped on the same doll part.
        // Current solution is to temporaly reserve a doll part by using taboo doll parts until the behavior is done with it.
        // Eg of cycle in SoldierAI (fixed now with taboo doll part)
        // - BehaviorThrowGrenade wants to equip the grenade, so unequip rifle (free action)
        // - but at next ai tick BehaviorEquipBestItems wants the rifle equiped, so equip rifle (free action)
        // - next ai tick BehaviorThrowGrenade triggers again
        // - etc...
        //
        // It relies on each competing behavior checking and setting taboo slots correctly.
        //
        // There are conceptually probably better solutions but I don't have time.
        // TODO -- could be improved by adding which behavior is reserving which slot and add safety code that
        //         barks when the wrong behavior wants to release or is marking.
        #region Taboo equipment slots

        /// <summary>
        /// A Behavior wants to reserve en equipment slot for use in the next ai ticks.
        /// The same Behavior can keep reserving the same slot over many ticks until it is done.
        /// It must then release the slot by unmarking it.
        /// Must reserve slots ONLY FOR AP FREE actions like Equip and Unequip.
        /// </summary>
        /// <param name="part"></param>
        /// <see cref="UnmarkEquipmentSlotAsTaboo(DollPart)"/>
        protected void MarkEquipmentSlotAsTaboo(DollPart part)
        {
            m_ReservedEquipmentSlots |= (1 << (int)part);
        }

        /// <summary>
        /// A Behavior release an equipment slot for use by other behaviors.
        /// MUST RELEASE an equipment slot before returning an NON-AP FREE action or the lock will persist
        /// for next turn.
        /// </summary>
        /// <param name="part"></param>
        /// <see cref="MarkEquipmentSlotAsTaboo(DollPart)"/>
        protected void UnmarkEquipmentSlotAsTaboo(DollPart part)
        {
            m_ReservedEquipmentSlots &= ~(1 << (int)part);
        }

        /// <summary>
        /// A Behavior checks if an equipment slot is reserved and it should not do anything with it.
        /// </summary>
        /// <param name="part"></param>
        /// <returns></returns>
        /// <see cref="MarkEquipmentSlotAsTaboo(DollPart)"/>
        protected bool IsEquipmentSlotTaboo(DollPart part)
        {
            return (m_ReservedEquipmentSlots & (1 << (int)part)) != 0;
        }
        #endregion
    }
}
