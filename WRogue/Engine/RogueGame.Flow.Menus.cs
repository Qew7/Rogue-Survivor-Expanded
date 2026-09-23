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
        void HandleCredits()
        {
            const int left = 0;
            const int right = 256;
            int gy = 0;

            // music.
            m_MusicManager.Stop();
            m_MusicManager.Play(GameMusics.SLEEP, MusicPriority.PRIORITY_BGM);

            // draw.
            m_UI.UI_Clear(Color.Black);
            DrawHeader();
            gy += BOLD_LINE_SPACING;
            m_UI.UI_DrawStringBold(Color.Yellow, "Credits", 0, gy);
            gy += 2 * BOLD_LINE_SPACING;
            m_UI.UI_DrawStringBold(Color.White, "Programming, Graphics & Music by Jacques Ruiz (roguedjack) 2018", 0, gy);
            gy += 2 * BOLD_LINE_SPACING;

            m_UI.UI_DrawStringBold(Color.White, "Programming", left, gy); m_UI.UI_DrawString(Color.White, "- C# NET 3.5, Microsoft Visual Studio Community 2017", right, gy);
            gy += BOLD_LINE_SPACING;
            m_UI.UI_DrawStringBold(Color.White, "Graphic softwares", left, gy); m_UI.UI_DrawString(Color.White, "- Inkscape, Paint.NET", right, gy);
            gy += BOLD_LINE_SPACING;
            m_UI.UI_DrawStringBold(Color.White, "Sound & Music softwares", left, gy); m_UI.UI_DrawString(Color.White, "- GuitarPro 7, Audacity", right, gy);
            gy += BOLD_LINE_SPACING;
            m_UI.UI_DrawStringBold(Color.White, "Sound samples", left, gy); m_UI.UI_DrawString(Color.White, @"- http://www.sound-fishing.net  http://www.soundsnap.com/", right, gy);
            //gy += BOLD_LINE_SPACING;
            //m_UI.UI_DrawStringBold(Color.White, "Installer", left, gy); m_UI.UI_DrawString(Color.White, @"- NSIS", right, gy);

            gy += 2 * BOLD_LINE_SPACING;
            m_UI.UI_DrawStringBold(Color.White, "Contact", 0, gy);
            gy += BOLD_LINE_SPACING;
            m_UI.UI_DrawString(Color.White, @"Email      : roguedjack@yahoo.fr", 0, gy);
            gy += BOLD_LINE_SPACING;
            m_UI.UI_DrawString(Color.White, @"Blog       : http://roguesurvivor.blogspot.com/", 0, gy);
            gy += BOLD_LINE_SPACING;
            m_UI.UI_DrawString(Color.White, @"Fans Forum : http://roguesurvivor.proboards.com/", 0, gy);
            gy += BOLD_LINE_SPACING;
            m_UI.UI_DrawStringBold(Color.White, "Thanks to the players for their feedback and eagerness to die!", 0, gy);
            gy += BOLD_LINE_SPACING;

            DrawFootnote(Color.White, "ESC to leave");
            m_UI.UI_Repaint();
            WaitEscape();
        }

        // alpha10 removed mention of mode and mentioned which options are always off in certain modes (vintage)
        void HandleOptions(bool ingame)
        {
            GameOptions prevOptions = s_Options;

            #region
            GameOptions.IDs[] list = new GameOptions.IDs[]
            {
                   // autosave
                   GameOptions.IDs.GAME_AUTOSAVE_PERIOD,  // alpha10.1
                   // display & sounds
                   GameOptions.IDs.UI_MUSIC,
                   GameOptions.IDs.UI_MUSIC_VOLUME,
                   GameOptions.IDs.UI_ANIM_DELAY,
                   GameOptions.IDs.UI_SHOW_MINIMAP,
                   GameOptions.IDs.UI_SHOW_PLAYER_TAG_ON_MINIMAP,
                   // helpers
                   GameOptions.IDs.UI_ADVISOR,
                   GameOptions.IDs.UI_COMBAT_ASSISTANT,
                   GameOptions.IDs.UI_SHOW_PLAYER_TARGETS,
                   GameOptions.IDs.UI_SHOW_TARGETS,
                   // sim
                   GameOptions.IDs.GAME_SIMULATE_DISTRICTS,
                   GameOptions.IDs.GAME_SIM_THREAD,
                   GameOptions.IDs.GAME_SIMULATE_SLEEP,
                   // death
                   GameOptions.IDs.GAME_DEATH_SCREENSHOT,
                   GameOptions.IDs.GAME_PERMADEATH,
                   // maps
                   GameOptions.IDs.GAME_CITY_SIZE,
                   GameOptions.IDs.GAME_DISTRICT_SIZE,
                   GameOptions.IDs.GAME_REVEAL_STARTING_DISTRICT,
                   // living
                   GameOptions.IDs.GAME_MAX_CIVILIANS,
                   // GameOptions.IDs.GAME_MAX_DOGS,
                   GameOptions.IDs.GAME_ZOMBIFICATION_CHANCE,
                   GameOptions.IDs.GAME_AGGRESSIVE_HUNGRY_CIVILIANS,
                   GameOptions.IDs.GAME_NPC_CAN_STARVE_TO_DEATH,
                   GameOptions.IDs.GAME_STARVED_ZOMBIFICATION_CHANCE,
                   // undeads
                   GameOptions.IDs.GAME_MAX_UNDEADS,
                   GameOptions.IDs.GAME_ALLOW_UNDEADS_EVOLUTION,
                   GameOptions.IDs.GAME_DAY_ZERO_UNDEADS_PERCENT,
                   GameOptions.IDs.GAME_ZOMBIE_INVASION_DAILY_INCREASE,
                   GameOptions.IDs.GAME_UNDEADS_UPGRADE_DAYS,
                   GameOptions.IDs.GAME_SHAMBLERS_UPGRADE,
                   GameOptions.IDs.GAME_SKELETONS_UPGRADE,
                   GameOptions.IDs.GAME_RATS_UPGRADE,
                   // events
                   GameOptions.IDs.GAME_NATGUARD_FACTOR,
                   GameOptions.IDs.GAME_SUPPLIESDROP_FACTOR,
                   // reinc
                   GameOptions.IDs.GAME_MAX_REINCARNATIONS,
                   GameOptions.IDs.GAME_REINC_LIVING_RESTRICTED,
                   GameOptions.IDs.GAME_REINCARNATE_AS_RAT,
                   GameOptions.IDs.GAME_REINCARNATE_TO_SEWERS
            };
            #endregion

            string[] menuEntries = new string[list.Length];
            string[] values = new string[list.Length];
            for (int i = 0; i < list.Length; i++)
            {
                menuEntries[i] = GameOptions.Name(list[i]);
                // alpha10 special mode notes
                GameOptions.IDs id = list[i];
                if (id == GameOptions.IDs.GAME_ALLOW_UNDEADS_EVOLUTION ||
                    id == GameOptions.IDs.GAME_RATS_UPGRADE ||
                    id == GameOptions.IDs.GAME_SKELETONS_UPGRADE ||
                    id == GameOptions.IDs.GAME_SHAMBLERS_UPGRADE)
                    menuEntries[i] += " -V";
                else if (id == GameOptions.IDs.GAME_ZOMBIFICATION_CHANCE ||
                    id == GameOptions.IDs.GAME_STARVED_ZOMBIFICATION_CHANCE)
                    menuEntries[i] += " =S";
            }

            bool loop = true;
            int selected = 0;
            char[] newlines = { '\n' };  // alpha10
            char[] spaces = { ' ' }; // alpha10
            do
            {
                for (int i = 0; i < list.Length; i++)
                    values[i] = s_Options.DescribeValue(m_Session.GameMode, list[i]);

                int gx, gy;
                gx = gy = 0;
                m_UI.UI_Clear(Color.Black);
                DrawHeader();
                gy += BOLD_LINE_SPACING;
                m_UI.UI_DrawStringBold(Color.Yellow, "Options", 0, gy);  // alpha10 dont mention current mode
                gy += 2 * BOLD_LINE_SPACING;
                DrawMenuOrOptions(selected, Color.White, menuEntries, Color.LightGreen, values, gx, ref gy, false, 400);

                // alpha10
                // describe current option.
                gy += BOLD_LINE_SPACING;
                m_UI.UI_DrawStringBold(Color.White, menuEntries[selected].TrimStart(spaces), gx, gy);
                gy += BOLD_LINE_SPACING;
                string desc = GameOptions.Describe(list[selected]);
                string[] descLines = desc.Split(newlines);
                foreach (string d in descLines)
                {
                    m_UI.UI_DrawString(Color.White, "  " + d, gx, gy);
                    gy += BOLD_LINE_SPACING;
                }

                // legend.
                gy += BOLD_LINE_SPACING;
                m_UI.UI_DrawStringBold(Color.Red, "* Caution : increasing these values makes the game runs slower and saving/loading longer.", gx, gy);
                gy += BOLD_LINE_SPACING;
                m_UI.UI_DrawStringBold(Color.White, "-V : option always OFF when playing VTG-Vintage", gx, gy);
                gy += BOLD_LINE_SPACING;
                m_UI.UI_DrawStringBold(Color.White, "=S : option used only when playing STD-Standard", gx, gy);
                gy += BOLD_LINE_SPACING;

                // difficulty rating.
                gy += BOLD_LINE_SPACING;
                int diffForSurvivor = (int)(100 * Scoring.ComputeDifficultyRating(s_Options, DifficultySide.FOR_SURVIVOR, 0));
                int diffforUndead = (int)(100 * Scoring.ComputeDifficultyRating(s_Options, DifficultySide.FOR_UNDEAD, 0));
                m_UI.UI_DrawStringBold(Color.Yellow, String.Format("Difficulty Rating : {0}% as survivor / {1}% as undead.", diffForSurvivor, diffforUndead), gx, gy);
                gy += BOLD_LINE_SPACING;
                m_UI.UI_DrawStringBold(Color.White, "Difficulty used for scoring automatically decrease with each reincarnation.", gx, gy);
                gy += 2 * BOLD_LINE_SPACING;

                // footnote.
                DrawFootnote(Color.White, "cursor to move and change values, R to restore previous values, ESC to save and leave");
                m_UI.UI_Repaint();

                // handle
                // get menu action.
                KeyEventArgs key = m_UI.UI_WaitKey();
                switch (key.KeyCode)
                {
                    case Keys.Up:       // move up
                        if (selected > 0) --selected;
                        else selected = menuEntries.Length - 1;
                        break;
                    case Keys.Down:     // move down
                        selected = (selected + 1) % menuEntries.Length;
                        break;

                    case Keys.R:        // restore previous.
                        s_Options = prevOptions;
                        break;

                    case Keys.Escape:   // validate and leave
                        loop = false;
                        break;

                    case Keys.Left:
                        switch ((GameOptions.IDs)list[selected])
                        {
                            case GameOptions.IDs.GAME_DISTRICT_SIZE: s_Options.DistrictSize -= 5; break;
                            case GameOptions.IDs.UI_MUSIC: s_Options.PlayMusic = !s_Options.PlayMusic; break;
                            case GameOptions.IDs.UI_MUSIC_VOLUME: s_Options.MusicVolume -= 5; break;
                            case GameOptions.IDs.UI_ANIM_DELAY: s_Options.IsAnimDelayOn = !s_Options.IsAnimDelayOn; break;
                            case GameOptions.IDs.UI_SHOW_MINIMAP: s_Options.IsMinimapOn = !s_Options.IsMinimapOn; break;
                            case GameOptions.IDs.UI_SHOW_PLAYER_TAG_ON_MINIMAP: s_Options.ShowPlayerTagsOnMinimap = !s_Options.ShowPlayerTagsOnMinimap; break;
                            case GameOptions.IDs.UI_ADVISOR: s_Options.IsAdvisorEnabled = !s_Options.IsAdvisorEnabled; break;
                            case GameOptions.IDs.UI_COMBAT_ASSISTANT: s_Options.IsCombatAssistantOn = !s_Options.IsCombatAssistantOn; break;
                            case GameOptions.IDs.UI_SHOW_TARGETS: s_Options.ShowTargets = !s_Options.ShowTargets; break;
                            case GameOptions.IDs.UI_SHOW_PLAYER_TARGETS: s_Options.ShowPlayerTargets = !s_Options.ShowPlayerTargets; break;
                            case GameOptions.IDs.GAME_MAX_CIVILIANS: s_Options.MaxCivilians -= 5; break;
                            case GameOptions.IDs.GAME_MAX_DOGS: --s_Options.MaxDogs; break;
                            case GameOptions.IDs.GAME_MAX_UNDEADS: s_Options.MaxUndeads -= 10; break;
                            case GameOptions.IDs.GAME_DAY_ZERO_UNDEADS_PERCENT: s_Options.DayZeroUndeadsPercent -= 5; break;
                            case GameOptions.IDs.GAME_ZOMBIE_INVASION_DAILY_INCREASE: --s_Options.ZombieInvasionDailyIncrease; break;
                            case GameOptions.IDs.GAME_CITY_SIZE: s_Options.CitySize -= 1; break;
                            case GameOptions.IDs.GAME_NPC_CAN_STARVE_TO_DEATH: s_Options.NPCCanStarveToDeath = !s_Options.NPCCanStarveToDeath; break;
                            case GameOptions.IDs.GAME_STARVED_ZOMBIFICATION_CHANCE: s_Options.StarvedZombificationChance -= 5; break;
                            case GameOptions.IDs.GAME_SIMULATE_DISTRICTS:
                                if (s_Options.SimulateDistricts != GameOptions.SimRatio.OFF)
                                    s_Options.SimulateDistricts = (GameOptions.SimRatio)(s_Options.SimulateDistricts - 1);
                                break;
                            case GameOptions.IDs.GAME_SIMULATE_SLEEP: s_Options.SimulateWhenSleeping = !s_Options.SimulateWhenSleeping; break;
                            case GameOptions.IDs.GAME_SIM_THREAD: s_Options.SimThread = !s_Options.SimThread; break;
                            case GameOptions.IDs.GAME_ZOMBIFICATION_CHANCE: s_Options.ZombificationChance -= 5; break;
                            case GameOptions.IDs.GAME_REVEAL_STARTING_DISTRICT: s_Options.RevealStartingDistrict = !s_Options.RevealStartingDistrict; break;
                            case GameOptions.IDs.GAME_ALLOW_UNDEADS_EVOLUTION: s_Options.AllowUndeadsEvolution = !s_Options.AllowUndeadsEvolution; break;
                            case GameOptions.IDs.GAME_UNDEADS_UPGRADE_DAYS:
                                if (s_Options.ZombifiedsUpgradeDays != GameOptions.ZupDays._FIRST)
                                    s_Options.ZombifiedsUpgradeDays = (GameOptions.ZupDays)(s_Options.ZombifiedsUpgradeDays - 1);
                                break;
                            case GameOptions.IDs.GAME_MAX_REINCARNATIONS: --s_Options.MaxReincarnations; break;
                            case GameOptions.IDs.GAME_REINCARNATE_AS_RAT: s_Options.CanReincarnateAsRat = !s_Options.CanReincarnateAsRat; break;
                            case GameOptions.IDs.GAME_REINCARNATE_TO_SEWERS: s_Options.CanReincarnateToSewers = !s_Options.CanReincarnateToSewers; break;
                            case GameOptions.IDs.GAME_REINC_LIVING_RESTRICTED: s_Options.IsLivingReincRestricted = !s_Options.IsLivingReincRestricted; break;
                            case GameOptions.IDs.GAME_PERMADEATH: s_Options.IsPermadeathOn = !s_Options.IsPermadeathOn; break;
                            case GameOptions.IDs.GAME_DEATH_SCREENSHOT: s_Options.IsDeathScreenshotOn = !s_Options.IsDeathScreenshotOn; break;
                            case GameOptions.IDs.GAME_AGGRESSIVE_HUNGRY_CIVILIANS: s_Options.IsAggressiveHungryCiviliansOn = !s_Options.IsAggressiveHungryCiviliansOn; break;
                            case GameOptions.IDs.GAME_NATGUARD_FACTOR: s_Options.NatGuardFactor -= 10; break;
                            case GameOptions.IDs.GAME_SUPPLIESDROP_FACTOR: s_Options.SuppliesDropFactor -= 10; break;
                            case GameOptions.IDs.GAME_RATS_UPGRADE:
                                s_Options.RatsUpgrade = !s_Options.RatsUpgrade;
                                break;
                            case GameOptions.IDs.GAME_SHAMBLERS_UPGRADE:
                                s_Options.ShamblersUpgrade = !s_Options.ShamblersUpgrade;
                                break;
                            case GameOptions.IDs.GAME_SKELETONS_UPGRADE:
                                s_Options.SkeletonsUpgrade = !s_Options.SkeletonsUpgrade;
                                break;
                            case GameOptions.IDs.GAME_AUTOSAVE_PERIOD: // alpha10.1
                                s_Options.AutoSavePeriodInHours -= 12;
                                break;
                        }
                        break;
                    case Keys.Right:
                        switch ((GameOptions.IDs)list[selected])
                        {
                            case GameOptions.IDs.GAME_DISTRICT_SIZE: s_Options.DistrictSize += 5; break;
                            case GameOptions.IDs.UI_MUSIC: s_Options.PlayMusic = !s_Options.PlayMusic; break;
                            case GameOptions.IDs.UI_MUSIC_VOLUME: s_Options.MusicVolume += 5; break;
                            case GameOptions.IDs.UI_ANIM_DELAY: s_Options.IsAnimDelayOn = !s_Options.IsAnimDelayOn; break;
                            case GameOptions.IDs.UI_SHOW_MINIMAP: s_Options.IsMinimapOn = !s_Options.IsMinimapOn; break;
                            case GameOptions.IDs.UI_SHOW_PLAYER_TAG_ON_MINIMAP: s_Options.ShowPlayerTagsOnMinimap = !s_Options.ShowPlayerTagsOnMinimap; break;
                            case GameOptions.IDs.UI_ADVISOR: s_Options.IsAdvisorEnabled = !s_Options.IsAdvisorEnabled; break;
                            case GameOptions.IDs.UI_COMBAT_ASSISTANT: s_Options.IsCombatAssistantOn = !s_Options.IsCombatAssistantOn; break;
                            case GameOptions.IDs.UI_SHOW_TARGETS: s_Options.ShowTargets = !s_Options.ShowTargets; break;
                            case GameOptions.IDs.UI_SHOW_PLAYER_TARGETS: s_Options.ShowPlayerTargets = !s_Options.ShowPlayerTargets; break;
                            case GameOptions.IDs.GAME_MAX_CIVILIANS: s_Options.MaxCivilians += 5; break;
                            case GameOptions.IDs.GAME_MAX_DOGS: ++s_Options.MaxDogs; break;
                            case GameOptions.IDs.GAME_MAX_UNDEADS: s_Options.MaxUndeads += 10; break;
                            case GameOptions.IDs.GAME_DAY_ZERO_UNDEADS_PERCENT: s_Options.DayZeroUndeadsPercent += 5; break;
                            case GameOptions.IDs.GAME_ZOMBIE_INVASION_DAILY_INCREASE: ++s_Options.ZombieInvasionDailyIncrease; break;
                            case GameOptions.IDs.GAME_CITY_SIZE: s_Options.CitySize += 1; break;
                            case GameOptions.IDs.GAME_NPC_CAN_STARVE_TO_DEATH: s_Options.NPCCanStarveToDeath = !s_Options.NPCCanStarveToDeath; break;
                            case GameOptions.IDs.GAME_STARVED_ZOMBIFICATION_CHANCE: s_Options.StarvedZombificationChance += 5; break;
                            case GameOptions.IDs.GAME_SIMULATE_DISTRICTS:
                                if (s_Options.SimulateDistricts != GameOptions.SimRatio.FULL)
                                {
                                    s_Options.SimulateDistricts = (GameOptions.SimRatio)(s_Options.SimulateDistricts + 1);
                                }
                                break;
                            case GameOptions.IDs.GAME_SIMULATE_SLEEP: s_Options.SimulateWhenSleeping = !s_Options.SimulateWhenSleeping; break;
                            case GameOptions.IDs.GAME_SIM_THREAD: s_Options.SimThread = !s_Options.SimThread; break;
                            case GameOptions.IDs.GAME_ZOMBIFICATION_CHANCE: s_Options.ZombificationChance += 5; break;
                            case GameOptions.IDs.GAME_REVEAL_STARTING_DISTRICT: s_Options.RevealStartingDistrict = !s_Options.RevealStartingDistrict; break;
                            case GameOptions.IDs.GAME_ALLOW_UNDEADS_EVOLUTION:
                                s_Options.AllowUndeadsEvolution = !s_Options.AllowUndeadsEvolution;
                                break;
                            case GameOptions.IDs.GAME_UNDEADS_UPGRADE_DAYS:
                                if (s_Options.ZombifiedsUpgradeDays != GameOptions.ZupDays._COUNT - 1)
                                    s_Options.ZombifiedsUpgradeDays = (GameOptions.ZupDays)(s_Options.ZombifiedsUpgradeDays + 1);
                                break;
                            case GameOptions.IDs.GAME_MAX_REINCARNATIONS: ++s_Options.MaxReincarnations; break;
                            case GameOptions.IDs.GAME_REINCARNATE_AS_RAT: s_Options.CanReincarnateAsRat = !s_Options.CanReincarnateAsRat; break;
                            case GameOptions.IDs.GAME_REINCARNATE_TO_SEWERS: s_Options.CanReincarnateToSewers = !s_Options.CanReincarnateToSewers; break;
                            case GameOptions.IDs.GAME_REINC_LIVING_RESTRICTED: s_Options.IsLivingReincRestricted = !s_Options.IsLivingReincRestricted; break;
                            case GameOptions.IDs.GAME_PERMADEATH: s_Options.IsPermadeathOn = !s_Options.IsPermadeathOn; break;
                            case GameOptions.IDs.GAME_DEATH_SCREENSHOT: s_Options.IsDeathScreenshotOn = !s_Options.IsDeathScreenshotOn; break;
                            case GameOptions.IDs.GAME_AGGRESSIVE_HUNGRY_CIVILIANS: s_Options.IsAggressiveHungryCiviliansOn = !s_Options.IsAggressiveHungryCiviliansOn; break;
                            case GameOptions.IDs.GAME_NATGUARD_FACTOR: s_Options.NatGuardFactor += 10; break;
                            case GameOptions.IDs.GAME_SUPPLIESDROP_FACTOR: s_Options.SuppliesDropFactor += 10; break;
                            case GameOptions.IDs.GAME_RATS_UPGRADE: s_Options.RatsUpgrade = !s_Options.RatsUpgrade; break;
                            case GameOptions.IDs.GAME_SHAMBLERS_UPGRADE: s_Options.ShamblersUpgrade = !s_Options.ShamblersUpgrade; break;
                            case GameOptions.IDs.GAME_SKELETONS_UPGRADE: s_Options.SkeletonsUpgrade = !s_Options.SkeletonsUpgrade; break;
                            case GameOptions.IDs.GAME_AUTOSAVE_PERIOD: // alpha10.1
                                s_Options.AutoSavePeriodInHours += 12;
                                break;
                        }
                        break;
                }

                // force some options combinations.
                if (s_Options.SimThread)
                    s_Options.SimulateWhenSleeping = false;
                // apply options.
                ApplyOptions(false);
            }
            while (loop);

            // save.
            SaveOptions();
        }

        void HandleRedefineKeys()
        {
            bool loop = true;
            int selected = 0;
            bool conflict = false;
            do
            {
                // check for conflict.
                conflict = s_KeyBindings.CheckForConflict();

                // draw
                string[] menuEntries = new string[]
                {
                    "Move N",
                    "Move NE",
                    "Move E",
                    "Move SE",
                    "Move S",
                    "Move SW",
                    "Move W",
                    "Move NW",
                    "Wait",
                    "Wait 1 hour",
                    "Abandon Game",
                    "Advisor Hint",
                    "Barricade",
                    "Break",
                    "Build Large Fortification",
                    "Build Small Fortification",
                    "City Info",
                    "Close",
                    "Fire",
                    "Give",
                    "Help",
                    "Hints screen",
                    "Negociate Trade",
                    "Item 1 slot",
                    "Item 2 slot",
                    "Item 3 slot",
                    "Item 4 slot",
                    "Item 5 slot",
                    "Item 6 slot",
                    "Item 7 slot",
                    "Item 8 slot",
                    "Item 9 slot",
                    "Item 10 slot",
                    "Lead",
                    "Load Game",
                    "Mark Enemies",
                    "Messages Log",
                    "Options",
                    "Order",
                    "Pull",  // alpha10
                    "Push",
                    "Quit Game",
                    "Redefine Keys",
                    "Run",
                    "Save Game",
                    "Screenshot",
                    "Shout",
                    "Sleep",
                    "Switch Place",
                    "Use Exit",
                    "Use Spray",
                };
                const int O_MOVE_N = 0;
                const int O_MOVE_NE = 1;
                const int O_MOVE_E = 2;
                const int O_MOVE_SE = 3;
                const int O_MOVE_S = 4;
                const int O_MOVE_SW = 5;
                const int O_MOVE_W = 6;
                const int O_MOVE_NW = 7;
                const int O_WAIT = 8;
                const int O_WAIT_LONG = 9;
                const int O_ABANDON = 10;
                const int O_ADVISOR = 11;
                const int O_BARRICADE = 12;
                const int O_BREAK = 13;
                const int O_BUILD_LARGE_F = 14;
                const int O_BUILD_SMALL_F = 15;
                const int O_CITYINFO = 16;
                const int O_CLOSE = 17;
                const int O_FIRE = 18;
                const int O_GIVE = 19;
                const int O_HELP = 20;
                const int O_HINTS_SCREEN = 21;
                const int O_INIT_TRADE = 22;
                const int O_ITEM_1 = 23;
                const int O_ITEM_2 = 24;
                const int O_ITEM_3 = 25;
                const int O_ITEM_4 = 26;
                const int O_ITEM_5 = 27;
                const int O_ITEM_6 = 28;
                const int O_ITEM_7 = 29;
                const int O_ITEM_8 = 30;
                const int O_ITEM_9 = 31;
                const int O_ITEM_10 = 32;
                const int O_LEAD = 33;
                const int O_LOAD = 34;
                const int O_MARKENEMY = 35;
                const int O_LOG = 36;
                const int O_OPTIONS = 37;
                const int O_ORDER = 38;
                const int O_PULL = 39;  // alpha10 inserted and shifted all below
                const int O_PUSH = 40;
                const int O_QUIT = 41;
                const int O_REDEFKEYS = 42;
                const int O_RUN = 43;
                const int O_SAVE = 44;
                const int O_SCREENSHOT = 45;
                const int O_SHOUT = 46;
                const int O_SLEEP = 47;
                const int O_SWITCH = 48;
                const int O_USE_EXIT = 49;
                const int O_USE_SPRAY = 50;
                string[] values = new string[]
                {
                    s_KeyBindings.Get(PlayerCommand.MOVE_N).ToString(),
                    s_KeyBindings.Get(PlayerCommand.MOVE_NE).ToString(),
                    s_KeyBindings.Get(PlayerCommand.MOVE_E).ToString(),
                    s_KeyBindings.Get(PlayerCommand.MOVE_SE).ToString(),
                    s_KeyBindings.Get(PlayerCommand.MOVE_S).ToString(),
                    s_KeyBindings.Get(PlayerCommand.MOVE_SW).ToString(),
                    s_KeyBindings.Get(PlayerCommand.MOVE_W).ToString(),
                    s_KeyBindings.Get(PlayerCommand.MOVE_NW).ToString(),
                    s_KeyBindings.Get(PlayerCommand.WAIT_OR_SELF).ToString(),
                    s_KeyBindings.Get(PlayerCommand.WAIT_LONG).ToString(),
                    s_KeyBindings.Get(PlayerCommand.ABANDON_GAME).ToString(),
                    s_KeyBindings.Get(PlayerCommand.ADVISOR).ToString(),
                    s_KeyBindings.Get(PlayerCommand.BARRICADE_MODE).ToString(),
                    s_KeyBindings.Get(PlayerCommand.BREAK_MODE).ToString(),
                    s_KeyBindings.Get(PlayerCommand.BUILD_LARGE_FORTIFICATION).ToString(),
                    s_KeyBindings.Get(PlayerCommand.BUILD_SMALL_FORTIFICATION).ToString(),
                    s_KeyBindings.Get(PlayerCommand.CITY_INFO).ToString(),
                    s_KeyBindings.Get(PlayerCommand.CLOSE_DOOR).ToString(),
                    s_KeyBindings.Get(PlayerCommand.FIRE_MODE).ToString(),
                    s_KeyBindings.Get(PlayerCommand.GIVE_ITEM).ToString(),
                    s_KeyBindings.Get(PlayerCommand.HELP_MODE).ToString(),
                    s_KeyBindings.Get(PlayerCommand.HINTS_SCREEN_MODE).ToString(),
                    s_KeyBindings.Get(PlayerCommand.NEGOCIATE_TRADE).ToString(),
                    s_KeyBindings.Get(PlayerCommand.ITEM_SLOT_0).ToString(),
                    s_KeyBindings.Get(PlayerCommand.ITEM_SLOT_1).ToString(),
                    s_KeyBindings.Get(PlayerCommand.ITEM_SLOT_2).ToString(),
                    s_KeyBindings.Get(PlayerCommand.ITEM_SLOT_3).ToString(),
                    s_KeyBindings.Get(PlayerCommand.ITEM_SLOT_4).ToString(),
                    s_KeyBindings.Get(PlayerCommand.ITEM_SLOT_5).ToString(),
                    s_KeyBindings.Get(PlayerCommand.ITEM_SLOT_6).ToString(),
                    s_KeyBindings.Get(PlayerCommand.ITEM_SLOT_7).ToString(),
                    s_KeyBindings.Get(PlayerCommand.ITEM_SLOT_8).ToString(),
                    s_KeyBindings.Get(PlayerCommand.ITEM_SLOT_9).ToString(),
                    s_KeyBindings.Get(PlayerCommand.LEAD_MODE).ToString(),
                    s_KeyBindings.Get(PlayerCommand.LOAD_GAME).ToString(),
                    s_KeyBindings.Get(PlayerCommand.MARK_ENEMIES_MODE).ToString(),
                    s_KeyBindings.Get(PlayerCommand.MESSAGE_LOG).ToString(),
                    s_KeyBindings.Get(PlayerCommand.OPTIONS_MODE).ToString(),
                    s_KeyBindings.Get(PlayerCommand.ORDER_MODE).ToString(),
                    s_KeyBindings.Get(PlayerCommand.PULL_MODE).ToString(), // alpha10
                    s_KeyBindings.Get(PlayerCommand.PUSH_MODE).ToString(),
                    s_KeyBindings.Get(PlayerCommand.QUIT_GAME).ToString(),
                    s_KeyBindings.Get(PlayerCommand.KEYBINDING_MODE).ToString(),
                    s_KeyBindings.Get(PlayerCommand.RUN_TOGGLE).ToString(),
                    s_KeyBindings.Get(PlayerCommand.SAVE_GAME).ToString(),
                    s_KeyBindings.Get(PlayerCommand.SCREENSHOT).ToString(),
                    s_KeyBindings.Get(PlayerCommand.SHOUT).ToString(),
                    s_KeyBindings.Get(PlayerCommand.SLEEP).ToString(),
                    s_KeyBindings.Get(PlayerCommand.SWITCH_PLACE).ToString(),
                    s_KeyBindings.Get(PlayerCommand.USE_EXIT).ToString(),
                    s_KeyBindings.Get(PlayerCommand.USE_SPRAY).ToString(),
                };

                int gx, gy;
                gx = gy = 0;
                m_UI.UI_Clear(Color.Black);
                DrawHeader();
                gy += BOLD_LINE_SPACING;
                m_UI.UI_DrawStringBold(Color.Yellow, "Redefine keys", 0, gy);
                gy += BOLD_LINE_SPACING;
                DrawMenuOrOptions(selected, Color.White, menuEntries, Color.LightGreen, values, gx, ref gy);
                if (conflict)
                {
                    m_UI.UI_DrawStringBold(Color.Red, "Conflicting keys. Please redefine the keys so the commands don't overlap.", gx, gy);
                    gy += BOLD_LINE_SPACING;
                }
                DrawFootnote(Color.White, "cursor to move, ENTER to rebind a key, ESC to save and leave");
                m_UI.UI_Repaint();

                // handle
                // get menu action.
                KeyEventArgs key = m_UI.UI_WaitKey();
                switch (key.KeyCode)
                {
                    case Keys.Up:       // move up
                        if (selected > 0) --selected;
                        else selected = menuEntries.Length - 1;
                        break;
                    case Keys.Down:     // move down
                        selected = (selected + 1) % menuEntries.Length;
                        break;

                    case Keys.Escape:   // leave.
                        if (!conflict)
                        {
                            loop = false;
                        }
                        break;

                    case Keys.Enter: // rebind
                        // say.
                        m_UI.UI_DrawStringBold(Color.Yellow, String.Format("rebinding {0}, press the new key.", menuEntries[selected]), gx, gy);
                        m_UI.UI_Repaint();

                        // read new key.
                        bool loopNewKey = true;
                        Keys newKeyData = Keys.None;
                        do
                        {
                            KeyEventArgs newKey = m_UI.UI_WaitKey();
                            // ignore Shift and Control alone.
                            if (newKey.KeyCode == Keys.ShiftKey || newKey.KeyCode == Keys.ControlKey)
                                continue;
                            // always ignore Alt.
                            if (newKey.Alt)
                                continue;
                            // done!
                            newKeyData = newKey.KeyData;
                            loopNewKey = false;
                        }
                        while (loopNewKey);

                        // get command.
                        PlayerCommand command;
                        switch (selected)
                        {
                            case O_MOVE_N: command = PlayerCommand.MOVE_N; break;
                            case O_MOVE_NE: command = PlayerCommand.MOVE_NE; break;
                            case O_MOVE_E: command = PlayerCommand.MOVE_E; break;
                            case O_MOVE_SE: command = PlayerCommand.MOVE_SE; break;
                            case O_MOVE_S: command = PlayerCommand.MOVE_S; break;
                            case O_MOVE_SW: command = PlayerCommand.MOVE_SW; break;
                            case O_MOVE_W: command = PlayerCommand.MOVE_W; break;
                            case O_MOVE_NW: command = PlayerCommand.MOVE_NW; break;
                            case O_WAIT: command = PlayerCommand.WAIT_OR_SELF; break;
                            case O_WAIT_LONG: command = PlayerCommand.WAIT_LONG; break;
                            case O_ABANDON: command = PlayerCommand.ABANDON_GAME; break;
                            case O_ADVISOR: command = PlayerCommand.ADVISOR; break;
                            case O_BARRICADE: command = PlayerCommand.BARRICADE_MODE; break;
                            case O_BREAK: command = PlayerCommand.BREAK_MODE; break;
                            case O_BUILD_LARGE_F: command = PlayerCommand.BUILD_LARGE_FORTIFICATION; break;
                            case O_BUILD_SMALL_F: command = PlayerCommand.BUILD_SMALL_FORTIFICATION; break;
                            case O_CITYINFO: command = PlayerCommand.CITY_INFO; break;
                            case O_CLOSE: command = PlayerCommand.CLOSE_DOOR; break;
                            case O_FIRE: command = PlayerCommand.FIRE_MODE; break;
                            case O_GIVE: command = PlayerCommand.GIVE_ITEM; break;
                            case O_HELP: command = PlayerCommand.HELP_MODE; break;
                            case O_HINTS_SCREEN: command = PlayerCommand.HINTS_SCREEN_MODE; break;
                            case O_INIT_TRADE: command = PlayerCommand.NEGOCIATE_TRADE; break;
                            case O_ITEM_1: command = PlayerCommand.ITEM_SLOT_0; break;
                            case O_ITEM_2: command = PlayerCommand.ITEM_SLOT_1; break;
                            case O_ITEM_3: command = PlayerCommand.ITEM_SLOT_2; break;
                            case O_ITEM_4: command = PlayerCommand.ITEM_SLOT_3; break;
                            case O_ITEM_5: command = PlayerCommand.ITEM_SLOT_4; break;
                            case O_ITEM_6: command = PlayerCommand.ITEM_SLOT_5; break;
                            case O_ITEM_7: command = PlayerCommand.ITEM_SLOT_6; break;
                            case O_ITEM_8: command = PlayerCommand.ITEM_SLOT_7; break;
                            case O_ITEM_9: command = PlayerCommand.ITEM_SLOT_8; break;
                            case O_ITEM_10: command = PlayerCommand.ITEM_SLOT_9; break;
                            case O_LEAD: command = PlayerCommand.LEAD_MODE; break;
                            case O_LOAD: command = PlayerCommand.LOAD_GAME; break;
                            case O_MARKENEMY: command = PlayerCommand.MARK_ENEMIES_MODE; break;
                            case O_LOG: command = PlayerCommand.MESSAGE_LOG; break;
                            case O_OPTIONS: command = PlayerCommand.OPTIONS_MODE; break;
                            case O_ORDER: command = PlayerCommand.ORDER_MODE; break;
                            case O_PULL: command = PlayerCommand.PULL_MODE; break;  // alpha10
                            case O_PUSH: command = PlayerCommand.PUSH_MODE; break;
                            case O_QUIT: command = PlayerCommand.QUIT_GAME; break;
                            case O_REDEFKEYS: command = PlayerCommand.KEYBINDING_MODE; break;
                            case O_RUN: command = PlayerCommand.RUN_TOGGLE; break;
                            case O_SAVE: command = PlayerCommand.SAVE_GAME; break;
                            case O_SCREENSHOT: command = PlayerCommand.SCREENSHOT; break;
                            case O_SHOUT: command = PlayerCommand.SHOUT; break;
                            case O_SLEEP: command = PlayerCommand.SLEEP; break;
                            case O_SWITCH: command = PlayerCommand.SWITCH_PLACE; break;
                            case O_USE_EXIT: command = PlayerCommand.USE_EXIT; break;
                            case O_USE_SPRAY: command = PlayerCommand.USE_SPRAY; break;
                            default:
                                throw new InvalidOperationException("unhandled selected");
                        }

                        // bind it.
                        s_KeyBindings.Set(command, newKeyData);

                        break;

                }
            }
            while (loop);

            // Save.
            SaveKeybindings();
        }

    }
}
