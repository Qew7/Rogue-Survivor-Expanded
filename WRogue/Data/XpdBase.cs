using System;
using System.Collections.Generic;
using System.Drawing;

namespace djack.RogueSurvivor.Data
{
    [Serializable]
    sealed class XpdBaseLoss
    {
        public Point Position;
        public string Resource, StoryId;
        public int Units;
        public long CauseId;
        public Actor Victim;
    }

    // XPD territory belongs to the claimant's leader and direct followers.
    [Serializable]
    class XpdBase
    {
        readonly Actor m_GroupLeader;
        readonly Faction m_Faction;
        readonly List<Point> m_Cells;
        // Null for the original claim and for saves made before linked levels existed.
        readonly XpdBase m_Root;
        Rectangle? m_FoodRoom;
        Rectangle? m_WeaponRoom;
        [System.Runtime.Serialization.OptionalField] List<XpdBaseLoss> m_UnnoticedLosses;

        public IEnumerable<Point> Cells { get { return m_Cells; } }
        public Actor GroupLeader { get { return m_GroupLeader; } }
        public Faction Faction { get { return m_Faction; } }
        public Rectangle? FoodRoom { get { return m_FoodRoom; } }
        public Rectangle? WeaponRoom { get { return m_WeaponRoom; } }
        public XpdBase Root { get { return m_Root ?? this; } }
        public IList<XpdBaseLoss> UnnoticedLosses { get { return m_UnnoticedLosses; } }

        public XpdBase(Actor claimant, IEnumerable<Point> cells)
            : this(claimant, cells, null)
        {
        }

        public XpdBase(Actor claimant, IEnumerable<Point> cells, XpdBase linkedBase)
        {
            if (claimant == null || claimant.Model.Abilities.IsUndead)
                throw new ArgumentException("A living actor must claim the base");
            m_GroupLeader = claimant.HasLeader ? claimant.Leader : claimant;
            m_Faction = claimant.Faction;
            if (linkedBase != null && !linkedBase.Owns(claimant))
                throw new ArgumentException("Actor does not own linked base");
            m_Root = linkedBase == null ? null : linkedBase.Root;
            m_Cells = new List<Point>(cells);
            if (m_Cells.Count == 0) throw new ArgumentException("Base needs cells");
        }

        public bool Owns(Actor actor)
        {
            if (actor == null || actor.Model.Abilities.IsUndead) return false;
            return m_GroupLeader != null &&
                (actor == m_GroupLeader || actor.Leader == m_GroupLeader);
        }

        public bool Contains(Point point) { return m_Cells.Contains(point); }
        public bool IsPartOf(XpdBase other) { return other != null && Root == other.Root; }

        public bool AddUnnoticedLoss(Point position, string resource, int units, long causeId, string storyId)
        {
            if (m_UnnoticedLosses == null) m_UnnoticedLosses = new List<XpdBaseLoss>();
            foreach (XpdBaseLoss loss in m_UnnoticedLosses)
                if (loss.Position == position && loss.Resource == resource && loss.StoryId == storyId)
                { loss.Units += units; return false; }
            return AppendUnnoticedLoss(new XpdBaseLoss { Position = position, Resource = resource,
                Units = units, CauseId = causeId, StoryId = storyId });
        }

        public bool AddUnnoticedCasualty(Point position, Actor victim, long causeId, string storyId)
        {
            if (m_UnnoticedLosses != null)
                foreach (XpdBaseLoss loss in m_UnnoticedLosses)
                    if (loss.Victim == victim && loss.StoryId == storyId) return false;
            return AppendUnnoticedLoss(new XpdBaseLoss { Position = position, Victim = victim,
                Resource = "casualty", Units = 1, CauseId = causeId, StoryId = storyId });
        }

        bool AppendUnnoticedLoss(XpdBaseLoss loss)
        {
            if (m_UnnoticedLosses == null) m_UnnoticedLosses = new List<XpdBaseLoss>();
            bool increased = m_UnnoticedLosses.Count < 32;
            if (!increased) m_UnnoticedLosses.RemoveAt(0);
            m_UnnoticedLosses.Add(loss);
            return increased;
        }

        public void SetFoodRoom(Rectangle room) { SetRoom(room); m_FoodRoom = room; }
        public void SetWeaponRoom(Rectangle room) { SetRoom(room); m_WeaponRoom = room; }

        void SetRoom(Rectangle room)
        {
            if (room.Width <= 0 || room.Height <= 0) throw new ArgumentException("Empty storage room");
            foreach (Point cell in m_Cells)
                if (room.Contains(cell)) return;
            throw new ArgumentException("Storage room is outside the base");
        }
    }
}
