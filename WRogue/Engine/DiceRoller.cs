using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace djack.RogueSurvivor.Engine
{
    [Serializable]
    class DiceRoller
    {
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
