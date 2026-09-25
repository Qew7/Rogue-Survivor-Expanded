using System.Collections.Generic;

namespace djack.RogueSurvivor.Engine
{
    sealed class OverlayCollection
    {
        readonly List<Overlay> m_Items = new List<Overlay>();
        Overlay[] m_Snapshot;

        public void Add(Overlay overlay)
        {
            lock (m_Items) { m_Items.Add(overlay); m_Snapshot = null; }
        }

        public void Remove(Overlay overlay)
        {
            lock (m_Items) { m_Items.Remove(overlay); m_Snapshot = null; }
        }

        public void Clear()
        {
            lock (m_Items) { m_Items.Clear(); m_Snapshot = null; }
        }

        public bool Contains(Overlay overlay)
        {
            lock (m_Items) return m_Items.Contains(overlay);
        }

        public void Draw(IRogueUI ui)
        {
            Overlay[] snapshot;
            lock (m_Items)
            {
                if (m_Snapshot == null) m_Snapshot = m_Items.ToArray();
                snapshot = m_Snapshot;
            }
            foreach (Overlay overlay in snapshot) overlay.Draw(ui);
        }
    }
}
