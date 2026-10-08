using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;

using djack.RogueSurvivor.Data;

namespace djack.RogueSurvivor.Engine
{
    class MessageManager
    {
        #region Fields
        readonly List<Message> m_Messages = new List<Message>();
        int m_LinesSpacing;
        int m_FadeoutFactor;
        readonly List<Message> m_History;
        int m_HistorySize;
        int m_ScrollOffset;
        #endregion

        #region Properties
        public int Count
        {
            get { return m_Messages.Count; }
        }

        public IEnumerable<Message> History
        {
            get { return m_History; }
        }

        public int ScrollOffset
        {
            get { return m_ScrollOffset; }
        }
        #endregion

        #region Init
        public MessageManager(int linesSpacing, int fadeoutFactor, int historySize)
        {
            if (linesSpacing < 0)
                throw new ArgumentOutOfRangeException("linesSpacing < 0");
            if (fadeoutFactor < 0)
                throw new ArgumentOutOfRangeException("fadeoutFactor < 0");

            m_LinesSpacing = linesSpacing;
            m_FadeoutFactor = fadeoutFactor;
            m_HistorySize = historySize;
            m_History = new List<Message>(historySize);
        }
        #endregion

        #region Managing messages
        public void Clear()
        {
            m_Messages.Clear();
        }

        public void ClearHistory()
        {
            m_History.Clear();
            m_ScrollOffset = 0;
        }

        public void Add(Message msg)
        {
            m_Messages.Add(msg);
            m_History.Add(msg);
            m_ScrollOffset = 0;
            if (m_History.Count > m_HistorySize)
            {
                m_History.RemoveAt(0);
            }
        }

        public void RemoveLastMessage()
        {
            if (m_Messages.Count == 0)
                return;
            m_Messages.RemoveAt(m_Messages.Count - 1);
        }
        #endregion

        #region Drawing
        public List<Message> WrappedHistory(IRogueUI ui, int maxWidth)
        {
            return WrapMessages(m_History, ui, maxWidth, 0);
        }

        public void Scroll(IRogueUI ui, int maxWidth, int maxLines, int lines)
        {
            int maxOffset = Math.Max(0, WrappedHistory(ui, maxWidth).Count - maxLines);
            m_ScrollOffset = Math.Max(0, Math.Min(maxOffset, m_ScrollOffset + lines));
        }

        static List<Message> WrapMessages(IList<Message> messages, IRogueUI ui, int maxWidth, int fadeoutFactor)
        {
            List<Message> lines = new List<Message>();
            for (int i = 0; i < messages.Count; i++)
            {
                Message message = messages[i];
                int alpha = Math.Max(64, 255 - fadeoutFactor * (messages.Count - 1 - i));
                Color color = Color.FromArgb(alpha, message.Color);
                foreach (string line in TextLayout.Wrap(message.Text, maxWidth, ui.UI_BoldTextWidth))
                    lines.Add(new Message(line, message.Turn, color));
            }
            return lines;
        }

        public void Draw(IRogueUI ui, int freshMessagesTurn, int gx, int gy, int maxWidth, int maxLines)
        {
            List<Message> lines = m_ScrollOffset == 0
                ? WrapMessages(m_Messages, ui, maxWidth, m_FadeoutFactor)
                : WrappedHistory(ui, maxWidth);
            int first = Math.Max(0, lines.Count - maxLines - m_ScrollOffset);
            for (int i = first; i < lines.Count - m_ScrollOffset; i++)
            {
                Message msg = lines[i];

                bool isLatest = msg.Turn >= freshMessagesTurn;

                if(isLatest)
                    ui.UI_DrawStringBold(msg.Color, msg.Text, gx, gy);
                else
                    ui.UI_DrawString(msg.Color, msg.Text, gx, gy);

                gy += m_LinesSpacing;
            }
        }
        #endregion
    }
}
