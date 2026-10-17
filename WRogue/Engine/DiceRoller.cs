using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace djack.RogueSurvivor.Engine
{
    [Serializable]
    class DiceRoller
    {
        [ThreadStatic] static DiceRoller s_OverrideTarget;
        internal static bool IsOverridden { get { return s_OverrideTarget != null; } }

        // A simulated district owns its random stream even when another district
        // runs on a different worker. Keep the override scoped to this thread.
        internal static void WithOverride(DiceRoller target, Action action)
        {
            DiceRoller previousTarget = s_OverrideTarget;
            s_OverrideTarget = target;
            try { action(); }
            finally { s_OverrideTarget = previousTarget; }
        }
        internal static Guid NewGuid()
        {
            DiceRoller roller = s_OverrideTarget;
            if (roller == null)
                roller = Session.Get.IdentityDiceRoller;
            if (roller == null) return Guid.NewGuid();
            byte[] bytes = new byte[16];
            for (int part = 0; part < 4; part++)
            {
                uint value = (uint)roller.Roll(Int32.MinValue, Int32.MaxValue);
                for (int offset = 0; offset < 4; offset++) bytes[part * 4 + offset] = (byte)(value >> (offset * 8));
            }
            return new Guid(bytes);
        }
        #region Fields
        uint m_State;
        #endregion

        #region Init
        public DiceRoller(int seed)
        {            
            m_State = (uint)seed;
            if (m_State == 0)
                m_State = 0x9e3779b9;
        }

        /// <summary>
        /// Seed with current time.
        /// </summary>
        public DiceRoller()
            : this((int)DateTime.UtcNow.Ticks)
        {
        }
        #endregion

        #region Rolling
        /// <summary>
        /// Roll in range [min, max[.
        /// </summary>
        /// <param name="min"></param>
        /// <param name="max"></param>
        /// <returns></returns>
        public int Roll(int min, int max)
        {
            // sanity check, fixes crashes.
            if (max <= min) return min;
            if (s_OverrideTarget != null && !ReferenceEquals(this, s_OverrideTarget))
                return s_OverrideTarget.Roll(min, max);

            lock (this)
            {
                ulong range = (ulong)((long)max - min);
                ulong limit = 0x100000000UL - (0x100000000UL % range);
                uint value;
                do { value = NextUInt(); } while ((ulong)value >= limit);
                return (int)(min + (long)((ulong)value % range));
            }
        }

        public float RollFloat()
        {
            if (s_OverrideTarget != null && !ReferenceEquals(this, s_OverrideTarget))
                return s_OverrideTarget.RollFloat();
            lock (this)
            {
                return (NextUInt() >> 8) / 16777216f;
            }
        }

        uint NextUInt()
        {
            uint value = m_State;
            value ^= value << 13;
            value ^= value >> 17;
            value ^= value << 5;
            m_State = value;
            return value;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="chance">chance as a percentage [0..100]</param>
        /// <returns></returns>
        public bool RollChance(int chance)
        {
            return Roll(0, 100) < chance;
        }
        #endregion
    }
}
