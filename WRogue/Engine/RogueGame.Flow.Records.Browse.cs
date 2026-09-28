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
        void DrawRecordsChoices(string title, IList<string> entries, int selected, string notice, string footer)
        {
            m_UI.UI_Clear(Color.Black); DrawHeader();
            m_UI.UI_DrawStringBold(Color.Yellow, TruncateString(title, 118), 0, BOLD_LINE_SPACING);
            m_UI.UI_DrawStringBold(Color.Gray, TruncateString(notice, 118), 0, 2 * BOLD_LINE_SPACING);
            int y = 4 * BOLD_LINE_SPACING, first = selected / 38 * 38;
            for (int i = first; i < entries.Count && i < first + 38; i++)
            {
                m_UI.UI_DrawStringBold(i == selected ? Color.LightGreen : Color.White,
                    (i == selected ? "> " : "  ") + TruncateString(entries[i], 116), 0, y);
                y += BOLD_LINE_SPACING;
            }
            DrawFootnote(Color.White, footer); m_UI.UI_Repaint();
        }
        static int MoveRecordsChoice(Keys key, int selected, int count)
        {
            if (key == Keys.Up) selected--;
            if (key == Keys.Down) selected++;
            if (key == Keys.PageUp) selected -= 38;
            if (key == Keys.PageDown) selected += 38;
            if (key == Keys.Home) selected = 0;
            if (key == Keys.End) selected = count - 1;
            return Math.Max(0, Math.Min(count - 1, selected));
        }
        void BrowseRecords(RecordsSave save)
        {
            RecordsQuery query = new RecordsQuery(); int selected = 0;
            Logger.WriteLine(Logger.Stage.RUN_MAIN, "records browser ready");
            while (true)
            {
                List<RecordsProfile> people = query.Select(save);
                List<string> labels = new List<string> { "All residents matching filters", "Most interesting NPC (matching filters)" };
                foreach (RecordsProfile person in people) labels.Add(person.Summary());
                selected = Math.Min(selected, labels.Count - 1);
                DrawRecordsChoices("Read Records - " + Path.GetFileName(save.Path), labels, selected,
                    "Matches " + people.Count + "/" + save.Profiles.Count + " | Sort: " + query.Sort + (query.Reverse ? " (reversed)" : "") + " | " + query.FilterSummary(),
                    "ENTER read | S search | F filters | O sort | V reverse | I most interesting | R reset | ESC back");
                Keys key = m_UI.UI_WaitKey().KeyCode;
                if (key == Keys.Escape) return;
                if (key == Keys.S)
                { string text = PromptRecordsText("NPC name contains", query.Name); if (text != null) query.Name = text.Trim(); selected = 0; }
                else if (key == Keys.F) { EditRecordsFilters(query); selected = 0; }
                else if (key == Keys.O)
                {
                    int order = ChooseRecord("Sort residents", new[] { "Name A-Z", "Items received over life (most)", "Memories (most)",
                        "Days lived (longest)", "Events (most)", "Interesting life (highest score)", "Unique participants in events (most)",
                        "Human kills (most)", "Help given (most)", "Resolved memories (most)", "Gained traits (most)" }, "V in browser reverses the order");
                    if (order >= 0) { query.Sort = (RecordsSort)order; query.Reverse = false; selected = 0; }
                }
                else if (key == Keys.V) { query.Reverse = !query.Reverse; selected = 0; }
                else if (key == Keys.R) { query = new RecordsQuery(); selected = 0; }
                else if (key == Keys.I || (key == Keys.Enter && selected == 1))
                {
                    RecordsProfile best = query.MostInteresting(save);
                    if (best == null) ShowRecordLines("No matches", new[] { "No matching NPCs. Change or reset filters." });
                    else
                    {
                        Logger.WriteLine(Logger.Stage.RUN_MAIN, "records interesting NPC ready: " + best.Resident.Name + " score " + best.Score);
                        ShowRecordsTimeline(save, best.Resident, query);
                    }
                }
                else if (key == Keys.Enter) ShowRecordsTimeline(save, selected == 0 ? null : people[selected - 2].Resident, query);
                else selected = MoveRecordsChoice(key, selected, labels.Count);
            }
        }
        void EditRecordsFilters(RecordsQuery query)
        {
            int selected = 0; string notice = "Filters combine with AND. Empty maximum days means unlimited.";
            while (true)
            {
                List<string> labels = new List<string>();
                for (int i = 0; i < RecordsQuery.FilterNames.Length; i++) labels.Add(RecordsQuery.FilterNames[i] + ": " + query.FilterValue(i));
                DrawRecordsChoices("Read Records - filters", labels, selected, notice, "ENTER edit | Left/Right change life status | ESC apply and return");
                Keys key = m_UI.UI_WaitKey().KeyCode;
                if (key == Keys.Escape) return;
                if (selected == 3 && (key == Keys.Enter || key == Keys.Left || key == Keys.Right))
                    query.Life = (RecordsLife)(((int)query.Life + (key == Keys.Left ? 2 : 1)) % 3);
                else if (key == Keys.Enter && selected == 13) { query.ClearFilters(); notice = "Filters cleared."; }
                else if (key == Keys.Enter)
                {
                    string value = PromptRecordsText(RecordsQuery.FilterNames[selected], query.FilterValue(selected));
                    if (value != null) notice = query.SetFilter(selected, value) ? "Filter updated." :
                        "Invalid value: use nonnegative numbers; minimum days must not exceed maximum.";
                }
                else selected = MoveRecordsChoice(key, selected, labels.Count);
            }
        }
        string PromptRecordsText(string title, string initial)
        {
            string text = initial ?? "";
            Logger.WriteLine(Logger.Stage.RUN_MAIN, "records prompt ready: " + title);
            while (true)
            {
                DrawRecordsChoices(title, new[] { text + "_" }, 0, "Text search ignores letter case.", "Type text | Ctrl+V paste | Ctrl+A clear | ENTER apply | ESC cancel");
                KeyEventArgs key = m_UI.UI_WaitKey();
                if (key.KeyCode == Keys.Escape) return null;
                if (key.KeyCode == Keys.Enter) return text;
                if (key.Control && key.KeyCode == Keys.A) { text = ""; continue; }
                if (key.Control && key.KeyCode == Keys.V)
                {
                    try { text += Clipboard.GetText().Replace("\r", "").Replace("\n", ""); } catch (Exception) { }
                    if (text.Length > 100) text = text.Substring(0, 100); continue;
                }
                if (key.KeyCode == Keys.Back) { if (text.Length > 0) text = text.Substring(0, text.Length - 1); continue; }
                if (text.Length >= 100 || key.Control || key.Alt) continue;
                if (key.KeyCode >= Keys.A && key.KeyCode <= Keys.Z)
                    text += (char)((key.Shift ? 'A' : 'a') + key.KeyCode - Keys.A);
                else if (key.KeyCode >= Keys.D0 && key.KeyCode <= Keys.D9) text += (char)('0' + key.KeyCode - Keys.D0);
                else if (key.KeyCode >= Keys.NumPad0 && key.KeyCode <= Keys.NumPad9) text += (char)('0' + key.KeyCode - Keys.NumPad0);
                else if (key.KeyCode == Keys.Space) text += " ";
                else if (key.KeyCode == Keys.OemMinus || key.KeyCode == Keys.Subtract) text += "-";
                else if (key.KeyCode == Keys.OemPeriod || key.KeyCode == Keys.Decimal) text += ".";
                else if (key.KeyCode == Keys.Oemcomma) text += ",";
                else if (key.KeyCode == Keys.OemQuotes) text += "'";
            }
        }
        void ShowRecordsTimeline(RecordsSave save, ResidentRecord resident, RecordsQuery query)
        {
            string title = resident == null ? "All residents" : resident.Name;
            Logger.WriteLine(Logger.Stage.RUN_MAIN, "records screen ready: " + title);
            string search = ""; RecordsEventFilter filter = RecordsEventFilter.All;
            int first = 0; const int pageSize = 40;
            List<string> lines = null;
            while (true)
            {
                if (lines == null)
                {
                    lines = new List<string>();
                    if (resident != null) lines.AddRange(new RecordsProfile(resident, save.Turn).Details());
                    lines.AddRange(RecordsReader.Lines(save, resident, query, search, filter));
                }
                int last = Math.Max(0, lines.Count - pageSize); first = Math.Min(first, last);
                m_UI.UI_Clear(Color.Black); DrawHeader();
                m_UI.UI_DrawStringBold(Color.Yellow, "Read Records - " + title, 0, BOLD_LINE_SPACING);
                m_UI.UI_DrawStringBold(Color.Gray, "Events: " + filter + " | Text: " + search, 0, 2 * BOLD_LINE_SPACING);
                int y = 4 * BOLD_LINE_SPACING;
                for (int i = first; i < lines.Count && i < first + pageSize; i++)
                { m_UI.UI_DrawStringBold(Color.White, TruncateString(lines[i], 120), 0, y); y += BOLD_LINE_SPACING; }
                DrawFootnote(Color.White, "Up/Down PgUp/PgDn Home/End | S search events | F event category | R reset | ESC back"); m_UI.UI_Repaint();
                Keys key = m_UI.UI_WaitKey().KeyCode;
                if (key == Keys.Escape) return;
                if (key == Keys.S) { string text = PromptRecordsText("Event text contains", search); if (text != null) search = text; first = 0; lines = null; }
                if (key == Keys.F)
                {
                    int choice = ChooseRecord("Event category", new[] { "All", "Memories and traits", "Combat", "Help", "Encounters and groups", "World events", "Life and survival" }, "Search text and category combine");
                    if (choice >= 0) filter = (RecordsEventFilter)choice; first = 0; lines = null;
                }
                if (key == Keys.R) { search = ""; filter = RecordsEventFilter.All; first = 0; lines = null; }
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
