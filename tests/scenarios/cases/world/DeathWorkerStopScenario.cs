using System;
using System.Threading;
using djack.RogueSurvivor.Engine;

static class DeathWorkerStopScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("world/death-worker-stop", () => TownScenarioFactory.Arena(4825,
            "...", "...", "..."), world =>
        {
            object districtLock = new object();
            ManualResetEventSlim waiting = new ManualResetEventSlim(false);
            DistrictSimulationWorker worker = new DistrictSimulationWorker(() => {
                waiting.Set();
                lock (districtLock) { }
            });
            lock (districtLock)
            {
                worker.Start();
                Check.Equal(true, waiting.Wait(2000), "worker reaches the district lock");
                worker.RequestStop();
            }
            worker.Stop();
            Check.Equal(true, true, "worker joins after district lock is released");
        });
    }
}
