using System.Drawing;
using System.Windows.Forms;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine.Actions;
using djack.RogueSurvivor.Gameplay.Personality;
using Message = djack.RogueSurvivor.Data.Message;

namespace djack.RogueSurvivor.Engine
{
    partial class RogueGame
    {
        bool HandlePlayerTalk(Actor player)
        {
            Actor target = null;
            int nearby = 0;
            for (int dx = -1; dx <= 1; dx++) for (int dy = -1; dy <= 1; dy++)
            {
                if (dx == 0 && dy == 0) continue;
                Point tile = new Point(player.Location.Position.X + dx, player.Location.Position.Y + dy);
                if (!player.Location.Map.IsInBounds(tile)) continue;
                Actor candidate = player.Location.Map.GetActorAt(tile);
                if (candidate == null || candidate.IsDead || candidate.IsSleeping ||
                    !candidate.Model.Abilities.IsIntelligent || candidate.Model.Abilities.IsUndead ||
                    m_Rules.AreEnemies(player, candidate)) continue;
                target = candidate; nearby++;
            }
            if (nearby == 0) { AddMessage(MakeErrorMessage("No one willing to talk nearby.")); return false; }
            if (nearby != 1)
            {
                ClearOverlays();
                AddOverlay(new OverlayPopup(new[] { "TALK - choose an adjacent person with a direction, ESC cancels" },
                    MODE_TEXTCOLOR, MODE_BORDERCOLOR, MODE_FILLCOLOR, new Point(0, 0)));
                RedrawPlayScreen();
                Direction dir = WaitDirectionOrCancel();
                ClearOverlays();
                if (dir == null || dir == Direction.NEUTRAL) return false;
                Point pos = player.Location.Position + dir;
                target = player.Location.Map.IsInBounds(pos) ? player.Location.Map.GetActorAt(pos) : null;
            }
            if (target == null || target.IsDead || target.IsSleeping || !target.Model.Abilities.IsIntelligent ||
                target.Model.Abilities.IsUndead || m_Rules.AreEnemies(player, target))
            { AddMessage(MakeErrorMessage("No one willing to talk there.")); return false; }
            NpcReaction pending = NpcConversation.Pending(player, target);
            if (pending != null)
            {
                NpcEventDefinition request = NpcContent.Event(pending.Kind);
                if (request == null || request.PlayerReply == null)
                {
                    player.Personality.Reactions.Remove(pending);
                    pending = null;
                }
            }
            bool? answer = null;
            if (pending != null)
            {
                AddMessage(new Message(target.TheName + (pending.Overheard ? " asked someone for " : " asked you for ") +
                    pending.Text + ". Y: agree, N: decline, ESC: later.",
                    m_Session.WorldTime.TurnCounter));
                RedrawPlayScreen();
                while (true)
                {
                    Keys key = m_UI.UI_WaitKey().KeyCode;
                    if (key == Keys.Y) { answer = true; break; }
                    if (key == Keys.N) { answer = false; break; }
                    if (key == Keys.Escape) return false;
                }
            }
            var action = new ActionPlayerTalk(player, this, target, answer);
            if (!action.IsLegal()) { AddMessage(MakeErrorMessage("You cannot talk to this person now.")); return false; }
            action.Perform();
            return true;
        }
    }
}
