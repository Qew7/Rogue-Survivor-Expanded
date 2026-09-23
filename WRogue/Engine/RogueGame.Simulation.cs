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
        #region Changing map & district

        void SetCurrentMap(Map map)
        {
            // set session field.
            m_Session.CurrentMap = map;

            // alpha10 update background music
            UpdateBgMusic();
        }

        void OnPlayerLeaveDistrict()
        {
            // remember when we left the district.
            m_Session.CurrentMap.LocalTime.TurnCounter = m_Session.WorldTime.TurnCounter;
        }

        void BeforePlayerEnterDistrict(District district)
        {
            // get entry map.
            Map entryMap = district.EntryMap;

            // get when we left the district.
            int lastTime = entryMap.LocalTime.TurnCounter;

            // if option set, simulate to catch current turn.
            // otherwise just jump int time.
            #region
            if (s_Options.IsSimON)
            {
                int catchupTo = m_Session.WorldTime.TurnCounter;  // alpha10
                int turnsToCatchup = catchupTo - entryMap.LocalTime.TurnCounter; // alpha10

                StopSimThread(false);  // alpha10

                if (turnsToCatchup > 0)
                {
                    // music.
                    m_MusicManager.Stop();
                    m_MusicManager.PlayLooping(GameMusics.INTERLUDE, MusicPriority.PRIORITY_EVENT);

                    // force player view to darkness (so he gets no messages).
                    if (m_Player != null)
                    {
                        m_Player.Location.Map.ClearView();
                        entryMap.ClearView();
                    }

                    // alpha10 nope not here
                    // stop simulation thread & get mutex.
                    //StopSimThread();
                    ////Monitor.Enter(m_SimMutex);  // alpha10 obsolete

                    // simulate loop.
                    #region
                    double timerStart = DateTime.UtcNow.TimeOfDay.TotalMilliseconds;
                    double lastRedraw = 0;
                    bool aborted = false;
                    while (entryMap.LocalTime.TurnCounter < catchupTo) // alpha10 changed from <= to <
                    {
                        double timerNow = DateTime.UtcNow.TimeOfDay.TotalMilliseconds;

                        // time to redraw?
                        bool doRedraw = (entryMap.LocalTime.TurnCounter == m_Session.WorldTime.TurnCounter) ||      // show last turn
                            entryMap.LocalTime.TurnCounter == lastTime ||                                           // show 1st turn
                            timerNow >= lastRedraw + 1000;                                                          // show every seconds

                        // redraw?
                        #region
                        if (doRedraw)
                        {
                            // remember we redrawed.
                            lastRedraw = timerNow;

                            // show.
                            ClearMessages();
                            AddMessage(new Message(String.Format("Simulating district, please wait {0}/{1}...", entryMap.LocalTime.TurnCounter, m_Session.WorldTime.TurnCounter), m_Session.WorldTime.TurnCounter, Color.White));
                            AddMessage(new Message("(this is an option you can tune)", m_Session.WorldTime.TurnCounter, Color.White));

                            // estimate turns per seconds and time left.
                            int turnsDone = entryMap.LocalTime.TurnCounter - lastTime;
                            if (turnsDone > 1)
                            {
                                int turnsLeft = m_Session.WorldTime.TurnCounter - entryMap.LocalTime.TurnCounter;
                                double turnsPerSecs = 1000.0f * (float)turnsDone / (1 + timerNow - timerStart);
                                AddMessage(new Message(String.Format("Turns per second    : {0:F2}.", turnsPerSecs), m_Session.WorldTime.TurnCounter, Color.White));

                                int secsLeft = (int)(turnsLeft / turnsPerSecs);
                                int mins = secsLeft / 60;
                                int secs = secsLeft % 60;
                                string etaFormat = (mins > 0 ? String.Format("{0} min {1:D2} secs", mins, secs) : String.Format("{0} secs", secs));
                                AddMessage(new Message(String.Format("Estimated time left : {0}.", etaFormat), m_Session.WorldTime.TurnCounter, Color.White));
                            }
                            if (aborted)
                                AddMessage(new Message("Simulation aborted!", m_Session.WorldTime.TurnCounter, Color.Red));
                            else
                                AddMessage(new Message("<keep ESC pressed to abort the simulation>", m_Session.WorldTime.TurnCounter, Color.Yellow));
                            RedrawPlayScreen();
                        }
                        #endregion

                        // aborted?
                        if (aborted)
                            break;

                        // check for abort.
                        #region
                        KeyEventArgs key = m_UI.UI_PeekKey();
                        if (key != null && key.KeyCode == Keys.Escape)
                        {
                            // jump in time for each map.
                            foreach (Map map in district.Maps)
                                map.LocalTime.TurnCounter = m_Session.WorldTime.TurnCounter;
                            // abort!
                            aborted = true;
                        }
                        #endregion

                        // if not aborted, simulate the district.
                        if (!aborted)
                        {
                            // sim the district.
                            SimulateDistrict(district);
                        }
                    }
                    #endregion

                    // alpha10 obsolete and fix
                    //// release mutex and restart sim thread.
                    //Monitor.Exit(m_SimMutex); // alpha10 obsolete
                    //RestartSimThread();  // alpha10 no no no! restart AFTER chaging the player district duh!

                    // Sim ends - either aborted or normal end.
                    #region
                    // remove "ESC" message.
                    RemoveLastMessage();

                    // since sim arbitrary messes with actor APs, we're not quite sure were they are now.
                    // so force them back to zero to have a clean start.
                    foreach (Map map in district.Maps)
                        foreach (Actor a in map.Actors)
                            if (!a.IsSleeping)
                                a.ActionPoints = 0;

                    // stop music.
                    m_MusicManager.Stop();
                    #endregion
                }  // sim has catchup to do
            } // sim on
            else
            {
                // jump in time for each map.
                foreach (Map map in district.Maps)
                    map.LocalTime.TurnCounter = m_Session.WorldTime.TurnCounter;
            }
            #endregion
        }

        // alpha10
        void AfterPlayerEnterDistrict()
        {
            // restart sim thread if on
            if (s_Options.IsSimON && s_Options.SimThread)
                StartSimThread();
        }

        void OnPlayerChangeMap()
        {
            RefreshPlayer();
        }
        #endregion
        #region Simulation
        SimFlags ComputeSimFlagsForTurn(int turn)
        {
            bool loDetail = false;

            switch (s_Options.SimulateDistricts)
            {
                case GameOptions.SimRatio.FULL:
                    loDetail = false;
                    break;
                case GameOptions.SimRatio.THREE_QUARTER:    // 3/4, skip 1 out of 4.
                    loDetail = (turn % 4 == 3);
                    break;
                case GameOptions.SimRatio.TWO_THIRDS:    // 2/3, skip 1 out of 3.
                    loDetail = (turn % 3 == 2);
                    break;
                case GameOptions.SimRatio.HALF:    // 1/2, skip 1 out of 2.
                    loDetail = (turn % 2 == 1);
                    break;
                case GameOptions.SimRatio.ONE_THIRD:    // 1/3, play 1 out of 3.
                    loDetail = (turn % 3 != 0);
                    break;
                case GameOptions.SimRatio.ONE_QUARTER:    // 1/4, play 1 out of 4.
                    loDetail = (turn % 4 != 0);
                    break;
                case GameOptions.SimRatio.OFF:
                    loDetail = true;
                    break;
                default:
                    throw new ArgumentOutOfRangeException("unhandled simRatio");
            }

            return loDetail ? SimFlags.LODETAIL_TURN : SimFlags.HIDETAIL_TURN;
        }

        void SimulateDistrict(District d)
        {
            AdvancePlay(d, ComputeSimFlagsForTurn(d.EntryMap.LocalTime.TurnCounter));
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="d"></param>
        /// <returns>true if simulated a district; false if didn't need to simulate.</returns>
        bool SimulateNearbyDistricts(District d)
        {
            bool hadToSim = false;
            int xmin = d.WorldPosition.X - 1;
            int xmax = d.WorldPosition.X + 1;
            int ymin = d.WorldPosition.Y - 1;
            int ymax = d.WorldPosition.Y + 1;
            m_Session.World.TrimToBounds(ref xmin, ref ymin);
            m_Session.World.TrimToBounds(ref xmax, ref ymax);

            for (int dx = xmin; dx <= xmax; dx++)
                for (int dy = ymin; dy <= ymax; dy++)
                {
                    // don't sim same district!
                    if (dx == d.WorldPosition.X && dy == d.WorldPosition.Y)
                        continue;

                    District otherDistrict = m_Session.World[dx, dy];

                    // don't sim if up to date!
                    int dTurns = d.EntryMap.LocalTime.TurnCounter - otherDistrict.EntryMap.LocalTime.TurnCounter;
                    if (dTurns > 0)
                    {
                        //Logger.WriteLine(Logger.Stage.RUN_MAIN, "sim has to catch " + dTurns + " turns");
                        // simulate district.
                        hadToSim = true;
                        SimulateDistrict(otherDistrict);
                        //Console.Out.WriteLine("  sim simulated district " + otherDistrict.Name+ " turn now "+otherDistrict.EntryMap.LocalTime.TurnCounter);
                    }
                    //else  // DEBUG
                    //    Console.Out.WriteLine("  sim district " + otherDistrict.Name + " is up to date " + otherDistrict.EntryMap.LocalTime.TurnCounter);
                }

            return hadToSim;
        }
        #endregion
        #region Simulation Thread
        // alpha10 obsolete, we do it "manually" in some places
        //void RestartSimThread()
        //{
        //    StopSimThread();
        //    StartSimThread();
        //}

        void StartSimThread()
        {
            if (s_Options.IsSimON && s_Options.SimThread)
            {
                Logger.WriteLine(Logger.Stage.RUN_MAIN, "starting sim...");

                if (m_SimThread == null)
                {
                    Logger.WriteLine(Logger.Stage.RUN_MAIN, "...allocating sim thread");
                    m_SimThread = new Thread(new ThreadStart(SimThreadProc));
                    m_SimThread.Name = "Simulation Thread";
                }
                else
                {
                    Logger.WriteLine(Logger.Stage.RUN_MAIN, "...sim thread already allocated");
                }

                Logger.WriteLine(Logger.Stage.RUN_MAIN, "...sim thread start.");
                lock (m_SimStateLock) { m_SimThreadDoRun = true; }; // alpha10
                m_SimThread.Start();
            }
        }

        // alpha10 StopSimThread is now blocking until the sim thread has actually stopped
        // allowed to abort when ending a game or dying because of weird bug in release build where the sim thread
        // doesnt want to stop when dying as undead and we have to abort it(!)
        /// <summary>
        ///
        /// </summary>
        /// <param name="abort">true to stop the thread by aborting, false to stop it cleanly (recommended)</param>
        void StopSimThread(bool abort)
        {
            Logger.WriteLine(Logger.Stage.RUN_MAIN, "stopping & clearing sim thread...");

            if (m_SimThread != null)
            {
                // abort thread if asked to otherwise stop it cleanly
                if (abort)
                {
                    Logger.WriteLine(Logger.Stage.RUN_MAIN, "...aborting sim thread");
                    try
                    {
                        m_SimThread.Abort();
                    }
                    catch (Exception e)
                    {
                        Logger.WriteLine(Logger.Stage.RUN_MAIN, "...exception when aborting (ignored) " + e.Message);
                    }
                    m_SimThread = null;
                    m_SimThreadDoRun = false;
                }
                else
                {
                    // try to stop cleanly
                    Logger.WriteLine(Logger.Stage.RUN_MAIN, "...telling sim thread to stop");
                    lock (m_SimStateLock) { m_SimThreadDoRun = false; };
                    Logger.WriteLine(Logger.Stage.RUN_MAIN, "...sim thread told to stop");
                    for (; ; )
                    {
                        Logger.WriteLine(Logger.Stage.RUN_MAIN, "...waiting for sim thread to stop");
                        Thread.Sleep(10);
                        bool stopped = false;
                        lock (m_SimStateLock) { stopped = !m_SimThreadIsWorking; }
                        if (!stopped && !m_SimThread.IsAlive)
                        {
                            Logger.WriteLine(Logger.Stage.RUN_MAIN, "...sim thread is not alive and did not stop properly, consider it stopped");
                            stopped = true;
                        }
                        if (stopped)
                            break;
                    }
                    Logger.WriteLine(Logger.Stage.RUN_MAIN, "...sim thread has stopped");
                    m_SimThread = null;
                }
            }

            Logger.WriteLine(Logger.Stage.RUN_MAIN, "stopping & clearing sim thread done!");
        }

        void SimThreadProc()
        {
            Logger.WriteLine(Logger.Stage.RUN_MAIN, "sim thread: starting loop");

            District playerDistrict = m_Player.Location.Map.District;  // alpha10

            lock (m_SimStateLock) { m_SimThreadIsWorking = true; }  // alpha10

            for (; ; )  // alpha10
            //while (true)
            {
                //Console.Out.WriteLine("sim thread loop");
                // alpha10
                bool stop = false;
                lock (m_SimStateLock) { stop = !m_SimThreadDoRun; }
                if (stop)
                    break;

                Thread.Sleep(10);
                //Monitor.Enter(m_SimMutex); // alpha10 obsolete
                try
                {
                    if (m_Player != null)
                    {
                        SimulateNearbyDistricts(playerDistrict);
                    }
                }
                catch (Exception e)
                {
                    Logger.WriteLine(Logger.Stage.RUN_MAIN, "sim thread: exception while running sim thread!");
                    Logger.WriteLine(Logger.Stage.RUN_MAIN, "sim thread: " + e.Message);
                    // stop sim thread, better than crashing i guess...
                    break;
                }
                //finally
                //{
                //    Monitor.Exit(m_SimMutex); // alpha10 obsolete
                //}
            }

            Logger.WriteLine(Logger.Stage.RUN_MAIN, "sim thread: told to stop, stoping work");
            lock (m_SimStateLock) { m_SimThreadIsWorking = false; }
            Logger.WriteLine(Logger.Stage.RUN_MAIN, "sim thread: working stopped");
        }
        #endregion
    }
}
