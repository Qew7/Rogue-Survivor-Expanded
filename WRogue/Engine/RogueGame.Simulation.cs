using System;
using System.Collections.Generic;
using System.Text;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using System.IO;
using System.Threading.Tasks;

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

        #endregion
        #region Simulation
        [ThreadStatic] static bool s_ParallelRestWorker;
        internal static bool IsParallelRestWorker { get { return s_ParallelRestWorker; } }
        internal int ParallelRestWorkers;
        bool m_BackgroundDistantSimulationActive;

        bool IsParallelRestCrossing(Actor actor, Point exitPoint, out District destination)
        {
            destination = null;
            if (!s_ParallelRestWorker || actor.IsPlayer || actor.IsDead || actor.Location.Map == null) return false;
            Exit exit = actor.Location.Map.GetExitAt(exitPoint);
            if (exit == null || exit.ToMap.District == actor.Location.Map.District) return false;
            destination = exit.ToMap.District;
            return destination != null;
        }

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

        static List<List<int>> RestSimulationWaves(List<District> batch, NpcStoryDirector director,
            District playerDistrict)
        {
            var keys = new HashSet<string>[batch.Count];
            var playerNeighbor = new bool[batch.Count];
            var districtIndex = new Dictionary<District, int>();
            var actorIndex = new Dictionary<Guid, int>();
            var activeStories = new HashSet<string>(StringComparer.Ordinal);
            lock (director)
                foreach (NpcStory story in director.Stories)
                    if (!story.Finished) activeStories.Add(story.Id);
            for (int i = 0; i < batch.Count; i++)
            {
                districtIndex.Add(batch[i], i);
                keys[i] = new HashSet<string>(StringComparer.Ordinal);
                Point playerPosition = playerDistrict.WorldPosition;
                Point position = batch[i].WorldPosition;
                playerNeighbor[i] = Math.Abs(position.X - playerPosition.X) +
                    Math.Abs(position.Y - playerPosition.Y) == 1;
                foreach (Map map in batch[i].Maps)
                {
                    foreach (Actor actor in map.Actors)
                    {
                        Guid id = actor.ExistingPersonalityIdentity;
                        if (id != Guid.Empty) actorIndex[id] = i;
                        Guid group = actor.ExistingGroupIdentity;
                        if (group != Guid.Empty) keys[i].Add("group:" + group);
                        if (actor.Personality != null)
                        {
                            foreach (NpcIntent intent in actor.Personality.Intents)
                                if (!intent.Finished && intent.StoryId != null && activeStories.Contains(intent.StoryId))
                                    keys[i].Add("story:" + intent.StoryId);
                            foreach (NpcFact fact in actor.Personality.Knowledge.Facts)
                                if (fact.StoryId != null && activeStories.Contains(fact.StoryId))
                                    keys[i].Add("story:" + fact.StoryId);
                        }
                    }
                    foreach (Exit exit in map.Exits)
                        if (exit.ToMap.District != batch[i])
                            keys[i].Add("arrival:" + exit.ToMap.Seed + ":" +
                                exit.ToPosition.X + "," + exit.ToPosition.Y);
                }
            }
            lock (director)
                foreach (NpcStory story in director.Stories)
                {
                    if (story.Finished) continue;
                    string key = "story:" + story.Id;
                    int i;
                    if (story.Place.Map != null && story.Place.Map.District != null &&
                        districtIndex.TryGetValue(story.Place.Map.District, out i)) keys[i].Add(key);
                    if (story.Resource.Map != null && story.Resource.Map.District != null &&
                        districtIndex.TryGetValue(story.Resource.Map.District, out i)) keys[i].Add(key);
                    foreach (NpcStoryRole role in story.Roles)
                        if (actorIndex.TryGetValue(role.ActorId, out i)) keys[i].Add(key);
                }
            var levels = new int[batch.Count];
            var waves = new List<List<int>>();
            for (int i = 0; i < batch.Count; i++)
            {
                for (int j = 0; j < i; j++)
                    if (playerNeighbor[i] || playerNeighbor[j] || keys[i].Overlaps(keys[j]))
                        levels[i] = Math.Max(levels[i], levels[j] + 1);
                while (waves.Count <= levels[i]) waves.Add(new List<int>());
                waves[levels[i]].Add(i);
            }
            return waves;
        }

        // Called only while the live turn is paused. StopSimThread joins after one
        // district turn, so input, saving, and district changes see a stable world.
        void SimulateBackgroundDistantDistricts()
        {
            World world = m_Session.World;
            District current = m_Session.CurrentMap.District;
            Point playerPosition = current.WorldPosition;
            while (m_SimWorker != null && !m_SimWorker.StopRequestedOnWorkerThread)
            {
                int target = m_Session.WorldTime.TurnCounter;
                int oldest = Int32.MaxValue;
                for (int x = 0; x < world.Size; x++) for (int y = 0; y < world.Size; y++)
                {
                    if (Math.Abs(x - playerPosition.X) <= 1 && Math.Abs(y - playerPosition.Y) <= 1)
                        continue;
                    District candidate = world[x, y];
                    if (candidate == null || candidate.EntryMap.LocalTime.TurnCounter >= target) continue;
                    oldest = Math.Min(oldest, candidate.EntryMap.LocalTime.TurnCounter);
                }
                if (oldest == Int32.MaxValue) return;
                var batch = new List<District>();
                int color = -1;
                for (int x = 0; x < world.Size; x++) for (int y = 0; y < world.Size; y++)
                {
                    if (Math.Abs(x - playerPosition.X) <= 1 && Math.Abs(y - playerPosition.Y) <= 1)
                        continue;
                    District candidate = world[x, y];
                    if (candidate == null || candidate.EntryMap.LocalTime.TurnCounter != oldest) continue;
                    if (color < 0) color = (x + y) & 1;
                    if (((x + y) & 1) == color)
                        batch.Add(candidate);
                }
                if (batch.Count > 4)
                    batch.RemoveRange(4, batch.Count - 4);
                SimulateBackgroundDistantBatch(batch, current);
            }
        }

        void SimulateBackgroundDistantBatch(List<District> batch, District current)
        {
            var slots = new HashSet<int>();
            foreach (District district in batch)
                foreach (Map map in district.Maps)
                {
                    slots.Add(map.LocalTime.TurnCounter / WorldTime.TURNS_PER_HOUR);
                    slots.Add((map.LocalTime.TurnCounter + 1) / WorldTime.TURNS_PER_HOUR);
                }
            foreach (int slot in slots)
                for (int station = 0; station < 4; station++) GetRadioProgram(station, slot);
            const int idsPerTurn = 65536;
            var eventIds = new long[batch.Count];
            for (int i = 0; i < batch.Count; i++)
                eventIds[i] = m_Session.ReservePersonalityEventIds(idsPerTurn);
            ResidentRecords records = m_Session.ResidentRecords;
            NpcStoryDirector director = m_Session.NpcDirector;
            var options = new ParallelOptions { MaxDegreeOfParallelism =
                Math.Max(1, ParallelRestWorkers > 0 ? ParallelRestWorkers : Math.Min(4, Environment.ProcessorCount)) };
            foreach (List<int> wave in RestSimulationWaves(batch, director, current))
                Parallel.ForEach(wave, options, i =>
                {
                    bool previous = s_ParallelRestWorker;
                    s_ParallelRestWorker = true;
                    try
                    {
                        m_Session.WithPersonalityEventIds(eventIds[i], idsPerTurn,
                            () => SimulateDistrict(batch[i]));
                    }
                    finally { s_ParallelRestWorker = previous; }
                });
            director.CompactFinished();
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

                    while (otherDistrict.EntryMap.LocalTime.TurnCounter < d.EntryMap.LocalTime.TurnCounter)
                    {
                        hadToSim = true;
                        SimulateDistrict(otherDistrict);
                        if (!m_IsGameRunning || m_HasLoadedGame || m_Player == null || m_Player.IsDead)
                            return hadToSim;
                    }
                }

            return hadToSim;
        }
        #endregion
        #region Simulation Thread
        bool m_RestartSimulationAfterReincarnation;
        void StartSimThread()
        {
            if (s_Options.IsSimON && m_BackgroundDistantSimulationActive &&
                m_Session.GamePreset != null && !m_Session.GamePreset.DisableDistantSimulationDuringRest)
            {
                if (m_SimWorker == null)
                    m_SimWorker = new DistrictSimulationWorker(SimulateBackgroundDistantDistricts);
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
