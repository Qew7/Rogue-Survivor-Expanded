using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace djack.RogueSurvivor.Engine
{
    sealed class ManualNavigator
    {
        readonly IList<string> m_Lines;
        int m_Line;

        public ManualNavigator(IList<string> lines)
        {
            if (lines == null) throw new ArgumentNullException("lines");
            m_Lines = lines;
        }

        public IList<string> Lines { get { return m_Lines; } }
        public int Line { get { return m_Line; } }

        public void Move(Keys key, int sectionChoice, int pageLines)
        {
            if (pageLines <= 0) throw new ArgumentOutOfRangeException("pageLines");
            if (sectionChoice >= 0)
            {
                if (sectionChoice == 0) m_Line = 0;
                else
                {
                    int section = 0;
                    for (int i = 0; i < m_Lines.Count; i++)
                    {
                        if (m_Lines[i] != "<SECTION>") continue;
                        if (++section != sectionChoice) continue;
                        m_Line = i + 1;
                        break;
                    }
                }
            }
            else
            {
                switch (key)
                {
                    case Keys.Up: --m_Line; break;
                    case Keys.Down: ++m_Line; break;
                    case Keys.PageUp: m_Line -= pageLines; break;
                    case Keys.PageDown: m_Line += pageLines; break;
                }
            }
            m_Line = Math.Max(0, Math.Min(m_Line, Math.Max(0, m_Lines.Count - pageLines)));
        }
    }
}
