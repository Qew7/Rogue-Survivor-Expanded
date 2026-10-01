using System;
using System.IO;
using System.IO.Compression;
using djack.RogueSurvivor;
using djack.RogueSurvivor.Engine;

static class HintsSaveTests
{
    public static void Run()
    {
        Logger.CreateFile();
        string path = Path.Combine(Path.GetTempPath(), "rogue-hints-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            GameHintsStatus hints = new GameHintsStatus();
            hints.SetAdvisorHintAsGiven(AdvisorHint._FIRST);
            GameHintsStatus.Save(hints, path);
            Check.Equal(true, GameHintsStatus.Load(path).IsAdvisorHintGiven(AdvisorHint._FIRST),
                "new hint save restores status");

            using (FileStream file = File.Create(path))
            {
                BinaryWriter header = new BinaryWriter(file);
                header.Write(new byte[] { (byte)'R', (byte)'S', (byte)'E', (byte)'1', 4 });
                header.Write(SetupConfig.GAME_VERSION); header.Write(0);
                using (GZipStream gzip = new GZipStream(file, CompressionMode.Compress))
                {
                    BinaryWriter old = new BinaryWriter(gzip); old.Write(1); old.Write(1);
                    old.Write(typeof(GameHintsStatus).AssemblyQualifiedName); old.Write((byte)0);
                    old.Write(1); old.Write("m_AdvisorHints"); old.Write((byte)1); old.Write(2);
                    old.Write(2); old.Write(typeof(bool[]).AssemblyQualifiedName); old.Write((byte)1);
                    old.Write(1); old.Write((int)AdvisorHint._COUNT);
                    for (int i = 0; i < (int)AdvisorHint._COUNT; i++)
                    { old.Write((byte)2); old.Write(typeof(bool).AssemblyQualifiedName); old.Write(i == 0); }
                    old.Write(0);
                }
            }
            Check.Equal(true, GameHintsStatus.Load(path).IsAdvisorHintGiven(AdvisorHint._FIRST),
                "version-4 settings survive format upgrade without supporting old worlds");
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
            if (File.Exists(path + ".bak")) File.Delete(path + ".bak");
        }
    }
}
