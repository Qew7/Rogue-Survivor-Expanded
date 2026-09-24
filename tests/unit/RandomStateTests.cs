using System;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization.Formatters.Binary;

static class RandomStateTests
{
    public static void Run()
    {
        Type sessionType = Check.Type("Engine.Session");
        object session = Check.Empty("Engine.Session");
        sessionType.GetProperty("Seed").SetValue(session, 12345, null);
        object roller = sessionType.GetProperty("GameDiceRoller").GetValue(session, null);
        Check.Call(roller, "Roll", 0, 1000);

        MemoryStream bytes = new MemoryStream();
        BinaryFormatter formatter = new BinaryFormatter();
        formatter.Serialize(bytes, session);
        int expected = (int)Check.Call(roller, "Roll", 0, 1000);
        bytes.Position = 0;
        object restored = formatter.Deserialize(bytes);
        object restoredRoller = sessionType.GetProperty("GameDiceRoller").GetValue(restored, null);
        Check.Equal(expected, Check.Call(restoredRoller, "Roll", 0, 1000), "save resumes random sequence");
    }
}
