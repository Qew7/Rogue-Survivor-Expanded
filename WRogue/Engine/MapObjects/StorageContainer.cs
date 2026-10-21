using System;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine.Items;

namespace djack.RogueSurvivor.Engine.MapObjects
{
    [Serializable]
    class StorageContainer : StateMapObject
    {
        public const int CLOSED = 0;
        public const int OPEN = 1;

        readonly Guid m_Id;
        readonly Inventory m_Contents;
        readonly string m_ClosedImage;
        readonly string m_OpenImage;

        public Guid Id { get { return m_Id; } }
        public Inventory Contents { get { return m_Contents; } }
        public bool IsOpen { get { return State == OPEN; } }

        public void CountSupplies(out string resource, out int units)
        {
            resource = null; units = 0;
            foreach (Item item in m_Contents.Items)
            {
                if (item is ItemNote) continue;
                string kind = item is ItemFood ? "food" : item is ItemMedicine ?
                    "medicine" : item.Model.SingleName;
                if (resource == null) resource = kind;
                else if (resource != kind) resource = "mixed";
                units += item.Quantity;
            }
        }

        public int CountClaimedUnits(Guid claimant)
        {
            int units = 0;
            if (claimant == Guid.Empty) return units;
            foreach (Item item in m_Contents.Items)
                if (item.StorageClaimantId == claimant && !(item is ItemNote))
                    units += item.Quantity;
            return units;
        }

        public int CountVisibleUnits(string resource)
        {
            int units = 0;
            foreach (Item item in m_Contents.Items)
            {
                if (item is ItemNote) continue;
                string kind = item is ItemFood ? "food" : item is ItemMedicine ?
                    "medicine" : item.Model.SingleName;
                if (resource == null || resource == "mixed" || resource == kind)
                    units += item.Quantity;
            }
            return units;
        }

        public string ClaimedResource(Guid claimant)
        {
            string resource = null;
            if (claimant == Guid.Empty) return null;
            foreach (Item item in m_Contents.Items)
            {
                if (item.StorageClaimantId != claimant || item is ItemNote) continue;
                string kind = item is ItemFood ? "food" : item is ItemMedicine ?
                    "medicine" : item.Model.SingleName;
                if (resource == null) resource = kind;
                else if (resource != kind) resource = "mixed";
            }
            return resource;
        }

        public StorageContainer(string name, string closedImage, string openImage, int hitPoints, int capacity)
            : base(name, closedImage, Break.BREAKABLE, Fire.UNINFLAMMABLE, hitPoints)
        {
            m_Id = DiceRoller.NewGuid();
            m_Contents = new Inventory(capacity);
            m_ClosedImage = closedImage;
            m_OpenImage = openImage;
            IsContainer = true;
            IsMovable = true;
            SetState(CLOSED);
        }

        public override void SetState(int state)
        {
            if (state != CLOSED && state != OPEN) throw new ArgumentOutOfRangeException("state");
            ImageID = state == OPEN ? m_OpenImage : m_ClosedImage;
            IsWalkable = state == OPEN;
            base.SetState(state);
        }
    }
}
