using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace djack.RogueSurvivor.Engine
{
    partial class RogueGame
    {
        static string GamePresetsPath
        {
            get { return Path.Combine(GetUserConfigPath(), "game-presets.dat"); }
        }

        bool HandleNewGamePreset()
        {
            GamePresetCollection saved;
            try { saved = GamePresetCollection.Load(GamePresetsPath); }
            catch (Exception error)
            {
                m_UI.UI_Clear(Color.Black);
                m_UI.UI_DrawStringBold(Color.Red, "Could not load game presets: " + error.Message, 0, 0);
                m_UI.UI_Repaint();
                m_UI.UI_WaitKey();
                return false;
            }
            GamePreset initial = BuiltInGamePreset(GameMode.GM_STANDARD);
            return EditGamePreset(initial, saved, GameMode.GM_STANDARD);
        }

        GamePreset BuiltInGamePreset(GameMode mode)
        {
            GamePreset preset = GamePreset.BuiltIn(mode);
            GameOptions options = s_Options;
            options.AllowUndeadsEvolution = mode != GameMode.GM_VINTAGE;
            if (mode == GameMode.GM_VINTAGE)
            {
                options.RatsUpgrade = false;
                options.SkeletonsUpgrade = false;
                options.ShamblersUpgrade = false;
            }
            preset.Options = options;
            preset.HasOptions = true;
            return preset;
        }

        bool SelectGamePreset(GamePresetCollection saved, out GamePreset preset, out GameMode origin)
        {
            List<GamePreset> presets = new List<GamePreset> {
                BuiltInGamePreset(GameMode.GM_STANDARD),
                BuiltInGamePreset(GameMode.GM_CORPSES_INFECTION),
                BuiltInGamePreset(GameMode.GM_VINTAGE),
                BuiltInGamePreset(GameMode.GM_XPD)
            };
            foreach (GamePreset custom in saved.Presets)
            {
                GamePreset copy = custom.Copy();
                if (!copy.HasOptions)
                {
                    copy.Options = s_Options;
                    copy.HasOptions = true;
                }
                presets.Add(copy);
            }
            preset = null;
            origin = GameMode.GM_STANDARD;
            int selected = 0;
            int previewPage = 0;
            int previewPages = (GamePresetLabels.Length - 3 + 9) / 10;
            while (true)
            {
                if (selected >= presets.Count) selected = presets.Count - 1;
                int first = selected / 10 * 10;
                int count = Math.Min(10, presets.Count - first);
                string[] entries = new string[count];
                for (int i = 0; i < count; i++) entries[i] = presets[first + i].Name;
                int gy = 0;
                m_UI.UI_Clear(Color.Black);
                m_UI.UI_DrawStringBold(Color.Yellow, "Choose a saved game preset", 0, gy);
                gy += 2 * BOLD_LINE_SPACING;
                DrawMenuOrOptions(selected - first, Color.White, entries, Color.White, null, 0, ref gy);
                GamePreset preview = presets[selected];
                string[] previewValues = GamePresetValues(preview);
                int previewFirst = 3 + previewPage * 10;
                int previewCount = Math.Min(10, GamePresetLabels.Length - previewFirst);
                gy += BOLD_LINE_SPACING;
                m_UI.UI_DrawStringBold(Color.Yellow,
                    String.Format("Options preview ({0}/{1})", previewPage + 1, previewPages), 0, gy);
                gy += BOLD_LINE_SPACING;
                for (int i = 0; i < previewCount; i++)
                {
                    int option = previewFirst + i;
                    m_UI.UI_DrawString(Color.Gray,
                        GamePresetLabels[option] + ": " + previewValues[option], 0, gy);
                    gy += BOLD_LINE_SPACING;
                }
                DrawFootnote(Color.White, "arrows choose/preview, ENTER load, DEL remove custom, ESC return");
                m_UI.UI_Repaint();
                KeyEventArgs key = m_UI.UI_WaitKey();
                if (key.KeyCode == Keys.Escape) return false;
                if (key.KeyCode == Keys.Up) selected = (selected + presets.Count - 1) % presets.Count;
                if (key.KeyCode == Keys.Down) selected = (selected + 1) % presets.Count;
                if (key.KeyCode == Keys.Left) previewPage = (previewPage + previewPages - 1) % previewPages;
                if (key.KeyCode == Keys.Right) previewPage = (previewPage + 1) % previewPages;
                if (key.KeyCode == Keys.Delete && selected >= 4)
                {
                    saved.Presets.RemoveAt(selected - 4);
                    saved.Save(GamePresetsPath);
                    presets.RemoveAt(selected);
                    if (selected >= presets.Count) selected--;
                }
                if (key.KeyCode == Keys.Enter)
                {
                    preset = presets[selected].Copy();
                    origin = selected < 4 ? (GameMode)selected : GameMode.GM_STANDARD;
                    return true;
                }
            }
        }

        static readonly string[] GamePresetLabels = {
                "Start game", "Load preset", "Save as preset", "Claimable bases", "Instant zombification",
                "Infection", "Corpses", "Zombie evolution", "Skeletons", "Shamblers",
                "Zombie masters", "Zombified humans", "Zombie rats", "Zombies in basements",
                "Zombies in sewers", "Hunger threshold", "Sleep threshold", "Sanity threshold",
                "Rot hunger threshold", "Corpse decay speed", "Corpse rising chance",
                "Base corpse rise chance", "Infection rate",
                "Undead rot speed", "Skeleton spawn weight", "Shambler spawn weight",
                "Master spawn weight", "Infection: weak at", "Infection: tired at",
                "Infection: vomiting at", "Infection: bleeding at", "Infection: death at",
                "Infection symptom rate", "Other gameplay options"
            };

        static readonly string[] GamePresetDescriptions = {
            "Start a new game with the settings shown here.",
            "Choose a built-in or saved preset. Its settings will appear on this screen.",
            "Save these settings under a name for future new games.",
            "Allow survivors to claim and manage enclosed buildings as bases.",
            "Turn killed humans into zombies immediately instead of leaving corpses.",
            "Enable infection from zombie attacks and its symptoms.",
            "Leave corpses after death. Corpses decay and may rise as zombies.",
            "Allow undead to evolve into stronger forms over time.",
            "Allow skeleton zombies to spawn.",
            "Allow shambler zombies to spawn.",
            "Allow zombie masters to spawn.",
            "Allow human victims to return as zombies.",
            "Allow zombie rats to spawn.",
            "Allow zombies to appear in basements.",
            "Allow zombies to appear in sewers.",
            "Hunger begins below this percentage of the normal food capacity.\nUse LEFT/RIGHT to change it in steps of 5%.",
            "Sleepiness begins below this percentage of the normal sleep capacity.\nUse LEFT/RIGHT to change it in steps of 5%.",
            "Sanity effects begin below this percentage of the normal sanity capacity.\nUse LEFT/RIGHT to change it in steps of 5%.",
            "Undead become hungry for rot below this percentage of normal rot capacity.\nUse LEFT/RIGHT to change it in steps of 5%.",
            "Scale how quickly corpses decay. 100% is the normal rate; 0% stops decay.",
            "Scale a corpse's chance to rise. 100% is the normal chance; 0% prevents rising.",
            "Add this many percentage points to the base chance for a corpse to rise.",
            "Scale infection gained from attacks. 100% is the normal amount; 0% prevents gain.",
            "Scale how quickly undead lose rot. 100% is the normal rate; 0% stops loss.",
            "Relative spawn weight for skeletons. Higher values make them more common.",
            "Relative spawn weight for shamblers. Higher values make them more common.",
            "Relative spawn weight for zombie masters. Higher values make them more common.",
            "Infection percentage at which weakness starts.",
            "Infection percentage at which tiredness starts.",
            "Infection percentage at which vomiting starts.",
            "Infection percentage at which bleeding starts.",
            "Infection percentage at which infection becomes fatal.",
            "Scale how often infection symptoms occur. 100% is the normal rate.",
            "Open the full gameplay options, including population, events and other rules."
        };

        static string[] GamePresetValues(GamePreset preset)
        {
            return new string[] {
                    "ENTER", "ENTER", "ENTER", OnOff(preset.Bases), OnOff(preset.ImmediateZombification),
                    OnOff(preset.Infection), OnOff(preset.Corpses), OnOff(preset.Evolution),
                    OnOff(preset.Skeletons), OnOff(preset.Shamblers), OnOff(preset.ZombieMasters),
                    OnOff(preset.Zombified), OnOff(preset.RatZombies), OnOff(preset.ZombiesInBasements),
                    OnOff(preset.ZombiesInSewers), preset.HungerThreshold + "%", preset.SleepThreshold + "%",
                    preset.SanityThreshold + "%", preset.RotThreshold + "%",
                    preset.CorpseDecayPercent + "%", preset.CorpseRiseChance + "%",
                    preset.CorpseBaseRiseChance + "%", preset.InfectionRatePercent + "%",
                    preset.RotDecayPercent + "%", preset.Options.SpawnSkeletonChance.ToString(),
                    preset.Options.SpawnZombieChance.ToString(), preset.Options.SpawnZombieMasterChance.ToString(),
                    preset.InfectionWeakThreshold + "%", preset.InfectionTiredThreshold + "%",
                    preset.InfectionVomitThreshold + "%", preset.InfectionBleedThreshold + "%",
                    preset.InfectionDeathThreshold + "%", preset.InfectionEffectRatePercent + "%", "ENTER"
            };
        }

        bool EditGamePreset(GamePreset preset, GamePresetCollection saved, GameMode origin)
        {
            string[] labels = GamePresetLabels;
            int selected = 0;
            string error = null;
            const int menuTop = 3 * BOLD_LINE_SPACING;
            const int rowHeight = 21;
            int descriptionTop = CANVAS_HEIGHT - 8 * BOLD_LINE_SPACING;
            int pageIndicatorY = descriptionTop - 2 * BOLD_LINE_SPACING;
            int pageSize = Math.Max(1, (pageIndicatorY - menuTop) / rowHeight);
            int pageCount = (labels.Length + pageSize - 1) / pageSize;
            Point previousMouse = Point.Empty;
            bool mouseReady = false;
            int descriptionIndex = selected;
            while (true)
            {
                string[] values = GamePresetValues(preset);
                int page = selected / pageSize;
                int first = page * pageSize;
                int count = Math.Min(pageSize, labels.Length - first);
                m_UI.UI_Clear(Color.Black);
                m_UI.UI_DrawStringBold(Color.Yellow, "New Game - Configure: " + preset.Name, 0, 0);
                for (int i = 0; i < count; i++)
                {
                    int option = first + i;
                    int y = menuTop + i * rowHeight;
                    m_UI.UI_DrawStringBold(option == descriptionIndex ? Color.Yellow : Color.White,
                        (option == selected ? "---> " : "     ") + labels[option], 0, y);
                    m_UI.UI_DrawStringBold(Color.LightGreen,
                        values[option] + (option == selected ? " <---" : ""), 340, y);
                }
                m_UI.UI_DrawStringBold(Color.Yellow,
                    String.Format("Settings {0}-{1} of {2}  |  Page {3}/{4}",
                        first + 1, first + count, labels.Length, page + 1, pageCount), 0, pageIndicatorY);
                m_UI.UI_DrawStringBold(Color.Yellow, labels[descriptionIndex], 0, descriptionTop);
                string[] descriptionLines = GamePresetDescriptions[descriptionIndex].Split('\n');
                for (int i = 0; i < descriptionLines.Length; i++)
                    m_UI.UI_DrawString(Color.LightGray, descriptionLines[i], 0,
                        descriptionTop + (i + 1) * BOLD_LINE_SPACING);
                if (error != null)
                    m_UI.UI_DrawString(Color.Red, error, 0, CANVAS_HEIGHT - 3 * BOLD_LINE_SPACING);
                DrawFootnote(Color.White, "UP/DOWN select, PGUP/PGDN page, LEFT/RIGHT change, ENTER choose, ESC cancels");
                m_UI.UI_Repaint();
                if (!mouseReady)
                {
                    previousMouse = m_UI.UI_GetMousePosition();
                    m_UI.UI_PeekKey(); // Discard the key that opened this screen.
                    mouseReady = true;
                }
                KeyEventArgs key = null;
                while (key == null)
                {
                    key = m_UI.UI_PeekKey();
                    Point mouse = m_UI.UI_GetMousePosition();
                    m_UI.UI_PeekMouseButtons();
                    if (mouse != previousMouse)
                    {
                        previousMouse = mouse;
                        int mouseY = (int)(mouse.Y / m_UI.UI_GetCanvasScaleY());
                        int mouseX = (int)(mouse.X / m_UI.UI_GetCanvasScaleX());
                        int row = (mouseY - menuTop) / rowHeight;
                        if (mouseX >= 0 && mouseX < CANVAS_WIDTH && mouseY >= menuTop &&
                            row >= 0 && row < count && mouseY < menuTop + count * rowHeight)
                        {
                            int hovered = first + row;
                            if (hovered != descriptionIndex)
                            {
                                descriptionIndex = hovered;
                                break;
                            }
                        }
                    }
                }
                if (key == null) continue;
                error = null;
                if (key.KeyCode == Keys.Escape) return false;
                if (key.KeyCode == Keys.Up) selected = (selected + labels.Length - 1) % labels.Length;
                if (key.KeyCode == Keys.Down) selected = (selected + 1) % labels.Length;
                if (key.KeyCode == Keys.PageUp) selected = Math.Max(0, selected - pageSize);
                if (key.KeyCode == Keys.PageDown) selected = Math.Min(labels.Length - 1, selected + pageSize);
                descriptionIndex = selected;
                if (selected >= 3 && selected < labels.Length - 1 &&
                    (key.KeyCode == Keys.Left || key.KeyCode == Keys.Right || key.KeyCode == Keys.Enter))
                    ChangeGamePreset(preset, selected - 1, key.KeyCode == Keys.Left ? -1 : 1);
                if (key.KeyCode == Keys.Enter && selected == 0)
                {
                    try
                    {
                        preset.Validate();
                        if (preset.HasOptions)
                        {
                            s_Options = preset.Options;
                            ApplyOptions(false);
                            SaveOptions();
                        }
                        m_Session.Reset();
                        m_Session.GameMode = origin;
                        m_Session.GamePreset = preset;
                        return true;
                    }
                    catch (ArgumentException problem) { error = problem.Message; }
                }
                if (key.KeyCode == Keys.Enter && selected == 1)
                {
                    GamePreset loaded;
                    GameMode loadedOrigin;
                    if (SelectGamePreset(saved, out loaded, out loadedOrigin))
                    {
                        preset = loaded;
                        origin = loadedOrigin;
                    }
                    m_UI.UI_PeekKey();
                }
                if (key.KeyCode == Keys.Enter && selected == 2)
                {
                    string name = PromptGamePresetName();
                    m_UI.UI_PeekKey();
                    if (name != null)
                    {
                        try
                        {
                            GamePreset copy = preset.Copy();
                            copy.Name = name;
                            copy.Validate();
                            saved.AddOrReplace(copy);
                            saved.Save(GamePresetsPath);
                            preset.Name = name;
                        }
                        catch (Exception problem) { error = problem.Message; }
                    }
                }
                if (key.KeyCode == Keys.Enter && selected == labels.Length - 1)
                {
                    GameOptions previous = s_Options;
                    s_Options = preset.HasOptions ? preset.Options : previous;
                    try
                    {
                        HandleOptions(false, false);
                        m_UI.UI_PeekKey();
                        preset.Options = s_Options;
                        preset.HasOptions = true;
                    }
                    finally
                    {
                        s_Options = previous;
                        ApplyOptions(false);
                    }
                }
            }
        }

        static string OnOff(bool value) { return value ? "ON" : "OFF"; }

        static int BoundedStep(int value, int delta, int max)
        {
            return Math.Max(0, Math.Min(max, value + delta));
        }

        static void ChangeGamePreset(GamePreset preset, int selected, int direction)
        {
            switch (selected)
            {
                case 2: preset.Bases = !preset.Bases; break;
                case 3: preset.ImmediateZombification = !preset.ImmediateZombification; break;
                case 4: preset.Infection = !preset.Infection; break;
                case 5: preset.Corpses = !preset.Corpses; break;
                case 6: preset.Evolution = !preset.Evolution; break;
                case 7: preset.Skeletons = !preset.Skeletons; break;
                case 8: preset.Shamblers = !preset.Shamblers; break;
                case 9: preset.ZombieMasters = !preset.ZombieMasters; break;
                case 10: preset.Zombified = !preset.Zombified; break;
                case 11: preset.RatZombies = !preset.RatZombies; break;
                case 12: preset.ZombiesInBasements = !preset.ZombiesInBasements; break;
                case 13: preset.ZombiesInSewers = !preset.ZombiesInSewers; break;
                case 14: preset.HungerThreshold = BoundedStep(preset.HungerThreshold, 5 * direction, 100); break;
                case 15: preset.SleepThreshold = BoundedStep(preset.SleepThreshold, 5 * direction, 100); break;
                case 16: preset.SanityThreshold = BoundedStep(preset.SanityThreshold, 5 * direction, 100); break;
                case 17: preset.RotThreshold = BoundedStep(preset.RotThreshold, 5 * direction, 100); break;
                case 18: preset.CorpseDecayPercent = BoundedStep(preset.CorpseDecayPercent, 10 * direction, 500); break;
                case 19: preset.CorpseRiseChance = BoundedStep(preset.CorpseRiseChance, 10 * direction, 500); break;
                case 20: preset.CorpseBaseRiseChance = BoundedStep(preset.CorpseBaseRiseChance, 5 * direction, 100); break;
                case 21: preset.InfectionRatePercent = BoundedStep(preset.InfectionRatePercent, 10 * direction, 500); break;
                case 22: preset.RotDecayPercent = BoundedStep(preset.RotDecayPercent, 10 * direction, 500); break;
                case 23:
                case 24:
                case 25:
                    GameOptions options = preset.Options;
                    if (selected == 23) options.SpawnSkeletonChance += 5 * direction;
                    if (selected == 24) options.SpawnZombieChance += 5 * direction;
                    if (selected == 25) options.SpawnZombieMasterChance += 5 * direction;
                    preset.Options = options;
                    preset.HasOptions = true;
                    break;
                case 26: preset.InfectionWeakThreshold = BoundedStep(preset.InfectionWeakThreshold, 5 * direction, 100); break;
                case 27: preset.InfectionTiredThreshold = BoundedStep(preset.InfectionTiredThreshold, 5 * direction, 100); break;
                case 28: preset.InfectionVomitThreshold = BoundedStep(preset.InfectionVomitThreshold, 5 * direction, 100); break;
                case 29: preset.InfectionBleedThreshold = BoundedStep(preset.InfectionBleedThreshold, 5 * direction, 100); break;
                case 30: preset.InfectionDeathThreshold = BoundedStep(preset.InfectionDeathThreshold, 5 * direction, 100); break;
                case 31: preset.InfectionEffectRatePercent = BoundedStep(preset.InfectionEffectRatePercent, 10 * direction, 500); break;
            }
        }

        string PromptGamePresetName()
        {
            string name = "";
            while (true)
            {
                m_UI.UI_Clear(Color.Black);
                m_UI.UI_DrawStringBold(Color.Yellow, "Save game preset", 0, 0);
                m_UI.UI_DrawStringBold(Color.White, "Name: " + name + "_", 0, 2 * BOLD_LINE_SPACING);
                DrawFootnote(Color.White, "letters, digits and spaces; ENTER saves, ESC cancels");
                m_UI.UI_Repaint();
                KeyEventArgs key = m_UI.UI_WaitKey();
                if (key.KeyCode == Keys.Escape) return null;
                if (key.KeyCode == Keys.Enter) return name.Trim();
                if (key.KeyCode == Keys.Back && name.Length > 0) name = name.Substring(0, name.Length - 1);
                if (name.Length >= 40) continue;
                if (key.KeyCode >= Keys.A && key.KeyCode <= Keys.Z)
                    name += key.KeyCode.ToString();
                else if (key.KeyCode >= Keys.D0 && key.KeyCode <= Keys.D9)
                    name += (char)('0' + key.KeyCode - Keys.D0);
                else if (key.KeyCode == Keys.Space) name += " ";
            }
        }
    }
}
