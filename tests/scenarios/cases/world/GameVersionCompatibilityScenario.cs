using System;
using System.IO;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;

static class GameVersionCompatibilityScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("storage/game-version-compatibility",
            () => TownScenarioFactory.Arena(4462, ".....", ".....", "....."), world =>
        {
            world.Map.SetTileModelAt(3, 1, world.Game.GameTiles.WALL_BRICK);
            string path = Path.Combine(Path.GetTempPath(),
                "rogue-version-scenario-" + Guid.NewGuid().ToString("N"));
            try
            {
                BinarySaveStore.Save(path, world.Map);
                byte[] current = File.ReadAllBytes(path);
                File.WriteAllBytes(path, WithVersion(current, "0.1.1"));
                Map loaded = BinarySaveStore.Load<Map>(path);
                Check.Equal(false, loaded.GetTileAt(3, 1).Model.IsWalkable,
                    "0.1.1 save retains map state");
                Check.Equal(true, new ModInfo { GameVersion = "0.1.1" }.SupportsCurrentGame,
                    "0.1.1 mod remains compatible");

                Version running = Version.Parse(djack.RogueSurvivor.SetupConfig.GAME_VERSION);
                string futureVersion = new Version(running.Major + 1, 0, 0).ToString();
                File.WriteAllBytes(path, WithVersion(current, futureVersion));
                bool rejected = false;
                try { BinarySaveStore.Load<Map>(path); }
                catch (IOException error)
                {
                    rejected = error.Message.Contains(futureVersion) &&
                        error.Message.Contains(djack.RogueSurvivor.SetupConfig.GAME_VERSION);
                }
                Check.Equal(true, rejected, "future save is rejected with both versions");
                Check.Equal(false, new ModInfo { GameVersion = futureVersion }.SupportsCurrentGame,
                    "future mod is rejected");
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
                if (File.Exists(path + ".bak")) File.Delete(path + ".bak");
            }
        });
    }

    static byte[] WithVersion(byte[] saved, string version)
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
}
