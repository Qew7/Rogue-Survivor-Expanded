using System.Drawing;
using System.Windows.Forms;

namespace djack.RogueSurvivor.Engine
{
    interface IPlayerInputSource
    {
        KeyEventArgs PeekKey();
        Point MousePosition();
        MouseButtons? PeekMouseButtons();
    }

    sealed class UiPlayerInputSource : IPlayerInputSource
    {
        readonly IRogueUI m_UI;
        public UiPlayerInputSource(IRogueUI ui) { m_UI = ui; }
        public KeyEventArgs PeekKey() { return m_UI.UI_PeekKey(); }
        public Point MousePosition() { return m_UI.UI_GetMousePosition(); }
        public MouseButtons? PeekMouseButtons() { return m_UI.UI_PeekMouseButtons(); }
    }

    struct PlayerInputEvent
    {
        public KeyEventArgs Key;
        public Point MousePosition;
        public MouseButtons? MouseButtons;
    }

    sealed class PlayerInputReader
    {
        readonly IPlayerInputSource m_Source;
        public PlayerInputReader(IPlayerInputSource source) { m_Source = source; }

        public PlayerInputEvent Read(KeyEventArgs pendingKey)
        {
            if (pendingKey == null)
                m_Source.PeekKey(); // consume a repeated key after the previous action.
            Point previous = m_Source.MousePosition();
            while (true)
            {
                KeyEventArgs key = pendingKey ?? m_Source.PeekKey();
                pendingKey = null;
                if (key != null)
                    return new PlayerInputEvent { Key = key, MousePosition = new Point(-1, -1) };

                Point position = m_Source.MousePosition();
                MouseButtons? buttons = m_Source.PeekMouseButtons();
                if (position != previous || buttons != null)
                    return new PlayerInputEvent { MousePosition = position, MouseButtons = buttons };
            }
        }
    }
}
