using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using djack.RogueSurvivor.Engine;

static class SaveBudgetRunner
{
    public static void Run(string copiedSave = null)
    {
        string directory = Path.Combine(Path.GetTempPath(), "rogue-save-budget-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "world.dat");
        try
        {
            if (copiedSave != null) File.Copy(copiedSave, path);
            Console.WriteLine("SAVE BUDGET limits: each operation <=10 seconds, each save <=50,000,000 bytes; RAM reported diagnostically.");
            Worker(copiedSave == null ? "save" : "copy", path);
            for (int i = 0; i < 2; i++) Worker(copiedSave == null ? "load" : "load-copy", path);
            if (copiedSave == null) Worker("records", path);
            long backup = File.Exists(path + ".bak") ? new FileInfo(path + ".bak").Length : 0;
            Console.WriteLine("SAVE BUDGET backup={0} bytes; retained primary+backup={1} bytes", backup, backup + new FileInfo(path).Length);
        }
        finally { Directory.Delete(directory, true); }
    }

    static void Worker(string mode, string path)
    {
        using (Process runtime = Process.GetCurrentProcess())
        {
            var start = new ProcessStartInfo(runtime.MainModule.FileName,
                Quote(Assembly.GetExecutingAssembly().Location) + " --save-budget-worker " + mode + " " + Quote(path)) { UseShellExecute = false };
            using (Process child = Process.Start(start))
            {
                if (!child.WaitForExit(60000))
                {
                    child.Kill(); child.WaitForExit();
                    throw new InvalidOperationException("Save budget worker timed out: " + mode);
                }
                if (child.ExitCode != 0) throw new InvalidOperationException("Save budget worker failed: " + mode);
            }
        }
    }

    static string Quote(string value) { return "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\""; }

    public static int RunWorker(string mode, string path)
    {
        try
        {
            if (mode == "records")
            {
                RecordsSave records = null;
                SaveBudgetMetrics.Measure(() => records = RecordsReader.Load(path)).Print("archive-only fresh process", path);
                Check.Equal(SaveBudgetFixture.ActorCount, records.Records.Residents.Count, "archive resident count");
                return 0;
            }
            TownScenarioFactory.Create(SaveBudgetFixture.Seed, false); // Models ready; no serializer warm-up.
            if (mode == "save") SaveBudgetFixture.Create();
            else
            {
                SaveBudgetMetrics.Measure(() => Check.Equal(true, Session.Load(path, Session.SaveFormat.FORMAT_BIN), "real session loads"))
                    .Print("load including map indexes, fresh process", path);
                if (mode == "load") SaveBudgetFixture.Validate(Session.Get);
                if (mode == "load" || mode == "load-copy") return 0;
                if (mode != "copy") throw new ArgumentException("Unknown save budget worker mode.");
            }
            for (int i = 0; i < 3; i++)
            {
                SaveBudgetMetrics.Measure(() => Session.Save(Session.Get, path, Session.SaveFormat.FORMAT_BIN))
                    .Print(i == 0 && mode == "save" ? "first save, cold serializer" : "atomic replacement " + (i + 1), path);
                if (File.Exists(path + ".bak")) SaveBudgetMetrics.AssertLimits(0, new FileInfo(path + ".bak").Length);
            }
            if (mode == "save") SaveBudgetFixture.Validate(Session.Get);
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
}
