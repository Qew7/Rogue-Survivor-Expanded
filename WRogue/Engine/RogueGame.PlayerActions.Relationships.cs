using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using djack.RogueSurvivor.Data;

namespace djack.RogueSurvivor.Engine
{
    partial class RogueGame
    {
        static string FeelingLabel(int feeling)
        {
            if (feeling >= 25) return "friendly";
            if (feeling > 0) return "warm";
            if (feeling <= -25) return "hostile";
            if (feeling < 0) return "wary";
            return "neutral";
        }

        IList<string> RelationshipLines(Actor player)
        {
            List<string> lines = new List<string>();
            PersonalityState state = player == null ? null : player.Personality;
            if (state == null)
            {
                lines.Add("No relationships recorded yet.");
                return lines;
            }
            AppendRelationships(lines, "People", state.People, "");
            AppendRelationships(lines, "Groups", state.Groups, "'s group");
            AppendRelationships(lines, "Factions", state.Factions, "");
            if (lines.Count == 0) lines.Add("No relationships recorded yet.");
            return lines;
        }

        static void AppendRelationships(List<string> lines, string heading,
            ICollection<RelationshipRecord> records, string suffix)
        {
            if (records.Count == 0) return;
            List<RelationshipRecord> sorted = new List<RelationshipRecord>(records);
            sorted.Sort((a, b) => String.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
            if (lines.Count > 0) lines.Add("");
            lines.Add(heading);
            foreach (RelationshipRecord record in sorted)
                lines.Add(String.Format("  {0}{1}: {2}", record.Name, suffix,
                    FeelingLabel(record.Feeling)));
        }

        void HandleRelationships()
        {
            IList<string> lines = RelationshipLines(m_Player);
            const int linesPerPage = 45;
            int page = 0;
            bool done = false;
            while (!done)
            {
                m_UI.UI_Clear(Color.Black);
                DrawHeader();
                int y = BOLD_LINE_SPACING;
                m_UI.UI_DrawStringBold(Color.Yellow, "Your relationships", 0, y);
                y += 2 * BOLD_LINE_SPACING;
                for (int i = page * linesPerPage; i < lines.Count && i < (page + 1) * linesPerPage; i++)
                {
                    m_UI.UI_DrawStringBold(Color.White, lines[i], 0, y);
                    y += BOLD_LINE_SPACING;
                }
                DrawFootnote(Color.White, "PgUp/PgDn to move, ESC to leave");
                m_UI.UI_Repaint();
                Keys key = m_UI.UI_WaitKey().KeyCode;
                if (key == Keys.Escape) done = true;
                else if (key == Keys.PageDown && (page + 1) * linesPerPage < lines.Count) page++;
                else if (key == Keys.PageUp && page > 0) page--;
            }
        }
    }
}
