using System;
using System.Collections.Generic;
using System.Text;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using System.IO;
using System.Diagnostics;

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

        int RemainingRestTurns()
        {
            if (m_Player.IsSleeping)
            {
                int regen = m_Rules.ActorSleepRegen(m_Player, m_Rules.IsOnCouch(m_Player));
                return regen > 0 ? Math.Max(0,
                    (m_Rules.ActorMaxSleep(m_Player) - m_Player.SleepPoints + regen - 1) / regen) : 0;
            }
            return m_IsPlayerLongWait && !m_IsPlayerLongWaitForcedStop ?
                Math.Max(0, m_PlayerLongWaitEnd.TurnCounter - m_Session.WorldTime.TurnCounter) : 0;
        }

        void SimulateDistantDistrictsThrough(int turn, int remainingRestTurns = 0)
        {
            World world = m_Session.World;
            int total = 0, districtsDue = 0;
            for (int x = 0; x < world.Size; x++)
                for (int y = 0; y < world.Size; y++)
                {
                    District district = world[x, y];
                    if (district != null && district != m_Session.CurrentMap.District)
                    {
                        int due = Math.Max(0, turn - district.EntryMap.LocalTime.TurnCounter);
                        total += due;
                        if (due > 0) districtsDue++;
                    }
                }
            if (total == 0)
            {
                m_RestHasDeferredSimulation = false;
                return;
            }
            // Budget the new district turns plus a share of older debt; simulate oldest turns first.
            int budget = remainingRestTurns > 0 ? Math.Min(total, districtsDue +
                (total - districtsDue + remainingRestTurns) / (remainingRestTurns + 1)) : total;
            int done = 0;
            Stopwatch elapsed = Stopwatch.StartNew();
            bool showedProgress = false;
            if (m_RestProgressLastDraw == 0) m_RestProgressLastDraw = Stopwatch.GetTimestamp();
            while (done < budget)
            {
                District next = null;
                for (int x = 0; x < world.Size; x++)
                    for (int y = 0; y < world.Size; y++)
                    {
                        District district = world[x, y];
                        if (district == null || district == m_Session.CurrentMap.District ||
                            district.EntryMap.LocalTime.TurnCounter >= turn) continue;
                        if (next == null || district.EntryMap.LocalTime.TurnCounter < next.EntryMap.LocalTime.TurnCounter)
                            next = district;
                    }
                if (next == null) break;
                long now = Stopwatch.GetTimestamp();
                if (now - m_RestProgressLastDraw >= Stopwatch.Frequency / 4)
                {
                    string eta = done == 0 ? "estimating" :
                        String.Format("~{0}s left", Math.Ceiling(elapsed.Elapsed.TotalSeconds * (budget - done) / done));
                    m_RestSimulationProgress = String.Format("Simulating city: {0}/{1} district turns, {2}", done, budget, eta);
                    RedrawPlayScreen();
                    m_RestProgressLastDraw = now;
                    showedProgress = true;
                }
                SimulateDistrict(next);
                done++;
                if (!m_IsGameRunning || m_HasLoadedGame || m_Player == null || m_Player.IsDead) break;
                if (m_IsPlayerLongWaitForcedStop || !m_Player.IsSleeping && !m_IsPlayerLongWait)
                    budget = total;
            }
            m_RestHasDeferredSimulation = done < total;
            m_RestSimulationProgress = null;
            if (showedProgress && m_IsGameRunning && !m_HasLoadedGame && m_Player != null && !m_Player.IsDead)
                RedrawPlayScreen();
        }

        void FinishRestSimulationIfNeeded()
        {
            if (m_RestHasDeferredSimulation && s_Options.IsSimON &&
                m_Session.GamePreset != null && !m_Session.GamePreset.DisableDistantSimulationDuringRest)
                SimulateDistantDistrictsThrough(m_Session.WorldTime.TurnCounter);
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
                    if (m_SimWorker != null && m_SimWorker.StopRequestedOnWorkerThread)
                        return hadToSim;
                    // don't sim same district!
                    if (dx == d.WorldPosition.X && dy == d.WorldPosition.Y)
                        continue;

                    District otherDistrict = m_Session.World[dx, dy];
                    if (otherDistrict == null) continue;

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
        bool m_RestartSimulationAfterReincarnation;
        void StartSimThread()
        {
            if (s_Options.IsSimON && s_Options.SimThread &&
                (m_Session.GamePreset == null || m_Session.GamePreset.DisableDistantSimulationDuringRest))
            {
                if (m_SimWorker == null)
                {
                    District playerDistrict = m_Player.Location.Map.District;
                    m_SimWorker = new DistrictSimulationWorker(delegate
                    {
                        while (m_Player != null && SimulateNearbyDistricts(playerDistrict))
                        {
                            if (m_SimWorker.StopRequestedOnWorkerThread) break;
                        }
                    });
                }
                m_SimWorker.Start();
            }
        }

        // Keep the old parameter for callers; every exit now stops cooperatively.
        void StopSimThread(bool abort)
        {
            if (m_SimWorker == null) return;
            m_SimWorker.Stop();
            m_SimWorker = null;
        }
        #endregion
    }
}
