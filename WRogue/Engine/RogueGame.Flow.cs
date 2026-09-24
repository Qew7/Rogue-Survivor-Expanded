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
        #region Game flow

        /// <summary>
        /// Main loop.
        /// </summary>
        public void Run()
        {
            // first run inits.
            InitDirectories();

            // load data.
            LoadData();

            // load options.
            LoadOptions();

            // load hints.
            LoadHints();

            // apply options.
            ApplyOptions(false);

            // load keys.
            LoadKeybindings();

            // load music & sfxs.
            m_UI.UI_Clear(Color.Black);
            m_UI.UI_DrawStringBold(Color.White, "Loading music...", 0, 0);
            m_UI.UI_Repaint();

            m_MusicManager.Load(GameMusics.ARMY, GameMusics.ARMY_FILE);
            m_MusicManager.Load(GameMusics.BIGBEAR_THEME_SONG, GameMusics.BIGBEAR_THEME_SONG_FILE);
            m_MusicManager.Load(GameMusics.BIKER, GameMusics.BIKER_FILE);
            m_MusicManager.Load(GameMusics.CHAR_UNDERGROUND_FACILITY, GameMusics.CHAR_UNDERGROUND_FACILITY_FILE);
            m_MusicManager.Load(GameMusics.DUCKMAN_THEME_SONG, GameMusics.DUCKMAN_THEME_SONG_FILE);
            m_MusicManager.Load(GameMusics.FAMU_FATARU_THEME_SONG, GameMusics.FAMU_FATARU_THEME_SONG_FILE);
            m_MusicManager.Load(GameMusics.FIGHT, GameMusics.FIGHT_FILE);
            m_MusicManager.Load(GameMusics.GANGSTA, GameMusics.GANGSTA_FILE);
            m_MusicManager.Load(GameMusics.HANS_VON_HANZ_THEME_SONG, GameMusics.HANS_VON_HANZ_THEME_SONG_FILE);
            m_MusicManager.Load(GameMusics.HEYTHERE, GameMusics.HEYTHERE_FILE);
            m_MusicManager.Load(GameMusics.HOSPITAL, GameMusics.HOSPITAL_FILE);
            m_MusicManager.Load(GameMusics.INSANE, GameMusics.INSANE_FILE);
            m_MusicManager.Load(GameMusics.INTERLUDE, GameMusics.INTERLUDE_FILE);
            m_MusicManager.Load(GameMusics.INTRO, GameMusics.INTRO_FILE);
            m_MusicManager.Load(GameMusics.LIMBO, GameMusics.LIMBO_FILE);
            m_MusicManager.Load(GameMusics.PLAYER_DEATH, GameMusics.PLAYER_DEATH_FILE);
            m_MusicManager.Load(GameMusics.REINCARNATE, GameMusics.REINCARNATE_FILE);
            m_MusicManager.Load(GameMusics.ROGUEDJACK_THEME_SONG, GameMusics.ROGUEDJACK_THEME_SONG_FILE);
            m_MusicManager.Load(GameMusics.SANTAMAN_THEME_SONG, GameMusics.SANTAMAN_THEME_SONG_FILE);
            m_MusicManager.Load(GameMusics.SEWERS, GameMusics.SEWERS_FILE);
            m_MusicManager.Load(GameMusics.SLEEP, GameMusics.SLEEP_FILE);
            m_MusicManager.Load(GameMusics.SUBWAY, GameMusics.SUBWAY_FILE);
            m_MusicManager.Load(GameMusics.SURVIVORS, GameMusics.SURVIVORS_FILE);
            // alpha10
            m_MusicManager.Load(GameMusics.SURFACE, GameMusics.SURFACE_FILE);

            m_UI.UI_Clear(Color.Black);
            m_UI.UI_DrawStringBold(Color.White, "Loading music... done!", 0, 0);
            m_UI.UI_Repaint();

            m_UI.UI_Clear(Color.Black);
            m_UI.UI_DrawStringBold(Color.White, "Loading sfxs...", 0, 0);
            m_UI.UI_Repaint();

            m_MusicManager.Load(GameSounds.UNDEAD_EAT, GameSounds.UNDEAD_EAT_FILE);
            m_MusicManager.Load(GameSounds.UNDEAD_RISE, GameSounds.UNDEAD_RISE_FILE);
            m_MusicManager.Load(GameSounds.NIGHTMARE, GameSounds.NIGHTMARE_FILE);

            m_UI.UI_Clear(Color.Black);
            m_UI.UI_DrawStringBold(Color.White, "Loading sfxs... done!", 0, 0);
            m_UI.UI_Repaint();


            // load and parse manual.
            LoadManual();

            // load hi score table.
            LoadHiScoreTable();

            // loop.
            while (m_IsGameRunning)
            {
                GameLoop();
            }

            // stop & dispose music.
            m_MusicManager.Stop();
            m_MusicManager.Dispose();

            // quit.
            m_UI.UI_DoQuit();
        }

        void GameLoop()
        {
            // main menu.
            HandleMainMenu();

            // play until player dies or quits.
            while (m_Player != null && !m_Player.IsDead && m_IsGameRunning)
            {
                // timer.
                DateTime timeBefore = DateTime.Now;

                // alpha10
                // roll player charisma for this turn
                m_Session.Player_TurnCharismaRoll = m_Rules.Roll(0, 100);

                // play.
                m_HasLoadedGame = false;
                AdvancePlay(m_Session.CurrentMap.District, SimFlags.NOT_SIMULATING);

                // if quit, don't bother.
                if (!m_IsGameRunning)
                    break;

                // timer.
                DateTime timeAfter = DateTime.Now;
                m_Session.Scoring.RealLifePlayingTime = m_Session.Scoring.RealLifePlayingTime.Add(timeAfter - timeBefore);

                // alpha10
                // check background music every N game hours
                if (m_Session.WorldTime.TurnCounter % BGMUSIC_UPDATE_TURNS == 0)
                    UpdateBgMusic();
            }
        }

        void InitDirectories()
        {
            int gy = 0;
            m_UI.UI_Clear(Color.Black);
            m_UI.UI_DrawStringBold(Color.Yellow, "Checking user game directories...", 0, gy);
            gy += BOLD_LINE_SPACING;
            m_UI.UI_Repaint();

            ///////////////////////
            // Create directories.
            //////////////////////
            bool created = false;
            created |= CheckDirectory(GetUserBasePath(), "base user", ref gy);
            created |= CheckDirectory(GetUserConfigPath(), "config", ref gy);
            created |= CheckDirectory(GetUserDocsPath(), "docs", ref gy);
            created |= CheckDirectory(GetUserGraveyardPath(), "graveyard", ref gy);
            created |= CheckDirectory(GetUserSavesPath(), "saves", ref gy);
            created |= CheckDirectory(GetUserScreenshotsPath(), "screenshots", ref gy);

            //////////////////
            // Copying manual.
            //////////////////
            created |= CheckCopyOfManual();

            if (created)
            {
                m_UI.UI_DrawStringBold(Color.Yellow, "Directories and game manual created.", 0, gy);
                gy += BOLD_LINE_SPACING;
                m_UI.UI_DrawStringBold(Color.Yellow, "Your game data directory is in the game folder:", 0, gy);
                gy += BOLD_LINE_SPACING;
                m_UI.UI_DrawString(Color.LightGreen, GetUserBasePath(), 0, gy);
                gy += BOLD_LINE_SPACING;
                m_UI.UI_DrawStringBold(Color.Yellow, "When you uninstall the game you can delete this directory.", 0, gy);
                gy += BOLD_LINE_SPACING;
                DrawFootnote(Color.White, "press ENTER");
                m_UI.UI_Repaint();
                Logger.WriteLine(Logger.Stage.RUN_MAIN, "directory setup ready for confirmation");
                WaitEnter();
            }
        }

        void HandleMainMenu()
        {
            bool loop = true;
            bool menuReadyLogged = false;
            bool isLoadEnabled = File.Exists(GetUserSave());

            string[] menuEntries = new string[] {
                "New Game",                                     // 0
                isLoadEnabled ?  "Load Game" : "(Load Game)",   // 1
                "Redefine keys",                                // 2
                "Options",                                      // 3
                "Game Manual",                                  // 4
                "All Hints",                                    // 5
                "Hi Scores",                                    // 6
                "Credits",                                      // 7
                "Quit Game" };                                  // 8
            int selected = 0;
            do
            {
                // music.
                if (!m_PlayedIntro)
                {
                    m_MusicManager.Stop();
                    m_MusicManager.Play(GameMusics.INTRO, MusicPriority.PRIORITY_EVENT);
                    m_PlayedIntro = true;
                }

                // display.
                int gx, gy;
                gx = gy = 0;
                m_UI.UI_Clear(Color.Black);
                DrawHeader();
                gy += BOLD_LINE_SPACING;
                m_UI.UI_DrawStringBold(Color.Yellow, "Main Menu", 0, gy);
                gy += 2 * BOLD_LINE_SPACING;
                DrawMenuOrOptions(selected, Color.White, menuEntries, Color.White, null, gx, ref gy);
                DrawFootnote(Color.White, "cursor to move, ENTER to select");

                // christmas special.
                DateTime dateNow = DateTime.Now;
                if (dateNow.Month == 12 && dateNow.Day >= 24 && dateNow.Day <= 26)
                {
                    const int NB_SANTAS = 10;
                    for (int i = 0; i < NB_SANTAS; i++)
                    {
                        int santax = m_Rules.Roll(0, 1024);
                        int santay = m_Rules.Roll(0, 768);
                        m_UI.UI_DrawImage(GameImages.ACTOR_SANTAMAN, santax, santay);
                        m_UI.UI_DrawStringBold(Color.Snow, "* Merry Christmas *", santax - 60, santay - 10);
                    }
                }

                // repaint.
                m_UI.UI_Repaint();
                if (!menuReadyLogged)
                {
                    Logger.WriteLine(Logger.Stage.RUN_MAIN, "main menu ready");
                    menuReadyLogged = true;
                }

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

                    case Keys.Enter:    // validate
                        {
                            switch (selected)
                            {
                                case 0:
                                    if (HandleNewCharacter())
                                    {
                                        StartNewGame();
                                        loop = false;
                                    }
                                    break;

                                case 1:
                                    if (!isLoadEnabled)
                                        break;
                                    gy += 2 * BOLD_LINE_SPACING;
                                    m_UI.UI_DrawStringBold(Color.Yellow, "Loading game, please wait...", gx, gy);
                                    m_UI.UI_Repaint();
                                    LoadGame(GetUserSave());
                                    loop = false;
                                    // alpha10
                                    if (s_Options.IsSimON && s_Options.SimThread)
                                        StartSimThread();
                                    break;

                                case 2:
                                    HandleRedefineKeys();
                                    break;

                                case 3:
                                    HandleOptions(false);
                                    ApplyOptions(false);
                                    break;

                                case 4:
                                    HandleHelpMode();
                                    break;

                                case 5:
                                    HandleHintsScreen();
                                    break;

                                case 6:
                                    HandleHiScores(true);
                                    break;

                                case 7:
                                    HandleCredits();
                                    break;

                                case 8:
                                    m_IsGameRunning = false;
                                    loop = false;
                                    break;

                                default:
                                    break;
                            } // switch selected
                            break;
                        }
                }
            }
            while (loop);
        }




        void StartNewGame()
        {
            bool isUndead = m_CharGen.IsUndead;

            // generate world.
            GenerateWorld(true, s_Options.CitySize);

            // scoring : hello there.
            m_Session.Scoring.AddVisit(m_Session.WorldTime.TurnCounter, m_Player.Location.Map);
            m_Session.Scoring.AddEvent(m_Session.WorldTime.TurnCounter, String.Format(isUndead ? "Rose in {0}." : "Woke up in {0}.", m_Player.Location.Map.Name));

            // setup proper scoring mode.
            m_Session.Scoring.Side = (isUndead ? DifficultySide.FOR_UNDEAD : DifficultySide.FOR_SURVIVOR);

            // alpha10.1
            // schedule first autosave.
            ScheduleNextAutoSave();

            // advisor on?
            // alpha10 not if undead
            if (s_Options.IsAdvisorEnabled)
            {
                ClearMessages();
                ClearMessagesHistory();
                if (m_Player.Model.Abilities.IsUndead)
                {
                    AddMessage(new Message("The Advisor is enabled but you will get no hint when playing undead.", 0, Color.Red));
                }
                else
                {
                    AddMessage(new Message("The Advisor is enabled and will give you hints during the game.", 0, Color.LightGreen));
                    AddMessage(new Message("The hints help a beginner learning the basic controls.", 0, Color.LightGreen));
                    AddMessage(new Message("You can disable the Advisor by going to the Options screen.", 0, Color.LightGreen));
                }
                AddMessage(new Message(String.Format("Press {0} during the game to change the options.", s_KeyBindings.Get(PlayerCommand.OPTIONS_MODE)), 0, Color.LightGreen));
                AddMessage(new Message("<press ENTER>", 0, Color.Yellow));
                RedrawPlayScreen();
                WaitEnter();
            }

            // welcome banner.
            ClearMessages();
            ClearMessagesHistory();
            AddMessage(new Message("*****************************", 0, Color.LightGreen));
            AddMessage(new Message("* Welcome to Rogue Survivor *", 0, Color.LightGreen));
            AddMessage(new Message("* We hope you like Zombies  *", 0, Color.LightGreen));
            AddMessage(new Message("*****************************", 0, Color.LightGreen));
            AddMessage(new Message(String.Format("Press {0} for help", s_KeyBindings.Get(PlayerCommand.HELP_MODE)), 0, Color.LightGreen));
            AddMessage(new Message(String.Format("Press {0} to redefine keys", s_KeyBindings.Get(PlayerCommand.KEYBINDING_MODE)), 0, Color.LightGreen));
            AddMessage(new Message("<press ENTER>", 0, Color.Yellow));
            RefreshPlayer();
            RedrawPlayScreen();
            WaitEnter();

            // wake up!
            ClearMessages();
            AddMessage(new Message(String.Format(isUndead ? "{0} rises..." : "{0} wakes up.", m_Player.Name), 0, Color.White));
            RedrawPlayScreen();

            // alpha10.1 reset/cleanup bot from previous session
#if DEBUG
            if (m_isBotMode)
                BotReleaseControl();
#endif

            // start simulation thread.
            StopSimThread(false);  // alpha10 stop-start
            StartSimThread();
            Logger.WriteLine(Logger.Stage.RUN_MAIN, "new game ready");
        }

        /// <summary>
        /// Advance play in district : could be player district (live district) or simulated district.
        /// </summary>
        /// <param name="district"></param>
        /// <param name="sim"></param>
        void AdvancePlay(District district, SimFlags sim)
        {
            lock (district)  // alpha10 lock district
            {
#if DEBUG_STATS
            Session.UpdateStats(district);
#endif

                // 0. Remember if current district.
                bool wasNight = m_Session.WorldTime.IsNight;
                DayPhase prevPhase = m_Session.WorldTime.Phase;

                // 1. Advance all maps.
                // if player quit/loaded at any time, don't bother!
                #region
                foreach (Map map in district.Maps)
                {
                    int prevLocalTurn = map.LocalTime.TurnCounter;
                    do
                    {
                        // play this map.
                        AdvancePlay(map, sim);
                        // check for reincarnation.
                        if (m_Player.IsDead)
                            HandleReincarnation();
                        // check stopping game.
                        if (!m_IsGameRunning || m_HasLoadedGame || m_Player.IsDead)
                            return;
                    }
                    while (map.LocalTime.TurnCounter == prevLocalTurn);
                }
                #endregion

                // 2. Advance district.
                #region
                // 2.1. Advance world time if current district.
                // alpha10 also check weather change
                #region
                if (district == m_Session.CurrentMap.District)
                {
                    ++m_Session.WorldTime.TurnCounter;

                    // sunrise/sunset.
                    bool canSeeSky = m_Rules.CanActorSeeSky(m_Player);  // alpha10 message ony if can see sky
                    bool isNight = m_Session.WorldTime.IsNight;
                    DayPhase newPhase = m_Session.WorldTime.Phase;
                    if (wasNight && !isNight)
                    {
                        if (canSeeSky) AddMessage(new Message("The sun is rising again for you...", m_Session.WorldTime.TurnCounter, DAY_COLOR));
                        OnNewDay();
                    }
                    else if (!wasNight && isNight)
                    {
                        if (canSeeSky) AddMessage(new Message("Night is falling upon you...", m_Session.WorldTime.TurnCounter, NIGHT_COLOR));
                        OnNewNight();
                    }
                    else if (prevPhase != newPhase)
                    {
                        if (canSeeSky) AddMessage(new Message(String.Format("Time passes, it is now {0}...", DescribeDayPhase(newPhase)), m_Session.WorldTime.TurnCounter, isNight ? NIGHT_COLOR : DAY_COLOR));
                    }


                    // alpha10
                    // if time to change weather do it and roll next change time.
                    if (m_Session.WorldTime.TurnCounter >= m_Session.World.NextWeatherCheckTurn)
                    {
                        ChangeWeather();
                        m_Session.World.NextWeatherCheckTurn = m_Session.WorldTime.TurnCounter + m_Rules.Roll(WEATHER_MIN_DURATION, WEATHER_MAX_DURATION);
                    }
                }
                #endregion

                // 2.2. Check for events.

                #region Entry/Surface map
                // 1 Invasion?
                if (CheckForEvent_ZombieInvasion(district.EntryMap))
                {
                    FireEvent_ZombieInvasion(district.EntryMap);
                }
                // 2 Refugees?
                if (CheckForEvent_RefugeesWave(district.EntryMap))
                {
                    FireEvent_RefugeesWave(district);
                }
                // 3 National guard?
                if (CheckForEvent_NationalGuard(district.EntryMap))
                {
                    FireEvent_NationalGuard(district.EntryMap);
                }
                // 4 Army drop supplies?
                if (CheckForEvent_ArmySupplies(district.EntryMap))
                {
                    FireEvent_ArmySupplies(district.EntryMap);
                }
                // 5 Bikers raid?
                if (CheckForEvent_BikersRaid(district.EntryMap))
                {
                    FireEvent_BikersRaid(district.EntryMap);
                }
                // 6 Gangsta raid?
                if (CheckForEvent_GangstasRaid(district.EntryMap))
                {
                    FireEvent_GangstasRaid(district.EntryMap);
                }
                // 7 Blackops raid?
                if (CheckForEvent_BlackOpsRaid(district.EntryMap))
                {
                    FireEvent_BlackOpsRaid(district.EntryMap);
                }
                // 8 Band of Survivors?
                if (CheckForEvent_BandOfSurvivors(district.EntryMap))
                {
                    FireEvent_BandOfSurvivors(district.EntryMap);
                }
                #endregion

                #region Sewers
                // 1 Sewers Invasion?
                if (CheckForEvent_SewersInvasion(district.SewersMap))
                {
                    FireEvent_SewersInvasion(district.SewersMap);
                }
                #endregion

                #region DISABLED Subway
#if false
            if (district.SubwayMap != null)
            {
                // 1 Subway Invasion?
                if (CheckForEvent_SubwayInvasion(district.SubwayMap))
                {
                    FireEvent_SubwayInvasion(district.SubwayMap);
                }
            }
#endif
                #endregion
                #endregion

                // 3. Simulate nearby districts?
                #region
                // if player is sleeping in this map and option enabled.
                if (s_Options.IsSimON && m_Player != null && m_Player.IsSleeping && s_Options.SimulateWhenSleeping && m_Player.Location.Map.District == district)
                {
                    SimulateNearbyDistricts(district);
                }
                #endregion
            }  // end lock district
        }

        void NotifyOrderablesAI(Map map, RaidType raid, Point position)
        {
            foreach (Actor a in map.Actors)
            {
                OrderableAI oAI = a.Controller as OrderableAI;
                if (oAI == null)
                    continue;
                oAI.OnRaid(raid, new Location(map, position), map.LocalTime.TurnCounter);
            }
        }

        void AdvancePlay(Map map, SimFlags sim)
        {
            //////////////////////////////////////////////////////////
            // 0. Secret maps.
            // 1. Get next actor to Act.
            // 2. If none move to next turn and return.
            // 3. Ask actor to act. Handle player and AI differently.
            //////////////////////////////////////////////////////////

            // 0. Secret maps.
            #region
            if (map.IsSecret)
            {
                // don't play the map at all, jump in time.
                ++map.LocalTime.TurnCounter;
                return;
            }
            #endregion

            // 1. Get next actor to Act.
            #region
            Actor actor = m_Rules.GetNextActorToAct(map, map.LocalTime.TurnCounter);

            // alpha10 ai loop bug detection
            if (actor != null && !actor.IsPlayer)
            {
                if (actor == m_DEBUG_prevAiActor)
                {
                    if (++m_DEBUG_sameAiActorCount >= DEBUG_AI_ACTOR_LOOP_COUNT_WARNING)
                    {
                        // TO DEVS: you might want to add a debug breakpoint here ->
                        Logger.WriteLine(Logger.Stage.RUN_MAIN, "WARNING: AI actor " + actor.Name + " is probably looping!!");
#if DEBUG
                        // in debug keep going to let us debug the ai
#else
                        // alpha10.1 in release, instead of "crashing" the game by throwing an exception force the AI to do
                        // spend a turn and emote. its better than crashing the game!
                        DoWait(actor);
                        DoEmote(actor, "My AI is looping, I'll  wait instead of crashing your game :)", true);
#endif
                    }
                }
                else
                {
                    m_DEBUG_sameAiActorCount = 0;
                    m_DEBUG_prevAiActor = actor;
                }
            }
            #endregion

            // 2. If none move to next turn and return.
            #region
            if (actor == null)
            {
                NextMapTurn(map, sim);
                return;
            }
            #endregion

            // 3. Ask actor to act. Handle player and AI differently.
            #region
            actor.PreviousStaminaPoints = actor.StaminaPoints;
            if (actor.Controller == null)
            {
                SpendActorActionPoints(actor, Rules.BASE_ACTION_COST);
            }
            else if (actor.IsPlayer)
            {
                HandlePlayerActor(actor);
                // if quit, dead or loaded, don't bother.
                if (!m_IsGameRunning || m_HasLoadedGame || m_Player.IsDead)
                    return;
                // Check special player events
                CheckSpecialPlayerEventsAfterAction(actor);
            }
            else
            {
                HandleAiActor(actor);
            }
            actor.PreviousHitPoints = actor.HitPoints;
            actor.PreviousFoodPoints = actor.FoodPoints;
            actor.PreviousSleepPoints = actor.SleepPoints;
            actor.PreviousSanity = actor.Sanity;
            #endregion
        }

        void SpendActorActionPoints(Actor actor, int actionCost)
        {
            actor.ActionPoints -= actionCost;
            actor.LastActionTurn = actor.Location.Map.LocalTime.TurnCounter;
        }

        void SpendActorStaminaPoints(Actor actor, int staminaCost)
        {
            if (actor.Model.Abilities.CanTire)
            {
                // night penalty?
                if (actor.Location.Map.LocalTime.IsNight && staminaCost > 0)
                    staminaCost += m_Rules.NightStaminaPenalty(actor);

                // exhausted?
                if (m_Rules.IsActorExhausted(actor))
                    staminaCost *= 2;

                // apply.
                actor.StaminaPoints -= staminaCost;
            }
            else
                actor.StaminaPoints = Rules.STAMINA_INFINITE;
        }

        void RegenActorStaminaPoints(Actor actor, int staminaRegen)
        {
            if (actor.Model.Abilities.CanTire)
                actor.StaminaPoints = Math.Min(m_Rules.ActorMaxSTA(actor), actor.StaminaPoints + staminaRegen);
            else
                actor.StaminaPoints = Rules.STAMINA_INFINITE;
        }

        void RegenActorHitPoints(Actor actor, int hpRegen)
        {
            actor.HitPoints = Math.Min(m_Rules.ActorMaxHPs(actor), actor.HitPoints + hpRegen);
        }

        void RegenActorSleep(Actor actor, int sleepRegen)
        {
            actor.SleepPoints = Math.Min(m_Rules.ActorMaxSleep(actor), actor.SleepPoints + sleepRegen);
        }

        void SpendActorSanity(Actor actor, int sanCost)
        {
            actor.Sanity -= sanCost;
            if (actor.Sanity < 0) actor.Sanity = 0;
        }

        void RegenActorSanity(Actor actor, int sanRegen)
        {
            actor.Sanity = Math.Min(m_Rules.ActorMaxSanity(actor), actor.Sanity + sanRegen);
        }

        // alpha10
        void DropActorScents(Actor actor)
        {
            // alpha10 dont drop if odor suppressed
            if (actor.OdorSuppressorCounter > 0)
                return;

            if (actor.Model.Abilities.IsUndead)
            {
                // ZM scent?
                if (actor.Model.Abilities.IsUndeadMaster)
                    actor.Location.Map.RefreshScentAt(Odor.UNDEAD_MASTER, Rules.UNDEAD_MASTER_SCENT_DROP, actor.Location.Position);
            }
            else
            {
                // Living scent.
                actor.Location.Map.RefreshScentAt(Odor.LIVING, Rules.LIVING_SCENT_DROP, actor.Location.Position);
            }
        }

        // alpha10
        void DecayActorScents(Actor actor)
        {
            // decay suppressor
            if (actor.OdorSuppressorCounter > 0)
            {
                int decay = m_Rules.OdorsDecay(actor.Location.Map, actor.Location.Position, m_Session.World.Weather);
                actor.OdorSuppressorCounter -= decay;
                if (actor.OdorSuppressorCounter < 0) actor.OdorSuppressorCounter = 0;
            }
        }

        void ModifyActorTrustInLeader(Actor a, int mod, bool addMessage)
        {
            // do it.
            a.TrustInLeader += mod;
            if (a.TrustInLeader > Rules.TRUST_MAX)
                a.TrustInLeader = Rules.TRUST_MAX;
            else if (a.TrustInLeader < Rules.TRUST_MIN)
                a.TrustInLeader = Rules.TRUST_MIN;

            // if leader is player, message.
            if (addMessage && a.Leader.IsPlayer)
                AddMessage(new Message(String.Format("({0} trust with {1})", mod, a.TheName), m_Session.WorldTime.TurnCounter, Color.White));
        }
        #endregion
    }
}
