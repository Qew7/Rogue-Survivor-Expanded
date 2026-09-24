using System.Collections.Generic;

namespace djack.RogueSurvivor.Engine
{
    sealed class OverlayCollection
    {
        readonly List<Overlay> m_Items = new List<Overlay>();

        public void Add(Overlay overlay)
        {
            lock (m_Items) m_Items.Add(overlay);
        }

        public void Remove(Overlay overlay)
        {
            lock (m_Items) m_Items.Remove(overlay);
        }

        public void Clear()
        {
            lock (m_Items) m_Items.Clear();
        }

        public bool Contains(Overlay overlay)
        {
            lock (m_Items) return m_Items.Contains(overlay);
        }

        public void Draw(IRogueUI ui)
        {
            Overlay[] snapshot;
            lock (m_Items) snapshot = m_Items.ToArray();
            foreach (Overlay overlay in snapshot) overlay.Draw(ui);
        }
    }
}
