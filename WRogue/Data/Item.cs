using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace djack.RogueSurvivor.Data
{
    [Serializable]
    class Item
    {
        #region Fields
        int  m_ModelID;
        int  m_Quantity;
        DollPart m_EquipedPart;
        // Lets an actor reclaim an item it just left on somebody else's base.
        Actor m_LastDroppedBy;
        Guid m_StoryIdentity;
        public Guid StoryIdentity { get { if (m_StoryIdentity == Guid.Empty) m_StoryIdentity = Guid.NewGuid(); return m_StoryIdentity; } }
        [System.Runtime.Serialization.OptionalField] Guid m_StolenFromGroupId;
        [System.Runtime.Serialization.OptionalField] Guid m_StolenFromLeaderId;
        [System.Runtime.Serialization.OptionalField] string m_StolenFromName;
        [System.Runtime.Serialization.OptionalField] string m_TheftStoryId;
        [System.Runtime.Serialization.OptionalField] long m_TheftEventId;
        #endregion

        #region Properties
        public ItemModel Model
        {
            get { return Models.Items[m_ModelID]; }
        }

        public virtual string ImageID
        {
            get { return this.Model.ImageID; }
        }

        public string TheName
        {
            get
            {
                ItemModel model = this.Model;
                if (model.IsProper)
                    return model.SingleName;
                if (m_Quantity > 1 || model.IsPlural)
                    return "some " + model.PluralName;
                else
                    return "the " + model.SingleName;
            }
        }

        public string AName
        {
            get
            {
                ItemModel model = this.Model;
                if (model.IsProper)
                    return model.SingleName;
                if (m_Quantity > 1 || model.IsPlural)
                    return "some " + model.PluralName;
                else if (model.IsAn)
                    return "an " + model.SingleName;
                else
                    return "a " + model.SingleName;
            }
        }

        public int  Quantity
        {
            get { return m_Quantity; }
            set
            {
                m_Quantity = value;
                if (m_Quantity < 0) m_Quantity = 0;
            }
        }

        public bool CanStackMore
        {
            get
            {
                ItemModel myModel = this.Model;
                return myModel.IsStackable && m_Quantity < myModel.StackingLimit;
            }
        }

        public DollPart EquippedPart
        {
            get { return m_EquipedPart; }
            set { m_EquipedPart = value; }
        }

        public bool IsEquipped
        {
            get { return m_EquipedPart != DollPart.NONE; }
        }

        public bool IsUnique
        {
            get;
            set;
        }

        public bool IsForbiddenToAI
        {
            get;
            set;
        }
        public Actor LastDroppedBy { get { return m_LastDroppedBy; } set { m_LastDroppedBy = value; } }
        public bool IsStolen { get { return m_StolenFromLeaderId != Guid.Empty; } }
        public Guid StolenFromGroupId { get { return m_StolenFromGroupId; } }
        public Guid StolenFromLeaderId { get { return m_StolenFromLeaderId; } }
        public string StolenFromName { get { return m_StolenFromName; } }
        public string TheftStoryId { get { return m_TheftStoryId; } }
        public long TheftEventId { get { return m_TheftEventId; } }
        public void MarkStolen(Guid groupId, Guid leaderId, string leaderName, string storyId, long eventId)
        {
            if (IsStolen) return;
            m_StolenFromGroupId = groupId; m_StolenFromLeaderId = leaderId;
            m_StolenFromName = leaderName; m_TheftStoryId = storyId; m_TheftEventId = eventId;
        }
        public void ClearTheft()
        {
            m_StolenFromGroupId = Guid.Empty; m_StolenFromLeaderId = Guid.Empty;
            m_StolenFromName = null; m_TheftStoryId = null; m_TheftEventId = 0;
        }
        public void RememberTheftEvent(long eventId) { if (IsStolen && m_TheftEventId == 0) m_TheftEventId = eventId; }
        public void CopyTheftFrom(Item source)
        {
            m_StolenFromGroupId = source.m_StolenFromGroupId; m_StolenFromLeaderId = source.m_StolenFromLeaderId;
            m_StolenFromName = source.m_StolenFromName; m_TheftStoryId = source.m_TheftStoryId;
            m_TheftEventId = source.m_TheftEventId;
        }
        public bool SameTheftAs(Item other)
        {
            return m_StolenFromLeaderId == other.m_StolenFromLeaderId &&
                m_StolenFromGroupId == other.m_StolenFromGroupId && m_TheftStoryId == other.m_TheftStoryId &&
                m_TheftEventId == other.m_TheftEventId;
        }
        #endregion

        #region Init
        public Item(ItemModel model)
        {
            m_ModelID = model.ID;
            m_Quantity = 1;
            m_EquipedPart = DollPart.NONE;
        }
        #endregion

        #region Pre-save
        public virtual void OptimizeBeforeSaving()
        {
            if (m_LastDroppedBy != null && m_LastDroppedBy.IsDead)
                m_LastDroppedBy = null;
        }
        #endregion
    }
}
