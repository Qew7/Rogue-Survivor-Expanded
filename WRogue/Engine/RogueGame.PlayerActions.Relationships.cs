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
                lines.Add(String.Format("  {0}{1}: {2}{3}", record.Name, suffix,
                    FeelingLabel(record.Feeling), record.Trust > 0 || record.Fear > 0 || record.Grievance > 0 ?
                    String.Format(" (trust {0}, fear {1}, grievance {2})", record.Trust, record.Fear, record.Grievance) : ""));
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

        IList<string> HeardJournalLines(Actor player)
        {
            List<string> lines = new List<string>();
            if (player != null && player.Personality != null)
                for (int i = player.Personality.HeardJournal.Count - 1; i >= 0; i--)
                {
                    HeardJournalEntry heard = player.Personality.HeardJournal[i];
                    if (heard.Kind == "story_note")
                    {
                        AppendJournalLine(lines, String.Format("Turn {0} | Story: {1}", heard.Turn, heard.Text));
                        continue;
                    }
                    AppendJournalLine(lines, String.Format("Turn {0} | {1} | {2}: \"{3}\"", heard.Turn,
                        heard.Kind == "heard_rumor" ? "Rumor" : heard.Kind == "heard_request" ? "Request" : "Reply",
                        heard.Speaker, heard.Text));
                }
            if (lines.Count == 0) lines.Add("You have no story notes, rumors or requests yet.");
            return lines;
        }

        static void AppendJournalLine(List<string> lines, string line)
        {
            const int width = 100;
            while (line.Length > width)
            {
                int split = line.LastIndexOf(' ', width);
                int building = line.LastIndexOf(" at the ", StringComparison.Ordinal);
                if (building >= 20 && split > building && line.Length - building < width - 4)
                    split = building;
                if (split < 20) split = width;
                lines.Add(line.Substring(0, split));
                line = "    " + line.Substring(split).TrimStart();
            }
            lines.Add(line);
        }

        void HandleHeardJournal()
        {
            IList<string> lines = HeardJournalLines(m_Player);
            var placeColors = new RecordsTextColors(new ResidentRecord[0],
                ResidentRecords.DistrictKindsFrom(m_Session.World));
            const int linesPerPage = 35;
            int page = 0;
            while (true)
            {
                m_UI.UI_Clear(Color.Black);
                DrawHeader();
                int y = BOLD_LINE_SPACING;
                m_UI.UI_DrawStringBold(Color.Yellow, "What you heard", 0, y);
                y += 2 * BOLD_LINE_SPACING;
                for (int i = page * linesPerPage; i < lines.Count && i < (page + 1) * linesPerPage; i++)
                {
                    DrawRecordsText(lines[i], 0, y, Color.White, placeColors);
                    y += BOLD_LINE_SPACING;
                }
                DrawFootnote(Color.White, "PgUp/PgDn to move, ESC to leave");
                m_UI.UI_Repaint();
                Keys key = m_UI.UI_WaitKey().KeyCode;
                if (key == Keys.Escape) return;
                if (key == Keys.PageDown && (page + 1) * linesPerPage < lines.Count) page++;
                if (key == Keys.PageUp && page > 0) page--;
            }
        }
    }
}
