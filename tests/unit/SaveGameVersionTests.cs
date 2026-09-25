using System;
using System.IO;
using djack.RogueSurvivor.Engine;

static class SaveGameVersionTests
{
    public static void Run()
    {
        string path = Path.Combine(Path.GetTempPath(),
            "rogue-game-version-" + Guid.NewGuid().ToString("N"));
        try
        {
            BinarySaveStore.Save(path, "compatible", new ModStamp[] {
                new ModStamp { Name = "example", Version = "2.0" } });
            Check.Equal("compatible", BinarySaveStore.Load<string>(path),
                "same game version loads");
            Check.Equal("example", BinarySaveStore.ReadMods(path)[0].Name,
                "mod manifest follows game version");

            byte[] current = File.ReadAllBytes(path);
            Check.Equal((byte)'0', current[6], "version starts after string length");
            byte[] previousPatch = (byte[])current.Clone();
            previousPatch[10] = (byte)'0'; // 0.1.1 -> 0.1.0
            File.WriteAllBytes(path, previousPatch);
            Check.Equal("compatible", BinarySaveStore.Load<string>(path),
                "previous patch save loads");
            Check.Equal("example", BinarySaveStore.ReadMods(path)[0].Name,
                "previous patch save retains mod manifest");

            byte[] incompatible = (byte[])current.Clone();
            incompatible[8] = (byte)'2'; // 0.1.1 -> 0.2.1
            File.WriteAllBytes(path, incompatible);
            AssertRejected(() => BinarySaveStore.ReadMods(path),
                "incompatible manifest rejected before mod selection");
            AssertRejected(() => BinarySaveStore.Load<string>(path),
                "incompatible payload rejected before deserialization");

            BinarySaveStore.Save(path, "backup", new ModStamp[0]);
            File.WriteAllBytes(path, incompatible);
            AssertRejected(() => BinarySaveStore.Load<string>(path),
                "compatible backup must not hide incompatible primary save");
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
            if (File.Exists(path + ".bak")) File.Delete(path + ".bak");
        }
    }

    static void AssertRejected(Action action, string description)
    {
        bool rejected = false;
        try { action(); }
        catch (IOException error)
        {
            rejected = error.Message.Contains("0.2.1") &&
                error.Message.Contains("0.1.1");
        }
        Check.Equal(true, rejected, description);
    }
}
