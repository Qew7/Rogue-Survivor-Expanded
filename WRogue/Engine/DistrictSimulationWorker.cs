using System;
using System.Threading;

namespace djack.RogueSurvivor.Engine
{
    // Owns the lifetime and cancellation state of background district simulation.
    sealed class DistrictSimulationWorker
    {
        readonly Action m_Simulate;
        readonly object m_StateLock = new object();
        Thread m_Thread;
        bool m_StopRequested;

        public DistrictSimulationWorker(Action simulate)
        {
            if (simulate == null) throw new ArgumentNullException("simulate");
            m_Simulate = simulate;
        }

        public bool StopRequestedOnWorkerThread
        {
            get
            {
                lock (m_StateLock)
                    return Thread.CurrentThread == m_Thread && m_StopRequested;
            }
        }

        public void Start()
        {
            lock (m_StateLock)
            {
                if (m_Thread != null) return;
                m_StopRequested = false;
                m_Thread = new Thread(Run);
                m_Thread.Name = "Simulation Thread";
                m_Thread.Start();
            }
        }

        public void Stop()
        {
            Thread thread;
            lock (m_StateLock)
            {
                thread = m_Thread;
                m_StopRequested = true;
            }
            if (thread == null) return;
            if (thread != Thread.CurrentThread)
                thread.Join();
            lock (m_StateLock)
                if (m_Thread == thread) m_Thread = null;
        }

        void Run()
        {
            while (true)
            {
                lock (m_StateLock)
                    if (m_StopRequested) return;
                Thread.Sleep(10);
                lock (m_StateLock)
                    if (m_StopRequested) return;
                try
                {
                    m_Simulate();
                }
                catch (Exception e)
                {
                    Logger.WriteLine(Logger.Stage.RUN_MAIN, "sim thread: " + e);
                    return;
                }
            }
        }
    }
}
