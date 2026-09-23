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
        #region Game loading & saving
        void HandleSaveGame()
        {
            // alpha10.1
            // manually saving the game delays (reschedule) the next autosave
            ScheduleNextAutoSave();

            DoSaveGame(GetUserSave());
        }

        // alpha10.1
        void CheckAutoSaveTime()
        {
            // sanity checks
            if (!m_IsGameRunning || m_Player == null || m_Player.IsDead)
                return;

            // option off?
            if (s_Options.AutoSavePeriodInHours <= 0)
                return;

            // not time yet?
            if (m_Session.WorldTime.TurnCounter < m_Session.NextAutoSaveTime)
                return;

            // autosave now and reschedule
            ScheduleNextAutoSave();
            Overlay popup = new OverlayPopup(new string[] { "AUTOSAVING..." }, Color.Yellow, Color.White, Color.Black, MapToScreen(m_Player.Location.Position.X, m_Player.Location.Position.Y));
            AddOverlay(popup);
            DoSaveGame(GetUserSave(), true);
            RemoveOverlay(popup);
            RedrawPlayScreen();
        }

        // alpha10.1
        void ScheduleNextAutoSave()
        {
            m_Session.NextAutoSaveTime = m_Session.WorldTime.TurnCounter + WorldTime.TURNS_PER_HOUR * s_Options.AutoSavePeriodInHours;
        }

        void HandleLoadGame()
        {
            DoLoadGame(GetUserSave());
        }

        // alpha10.1 messages modified for autosave
        // alpha10.1 start & stop sim thread here instead of caller
        void DoSaveGame(string saveName, bool isAutoSave = false)
        {
            StopSimThread(false);  // alpha10.1

            string savingOrAutosaving = isAutoSave ? "AUTOSAVING" : "SAVING";

            ClearMessages();
            AddMessage(new Message(String.Format("{0} GAME, PLEASE WAIT...", savingOrAutosaving), m_Session.WorldTime.TurnCounter, Color.Yellow));
            RedrawPlayScreen();
            m_UI.UI_Repaint();

            // save session object.
            Session.Save(m_Session, saveName, Session.SaveFormat.FORMAT_BIN);

            AddMessage(new Message(String.Format("{0} DONE.", savingOrAutosaving), m_Session.WorldTime.TurnCounter, Color.Yellow));
            RedrawPlayScreen();
            m_UI.UI_Repaint();

            StartSimThread();  // alpha10.1
        }

        // alpha10.1 start & stop sim thread here instead of caller
        void DoLoadGame(string saveName)
        {
            StopSimThread(false); // alpha10.1

            ClearMessages();
            AddMessage(new Message("LOADING GAME, PLEASE WAIT...", m_Session.WorldTime.TurnCounter, Color.Yellow));
            RedrawPlayScreen();
            m_UI.UI_Repaint();

            if (!LoadGame(saveName))
            {
                AddMessage(new Message("LOADING FAILED, NO GAME SAVED OR VERSION NOT COMPATIBLE.", m_Session.WorldTime.TurnCounter, Color.Red));
            }

            StartSimThread();  // alpha10.1
        }

        void DeleteSavedGame(string saveName)
        {
            // do it.
            if (Session.Delete(saveName))
            {
                // tell.
                AddMessage(new Message("PERMADEATH : SAVE GAME DELETED!", m_Session.WorldTime.TurnCounter, Color.Red));
            }
        }

        bool LoadGame(string saveName)
        {
            // load session object.
            bool loaded = Session.Load(saveName, Session.SaveFormat.FORMAT_BIN);
            if (!loaded)
                return false;
            m_Session = Session.Get;
            m_Rules = new Rules(new DiceRoller(m_Session.Seed));

            RefreshPlayer();

            AddMessage(new Message("LOADING DONE.", m_Session.WorldTime.TurnCounter, Color.Yellow));
            AddMessage(new Message("Welcome back to Rogue Survivor!", m_Session.WorldTime.TurnCounter, Color.LightGreen));
            RedrawPlayScreen();
            m_UI.UI_Repaint();

            // Log ;/
            m_Session.Scoring.AddEvent(m_Session.WorldTime.TurnCounter, "<Loaded game>");

            return true;
        }
        #endregion
        #region Options loading, saving & applying
        void LoadOptions()
        {
            // load.
            s_Options = GameOptions.Load(GetUserOptionsFilePath());
        }

        void SaveOptions()
        {
            // save
            GameOptions.Save(s_Options, GetUserOptionsFilePath());
        }

        void ApplyOptions(bool ingame)
        {
            m_MusicManager.IsMusicEnabled = Options.PlayMusic;
            m_MusicManager.Volume = Options.MusicVolume;

            // update difficulty.
            if (m_Session != null && m_Session.Scoring != null)
            {
                m_Session.Scoring.Side = (m_Player == null || !m_Player.Model.Abilities.IsUndead) ? DifficultySide.FOR_SURVIVOR : DifficultySide.FOR_UNDEAD;
                m_Session.Scoring.DifficultyRating = Scoring.ComputeDifficultyRating(s_Options, m_Session.Scoring.Side, m_Session.Scoring.ReincarnationNumber);
            }

            if (!m_MusicManager.IsMusicEnabled)
                m_MusicManager.Stop();
        }
        #endregion
        #region Keybindings loading & saving
        void LoadKeybindings()
        {
            m_UI.UI_Clear(Color.Black);
            m_UI.UI_DrawStringBold(Color.White, "Loading keybindings...", 0, 0);
            m_UI.UI_Repaint();

            s_KeyBindings = Keybindings.Load(GetUserConfigPath() + "keys.dat");

            m_UI.UI_Clear(Color.Black);
            m_UI.UI_DrawStringBold(Color.White, "Loading keybindings... done!", 0, 0);
            m_UI.UI_Repaint();

        }

        void SaveKeybindings()
        {
            m_UI.UI_Clear(Color.Black);
            m_UI.UI_DrawStringBold(Color.White, "Saving keybindings...", 0, 0);
            m_UI.UI_Repaint();

            Keybindings.Save(s_KeyBindings, GetUserConfigPath() + "keys.dat");

            m_UI.UI_Clear(Color.Black);
            m_UI.UI_DrawStringBold(Color.White, "Saving keybindings... done!", 0, 0);
            m_UI.UI_Repaint();
        }
        #endregion
        #region Hints saving & loading
        void LoadHints()
        {
            m_UI.UI_Clear(Color.Black);
            m_UI.UI_DrawStringBold(Color.White, "Loading hints...", 0, 0);
            m_UI.UI_Repaint();

            s_Hints = GameHintsStatus.Load(GetUserConfigPath() + "hints.dat");

            m_UI.UI_Clear(Color.Black);
            m_UI.UI_DrawStringBold(Color.White, "Loading hints... done!", 0, 0);
            m_UI.UI_Repaint();
        }

        void SaveHints()
        {
            GameHintsStatus.Save(s_Hints, GetUserConfigPath() + "hints.dat");
        }
        #endregion
        #region Game user data paths
        public static string GetUserBasePath()
        {
            return SetupConfig.DirPath;
            /*
            string myDocs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            return myDocs + @"\Rogue Survivor\" + SetupConfig.GAME_VERSION + @"\";
             */
        }

        public static string GetUserSavesPath()
        {
            return GetUserBasePath() + @"Saves\";
        }

        public static string GetUserSave()
        {
            return GetUserSavesPath() + "save.dat";
        }

        public static string GetUserDocsPath()
        {
            return GetUserBasePath() + @"Docs\";
        }

        public static string GetUserGraveyardPath()
        {
            return GetUserBasePath() + @"Graveyard\";
        }

        /// <summary>
        /// "grave_[id]"
        /// </summary>
        /// <returns></returns>
        public string GetUserNewGraveyardName()
        {
            string name;
            int i = 0;
            bool isFreeID = false;
            do
            {
                name = String.Format("grave_{0:D3}", i);
                isFreeID = !File.Exists(GraveFilePath(name));
                ++i;
            }
            while (!isFreeID);

            return name;
        }

        public static string GraveFilePath(string graveName)
        {
            return GetUserGraveyardPath() + graveName + ".txt";
        }

        public static string GetUserConfigPath()
        {
            return GetUserBasePath() + @"Config\";
        }

        public static string GetUserOptionsFilePath()
        {
            return GetUserConfigPath() + @"options.dat";
        }

        public static string GetUserScreenshotsPath()
        {
            return GetUserBasePath() + @"Screenshots\";
        }

        /// <summary>
        /// "screenshot_[id]"
        /// </summary>
        /// <returns></returns>
        public string GetUserNewScreenshotName()
        {
            string name;
            int i = 0;
            bool isFreeID = false;
            do
            {
                name = String.Format("screenshot_{0:D3}", i);
                isFreeID = !File.Exists(ScreenshotFilePath(name));
                ++i;
            }
            while (!isFreeID);

            return name;
        }

        public string ScreenshotFilePath(string shotname)
        {
            return GetUserScreenshotsPath() + shotname + "." + m_UI.UI_ScreenshotExtension();
        }

        bool CreateDirectory(string path)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
                return true;
            }
            else
                return false;
        }

        bool CheckDirectory(string path, string description, ref int gy)
        {
            m_UI.UI_DrawString(Color.White, String.Format("{0} : {1}...", description, path), 0, gy);
            gy += BOLD_LINE_SPACING;
            m_UI.UI_Repaint();
            bool created = CreateDirectory(path);
            m_UI.UI_DrawString(Color.White, "ok.", 0, gy);
            gy += BOLD_LINE_SPACING;
            m_UI.UI_Repaint();

            return created;
        }

        bool CheckCopyOfManual()
        {
            string src_path = @"Resources\Manual\";
            string dst_path = GetUserDocsPath();
            string filename = "RS Manual.txt";

            // copy file.
            bool copied = false;
            Logger.WriteLine(Logger.Stage.INIT_MAIN, "checking for manual...");
            if (!File.Exists(dst_path + filename))
            {
                Logger.WriteLine(Logger.Stage.INIT_MAIN, "copying manual...");
                copied = true;
                File.Copy(src_path + filename, dst_path + filename);
                Logger.WriteLine(Logger.Stage.INIT_MAIN, "copying manual... done!");
            }
            Logger.WriteLine(Logger.Stage.INIT_MAIN, "checking for manual... done!");

            return copied;
        }

        string GetUserManualFilePath()
        {
            return GetUserDocsPath() + "RS Manual.txt";
        }

        string GetUserHiScorePath()
        {
            return GetUserSavesPath();
        }

        string GetUserHiScoreFilePath()
        {
            return GetUserHiScorePath() + "hiscores.dat";
        }

        string GetUserHiScoreTextFilePath()
        {
            return GetUserHiScorePath() + "hiscores.txt";
        }
        #endregion
    }
}
