using System;
using System.Threading;
using djack.RogueSurvivor.Engine;

static class SimulationWorkerTests
{
    public static void Run()
    {
        int calls = 0;
        ManualResetEvent entered = new ManualResetEvent(false);
        ManualResetEvent release = new ManualResetEvent(false);
        DistrictSimulationWorker worker = new DistrictSimulationWorker(delegate
        {
            Interlocked.Increment(ref calls);
            entered.Set();
            release.WaitOne();
        });
        worker.Start();
        worker.Start();
        Check.Equal(true, entered.WaitOne(2000), "simulation worker starts once");

        Thread stop = new Thread(worker.Stop);
        stop.Start();
        Thread.Sleep(30);
        Check.Equal(true, stop.IsAlive, "stop waits for active simulation");
        release.Set();
        Check.Equal(true, stop.Join(2000), "simulation worker stops");
        Check.Equal(1, calls, "stop prevents another simulation pass");
        worker.Stop();

        entered.Reset();
        worker.Start();
        Check.Equal(true, entered.WaitOne(2000), "worker can restart");
        worker.Stop();
        Check.Equal(2, calls, "restart runs one simulation pass");

        int idleCalls = 0;
        ManualResetEvent firstPass = new ManualResetEvent(false);
        ManualResetEvent secondPass = new ManualResetEvent(false);
        DistrictSimulationWorker idle = new DistrictSimulationWorker(delegate
        {
            if (Interlocked.Increment(ref idleCalls) == 2) secondPass.Set();
            firstPass.Set();
        });
        idle.Start();
        Check.Equal(true, firstPass.WaitOne(2000), "idle worker runs initial pass");
        Thread.Sleep(100);
        Check.Equal(1, idleCalls, "idle worker does not poll");
        idle.NotifyWork();
        Check.Equal(true, secondPass.WaitOne(2000), "new work wakes worker");
        Thread.Sleep(50);
        Check.Equal(2, idleCalls, "new work wakes worker once");
        idle.Stop();
    }
}
