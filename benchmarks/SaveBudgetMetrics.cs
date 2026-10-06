using System;
using System.Diagnostics;
using System.Threading;

// All collection before/after sampling is outside the timed operation.
sealed class SaveBudgetMetrics
{
    public const double MaximumSeconds = 10;
    public const long MaximumBytes = 50000000;
    public double Seconds;
    public long ManagedBefore, ManagedPeak, ManagedRetained, RssBefore, RssPeak;
    public int[] Collections = new int[3];

    public static SaveBudgetMetrics Measure(Action operation)
    {
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        var result = new SaveBudgetMetrics { ManagedBefore = GC.GetTotalMemory(false) };
        using (Process process = Process.GetCurrentProcess())
        using (var stop = new ManualResetEvent(false))
        using (var ready = new ManualResetEvent(false))
        {
            process.Refresh(); result.RssBefore = process.WorkingSet64;
            Action sample = () => {
                result.ManagedPeak = Math.Max(result.ManagedPeak, GC.GetTotalMemory(false));
                process.Refresh(); result.RssPeak = Math.Max(result.RssPeak, process.WorkingSet64);
            };
            var sampler = new Thread(() => { sample(); ready.Set(); while (!stop.WaitOne(10)) sample(); sample(); });
            sampler.IsBackground = true; sampler.Start(); ready.WaitOne();
            for (int i = 0; i < 3; i++) result.Collections[i] = GC.CollectionCount(i);
            Stopwatch timer = Stopwatch.StartNew();
            try { operation(); }
            finally { timer.Stop(); result.Seconds = timer.Elapsed.TotalSeconds; stop.Set(); sampler.Join(); }
        }
        for (int i = 0; i < 3; i++) result.Collections[i] = GC.CollectionCount(i) - result.Collections[i];
        result.ManagedRetained = GC.GetTotalMemory(true) - result.ManagedBefore;
        return result;
    }

    public void Print(string operation, string path)
    {
        long bytes = new System.IO.FileInfo(path).Length;
        Console.WriteLine("SAVE BUDGET {0}: {1:F3} s; file={2} bytes ({3:F2} MB); managed baseline={4:F1} MB, sampled peak={5:F1} MB, peak delta={6:F1} MB, retained delta={7:F1} MB; RSS baseline={8:F1} MB, sampled peak={9:F1} MB, peak delta={13:F1} MB; GC={10}/{11}/{12}",
            operation, Seconds, bytes, bytes / 1000000d, ManagedBefore / 1000000d, ManagedPeak / 1000000d,
            Math.Max(0, ManagedPeak - ManagedBefore) / 1000000d, ManagedRetained / 1000000d,
            RssBefore / 1000000d, RssPeak / 1000000d, Collections[0], Collections[1], Collections[2], Math.Max(0, RssPeak - RssBefore) / 1000000d);
        AssertLimits(Seconds, bytes);
    }

    public static void AssertLimits(double seconds, long bytes)
    {
        if (Double.IsNaN(seconds) || Double.IsInfinity(seconds) || seconds < 0 || seconds > MaximumSeconds)
            throw new InvalidOperationException("Save/load time budget exceeded: " + seconds + " seconds; maximum is " + MaximumSeconds + ".");
        if (bytes < 0 || bytes > MaximumBytes)
            throw new InvalidOperationException("Save size budget exceeded: " + bytes + " bytes; maximum is " + MaximumBytes + ".");
    }
}
