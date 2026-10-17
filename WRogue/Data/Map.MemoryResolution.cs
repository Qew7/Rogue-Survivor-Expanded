using System;

namespace djack.RogueSurvivor.Data
{
    partial class Map
    {
        [NonSerialized] int m_aux_NextMemoryResolutionTurn;

        public int NextMemoryResolutionTurn { get { return m_aux_NextMemoryResolutionTurn; } }

        public void ConsiderMemoryResolution(Actor actor)
        {
            if (actor.Personality == null) return;
            foreach (MemoryInstance memory in actor.Personality.Memories)
                m_aux_NextMemoryResolutionTurn = Math.Min(m_aux_NextMemoryResolutionTurn,
                    Math.Min(memory.ResolveTurn, memory.StartTurn + 2 * WorldTime.TURNS_PER_DAY));
        }

        public void ResetMemoryResolutionTurn()
        { m_aux_NextMemoryResolutionTurn = int.MaxValue; }
    }
}
