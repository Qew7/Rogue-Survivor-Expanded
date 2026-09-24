using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using djack.RogueSurvivor.Gameplay;

namespace djack.RogueSurvivor.Engine
{
    partial class RogueGame
    {
        void ReloadModResources()
        {
            Logger.WriteLine(Logger.Stage.INIT_GFX, "reloading mod resources...");
            GameImages.LoadResources(m_UI);
            m_GameItems = new GameItems();
            m_GameActors = new GameActors();
            LoadData();
            Logger.WriteLine(Logger.Stage.INIT_GFX, "reloading mod resources done");
        }

        bool HandleModSelection()
        {
            ModInfo[] previous = ModCatalog.Selected;
            ModLoadOrder mods = new ModLoadOrder(ModCatalog.Discover("mods"), previous);
            int selected = 0;
            Logger.WriteLine(Logger.Stage.RUN_MAIN, "mod selection ready");
            while (true)
            {
                m_UI.UI_Clear(Color.Black);
                int y = 0;
                m_UI.UI_DrawStringBold(Color.Yellow, "Choose Mods (top enabled mod has highest priority)", 0, y);
                y += 2 * BOLD_LINE_SPACING;
                if (mods.Count == 0)
                    m_UI.UI_DrawString(Color.LightGray, "No mods found in the mods folder.", 0, y);
                else
                {
                    int pageSize = Math.Max(1, (CANVAS_HEIGHT - 12 * BOLD_LINE_SPACING - y)
                        / BOLD_LINE_SPACING);
                    int pageStart = (selected / pageSize) * pageSize;
                    int pageLength = Math.Min(pageSize, mods.Count - pageStart);
                    string[] entries = new string[pageLength];
                    for (int index = 0; index < pageLength; index++)
                    {
                        int modIndex = pageStart + index;
                        entries[index] = mods.IsEnabled(modIndex)
                            ? String.Format("[x] {0}. {1}", modIndex + 1, mods[modIndex].Name)
                            : "[ ] " + mods[modIndex].Name;
                    }
                    DrawMenuOrOptions(selected - pageStart, Color.White, entries,
                        Color.LightGray, null, 0, ref y);
                    if (mods.Count > pageSize)
                        m_UI.UI_DrawString(Color.LightGray,
                            String.Format("Page {0}/{1}", selected / pageSize + 1,
                                (mods.Count + pageSize - 1) / pageSize), 0, y);
                }
                y += BOLD_LINE_SPACING;
                if (mods.Count > 0)
                {
                    ModInfo mod = mods[selected];
                    string[] authors = mod.GetAuthors();
                    if (authors.Length > 0)
                    {
                        m_UI.UI_DrawString(Color.LightGray, "Authors: " + String.Join(", ", authors), 0, y);
                        y += BOLD_LINE_SPACING;
                    }
                    if (!String.IsNullOrEmpty(mod.Description))
                    {
                        m_UI.UI_DrawString(Color.LightGray, mod.Description, 0, y);
                        y += BOLD_LINE_SPACING;
                    }
                    foreach (string website in mod.GetWebsites())
                    {
                        m_UI.UI_DrawString(Color.LightGray, website, 0, y);
                        y += BOLD_LINE_SPACING;
                    }
                }
                DrawFootnote(Color.White,
                    "UP/DOWN choose, SPACE toggle, LEFT/RIGHT priority, ENTER apply, ESC cancel");
                m_UI.UI_Repaint();
                KeyEventArgs key = m_UI.UI_WaitKey();
                if (mods.Count > 0)
                {
                    if (key.KeyCode == Keys.Up) selected = (selected + mods.Count - 1) % mods.Count;
                    else if (key.KeyCode == Keys.Down) selected = (selected + 1) % mods.Count;
                    else if (key.KeyCode == Keys.Space) selected = mods.Toggle(selected);
                    else if (key.KeyCode == Keys.Left) selected = mods.Move(selected, -1);
                    else if (key.KeyCode == Keys.Right) selected = mods.Move(selected, 1);
                }
                if (key.KeyCode == Keys.Escape) return false;
                if (key.KeyCode == Keys.Enter)
                {
                    ModInfo[] chosen = mods.Selected();
                    if (chosen.Length == previous.Length)
                    {
                        bool unchanged = true;
                        for (int index = 0; index < chosen.Length; index++)
                            if (chosen[index].DirectoryPath != previous[index].DirectoryPath)
                                unchanged = false;
                        if (unchanged) return false;
                    }
                    ModCatalog.Select(chosen);
                    ModInfo[] active = ModCatalog.Selected;
                    string[] names = Array.ConvertAll(active, mod => mod.Name);
                    Logger.WriteLine(Logger.Stage.RUN_MAIN,
                        "selected mods: " + (names.Length == 0 ? "original" : String.Join(", ", names)));
                    return true;
                }
            }
        }
    }
}
