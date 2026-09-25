using System;
using System.Collections.Generic;
using System.Drawing;

namespace djack.RogueSurvivor.Data
{
    // XPD territory belongs to a leader's group, or to an ordinary NPC faction.
    [Serializable]
    class XpdBase
    {
        readonly Actor m_GroupLeader;
        readonly Faction m_Faction;
        readonly List<Point> m_Cells;
        Rectangle? m_FoodRoom;
        Rectangle? m_WeaponRoom;

        public IEnumerable<Point> Cells { get { return m_Cells; } }
        public Actor GroupLeader { get { return m_GroupLeader; } }
        public Faction Faction { get { return m_Faction; } }
        public Rectangle? FoodRoom { get { return m_FoodRoom; } }
        public Rectangle? WeaponRoom { get { return m_WeaponRoom; } }

        public XpdBase(Actor claimant, IEnumerable<Point> cells)
        {
            if (claimant == null || claimant.Model.Abilities.IsUndead)
                throw new ArgumentException("A living actor must claim the base");
            m_GroupLeader = claimant.HasLeader ? claimant.Leader :
                claimant.IsPlayer || claimant.CountFollowers > 0 ? claimant : null;
            m_Faction = claimant.Faction;
            m_Cells = new List<Point>(cells);
            if (m_Cells.Count == 0) throw new ArgumentException("Base needs cells");
        }

        public bool Owns(Actor actor)
        {
            if (actor == null || actor.Model.Abilities.IsUndead) return false;
            if (m_GroupLeader != null)
                return actor == m_GroupLeader || actor.Leader == m_GroupLeader;
            return actor.Faction == m_Faction && !actor.IsPlayer &&
                !(actor.HasLeader && actor.Leader.IsPlayer);
        }

        public bool Contains(Point point) { return m_Cells.Contains(point); }

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
