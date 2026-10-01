using System;
using System.IO;
using System.Security.Cryptography;
using djack.RogueSurvivor.Engine;

static class SaveBudgetCopyScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("storage/save-budget-copy", () => TownScenarioFactory.Create(4681, false), world =>
        {
            string directory = Path.Combine(Path.GetTempPath(), "budget-source-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory); string path = Path.Combine(directory, "source.dat");
            try
            {
                Session.Get.GamePreset.NpcPersonalitiesEnabled = false;
                BinarySaveStore.Save(path, Session.Get);
                File.WriteAllText(path + ".bak", "original backup must remain untouched");
                string original = Digest(path), backup = Digest(path + ".bak");
                DateTime written = File.GetLastWriteTimeUtc(path);
                SaveBudgetRunner.Run(path);
                Check.Equal(original, Digest(path), "copied-save budget never modifies the input");
                Check.Equal(backup, Digest(path + ".bak"), "copied-save budget never replaces the original backup");
                Check.Equal(written, File.GetLastWriteTimeUtc(path), "input modification time remains unchanged");
                Check.Equal(false, BinarySaveStore.LoadExact<Session>(path).GamePreset.NpcPersonalitiesEnabled,
                    "performance checks also support saves with personality disabled");
            }
            finally { Directory.Delete(directory, true); }
        });
    }

    static string Digest(string path)
    {
        using (SHA256 hash = SHA256.Create()) using (FileStream file = File.OpenRead(path))
            return Convert.ToBase64String(hash.ComputeHash(file));
    }
}
