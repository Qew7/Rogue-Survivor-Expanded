using System;
using System.Collections.Generic;
using System.Text;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using System.IO;

using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine.Actions;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Engine.MapObjects;
using djack.RogueSurvivor.Gameplay;
using djack.RogueSurvivor.Gameplay.AI;
using djack.RogueSurvivor.Gameplay.Generators;

using Message = djack.RogueSurvivor.Data.Message;
using djack.RogueSurvivor.Engine.Tasks;
using ItemRating = djack.RogueSurvivor.Gameplay.AI.BaseAI.ItemRating;
using TradeRating = djack.RogueSurvivor.Gameplay.AI.BaseAI.TradeRating;

namespace djack.RogueSurvivor.Engine
{
    partial class RogueGame
    {
        #region Manual
        void LoadManual()
        {
            m_UI.UI_Clear(Color.Black);
            int gy = 0;
            m_UI.UI_DrawStringBold(Color.White, "Loading game manual...", 0, 0);
            gy += BOLD_LINE_SPACING;
            m_UI.UI_Repaint();

            m_Manual = new TextFile();
            m_ManualLine = 0;
            if (!m_Manual.Load(GetUserManualFilePath()))
            {
                // error.
                m_UI.UI_DrawStringBold(Color.Red, "Error while loading the manual.", 0, gy);
                gy += BOLD_LINE_SPACING;
                m_UI.UI_DrawStringBold(Color.Red, "The manual won't be available ingame.", 0, gy);
                gy += BOLD_LINE_SPACING;
                m_UI.UI_Repaint();
                DrawFootnote(Color.White, "press ENTER");
                WaitEnter();

                // delete manual.
                m_Manual = null;
                return;
            }

            m_UI.UI_DrawStringBold(Color.White, "Parsing game manual...", 0, gy);
            gy += BOLD_LINE_SPACING;
            m_UI.UI_Repaint();
            m_Manual.FormatLines(TEXTFILE_CHARS_PER_LINE);

            m_UI.UI_Clear(Color.Black);
            m_UI.UI_DrawStringBold(Color.White, "Game manual... done!", 0, gy);
            m_UI.UI_Repaint();
        }
        #endregion
        #region HiScore
        void HandleHiScores(bool saveToTextfile)
        {
            TextFile file = null;
            if (saveToTextfile)
                file = new TextFile();

            m_UI.UI_Clear(Color.Black);
            int gy = 0;
            DrawHeader();
            gy += BOLD_LINE_SPACING;
            m_UI.UI_DrawStringBold(Color.Yellow, "Hi Scores", 0, gy);
            gy += BOLD_LINE_SPACING;
            m_UI.UI_DrawStringBold(Color.White, "---------+---------+---------+---------+---------+---------+---------+---------+---------+---------+---------+---------+", 0, gy);
            gy += BOLD_LINE_SPACING;

            // display.
            m_UI.UI_DrawStringBold(Color.White, "Rank | Name, Skills, Death       |  Score |Difficulty|Survival|  Kills |Achievm.|      Game Time | Playing time", 0, gy);
            gy += BOLD_LINE_SPACING;

            // text.
            if (saveToTextfile)
            {
                file.Append(String.Format("ROGUE SURVIVOR {0}", SetupConfig.GAME_VERSION));
                file.Append("Hi Scores");
                file.Append("Rank | Name, Skills, Death       |  Score |Difficulty|Survival|  Kills |Achievm.|      Game Time | Playing time");
            }

            // individual entries.
            for (int i = 0; i < m_HiScoreTable.Count; i++)
            {
                // display.
                Color rankColor = (i == 0 ? Color.LightYellow : i == 1 ? Color.LightCyan : i == 2 ? Color.LightGreen : Color.DimGray);
                m_UI.UI_DrawStringBold(rankColor, "------------------------------------------------------------------------------------------------------------------------", 0, gy);
                gy += BOLD_LINE_SPACING;
                HiScore hi = m_HiScoreTable[i];
                string line = String.Format("{0,3}. | {1,-25} | {2,6} |     {3,3}% | {4,6} | {5,6} | {6,6} | {7,14} | {8}",
                    i + 1, TruncateString(hi.Name, 25),
                    hi.TotalPoints, hi.DifficultyPercent, hi.SurvivalPoints, hi.KillPoints, hi.AchievementPoints,
                    new WorldTime(hi.TurnSurvived).ToString(), TimeSpanToString(hi.PlayingTime));
                m_UI.UI_DrawStringBold(rankColor, line, 0, gy);
                gy += BOLD_LINE_SPACING;
                m_UI.UI_DrawStringBold(rankColor, String.Format("     | {0}.", hi.SkillsDescription), 0, gy);
                gy += BOLD_LINE_SPACING;
                m_UI.UI_DrawStringBold(rankColor, String.Format("     | {0}.", hi.Death), 0, gy);
                gy += BOLD_LINE_SPACING;

                // text.
                if (saveToTextfile)
                {
                    file.Append("------------------------------------------------------------------------------------------------------------------------");
                    file.Append(line);
                    file.Append(String.Format("     | {0}", hi.SkillsDescription));
                    file.Append(String.Format("     | {0}", hi.Death));
                }
            }

            // save.
            string textfilePath = GetUserHiScoreTextFilePath();
            if (saveToTextfile)
                file.Save(textfilePath);

            // display.
            m_UI.UI_DrawStringBold(Color.White, "---------+---------+---------+---------+---------+---------+---------+---------+---------+---------+---------+---------+", 0, gy);
            gy += BOLD_LINE_SPACING;
            if (saveToTextfile)
            {
                m_UI.UI_DrawStringBold(Color.White, textfilePath, 0, gy);
                gy += BOLD_LINE_SPACING;
            }
            DrawFootnote(Color.White, "press ESC to leave");
            m_UI.UI_Repaint();
            WaitEscape();
        }

        void LoadHiScoreTable()
        {
            m_UI.UI_Clear(Color.Black);
            m_UI.UI_DrawStringBold(Color.White, "Loading hiscores table...", 0, 0);
            m_UI.UI_Repaint();

            m_HiScoreTable = HiScoreTable.Load(GetUserHiScoreFilePath());
            if (m_HiScoreTable == null)
            {
                m_HiScoreTable = new HiScoreTable(HiScoreTable.DEFAULT_MAX_ENTRIES);
                m_HiScoreTable.Clear();
            }

            m_UI.UI_Clear(Color.Black);
            m_UI.UI_DrawStringBold(Color.White, "Loading hiscores table... done!", 0, 0);
            m_UI.UI_Repaint();
        }

        void SaveHiScoreTable()
        {
            m_UI.UI_Clear(Color.Black);
            m_UI.UI_DrawStringBold(Color.White, "Saving hiscores table...", 0, 0);
            m_UI.UI_Repaint();

            HiScoreTable.Save(m_HiScoreTable, GetUserHiScoreFilePath());

            m_UI.UI_Clear(Color.Black);
            m_UI.UI_DrawStringBold(Color.White, "Saving hiscores table... done!", 0, 0);
            m_UI.UI_Repaint();
        }
        #endregion
    }
}
