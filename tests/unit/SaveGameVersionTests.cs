using System;
using System.IO;
using System.Reflection;
using djack.RogueSurvivor.Engine;

static class SaveGameVersionTests
{
    public static void Run()
    {
        Assembly gameAssembly = typeof(djack.RogueSurvivor.SetupConfig).Assembly;
        string executableVersion = djack.RogueSurvivor.SetupConfig.GAME_VERSION + ".0";
        Check.Equal(executableVersion, gameAssembly.GetName().Version.ToString(),
            "executable assembly version matches game version");
        AssemblyFileVersionAttribute fileVersion = (AssemblyFileVersionAttribute)
            Attribute.GetCustomAttribute(gameAssembly, typeof(AssemblyFileVersionAttribute));
        Check.Equal(executableVersion, fileVersion.Version,
            "executable file version matches game version");

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
            File.WriteAllBytes(path, WithGameVersion(current, "0.1.0"));
            Check.Equal("compatible", BinarySaveStore.Load<string>(path),
                "earliest supported save loads");
            Check.Equal("example", BinarySaveStore.ReadMods(path)[0].Name,
                "earliest supported save retains mod manifest");
            File.WriteAllBytes(path, WithGameVersion(current, "0.1.1"));
            Check.Equal("compatible", BinarySaveStore.Load<string>(path),
                "previous release save loads");
            File.WriteAllBytes(path, WithGameVersion(current, "0.1.2"));
            AssertRejected(() => BinarySaveStore.Load<string>(path),
                "unknown previous-series patch is rejected", "0.1.2");

            Version running = Version.Parse(djack.RogueSurvivor.SetupConfig.GAME_VERSION);
            string futureVersion = new Version(running.Major + 1, 0, 0).ToString();
            byte[] incompatible = WithGameVersion(current, futureVersion);
            File.WriteAllBytes(path, incompatible);
            AssertRejected(() => BinarySaveStore.ReadMods(path),
                "incompatible manifest rejected before mod selection", futureVersion);
            AssertRejected(() => BinarySaveStore.Load<string>(path),
                "incompatible payload rejected before deserialization", futureVersion);

            BinarySaveStore.Save(path, "backup", new ModStamp[0]);
            File.WriteAllBytes(path, incompatible);
            AssertRejected(() => BinarySaveStore.Load<string>(path),
                "compatible backup must not hide incompatible primary save", futureVersion);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
            if (File.Exists(path + ".bak")) File.Delete(path + ".bak");
        }
    }

    static byte[] WithGameVersion(byte[] saved, string version)
    {
        using (MemoryStream input = new MemoryStream(saved))
        using (MemoryStream output = new MemoryStream())
        {
            byte[] header = new byte[5];
            if (input.Read(header, 0, header.Length) != header.Length)
                throw new InvalidDataException("Truncated save header");
            output.Write(header, 0, header.Length);
            new BinaryReader(input).ReadString();
            new BinaryWriter(output).Write(version);
            byte[] payload = new byte[(int)(input.Length - input.Position)];
            input.Read(payload, 0, payload.Length);
            output.Write(payload, 0, payload.Length);
            return output.ToArray();
        }
    }

    static void AssertRejected(Action action, string description, string savedVersion)
    {
        bool rejected = false;
        try { action(); }
        catch (IOException error)
        {
            rejected = error.Message.Contains(savedVersion) &&
                error.Message.Contains(djack.RogueSurvivor.SetupConfig.GAME_VERSION);
        }
        Check.Equal(true, rejected, description);
    }
}
