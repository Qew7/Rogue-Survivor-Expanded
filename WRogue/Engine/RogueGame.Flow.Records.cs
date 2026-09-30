using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using djack.RogueSurvivor.Data;

namespace djack.RogueSurvivor.Engine
{
    partial class RogueGame
    {
        void HandleReadRecords()
        {
            ReadRecordsFrom(GetUserSavesPath());
        }

        void ReadRecordsFrom(string directory)
        {
            m_UI.UI_Clear(Color.Black);
            m_UI.UI_DrawStringBold(Color.Yellow, "Reading save records, please wait...", 0, 0);
            m_UI.UI_Repaint();
            int skipped;
            List<RecordsSave> saves;
            try { saves = RecordsReader.Find(directory, out skipped); }
            catch (Exception error)
            {
                ShowRecordLines("Read Records", new[] { "Could not read saves: " + error.Message });
                return;
            }
            if (saves.Count == 0)
            {
                ShowRecordLines("Read Records", new[] {
                    "No readable saves with NPC traits and memories enabled.",
                    "Place .dat or .sav saves (or their .bak backups) in the Saves folder." });
                return;
            }
            string[] labels = saves.ConvertAll(save => Path.GetFileName(save.Path) + " | " +
                new WorldTime(save.Turn) + (save.Records.IsPartial ? " | partial history" : "")).ToArray();
            while (true)
            {
                int choice = ChooseRecord("Read Records - choose a save", labels,
                    "Disabled or unreadable saves skipped: " + skipped);
                if (choice < 0) return;
                RecordsSave save = saves[choice];
                List<ResidentRecord> people = RecordsReader.Residents(save);
                List<string> residents = new List<string> { "All residents" };
                foreach (ResidentRecord resident in people)
                    residents.Add(resident.Name + " [" + resident.Identity.ToString("N").Substring(0, 8) +
                        "] | from " + new WorldTime(resident.SpawnTurn) +
                        (resident.DeathTurn == -2 ? " | dead (date unknown)" :
                        resident.DeathTurn < 0 ? " | alive" : " | died " + new WorldTime(resident.DeathTurn)));
                while (true)
                {
                    int person = ChooseRecord("Read Records - " + Path.GetFileName(save.Path), residents.ToArray(),
                        "Recorded residents: " + people.Count);
                    if (person < 0) break;
                    ResidentRecord selected = person == 0 ? null : people[person - 1];
                    ShowRecordLines(selected == null ? "All residents" : selected.Name,
                        RecordsReader.Lines(save, selected));
                }
            }
        }

        int ChooseRecord(string title, string[] entries, string notice)
        {
            Logger.WriteLine(Logger.Stage.RUN_MAIN, "records selection ready: " + title);
            int selected = 0;
            const int pageSize = 40;
            while (true)
            {
                m_UI.UI_Clear(Color.Black);
                DrawHeader();
                m_UI.UI_DrawStringBold(Color.Yellow, title, 0, BOLD_LINE_SPACING);
                m_UI.UI_DrawStringBold(Color.Gray, notice, 0, 2 * BOLD_LINE_SPACING);
                int y = 4 * BOLD_LINE_SPACING;
                int first = selected / pageSize * pageSize;
                for (int i = first; i < entries.Length && i < first + pageSize; i++)
                {
                    m_UI.UI_DrawStringBold(i == selected ? Color.LightGreen : Color.White,
                        (i == selected ? "> " : "  ") + TruncateString(entries[i], 118), 0, y);
                    y += BOLD_LINE_SPACING;
                }
                DrawFootnote(Color.White, "Up/Down, PgUp/PgDn to choose, ENTER to read, ESC to return");
                m_UI.UI_Repaint();
                Keys key = m_UI.UI_WaitMenuKey().KeyCode;
                if (key == Keys.Escape) return -1;
                if (key == Keys.Enter && entries.Length > 0) return selected;
                if (key == Keys.Up) selected = Math.Max(0, selected - 1);
                if (key == Keys.Down) selected = Math.Min(entries.Length - 1, selected + 1);
                if (key == Keys.PageUp) selected = Math.Max(0, selected - pageSize);
                if (key == Keys.PageDown) selected = Math.Min(entries.Length - 1, selected + pageSize);
            }
        }

        void ShowRecordLines(string title, IList<string> lines)
        {
            Logger.WriteLine(Logger.Stage.RUN_MAIN, "records screen ready: " + title);
            int first = 0;
            const int pageSize = 45;
            while (true)
            {
                m_UI.UI_Clear(Color.Black);
                DrawHeader();
                m_UI.UI_DrawStringBold(Color.Yellow, "Read Records - " + title, 0, BOLD_LINE_SPACING);
                int y = 3 * BOLD_LINE_SPACING;
                for (int i = first; i < lines.Count && i < first + pageSize; i++)
                {
                    m_UI.UI_DrawStringBold(Color.White, lines[i], 0, y);
                    y += BOLD_LINE_SPACING;
                }
                DrawFootnote(Color.White, "Up/Down, PgUp/PgDn to scroll, Home/End, ESC to return");
                m_UI.UI_Repaint();
                Keys key = m_UI.UI_WaitMenuKey().KeyCode;
                if (key == Keys.Escape) return;
                int last = Math.Max(0, lines.Count - pageSize);
                if (key == Keys.Up) first = Math.Max(0, first - 1);
                if (key == Keys.Down) first = Math.Min(last, first + 1);
                if (key == Keys.PageUp) first = Math.Max(0, first - pageSize);
                if (key == Keys.PageDown) first = Math.Min(last, first + pageSize);
                if (key == Keys.Home) first = 0;
                if (key == Keys.End) first = last;
            }
        }
    }
}
