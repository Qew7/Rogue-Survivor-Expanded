using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;

using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine.MapObjects;

namespace djack.RogueSurvivor.Engine.Actions
{
    class ActionGetFromContainer : ActorAction
    {
        #region Fields
        Point m_Position;
        Item m_RequestedItem;
        #endregion

        #region Properties
        /// <summary>
        /// Gets item that will be taken : top item from container position.
        /// </summary>
        public Item Item
        {
            get
            {
                Map map = m_Actor.Location.Map;
                StorageContainer storage = map.GetMapObjectAt(m_Position) as StorageContainer;
                Inventory items = storage == null ? map.GetItemsAt(m_Position) : storage.Contents;
                return m_RequestedItem ?? (items == null ? null : items.TopItem);
            }
        }
        #endregion

        #region Init
        public ActionGetFromContainer(Actor actor, RogueGame game, Point position, Item requestedItem = null)
            : base(actor, game)
        {
            m_Position = position;
            m_RequestedItem = requestedItem;
        }
        #endregion

        #region ActorAction
        public override bool IsLegal()
        {
            if (m_RequestedItem == null)
                return m_Game.Rules.CanActorGetItemFromContainer(m_Actor, m_Position, out m_FailReason);
            StorageContainer storage = m_Actor.Location.Map.GetMapObjectAt(m_Position) as StorageContainer;
            return m_Game.Rules.CanActorGetStorageItem(m_Actor, storage, m_RequestedItem, out m_FailReason);
        }

        public override void Perform()
        {
            if (m_RequestedItem == null) m_Game.DoTakeFromContainer(m_Actor, m_Position);
            else m_Game.DoTakeStorageItem(m_Actor,
                m_Actor.Location.Map.GetMapObjectAt(m_Position) as StorageContainer, m_RequestedItem);
        }
        #endregion
    }
}
