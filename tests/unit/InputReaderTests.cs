using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using djack.RogueSurvivor.Engine;

static class InputReaderTests
{
    sealed class FakeSource : IPlayerInputSource
    {
        public readonly Queue<KeyEventArgs> Keys = new Queue<KeyEventArgs>();
        public readonly Queue<Point> Positions = new Queue<Point>();
        public readonly Queue<MouseButtons?> Buttons = new Queue<MouseButtons?>();
        public int KeyReads;
        public KeyEventArgs PeekKey()
        {
            KeyReads++;
            return Keys.Count == 0 ? null : Keys.Dequeue();
        }
        public Point MousePosition() { return Positions.Dequeue(); }
        public MouseButtons? PeekMouseButtons() { return Buttons.Count == 0 ? null : Buttons.Dequeue(); }
    }

    public static void Run()
    {
        FakeSource pending = new FakeSource();
        pending.Positions.Enqueue(new Point(4, 4));
        PlayerInputEvent key = new PlayerInputReader(pending).Read(new KeyEventArgs(Keys.M));
        Check.Equal(Keys.M, key.Key.KeyCode, "pending key delivered");
        Check.Equal(new Point(-1, -1), key.MousePosition, "key has no mouse target");
        Check.Equal(0, pending.KeyReads, "pending key is not discarded");

        FakeSource move = new FakeSource();
        move.Positions.Enqueue(new Point(4, 4));
        move.Positions.Enqueue(new Point(5, 4));
        PlayerInputEvent hover = new PlayerInputReader(move).Read(null);
        Check.Equal(new Point(5, 4), hover.MousePosition, "mouse hover delivered");
        Check.Equal(null, hover.Key, "hover has no key");

        FakeSource click = new FakeSource();
        click.Positions.Enqueue(new Point(4, 4));
        click.Positions.Enqueue(new Point(4, 4));
        click.Buttons.Enqueue(MouseButtons.Left);
        PlayerInputEvent pressed = new PlayerInputReader(click).Read(null);
        Check.Equal(MouseButtons.Left, pressed.MouseButtons, "click delivered without movement");

        FakeSource repeated = new FakeSource();
        repeated.Keys.Enqueue(new KeyEventArgs(Keys.M));
        repeated.Keys.Enqueue(new KeyEventArgs(Keys.NumPad6));
        repeated.Positions.Enqueue(new Point(4, 4));
        PlayerInputEvent next = new PlayerInputReader(repeated).Read(null);
        Check.Equal(Keys.NumPad6, next.Key.KeyCode,
            "modal input drops one repeated key and accepts the next one");
    }
}
